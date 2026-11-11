using System.Text.RegularExpressions;
using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Elasticsearch için ayrı bölüm: başlat/durdur butonları, süreç durumu ve
/// loglardan çözümlenen cluster health (GREEN/YELLOW/RED) bilgisini gösterir.
/// </summary>
public class ElasticPanel : Panel
{
    public const string DefaultServiceName = "elasticsearch";

    private static readonly Regex HealthRegex = new(
        "Cluster health status changed from \\[(?<from>\\w+)\\] to \\[(?<to>\\w+)\\](?: \\(reason: \\[(?<reason>.*?)\\]\\))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CurrentHealthRegex = new(
        "current\\.health=\"(?<h>\\w+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StartedRegex = new(
        @"\]\s+started\b", RegexOptions.Compiled);

    private readonly Label _lblStatus;
    private readonly Label _lblHealth;
    private readonly Label _lblLast;
    private readonly ListBox _events;
    private readonly Button _btnStart;
    private readonly Button _btnStop;
    private readonly Panel _dot;

    private Color _healthColor = AppTheme.Stopped;

    public string ServiceName { get; }

    public event Action<string>? StartRequested;
    public event Action<string>? StopRequested;

    public ElasticPanel(string serviceName = DefaultServiceName)
    {
        ServiceName = serviceName;
        Height = 210;
        Dock = DockStyle.Bottom;
        BackColor = AppTheme.SurfaceAlt;
        Padding = new Padding(12, 8, 12, 8);

        var line = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = AppTheme.Border };

        var title = new Label
        {
            Text = "ELASTICSEARCH",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(12, 10)
        };

        _dot = new Panel { Size = new Size(12, 12), Location = new Point(12, 36) };
        _dot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_healthColor);
            e.Graphics.FillEllipse(b, 0, 0, 11, 11);
        };

        _lblHealth = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = AppTheme.Stopped,
            AutoSize = true,
            Location = new Point(30, 31)
        };

        _lblStatus = new Label
        {
            Text = "STOPPED",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.Stopped,
            AutoSize = true,
            Location = new Point(12, 54)
        };

        _btnStart = CreateButton("▶ Başlat", AppTheme.Running, 130);
        _btnStop = CreateButton("■ Durdur", AppTheme.Error, 190);
        _btnStart.Click += (_, _) => StartRequested?.Invoke(ServiceName);
        _btnStop.Click += (_, _) => StopRequested?.Invoke(ServiceName);

        _lblLast = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = AppTheme.TextMuted,
            AutoSize = false,
            Size = new Size(230, 16),
            Location = new Point(12, 74)
        };

        _events = new ListBox
        {
            Location = new Point(12, 94),
            Size = new Size(230, 106),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.TextSecondary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 7.5f),
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 16
        };
        _events.DrawItem += DrawEvent;

        Controls.AddRange(new Control[] { line, title, _dot, _lblHealth, _lblStatus, _btnStart, _btnStop, _lblLast, _events });
        Resize += (_, _) =>
        {
            _btnStop.Location = new Point(Width - 12 - 76, 30);
            _btnStart.Location = new Point(Width - 12 - 76 - 4 - 76, 30);
            _events.Width = Width - 24;
            _lblLast.Width = Width - 24;
        };
    }

    private Button CreateButton(string text, Color color, int x)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(76, 24),
            Location = new Point(x, 30),
            BackColor = AppTheme.Surface,
            ForeColor = color,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f),
            Cursor = Cursors.Hand
        };
        return b;
    }

    private sealed record HealthEvent(DateTime Time, string Text, Color Color);

    private void DrawEvent(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var ev = (HealthEvent)_events.Items[e.Index];
        using var brush = new SolidBrush(ev.Color);
        e.Graphics.DrawString($"[{ev.Time:HH:mm:ss}] {ev.Text}", _events.Font, brush, e.Bounds.X + 2, e.Bounds.Y + 1);
    }

    // ──────────────────────────────────────────────
    // Dışarıdan çağrılan güncellemeler (UI thread)
    // ──────────────────────────────────────────────

    public void UpdateStatus(ServiceState state)
    {
        _lblStatus.Text = AppTheme.LabelForStatus(state.Status);
        _lblStatus.ForeColor = AppTheme.ForStatus(state.Status);

        _btnStart.Enabled = state.Status is ServiceStatus.Stopped or ServiceStatus.Error;
        _btnStop.Enabled = state.Status is ServiceStatus.Running or ServiceStatus.Starting;

        if (state.Status is ServiceStatus.Stopped or ServiceStatus.Error)
            SetHealth("—", AppTheme.Stopped);
        else if (state.Status == ServiceStatus.Starting)
            SetHealth("STARTING", AppTheme.Starting);
    }

    public void SetUnavailable()
    {
        _btnStart.Enabled = false;
        _btnStop.Enabled = false;
        _lblStatus.Text = "services.json içinde 'elasticsearch' servisi yok";
        _lblStatus.ForeColor = AppTheme.TextMuted;
    }

    public void ProcessLog(LogEntry entry)
    {
        var msg = entry.Message;

        var m = HealthRegex.Match(msg);
        if (m.Success)
        {
            var to = m.Groups["to"].Value.ToUpperInvariant();
            var from = m.Groups["from"].Value.ToUpperInvariant();
            AddEvent($"{from} → {to}", ColorForHealth(to));
            SetHealth(to, ColorForHealth(to));
            return;
        }

        var c = CurrentHealthRegex.Match(msg);
        if (c.Success)
        {
            var h = c.Groups["h"].Value.ToUpperInvariant();
            SetHealth(h, ColorForHealth(h));
            return;
        }

        if (StartedRegex.IsMatch(msg))
            AddEvent("Node başladı", AppTheme.Running);
        else if (msg.Contains("stopping ...", StringComparison.OrdinalIgnoreCase))
            AddEvent("Node duruyor", AppTheme.Starting);
    }

    private void SetHealth(string text, Color color)
    {
        _lblHealth.Text = text;
        _lblHealth.ForeColor = color;
        _healthColor = color;
        _dot.Invalidate();
    }

    private void AddEvent(string text, Color color)
    {
        var now = DateTime.Now;
        _events.Items.Insert(0, new HealthEvent(now, text, color));
        while (_events.Items.Count > 50)
            _events.Items.RemoveAt(_events.Items.Count - 1);
        _lblLast.Text = $"Son olay: {now:HH:mm:ss} — {text}";
    }

    private static Color ColorForHealth(string h) => h switch
    {
        "GREEN" => AppTheme.Running,
        "YELLOW" => AppTheme.Starting,
        "RED" => AppTheme.Error,
        _ => AppTheme.Stopped
    };
}
