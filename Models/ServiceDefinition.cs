namespace DevManager.Models;

/// <summary>
/// JSON config dosyasından yüklenen servis tanımı.
/// </summary>
public class ServiceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? HealthCheckUrl { get; set; }
    public bool AutoStart { get; set; } = false;
}
