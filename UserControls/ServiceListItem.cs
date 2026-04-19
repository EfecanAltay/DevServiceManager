using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Sol panelde her servis için gösterilen kart bileşeni.
/// </summary>
public class ServiceListItem : UserControl
{
    private readonly Label _lblName;
    private readonly Label _lblPort;
    private readonly Panel _statusBadge;
    private readonly Label _lblStatus;

    private ServiceState _state;
    private bool _selected;

    public ServiceState State => _state;

    public event Action<ServiceListItem>? Clicked;

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            BackColor = value ? AppTheme.SurfaceAlt : AppTheme.Surface;
            Invalidate();
        }
    }

    public ServiceListItem(ServiceState state)
    {
        _state = state;

        Height = 64;
        Dock = DockStyle.Top;
        Padding = new Padding(12, 8, 12, 8);
        BackColor = AppTheme.Surface;
        Cursor = Cursors.Hand;

        // Servis adı
        _lblName = new Label
        {
            Text = state.Definition.Name,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(12, 10)
        };

        // Port
        _lblPort = new Label
        {
            Text = $":{state.Definition.Port}",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(12, 32)
        };

        // Durum badge
        _statusBadge = new Panel
        {
            Size = new Size(70, 18),
            Location = new Point(Width - 90, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _lblStatus = new Label
        {
            Text = AppTheme.LabelForStatus(state.Status),
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = AppTheme.ForStatus(state.Status),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        _statusBadge.Controls.Add(_lblStatus);

        Controls.AddRange(new Control[] { _lblName, _lblPort, _statusBadge });

        // Hover efekti
        MouseEnter += (_, _) => { if (!_selected) BackColor = Color.FromArgb(36, 40, 52); };
        MouseLeave += (_, _) => { if (!_selected) BackColor = AppTheme.Surface; };
        Click += (_, _) => Clicked?.Invoke(this);
        foreach (Control c in Controls)
        {
            c.MouseEnter += (_, _) => OnMouseEnter(EventArgs.Empty);
            c.MouseLeave += (_, _) => OnMouseLeave(EventArgs.Empty);
            c.Click += (_, _) => Clicked?.Invoke(this);
        }
    }

    public void UpdateState(ServiceState state)
    {
        _state = state;
        _lblStatus.Text = AppTheme.LabelForStatus(state.Status);
        _lblStatus.ForeColor = AppTheme.ForStatus(state.Status);
        _statusBadge.BackColor = Color.FromArgb(30,
            AppTheme.ForStatus(state.Status).R,
            AppTheme.ForStatus(state.Status).G,
            AppTheme.ForStatus(state.Status).B);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Alt çizgi
        using var pen = new Pen(AppTheme.Border, 1);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}
