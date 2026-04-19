using DevManager.Models;

namespace DevManager.Helpers;

/// <summary>
/// Uygulama genelinde kullanılan renkler ve tasarım sabitleri.
/// </summary>
public static class AppTheme
{
    // ── Arka plan ────────────────────────────────
    public static readonly Color Background     = Color.FromArgb(18, 20, 26);
    public static readonly Color Surface        = Color.FromArgb(26, 29, 38);
    public static readonly Color SurfaceAlt     = Color.FromArgb(32, 36, 48);
    public static readonly Color Border         = Color.FromArgb(48, 54, 70);

    // ── Metin ────────────────────────────────────
    public static readonly Color TextPrimary    = Color.FromArgb(220, 225, 240);
    public static readonly Color TextSecondary  = Color.FromArgb(130, 140, 165);
    public static readonly Color TextMuted      = Color.FromArgb(80, 90, 115);

    // ── Vurgu ────────────────────────────────────
    public static readonly Color Accent         = Color.FromArgb(99, 135, 255);
    public static readonly Color AccentHover    = Color.FromArgb(120, 155, 255);

    // ── Durum renkleri ───────────────────────────
    public static readonly Color Running        = Color.FromArgb(52, 211, 153);
    public static readonly Color Stopped        = Color.FromArgb(100, 110, 135);
    public static readonly Color Starting       = Color.FromArgb(251, 191, 36);
    public static readonly Color Error          = Color.FromArgb(248, 113, 113);

    // ── Log renkleri ─────────────────────────────
    public static readonly Color LogInfo        = Color.FromArgb(190, 200, 220);
    public static readonly Color LogError       = Color.FromArgb(248, 113, 113);
    public static readonly Color LogSystem      = Color.FromArgb(99, 135, 255);

    public static Color ForStatus(ServiceStatus status) => status switch
    {
        ServiceStatus.Running  => Running,
        ServiceStatus.Starting => Starting,
        ServiceStatus.Error    => Error,
        _                      => Stopped
    };

    public static string LabelForStatus(ServiceStatus status) => status switch
    {
        ServiceStatus.Running  => "RUNNING",
        ServiceStatus.Starting => "STARTING",
        ServiceStatus.Error    => "ERROR",
        _                      => "STOPPED"
    };
}
