using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Sağ panelde seçili servisin detaylarını ve kontrol butonlarını gösterir.
/// </summary>
public class ServiceDetailPanel : UserControl
{
    // ── Bilgi alanları ──
    private readonly Label _lblName;
    private readonly Label _lblStatusBadge;
    private readonly Label _lblPid;
    private readonly Label _lblStartedAt;
    private readonly Label _lblPort;
    private readonly Label _lblCommand;
    private readonly Label _lblWorkDir;
    private readonly Label _lblHealthUrl;
    private readonly Label _lblError;

    // ── Butonlar ──
    private readonly Button _btnStart;
    private readonly Button _btnStop;
    private readonly Button _btnRestart;

    private ServiceState? _currentState;

    public event Func<string, Task>? StartRequested;
    public event Func<string, Task>? StopRequested;
    public event Func<string, Task>? RestartRequested;

    public ServiceDetailPanel()
    {
        BackColor = AppTheme.Surface;
        Dock = DockStyle.Fill;
        Padding = new Padding(20, 16, 20, 16);

        // ── Servis adı ve durum ──
        _lblName = CreateLabel("—", 16f, FontStyle.Bold, AppTheme.TextPrimary);
        _lblName.Location = new Point(20, 16);
        _lblName.AutoSize = true;

        _lblStatusBadge = CreateLabel("STOPPED", 8f, FontStyle.Bold, AppTheme.Stopped);
        _lblStatusBadge.Location = new Point(20, 44);
        _lblStatusBadge.AutoSize = true;

        // ── Butonlar ──
        _btnStart   = CreateActionButton("▶  Start",   AppTheme.Running);
        _btnStop    = CreateActionButton("■  Stop",    AppTheme.Error);
        _btnRestart = CreateActionButton("↻  Restart", AppTheme.Starting);

        _btnStart.Click   += async (_, _) => await OnStart();
        _btnStop.Click    += async (_, _) => await OnStop();
        _btnRestart.Click += async (_, _) => await OnRestart();

        var btnPanel = new FlowLayoutPanel
        {
            Location = new Point(20, 72),
            Size = new Size(400, 40),
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        btnPanel.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnRestart });

        // ── Bilgi ızgarası ──
        int y = 130;
        _lblPort      = AddInfoRow("Port",          "—", ref y);
        _lblCommand   = AddInfoRow("Komut",         "—", ref y);
        _lblWorkDir   = AddInfoRow("Dizin",         "—", ref y);
        _lblHealthUrl = AddInfoRow("Health Check",  "—", ref y);
        _lblPid       = AddInfoRow("PID",           "—", ref y);
        _lblStartedAt = AddInfoRow("Başlatma",      "—", ref y);

        y += 8;
        _lblError = CreateLabel("", 8.5f, FontStyle.Italic, AppTheme.Error);
        _lblError.Location = new Point(20, y);
        _lblError.Size = new Size(Width - 40, 40);
        _lblError.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        Controls.AddRange(new Control[]
        {
            _lblName, _lblStatusBadge, btnPanel, _lblError
        });

