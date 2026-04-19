using DevManager.Helpers;
using DevManager.Models;

namespace DevManager.UserControls;

/// <summary>
/// Sol panelde her çözüm (solution) için gösterilen kart bileşeni.
/// </summary>
public class SolutionListItem : UserControl
{
    private readonly Label _lblName;
    private readonly Label _lblCount;
    private readonly Label _lblChevron;
    private readonly SolutionDefinition _solution;
    private bool _selected;

    public SolutionDefinition Solution => _solution;

    public event Action<SolutionListItem>? Clicked;

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

    public SolutionListItem(SolutionDefinition solution)
    {
        _solution = solution;

        Height = 64;
        Dock = DockStyle.Top;
        Padding = new Padding(12, 8, 12, 8);
        BackColor = AppTheme.Surface;
        Cursor = Cursors.Hand;

        // Çözüm adı
        _lblName = new Label
        {
            Text = solution.Name,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(12, 12)
        };

        // Servis sayısı
        _lblCount = new Label
        {
            Text = $"{solution.Services.Count} servis",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(12, 34)
        };

        // Chevron
        _lblChevron = new Label
        {
            Text = "", // Segoe MDL2 Assets chevron right
            Font = new Font("Segoe MDL2 Assets", 10f),
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(230, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        Controls.AddRange(new Control[] { _lblName, _lblCount, _lblChevron });

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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Alt çizgi
        using var pen = new Pen(AppTheme.Border, 1);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}
