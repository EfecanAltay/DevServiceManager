using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevManager.Models;
using Microsoft.VisualBasic.Devices;

namespace DevManager.Services;

/// <summary>
/// Tek bir servis için process yaşam döngüsünü yönetir.
/// stdout/stderr async okunur ve LogReceived event'i ile dışarı iletilir.
/// </summary>
public class ProcessRunner : IDisposable
{
    private readonly object _lock = new();
    private Process? _process;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<int>? ProcessIDHandled;
    public event Action<LogEntry>? LogReceived;
    public event Action<int>? ProcessExited; // exit code

    public string ServiceName { get; }

    public int? ProcessId => _process is { HasExited: false } ? _process?.Id : null;



    public ProcessRunner(string serviceName)
    {
        ServiceName = serviceName;
    }

    public async Task StartAsync(ServiceDefinition def, CancellationToken cancellationToken = default)
    {
        if (_process is { HasExited: false })
            throw new InvalidOperationException($"{ServiceName} zaten çalışıyor.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var psi = CreateProcessStartInfo(def);
        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _process.Exited += OnProcessExited;

        _process.Start();

        this.ProcessIDHandled?.Invoke(_process.Id);

        Emit($"Process başlatıldı. PID: {_process.Id}", LogLevel.System);

        Process? process;
        if (_process != null)
        {
            process = _process;
            _ = ReadStreamAsync(process.StandardOutput, LogLevel.Info, _cts.Token);
            _ = ReadStreamAsync(process.StandardError, LogLevel.Error, _cts.Token);
        }

        await Task.CompletedTask;
    }

    public async Task StopAsync2()
    {
        if (_process == null || _process.HasExited)
        {
            Emit("Process zaten durmuş.", LogLevel.System);
            return;
        }
        var process = _process;
        if (process == null) return;

        process.Kill(true);

        try
        {
            _cts?.Cancel();
            await process.WaitForExitAsync(CancellationToken.None)
                         .WaitAsync(TimeSpan.FromSeconds(5));
            Emit("Process durduruldu.", LogLevel.System);

        }
        catch (TimeoutException ex)
        {
            Emit($"Durdurma hatası: {ex.Message}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            Emit($"Durdurma hatası: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            CleanupProcess();
        }
    }

    public async Task StopAsync()
    {
        Process? process;

        lock (_lock)
        {
            process = _process;
            _process = null;
        }

        if (process == null)
            return;

        try
        {
            int pid;

            try
            {
                pid = process.Id;
            }
            catch (InvalidOperationException)
            {
                Emit("Process artık aktif bir işletim sistemi süreci ile ilişkili değil.", LogLevel.Warning);
                process.Dispose();
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    this._cts?.Cancel();
                    await KillProcessTreeAsync(pid);
                }
            }
            catch (InvalidOperationException)
            {
                Emit("Process zaten kapanmış.", LogLevel.Warning);
            }

            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                Emit("Process kapanması beklenen sürede tamamlanmadı.", LogLevel.Warning);
            }
            catch (InvalidOperationException)
            {
                Emit("WaitForExit sırasında process ilişkisi yoktu.", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            Emit($"Durdurma hatası: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        int exitCode = _process?.ExitCode ?? -1;
        Emit($"Process sonlandı. Çıkış kodu: {exitCode}", LogLevel.System);
        ProcessExited?.Invoke(exitCode);
        CleanupProcess();
    }

    private async Task ReadStreamAsync(StreamReader reader, LogLevel level, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is not null)
                    Emit(line, level);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Emit($"Stream okuma hatası: {ex.Message}", LogLevel.Error);
        }
    }

    private static readonly Regex AnsiRegex = new(@"\x1b\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

    private void Emit(string message, LogLevel level)
    {
        // ANSI escape kodlarını temizle (terminaller için olan renk kodları vb.)
        var cleaned = AnsiRegex.Replace(message, string.Empty);
        LogReceived?.Invoke(new LogEntry(ServiceName, cleaned, level));
    }

    private ProcessStartInfo CreateProcessStartInfo(ServiceDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.WorkingDirectory) || !Directory.Exists(def.WorkingDirectory))
            throw new DirectoryNotFoundException(
                $"Çalıştırılacak dizin bulunamadı: '{def.WorkingDirectory}'. Lütfen services.json içindeki WorkingDirectory değerini doğrulayın.");

        var command = def.Command.Trim();
        var arguments = def.Arguments?.Trim() ?? string.Empty;

        // EĞER: Windows üzerindeyse VE (tam yol verilmişse VEYA .exe/.bat/.cmd uzantısı varsa VEYA komut içinde boşluk yoksa)
        // O zaman doğrudan çalıştırabiliriz (cmd.exe /c kullanmadan).
        bool isDirectExecutable = OperatingSystem.IsWindows() && 
            (Path.IsPathRooted(command) || 
             command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
             command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || 
             command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));

        if (isDirectExecutable)
        {
            return new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = def.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
        }

        // Değilse ve Windows'taysa, cmd.exe /c ile çalıştır (npm vb. scriptler için)
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{command}\" {arguments}".TrimEnd(),
                WorkingDirectory = def.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
        }

        // Unix tabanlı sistemler için (Linux/macOS)
        return new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = def.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
    }

    private async Task KillProcessTreeAsync(int pid)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }

    private void CleanupProcess()
    {
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        CleanupProcess();
    }
}
