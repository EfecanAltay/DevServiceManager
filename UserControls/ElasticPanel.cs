using System.Text.RegularExpressions;
using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Elasticsearch log satırlarını çözümler ve ne anlama geldiklerini açıklar.
/// </summary>
public static class ElasticLogInterpreter
{
    private static readonly Regex HealthRegex = new(
        "Cluster health status changed from \\[(?<from>\\w+)\\] to \\[(?<to>\\w+)\\](?: \\(reason: \\[(?<reason>.*?)\\]\\))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StartedRegex = new(@"\]\s+started\b", RegexOptions.Compiled);

    public sealed record HealthChange(string From, string To, string Reason);

    public static HealthChange? ParseHealthChange(string message)
    {
        var m = HealthRegex.Match(message);
        return m.Success
            ? new HealthChange(m.Groups["from"].Value.ToUpperInvariant(),
                               m.Groups["to"].Value.ToUpperInvariant(),
                               m.Groups["reason"].Value)
            : null;
    }

    public static bool IsNodeStarted(string message) => StartedRegex.IsMatch(message);

    public static string DescribeHealth(string health) => health switch
    {
        "GREEN" => "Tüm primary ve replica shard'lar atanmış; cluster tamamen sağlıklı.",
        "YELLOW" => "Tüm primary shard'lar aktif, ancak bazı replica shard'lar atanamamış. Veriye erişilebilir; tek node'lu geliştirme ortamında normaldir.",
        "RED" => "En az bir primary shard atanmamış; ilgili indekslerde veri erişilemez. Başlangıçta shard'lar yüklenirken geçicidir.",
        _ => "Bilinmeyen cluster durumu."
    };

    /// <summary>Log satırı için kullanıcıya gösterilecek açıklamayı döndürür; açıklanacak bir şey yoksa null.</summary>
    public static string? Explain(string message)
    {
        var change = ParseHealthChange(message);
        if (change is not null)
        {
            var reason = change.Reason.StartsWith("shards started", StringComparison.OrdinalIgnoreCase)
                ? " Sebep: shard'lar başlatıldı."
                : string.IsNullOrWhiteSpace(change.Reason) ? "" : $" Sebep: {change.Reason}.";

            var transition = (change.From, change.To) switch
            {
                ("RED", "YELLOW") => "Primary shard'lar artık aktif, veriye erişilebilir; replica shard'lar bekliyor.",
                ("YELLOW", "GREEN") => "Replica shard'lar da atandı; cluster tam sağlıklı.",
                ("RED", "GREEN") => "Tüm shard'lar atandı; cluster tam sağlıklı.",
                _ => DescribeHealth(change.To)
            };
            return $"ℹ Cluster durumu {change.From} → {change.To}: {transition}{reason}";
        }

        if (IsNodeStarted(message))
            return "ℹ Elasticsearch node'u başladı ve istekleri kabul etmeye hazır.";

        if (message.Contains("stopping ...", StringComparison.OrdinalIgnoreCase))
            return "ℹ Elasticsearch kapanıyor.";

        if (message.Contains("sun.misc.Unsafe::arrayBaseOffset has been called", StringComparison.OrdinalIgnoreCase))
            return "ℹ JVM uyarısı: paketlenmiş protobuf kütüphanesi eski sun.misc.Unsafe API'sini kullanıyor. Zararsızdır, hata değildir.";

        // ── Kibana ──
        if (message.Contains("Kibana is now available", StringComparison.OrdinalIgnoreCase))
            return "ℹ Kibana hazır ve kullanılabilir (http://localhost:5601).";

        if (message.Contains("http server running at", StringComparison.OrdinalIgnoreCase))
            return "ℹ Kibana web sunucusu dinlemeye başladı; eklentiler yüklenirken birkaç saniye daha 'not ready' görebilirsiniz.";

        if (message.Contains("Kibana server is not ready yet", StringComparison.OrdinalIgnoreCase))
            return "ℹ Kibana henüz hazır değil; başlangıç işlemleri (migration, eklenti yükleme) sürüyor.";

        if (message.Contains("Unable to connect to Elasticsearch", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No Living connections", StringComparison.OrdinalIgnoreCase))
            return "ℹ Kibana Elasticsearch'e bağlanamıyor. Elasticsearch'ün çalıştığını ve hazır olduğunu (cluster YELLOW/GREEN) kontrol edin.";

        return null;
    }
}

/// <summary>
/// Elasticsearch için panel: başlat/durdur, süreç durumu ve loglardan çözümlenen
/// cluster health bilgisini açıklamalarıyla gösterir. Header'dan açılan bir açılır panel olarak kullanılır.
/// </summary>
public class ElasticPanel : Panel
{
    public const string DefaultServiceName = "elasticsearch";
    public const string KibanaServiceName = "kibana";
    public const string KibanaUrl = "http://localhost:5601";

    private readonly Label _lblStatus;
    private readonly Label _lblHealth;
    private readonly Label _lblDescription;
    private readonly Label _lblKibanaStatus;
    private readonly ListBox _events;
    private readonly Button _btnStart;
    private readonly Button _btnStop;
    private readonly Button _btnKibanaStart;
    private readonly Button _btnKibanaStop;
    private readonly Button _btnKibanaOpen;
    private readonly Panel _dot;

    private Color _healthColor = AppTheme.Stopped;

    public string ServiceName { get; }
    public string HealthText { get; private set; } = "—";
    public Color HealthColor => _healthColor;

    public event Action<string>? StartRequested;
    public event Action<string>? StopRequested;
    public event Action? HealthChanged;

    /// <summary>Verilen servis adı bu panelin yönettiği servislerden biri mi (Elasticsearch/Kibana)?</summary>
    public bool Handles(string serviceName) =>
        serviceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase) ||
        serviceName.Equals(KibanaServiceName, StringComparison.OrdinalIgnoreCase);

    public ElasticPanel(string serviceName = DefaultServiceName)
    {
        ServiceName = serviceName;
        Size = new Size(400, 440);
        BackColor = AppTheme.SurfaceAlt;
        Visible = false;

        Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        var title = new Label
        {
            Text = "ELASTICSEARCH",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(14, 12)
        };

        _dot = new Panel { Size = new Size(12, 12), Location = new Point(14, 42) };
        _dot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_healthColor);
            e.Graphics.FillEllipse(b, 0, 0, 11, 11);
        };

