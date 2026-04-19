using DevManager.Configuration;
using DevManager.Helpers;
using DevManager.Models;
using DevManager.Services;

namespace DevManager.Managers;

/// <summary>
/// Tüm servislerin yaşam döngüsünü ve durumunu yönetir.
/// UI bu sınıf üzerinden iletişim kurar.
/// </summary>
public class ServiceManager : IDisposable
{
    private readonly Dictionary<string, ServiceState> _states = new();
    private readonly Dictionary<string, ProcessRunner> _runners = new();
    private readonly Dictionary<string, List<LogEntry>> _logs = new();
    private readonly System.Threading.Timer _pollTimer;
    private bool _disposed;
    private List<SolutionDefinition> _solutions = new();

    // UI için olaylar
    public event Action<ServiceState>? StatusChanged;
    public event Action<LogEntry>? LogReceived;

    public IReadOnlyList<SolutionDefinition> Solutions => _solutions.AsReadOnly();

    public IReadOnlyList<ServiceState> Services =>
        _states.Values.ToList().AsReadOnly();

    public ServiceManager()
    {
        _pollTimer = new System.Threading.Timer(
            _ => _ = PollAllAsync(),
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5));
    }

    public void LoadServices(List<SolutionDefinition> solutions)
    {
        _solutions = solutions;
        foreach (var sol in solutions)
        {
            foreach (var def in sol.Services)
            {
                _states[def.Name] = new ServiceState(sol.Name, def);
                _logs[def.Name] = new List<LogEntry>();
            }
        }
    }

    public void Reset()
    {
        // Stop all services synchronously for reset
        foreach (var runner in _runners.Values)
        {
            runner.StopAsync().Wait(2000);
            runner.Dispose();
        }
        
        _runners.Clear();
        _states.Clear();
        _logs.Clear();
        _solutions.Clear();
    }

    // ──────────────────────────────────────────────
    // CRUD işlemleri
    // ──────────────────────────────────────────────

    public async Task StartAsync(string serviceName)
    {
        await Task.Run(async () =>
        {
            if (!_states.TryGetValue(serviceName, out var state)) return;

            if (state.Status == ServiceStatus.Running || state.Status == ServiceStatus.Starting)
                return;

            UpdateStatus(state, ServiceStatus.Starting);

            try
            {
                var runner = new ProcessRunner(serviceName);
                runner.LogReceived += OnLogReceived;
                runner.ProcessExited += exitCode => OnProcessExited(serviceName, exitCode);
                runner.ProcessIDHandled += pidID =>
                {
                    state.ProcessId = pidID;
                    UpdateStatus(state, ServiceStatus.Running);
                };
                _runners[serviceName] = runner;
                state.StartedAt = DateTime.Now;
                await runner.StartAsync(state.Definition);
            }
            catch (Exception ex)
            {
                state.LastError = ex.Message;
                UpdateStatus(state, ServiceStatus.Error);
                AddSystemLog(serviceName, $"Başlatma hatası: {ex.Message}", LogLevel.Error);
            }
        });
    }

    public async Task StopAsync(string serviceName)
    {
        await Task.Run(async () =>
        {
            if (!_states.TryGetValue(serviceName, out var state)) return;

            if (_runners.TryGetValue(serviceName, out var runner))
            {
                await runner.StopAsync();
                runner.Dispose();
                _runners.Remove(serviceName);
            }

            state.ProcessId = null;
            UpdateStatus(state, ServiceStatus.Stopped);
        });
    }

    public async Task RestartAsync(string serviceName)
    {
        await Task.Run(async () =>
        {
            await StopAsync(serviceName);
            await Task.Delay(500);
            await StartAsync(serviceName);
        });
    }

    public async Task StartAllAsync()
    {
        var tasks = _states.Keys.Select(name => StartAsync(name));
        await Task.WhenAll(tasks);
    }

    public async Task StopAllAsync()
    {
        var tasks = _states.Keys.Select(name => StopAsync(name));
        await Task.WhenAll(tasks);
    }

    // ──────────────────────────────────────────────
    // Log erişimi
    // ──────────────────────────────────────────────

    public IReadOnlyList<LogEntry> GetLogs(string serviceName) =>
        _logs.TryGetValue(serviceName, out var list)
            ? list.AsReadOnly()
            : Array.Empty<LogEntry>();

    public void ClearLogs(string serviceName)
    {
        if (_logs.TryGetValue(serviceName, out var list))
            list.Clear();
    }

    // ──────────────────────────────────────────────
    // İç yardımcılar
    // ──────────────────────────────────────────────

    private void OnLogReceived(LogEntry entry)
    {
        if (_logs.TryGetValue(entry.ServiceName, out var list))
        {
            list.Add(entry);
            // Bellek sınırı: son 5000 satır
            if (list.Count > 5000)
                list.RemoveRange(0, list.Count - 5000);
        }
        LogReceived?.Invoke(entry);
    }

    private void OnProcessExited(string serviceName, int exitCode)
    {
        if (!_states.TryGetValue(serviceName, out var state)) return;

        state.ProcessId = null;
        var newStatus = exitCode == 0 ? ServiceStatus.Stopped : ServiceStatus.Error;
        if (exitCode != 0)
            state.LastError = $"Process beklenmedik şekilde kapandı. Çıkış kodu: {exitCode}";
        UpdateStatus(state, newStatus);
    }

    private async Task PollAllAsync()
    {
        foreach (var state in _states.Values)
        {
            if (state.Status != ServiceStatus.Running) continue;
            await VerifyRunningAsync(state);
        }
    }

    private async Task VerifyRunningAsync(ServiceState state)
    {
        var def = state.Definition;

        // 1. Port kontrolü (Sadece Port > 0 ise)
        bool portOpen = true;
        if (def.Port > 0)
            portOpen = await PortChecker.IsPortOpenAsync(def.Port);

        // 2. Health check (Sadece URL varsa)
        bool healthy = true;
        if (!string.IsNullOrWhiteSpace(def.HealthCheckUrl))
            healthy = await HealthChecker.CheckAsync(def.HealthCheckUrl);

        if (!portOpen || !healthy)
        {
            // Runner hâlâ process tutuyorsa gerçekten çalışıyor demektir
            // ama port/health başarısızsa Error olarak işaretle
            if (_runners.TryGetValue(def.Name, out var runner) && runner.ProcessId is not null)
            {
                // Process var ama yanıt vermiyor
                if (state.Status == ServiceStatus.Running)
                {
                    if (!portOpen)
                        state.LastError = $"Port {def.Port} kapalı (servis yanıt vermiyor)";
                    else if (!healthy)
                        state.LastError = "Health check başarısız";
                }
            }
        }
    }

    private void UpdateStatus(ServiceState state, ServiceStatus newStatus)
    {
        state.Status = newStatus;
        StatusChanged?.Invoke(state);
    }

    private void AddSystemLog(string serviceName, string message, LogLevel level = LogLevel.System)
    {
        var entry = new LogEntry(serviceName, message, level);
        OnLogReceived(entry);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Dispose();
        foreach (var runner in _runners.Values)
            runner.Dispose();
        _runners.Clear();
    }
}
