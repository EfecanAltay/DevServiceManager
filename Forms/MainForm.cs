using DevManager.Configuration;
using DevManager.Helpers;
using DevManager.Managers;
using DevManager.Models;
using DevManager.UserControls;

namespace DevManager.Forms;

public class MainForm : Form
{
    private readonly ServiceManager _manager;

    // ── UI State ──
    private enum ViewMode { Solutions, Services }
    private ViewMode _mode = ViewMode.Solutions;
    private SolutionDefinition? _currentSolution;

    // ── Sol panel ──
    private readonly Panel _leftPanel;
    private readonly Panel _listContainer;
    private readonly Label _lblTitle;
    private readonly Label _lblCount;
    private readonly Button _btnBack;
    private readonly Button _btnStartAll;
    private readonly Button _btnStopAll;

    // ── Sağ panel ──
    private readonly SplitContainer _rightSplit;
    private readonly ServiceDetailPanel _detailPanel;
    private readonly LogPanel _logPanel;

    // ── Seçili öğeler ──
    private ServiceListItem? _selectedServiceItem;
    private readonly Dictionary<string, ServiceListItem> _serviceItems = new();
    private readonly List<SolutionListItem> _solutionItems = new();

    // ── Arka Plan / Sistem Tepsisi ──
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private bool _isExiting = false;

    public MainForm(ServiceManager manager)
    {
        _manager = manager;

        Text = "Dev Service Manager";
        Size = new Size(1200, 760);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.TextPrimary;
        Font = new Font("Segoe UI", 9f);

        // ── Titlebar üst şerit ──
        var topBar = BuildTopBar();

        // ── Sol panel ──
        _leftPanel = new Panel
        {
            Width = 260,
            Dock = DockStyle.Left,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0)
        };

        var leftHeader = BuildLeftHeader(out _lblTitle, out _lblCount, out _btnBack, out _btnStartAll, out _btnStopAll);