        _lblHealth = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = AppTheme.Stopped,
            AutoSize = true,
            Location = new Point(32, 36)
        };

        _lblStatus = new Label
        {
            Text = "STOPPED",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.Stopped,
            AutoSize = true,
            Location = new Point(14, 62)
        };

        _btnStart = CreateButton("▶ Başlat", AppTheme.Running);
        _btnStop = CreateButton("■ Durdur", AppTheme.Error);
        _btnStart.Click += (_, _) => StartRequested?.Invoke(ServiceName);
        _btnStop.Click += (_, _) => StopRequested?.Invoke(ServiceName);

        _lblDescription = new Label
        {
            Text = "Elasticsearch loglarından cluster durumu burada açıklanır.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = false,
            Location = new Point(14, 84),
            Size = new Size(372, 62)
        };

        var kibanaTitle = new Label
        {
            Text = "KIBANA",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(14, 152)
        };

        _lblKibanaStatus = new Label
        {
            Text = "STOPPED",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.Stopped,
            AutoSize = true,
            Location = new Point(14, 176)
        };

        _btnKibanaStart = CreateButton("▶ Başlat", AppTheme.Running);
        _btnKibanaStop = CreateButton("■ Durdur", AppTheme.Error);
        _btnKibanaOpen = CreateButton("⇱ Aç", AppTheme.Accent);
        _btnKibanaStart.Click += (_, _) => StartRequested?.Invoke(KibanaServiceName);
        _btnKibanaStop.Click += (_, _) => StopRequested?.Invoke(KibanaServiceName);
        _btnKibanaOpen.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(KibanaUrl) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show($"Tarayıcı açılamadı: {ex.Message}", "Kibana"); }
        };
        _btnKibanaOpen.Location = new Point(Width - 14 - 82, 168);
        _btnKibanaStop.Location = new Point(Width - 14 - 82 - 6 - 82, 168);
        _btnKibanaStart.Location = new Point(Width - 14 - 82 - 6 - 82 - 6 - 82, 168);
        _btnKibanaStart.Enabled = _btnKibanaStop.Enabled = _btnKibanaOpen.Enabled = false;

        var lblEvents = new Label
        {
            Text = "OLAYLAR",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(14, 208)
        };

        _events = new ListBox
        {
            Location = new Point(14, 226),
            Size = new Size(372, 200),
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.TextSecondary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 8f),
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawVariable
        };
        _events.MeasureItem += MeasureEvent;
        _events.DrawItem += DrawEvent;

        _btnStop.Location = new Point(Width - 14 - 82, 34);
        _btnStart.Location = new Point(Width - 14 - 82 - 6 - 82, 34);

        Controls.AddRange(new Control[]
        {
            title, _dot, _lblHealth, _lblStatus, _btnStart, _btnStop, _lblDescription,
            kibanaTitle, _lblKibanaStatus, _btnKibanaStart, _btnKibanaStop, _btnKibanaOpen,
            lblEvents, _events
        });
    }

    private static Button CreateButton(string text, Color color) => new()
    {
        Text = text,
        Size = new Size(82, 26),
        BackColor = AppTheme.Surface,
        ForeColor = color,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8f),
        Cursor = Cursors.Hand
    };

    private sealed record HealthEvent(DateTime Time, string Text, Color Color);

    private void MeasureEvent(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index < 0) return;
        var ev = (HealthEvent)_events.Items[e.Index];
        var size = TextRenderer.MeasureText(e.Graphics, FormatEvent(ev), _events.Font,
            new Size(_events.Width - 8, int.MaxValue), TextFormatFlags.WordBreak);
        e.ItemHeight = size.Height + 6;
    }

    private void DrawEvent(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var ev = (HealthEvent)_events.Items[e.Index];
        var rect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 2);
        TextRenderer.DrawText(e.Graphics, FormatEvent(ev), _events.Font, rect, ev.Color, TextFormatFlags.WordBreak);
    }

    private static string FormatEvent(HealthEvent ev) => $"[{ev.Time:HH:mm:ss}] {ev.Text}";

    // ──────────────────────────────────────────────
    // Güncellemeler (UI thread)
    // ──────────────────────────────────────────────

    public void UpdateStatus(ServiceState state)
    {
        if (state.Definition.Name.Equals(KibanaServiceName, StringComparison.OrdinalIgnoreCase))
        {
            UpdateKibanaStatus(state);
            return;
        }

        _lblStatus.Text = AppTheme.LabelForStatus(state.Status);
        _lblStatus.ForeColor = AppTheme.ForStatus(state.Status);

        _btnStart.Enabled = state.Status is ServiceStatus.Stopped or ServiceStatus.Error;
        _btnStop.Enabled = state.Status is ServiceStatus.Running or ServiceStatus.Starting;

        if (state.Status is ServiceStatus.Stopped or ServiceStatus.Error)
        {
            SetHealth("—", AppTheme.Stopped);
            _lblDescription.Text = "Elasticsearch çalışmıyor.";
        }
        else if (state.Status == ServiceStatus.Starting)
        {
            SetHealth("STARTING", AppTheme.Starting);
            _lblDescription.Text = "Elasticsearch başlatılıyor; shard'lar yüklenirken cluster geçici olarak RED olabilir.";
        }
    }

    private void UpdateKibanaStatus(ServiceState state)
    {
        _lblKibanaStatus.Text = AppTheme.LabelForStatus(state.Status);
        _lblKibanaStatus.ForeColor = AppTheme.ForStatus(state.Status);
        _btnKibanaStart.Enabled = state.Status is ServiceStatus.Stopped or ServiceStatus.Error;
        _btnKibanaStop.Enabled = state.Status is ServiceStatus.Running or ServiceStatus.Starting;
        _btnKibanaOpen.Enabled = state.Status == ServiceStatus.Running;
    }

    public void SetUnavailable()
    {
        _btnStart.Enabled = false;
        _btnStop.Enabled = false;
        _lblStatus.Text = "services.json içinde 'elasticsearch' servisi yok";
        _lblStatus.ForeColor = AppTheme.TextMuted;
    }

    public void SetKibanaUnavailable()
    {
        _btnKibanaStart.Enabled = _btnKibanaStop.Enabled = _btnKibanaOpen.Enabled = false;
        _lblKibanaStatus.Text = "services.json içinde 'kibana' servisi yok";
        _lblKibanaStatus.ForeColor = AppTheme.TextMuted;
    }

    /// <summary>Sistem notları (açıklamalar) dışındaki gerçek Elasticsearch/Kibana loglarını işler.</summary>
    public void ProcessLog(LogEntry entry)
    {
        if (entry.Level == LogLevel.System) return;

        if (entry.ServiceName.Equals(KibanaServiceName, StringComparison.OrdinalIgnoreCase))
        {
            if (entry.Message.Contains("Kibana is now available", StringComparison.OrdinalIgnoreCase))
                AddEvent("Kibana: hazır ve kullanılabilir.", AppTheme.Running);
            else if (entry.Message.Contains("Unable to connect to Elasticsearch", StringComparison.OrdinalIgnoreCase)
                     || entry.Message.Contains("No Living connections", StringComparison.OrdinalIgnoreCase))
                AddEvent("Kibana: Elasticsearch'e bağlanamıyor.", AppTheme.Error);
            return;
        }

        var change = ElasticLogInterpreter.ParseHealthChange(entry.Message);
        if (change is not null)
        {
            var color = ColorForHealth(change.To);
            SetHealth(change.To, color);
            _lblDescription.Text = ElasticLogInterpreter.DescribeHealth(change.To);
            AddEvent(ElasticLogInterpreter.Explain(entry.Message)!.TrimStart('ℹ', ' '), color);
            return;
        }

        if (ElasticLogInterpreter.IsNodeStarted(entry.Message))
            AddEvent("Node başladı ve istekleri kabul etmeye hazır.", AppTheme.Running);
        else if (entry.Message.Contains("stopping ...", StringComparison.OrdinalIgnoreCase))
            AddEvent("Node kapanıyor.", AppTheme.Starting);
    }

    private void SetHealth(string text, Color color)
    {
        HealthText = text;
        _lblHealth.Text = text;
        _lblHealth.ForeColor = color;
        _healthColor = color;
        _dot.Invalidate();
        HealthChanged?.Invoke();
    }

    private void AddEvent(string text, Color color)
    {
        _events.Items.Insert(0, new HealthEvent(DateTime.Now, text, color));
        while (_events.Items.Count > 50)
            _events.Items.RemoveAt(_events.Items.Count - 1);
    }

    private static Color ColorForHealth(string h) => h switch
    {
        "GREEN" => AppTheme.Running,
        "YELLOW" => AppTheme.Starting,
        "RED" => AppTheme.Error,
        _ => AppTheme.Stopped
    };
}
