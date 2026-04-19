namespace DevManager.Helpers;

public static class HealthChecker
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    /// <summary>
    /// Health check URL'ine GET isteği atar; 2xx dönerse sağlıklı kabul eder.
    /// </summary>
    public static async Task<bool> CheckAsync(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
