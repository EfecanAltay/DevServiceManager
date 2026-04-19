namespace DevManager.Models;

public enum LogLevel
{
    Info,
    Error,
    Warning,
    System
}

public class LogEntry
{
    public DateTime Timestamp { get; }
    public string ServiceName { get; }
    public string Message { get; }
    public LogLevel Level { get; }

    public LogEntry(string serviceName, string message, LogLevel level = LogLevel.Info)
    {
        Timestamp = DateTime.Now;
        ServiceName = serviceName;
        Message = message;
        Level = level;
    }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] {Message}";
}
