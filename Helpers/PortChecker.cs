using System.Net.Sockets;

namespace DevManager.Helpers;

public static class PortChecker
{
    /// <summary>
    /// Verilen porta TCP bağlantısı açık mı kontrol eder.
    /// </summary>
    public static async Task<bool> IsPortOpenAsync(int port, int timeoutMs = 1000)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(connectTask, timeoutTask);
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