        _listContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Surface
        };

        _leftPanel.Controls.Add(_listContainer);
        _leftPanel.Controls.Add(leftHeader);

        // ── Sağ taraf: üst detay + alt log ──
        _detailPanel = new ServiceDetailPanel();
        _detailPanel.StartRequested   += name => _manager.StartAsync(name);
        _detailPanel.StopRequested    += name => _manager.StopAsync(name);
        _detailPanel.RestartRequested += name => _manager.RestartAsync(name);

        _logPanel = new LogPanel();

        _rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 260,
            BackColor = AppTheme.Background,
            Panel1MinSize = 200,
            Panel2MinSize = 150
        };
        _rightSplit.Panel1.Controls.Add(_detailPanel);
        _rightSplit.Panel2.Controls.Add(_logPanel);

        // ── Ayırıcı çizgi ──
        var splitter = new Panel
        {
            Width = 1,
            Dock = DockStyle.Left,
            BackColor = AppTheme.Border
        };

        Controls.Add(_rightSplit);
        Controls.Add(splitter);
        Controls.Add(_leftPanel);
        Controls.Add(topBar);

        // ── Manager olayları ──
        _manager.StatusChanged += OnStatusChanged;
        _manager.LogReceived   += OnLogReceived;

        // ── Başlangıç görünümü ──
        ShowSolutions();

        // ── Tepsi simgesi ──
        InitializeTray();
    }

    private void InitializeTray()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Göster", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("Çıkış", null, (_, _) => 
        {
            _isExiting = true;
            Close();
        });

        _trayIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            ContextMenuStrip = _trayMenu,
            Text = "Dev Service Manager",
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    // ──────────────────────────────────────────────
    // UI Oluşturma
    // ──────────────────────────────────────────────

    private Panel BuildTopBar()
    {
        var bar = new Panel
        {
            Height = 48,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(14, 16, 22),
            Padding = new Padding(16, 0, 16, 0)
        };

        var logo = new Label
        {
            Text = "⚙  Dev Service Manager",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        var btnExport = CreateTopBarButton("  Export", 220);
        btnExport.Click += OnExportConfig;

        var btnImport = CreateTopBarButton("  Import", 320);
        btnImport.Click += OnImportConfig;

        var configPath = new Label
        {
            Text = ConfigurationLoader.GetConfigPath(),
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        configPath.Location = new Point(bar.Width - configPath.PreferredWidth - 16, 16);

        bar.Controls.AddRange(new Control[] { logo, btnExport, btnImport, configPath });
        bar.Resize += (_, _) =>
        {
            configPath.Location = new Point(bar.Width - configPath.PreferredWidth - 16, 16);
        };

        return bar;
    }

    private Button CreateTopBarButton(string text, int x) => new()
    {
        Text = text,
        Location = new Point(x, 10),
        Size = new Size(90, 28),
        BackColor = Color.FromArgb(30, 34, 44),
        ForeColor = AppTheme.TextSecondary,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8f),
        Cursor = Cursors.Hand
    };

    private void OnExportConfig(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = "services_export.json",
            Title = "Export Configuration"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ConfigurationLoader.Save(_manager.Solutions.ToList(), sfd.FileName);
                MessageBox.Show("Configuration exported successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnImportConfig(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Configuration"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var result = MessageBox.Show(
                "Importing a new configuration will stop all currently running services.\n\nContinue?",
                "Import Configuration",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Validate file first
                    var (solutions, _) = ConfigurationLoader.LoadFromFile(ofd.FileName);
                    
                    // Reset manager and overwrite main config
                    _manager.Reset();
                    ConfigurationLoader.Save(solutions);
                    
                    // Reload everything
                    _manager.LoadServices(solutions);
                    ShowSolutions();
                    
                    MessageBox.Show("Configuration imported and reloaded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private Panel BuildLeftHeader(out Label lblTitle, out Label lblCount, out Button btnBack, out Button btnStart, out Button btnStop)
    {
        var header = new Panel
        {
            Height = 110,
            Dock = DockStyle.Top,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(12, 10, 12, 10)
        };

        btnBack = new Button
        {
            Text = "", // Segoe MDL2 Assets Back
            Font = new Font("Segoe MDL2 Assets", 10f),
            Size = new Size(24, 24),
            Location = new Point(10, 10),
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Visible = false
        };
        btnBack.FlatAppearance.BorderSize = 0;
        btnBack.Click += (_, _) => ShowSolutions();

        lblTitle = new Label
        {
            Text = "SOLUTIONS",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(12, 12)
        };

        lblCount = new Label
        {
            Text = "0 çözüm",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(12, 30)
        };

        btnStart = CreateGlobalButton("▶ Tümünü Başlat", AppTheme.Running, 12, 56);
        btnStop  = CreateGlobalButton("■ Tümünü Durdur", AppTheme.Error,   12, 82);

        btnStart.Click += async (_, _) => 
        {
            if (_mode == ViewMode.Solutions)
                await _manager.StartAllAsync();
            else if (_currentSolution != null)
            {
                var tasks = _currentSolution.Services.Select(s => _manager.StartAsync(s.Name));
                await Task.WhenAll(tasks);
            }
        };

        btnStop.Click += async (_, _) => 
        {
            if (_mode == ViewMode.Solutions)
                await _manager.StopAllAsync();
            else if (_currentSolution != null)
            {
                var tasks = _currentSolution.Services.Select(s => _manager.StopAsync(s.Name));
                await Task.WhenAll(tasks);
            }
        };

        header.Controls.AddRange(new Control[] { btnBack, lblTitle, lblCount, btnStart, btnStop });

        // Alt çizgi
        var line = new Panel
        {
            Height = 1,
            Dock = DockStyle.Bottom,
            BackColor = AppTheme.Border
        };
        header.Controls.Add(line);

        return header;
    }

    private Button CreateGlobalButton(string text, Color color, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(230, 22),
        BackColor = AppTheme.Surface,
        ForeColor = color,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 8.5f),
        Cursor    = Cursors.Hand
    };

    private void ShowSolutions()
    {
        _mode = ViewMode.Solutions;
        _currentSolution = null;
        _btnBack.Visible = false;
        _lblTitle.Location = new Point(12, 12);
        _lblTitle.Text = "SOLUTIONS";
        _lblCount.Text = $"{_manager.Solutions.Count} çözüm";

        _listContainer.Controls.Clear();
        _solutionItems.Clear();

        var solutions = _manager.Solutions.Reverse().ToList();
        foreach (var sol in solutions)
        {
            var item = new SolutionListItem(sol);
            item.Clicked += SelectSolution;
            _solutionItems.Add(item);
            _listContainer.Controls.Add(item);
        }
    }

    private void ShowServices(SolutionDefinition sol)
    {
        _mode = ViewMode.Services;
        _currentSolution = sol;
        _btnBack.Visible = true;
        _lblTitle.Location = new Point(38, 12);
        _lblTitle.Text = sol.Name.ToUpper();
        _lblCount.Text = $"{sol.Services.Count} servis";

        _listContainer.Controls.Clear();
        _serviceItems.Clear();

        var states = _manager.Services
            .Where(s => s.SolutionName == sol.Name)
            .Reverse()
            .ToList();

        foreach (var state in states)
        {
            var item = new ServiceListItem(state);
            item.Clicked += SelectItem;
            _serviceItems[state.Definition.Name] = item;
            _listContainer.Controls.Add(item);
        }

        // Eğer bu çözümdeki bir servis seçiliyse onu işaretle
        if (_selectedServiceItem != null && _serviceItems.TryGetValue(_selectedServiceItem.State.Definition.Name, out var newItem))
        {
            _selectedServiceItem = newItem;
            _selectedServiceItem.Selected = true;
        }
    }

    private void SelectSolution(SolutionListItem item)
    {
        ShowServices(item.Solution);
    }

    private void SelectItem(ServiceListItem item)
    {
        if (_selectedServiceItem is not null)
            _selectedServiceItem.Selected = false;

        _selectedServiceItem = item;
        item.Selected = true;

        var state = item.State;
        _detailPanel.ShowService(state);

        _logPanel.SetTitle($"{state.Definition.Name} — Loglar");
        _logPanel.LoadLogs(_manager.GetLogs(state.Definition.Name), state.Definition.Name);
    }

    // ──────────────────────────────────────────────
    // Manager olayları
    // ──────────────────────────────────────────────

    private void OnStatusChanged(ServiceState state)
    {
        InvokeIfNeeded(() =>
        {
            if (_mode == ViewMode.Services && _currentSolution?.Name == state.SolutionName)
            {
                if (_serviceItems.TryGetValue(state.Definition.Name, out var item))
                    item.UpdateState(state);
            }

            if (_selectedServiceItem?.State.Definition.Name == state.Definition.Name)
                _detailPanel.UpdateStatus(state);
        });
    }

    private void OnLogReceived(LogEntry entry)
    {
        // Sadece seçili servisin loglarını canlı göster
        if (_selectedServiceItem?.State.Definition.Name == entry.ServiceName)
            _logPanel.AppendLog(entry);
    }

    // ──────────────────────────────────────────────
    // Kapanış
    // ──────────────────────────────────────────────

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Eğer tepsi menüsünden Çıkış denmediyse, sadece gizle
        if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        var running = _manager.Services
            .Where(s => s.Status == ServiceStatus.Running || s.Status == ServiceStatus.Starting)
            .ToList();

        if (running.Count > 0)
        {
            var result = MessageBox.Show(
                $"{running.Count} servis hâlâ çalışıyor.\nKapatmadan önce durdurulsun mu?",
                "Çalışan Servisler",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                _isExiting = false; // Çıkış iptal edildi
                return;
            }

            if (result == DialogResult.Yes)
            {
                // Senkron bekle (UI kapatılıyor)
                Task.Run(() => _manager.StopAllAsync()).Wait(5000);
            }
        }

        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
        _manager.Dispose();
        base.OnFormClosing(e);
    }

    private void InvokeIfNeeded(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }
}
