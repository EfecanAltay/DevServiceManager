using System.Collections.Concurrent;
using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Canlı log akışı, arama ve filtreleme içeren log paneli.
/// </summary>
public class LogPanel : UserControl
{
    private readonly RichTextBox _rtb;
    private readonly TextBox _txtSearch;
    private readonly Button _btnClear;
    private readonly Button _btnAutoScroll;
    private readonly Label _lblTitle;

    private bool _autoScroll = true;
    private string _searchTerm = string.Empty;
    private string? _filterService;

    // Tüm logları tutar; filtre/arama uygulandığında yeniden render eder
    private readonly List<LogEntry> _buffer = new();
    private const int MaxBuffer = 5000;

    // Bekleyen loglar için kuyruk ve zamanlayıcı
    private readonly ConcurrentQueue<LogEntry> _pendingLogs = new();
    private readonly System.Windows.Forms.Timer _flushTimer;

    public LogPanel()
    {
        BackColor = AppTheme.Background;
        Dock = DockStyle.Fill;

        // ── Başlık çubuğu ──
        var toolbar = new Panel
        {
            Height = 40,
            Dock = DockStyle.Top,
            BackColor = AppTheme.Surface,
            Padding = new Padding(8, 0, 8, 0)
        };

        _lblTitle = new Label
        {
            Text = "LOG ÇIKTISI",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(8, 12)
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "Logda ara...",
            Width = 180,
            Height = 24,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _txtSearch.TextChanged += OnSearchChanged;

        _btnClear = CreateToolButton("Temizle", 70);
        _btnClear.Click += (_, _) => Clear();

        _btnAutoScroll = CreateToolButton("Auto↓ ON", 80);
        _btnAutoScroll.Click += ToggleAutoScroll;

        toolbar.Controls.AddRange(new Control[] { _lblTitle, _txtSearch, _btnAutoScroll, _btnClear });

        // ── Log metni ──
        _rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 14, 18),
            ForeColor = AppTheme.LogInfo,
            Font = new Font("Cascadia Code", 9f),
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            WordWrap = false,
            Padding = new Padding(6)
        };

        Controls.Add(_rtb);
        Controls.Add(toolbar);

        // Timer ayarları
        _flushTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _flushTimer.Tick += (_, _) => FlushBuffer();
        _flushTimer.Start();

        // Toolbar layout
        ResizeRedraw = true;
        Resize += (_, _) => LayoutToolbar(toolbar);
        LayoutToolbar(toolbar);
    }

    private void LayoutToolbar(Panel toolbar)
    {
        _btnClear.Location = new Point(toolbar.Width - 80, 9);
        _btnAutoScroll.Location = new Point(toolbar.Width - 170, 9);
        _txtSearch.Location = new Point(toolbar.Width - 360, 8);
    }

    private Button CreateToolButton(string text, int width) => new Button
    {
        Text = text,
        Width = width,
        Height = 24,
        BackColor = AppTheme.SurfaceAlt,
        ForeColor = AppTheme.TextPrimary,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8f),
        Cursor = Cursors.Hand
    };

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    public void AppendLog(LogEntry entry)
    {
        _pendingLogs.Enqueue(entry);
    }

    private void FlushBuffer()
    {
        if (IsDisposed || _pendingLogs.IsEmpty) return;

        _rtb.SuspendLayout();
        bool scrollNeeded = false;
        try
        {
            int processed = 0;
            while (_pendingLogs.TryDequeue(out var entry) && processed < 500)
            {
                _buffer.Add(entry);
                if (_buffer.Count > MaxBuffer)
                    _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);

                if (PassesFilter(entry))
                {
                    AppendToRtb(entry, skipScroll: true);
                    scrollNeeded = true;
                }
                processed++;
            }
        }
        finally
        {
            if (scrollNeeded && _autoScroll)
                _rtb.ScrollToCaret();
            _rtb.ResumeLayout();
        }
    }

    public void LoadLogs(IEnumerable<LogEntry> entries, string? serviceName = null)
    {
        _filterService = serviceName;
        while (_pendingLogs.TryDequeue(out _)) { } // Bekleyenleri temizle
        _buffer.Clear();
        _buffer.AddRange(entries);
        Rerender();
    }

    public void SetServiceFilter(string? serviceName)
    {
        _filterService = serviceName;
        Rerender();
    }

    public void Clear()
    {
        while (_pendingLogs.TryDequeue(out _)) { } // Bekleyenleri temizle
        _buffer.Clear();
        InvokeIfNeeded(() => _rtb.Clear());
    }

    public void SetTitle(string title)
    {
        _lblTitle.Text = title.ToUpperInvariant();
    }

    // ──────────────────────────────────────────────
    // İç yardımcılar
    // ──────────────────────────────────────────────

    private void AppendToRtb(LogEntry entry, bool skipScroll = false)
    {
        if (IsDisposed) return;

        var color = entry.Level switch
        {
            LogLevel.Error  => AppTheme.LogError,
            LogLevel.System => AppTheme.LogSystem,
            _               => AppTheme.LogInfo
        };
        
        _rtb.SelectionStart = _rtb.TextLength;
        _rtb.SelectionLength = 0;
        _rtb.SelectionColor = AppTheme.TextMuted;
        _rtb.AppendText($"[{entry.Timestamp:HH:mm:ss}] ");
        _rtb.SelectionColor = color;
        _rtb.AppendText(entry.Message + "\n");

        if (!skipScroll && _autoScroll)
            _rtb.ScrollToCaret();
    }

    private void Rerender()
    {
        InvokeIfNeeded(() =>
        {
            _rtb.Clear();
            _rtb.SuspendLayout();
            foreach (var entry in _buffer.Where(PassesFilter))
                AppendToRtb(entry, skipScroll: true);
            
            if (_autoScroll)
                _rtb.ScrollToCaret();
            _rtb.ResumeLayout();
        });
    }

    private bool PassesFilter(LogEntry entry)
    {
        if (_filterService is not null && entry.ServiceName != _filterService)
            return false;
        if (!string.IsNullOrWhiteSpace(_searchTerm) &&
            !entry.Message.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        _searchTerm = _txtSearch.Text;
        Rerender();
    }

    private void ToggleAutoScroll(object? sender, EventArgs e)
    {
        _autoScroll = !_autoScroll;
        _btnAutoScroll.Text = _autoScroll ? "Auto↓ ON" : "Auto↓ OFF";
        _btnAutoScroll.ForeColor = _autoScroll ? AppTheme.Running : AppTheme.TextSecondary;
    }

    private void InvokeIfNeeded(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }
}
