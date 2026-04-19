namespace DevManager.Models;

public enum ServiceStatus
{
    Stopped,
    Starting,
    Running,
    Error
}

/// <summary>
/// Çalışma zamanındaki servis durumunu tutar.
/// Definition (config) + runtime bilgisini birleştirir.
/// </summary>
public class ServiceState
{
    public string SolutionName { get; }
    public ServiceDefinition Definition { get; }

    public ServiceStatus Status { get; set; } = ServiceStatus.Stopped;
    public int? ProcessId { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? LastError { get; set; }

    public ServiceState(string solutionName, ServiceDefinition definition)
    {
        SolutionName = solutionName;
        Definition = definition;
    }
}
