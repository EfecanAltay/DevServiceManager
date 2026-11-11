using System.Text.Json;
using DevManager.Models;

namespace DevManager.Configuration;

public static class ConfigurationLoader
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "services.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static (List<SolutionDefinition> Solutions, bool WasCreated) Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var sample = CreateSampleConfig();
            Save(sample);
            return (sample, true);
        }

        var result = LoadFromFile(ConfigPath);
        if (EnsureKibanaService(result.Solutions))
            Save(result.Solutions);
        return result;
    }

    /// <summary>
    /// Kibana tanımı yoksa varsayılan olarak D:\Elastic\kibana-9.5.6\bin\kibana.bat'ı çalıştıracak
    /// servisi, Elasticsearch ile aynı solution'a (yoksa "Elastic" solution'ına) ekler.
    /// </summary>
    private static bool EnsureKibanaService(List<SolutionDefinition> solutions)
    {
        bool Has(string name) => solutions.Any(s =>
            s.Services.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        if (Has("kibana")) return false;

        var kibana = new ServiceDefinition
        {
            Name = "kibana",
            WorkingDirectory = @"D:\Elastic\kibana-9.5.6\bin",
            Command = "kibana.bat",
            Arguments = "",
            Port = 5601,
            HealthCheckUrl = null,
            AutoStart = false
        };

        var target = solutions.FirstOrDefault(s =>
                         s.Services.Any(x => x.Name.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase)))
                     ?? solutions.FirstOrDefault(s => s.Name.Equals("Elastic", StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            target = new SolutionDefinition { Name = "Elastic", Services = [] };
            solutions.Add(target);
        }

        target.Services.Add(kibana);
        return true;
    }

    public static (List<SolutionDefinition> Solutions, bool WasCreated) LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var solutions = JsonSerializer.Deserialize<List<SolutionDefinition>>(json, JsonOptions)
                           ?? new List<SolutionDefinition>();
            return (solutions, false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Yapılandırma dosyası okunamadı: {ex.Message}", ex);
        }
    }

    public static void Save(List<SolutionDefinition> solutions, string? path = null)
    {
        var targetPath = path ?? ConfigPath;
        var json = JsonSerializer.Serialize(solutions, JsonOptions);
        File.WriteAllText(targetPath, json);
    }

    public static string GetConfigPath() => ConfigPath;

    private static List<SolutionDefinition> CreateSampleConfig() =>
    [
        new SolutionDefinition
        {
            Name = "TheFoodHeroServer",
            Services = [
                new ServiceDefinition { Name = "app-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:app", Port = 1919, HealthCheckUrl = "http://localhost:1919/health" },
                new ServiceDefinition { Name = "auth-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:auth", Port = 4000, HealthCheckUrl = "http://localhost:4000/health" },
                new ServiceDefinition { Name = "geo-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:geo", Port = 5005, HealthCheckUrl = "http://localhost:5005/health" },
                new ServiceDefinition { Name = "user-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:user", Port = 5000, HealthCheckUrl = "http://localhost:5000/health" },
                new ServiceDefinition { Name = "food-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:food", Port = 5002, HealthCheckUrl = "http://localhost:5002/health" },
                new ServiceDefinition { Name = "notify-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:notify", Port = 3001, HealthCheckUrl = "http://localhost:3001/health" },
                new ServiceDefinition { Name = "appointments-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:appointments", Port = 5001, HealthCheckUrl = "http://localhost:5001/health" },
                new ServiceDefinition { Name = "diet-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:diet", Port = 4004, HealthCheckUrl = "http://localhost:4004/health" },
                new ServiceDefinition { Name = "payment-service", WorkingDirectory = @"D:\Dev\TheFoodHeroAPI", Command = "npm", Arguments = "run serve:payment", Port = 5003, HealthCheckUrl = "http://localhost:5003/health" },
                new ServiceDefinition { Name = "proxy-server", WorkingDirectory = @"D:\Dev\TFH_Proxy_Server", Command = "node", Arguments = "proxy-server.js", Port = 0 },
                new ServiceDefinition { Name = "consul", WorkingDirectory = @"D:\Consul", Command = "consul.exe", Arguments = @"agent -server -bootstrap-expect=1 -client=0.0.0.0 -bind=0.0.0.0 -ui -data-dir=D:\Consul\data -config-dir=D:\Consul\config.d", Port = 8500, HealthCheckUrl = "http://localhost:8500/ui" }
            ]
        },
        new SolutionDefinition
        {
            Name = "TheFoodHeroWEB",
            Services = [
                new ServiceDefinition { Name = "web-server", WorkingDirectory = @"D:\Dev\TheFoodHeroWeb", Command = "npm", Arguments = "run dev", Port = 5173, HealthCheckUrl = "http://localhost:5173" }
            ]
        }
    ];
}