        SetEmpty();
    }

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    public void ShowService(ServiceState state)
    {
        _currentState = state;
        var def = state.Definition;

        _lblName.Text        = def.Name;
        _lblPort.Text        = def.Port > 0 ? def.Port.ToString() : "—";
        _lblCommand.Text     = $"{def.Command} {def.Arguments}";
        _lblWorkDir.Text     = TruncatePath(def.WorkingDirectory);
        _lblHealthUrl.Text   = !string.IsNullOrWhiteSpace(def.HealthCheckUrl) ? def.HealthCheckUrl : "—";
        _lblPid.Text         = state.ProcessId?.ToString() ?? "—";
        _lblStartedAt.Text   = state.StartedAt?.ToString("dd.MM.yyyy HH:mm:ss") ?? "—";
        _lblError.Text       = state.LastError ?? string.Empty;

        UpdateStatusDisplay(state.Status);
    }

    public void UpdateStatus(ServiceState state)
    {
        if (_currentState?.Definition.Name != state.Definition.Name) return;
        _currentState = state;

        _lblPid.Text       = state.ProcessId?.ToString() ?? "—";
        _lblStartedAt.Text = state.StartedAt?.ToString("dd.MM.yyyy HH:mm:ss") ?? "—";
        _lblError.Text     = state.LastError ?? string.Empty;

        UpdateStatusDisplay(state.Status);
    }

    public void SetEmpty()
    {
        _currentState = null;
        _lblName.Text      = "Servis seçin";
        _lblStatusBadge.Text = "";
        _lblPort.Text = _lblCommand.Text = _lblWorkDir.Text =
        _lblHealthUrl.Text = _lblPid.Text = _lblStartedAt.Text = "—";
        _lblError.Text = "";
        SetButtonsEnabled(false, false, false);
    }

    // ──────────────────────────────────────────────
    // İç yardımcılar
    // ──────────────────────────────────────────────

    private void UpdateStatusDisplay(ServiceStatus status)
    {
        _lblStatusBadge.Text      = AppTheme.LabelForStatus(status);
        _lblStatusBadge.ForeColor = AppTheme.ForStatus(status);

        bool running  = status == ServiceStatus.Running;
        bool stopped  = status == ServiceStatus.Stopped || status == ServiceStatus.Error;
        bool starting = status == ServiceStatus.Starting;

        SetButtonsEnabled(
            startEnabled:   stopped,
            stopEnabled:    running || starting,
            restartEnabled: running
        );
    }

    private void SetButtonsEnabled(bool startEnabled, bool stopEnabled, bool restartEnabled)
    {
        _btnStart.Enabled   = startEnabled;
        _btnStop.Enabled    = stopEnabled;
        _btnRestart.Enabled = restartEnabled;

        _btnStart.ForeColor   = startEnabled   ? AppTheme.Running  : AppTheme.TextMuted;
        _btnStop.ForeColor    = stopEnabled     ? AppTheme.Error    : AppTheme.TextMuted;
        _btnRestart.ForeColor = restartEnabled  ? AppTheme.Starting : AppTheme.TextMuted;
    }

    private async Task OnStart()
    {
        if (_currentState is null) return;
        SetButtonsEnabled(false, false, false);
        if (StartRequested is not null)
            await StartRequested(_currentState.Definition.Name);
    }

    private async Task OnStop()
    {
        if (_currentState is null) return;
        SetButtonsEnabled(false, false, false);
        if (StopRequested is not null)
            await StopRequested(_currentState.Definition.Name);
    }

    private async Task OnRestart()
    {
        if (_currentState is null) return;
        SetButtonsEnabled(false, false, false);
        if (RestartRequested is not null)
            await RestartRequested(_currentState.Definition.Name);
    }

    // ── UI Fabrika ──
    private Label AddInfoRow(string labelText, string value, ref int y)
    {
        var lbl = CreateLabel(labelText + ":", 8.5f, FontStyle.Regular, AppTheme.TextSecondary);
        lbl.Location = new Point(20, y);
        lbl.AutoSize = true;
        Controls.Add(lbl);

        var val = CreateLabel(value, 9f, FontStyle.Regular, AppTheme.TextPrimary);
        val.Location = new Point(120, y);
        val.Size = new Size(Width - 140, 18);
        val.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        Controls.Add(val);

        y += 24;
        return val;
    }

    private static Label CreateLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", size, style),
        ForeColor = color,
        BackColor = Color.Transparent
    };

    private Button CreateActionButton(string text, Color accent) => new()
    {
        Text      = text,
        Size      = new Size(110, 32),
        Margin    = new Padding(0, 0, 8, 0),
        BackColor = AppTheme.SurfaceAlt,
        ForeColor = accent,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
        Cursor    = Cursors.Hand
    };

    private static string TruncatePath(string path) =>
        path.Length > 48 ? "..." + path[^45..] : path;
}
