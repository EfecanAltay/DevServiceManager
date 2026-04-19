using DevManager.Configuration;
using DevManager.Forms;
using DevManager.Managers;

namespace DevManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Config yükle
        List<DevManager.Models.SolutionDefinition> solutions;
        bool configCreated = false;

        try
        {
            (solutions, configCreated) = ConfigurationLoader.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Yapılandırma yüklenemedi:\n\n{ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (configCreated)
        {
            MessageBox.Show(
                $"Örnek bir services.json oluşturuldu:\n\n{ConfigurationLoader.GetConfigPath()}\n\n" +
                "Lütfen servis tanımlarını düzenleyip uygulamayı yeniden başlatın.",
                "İlk Çalıştırma",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        var manager = new ServiceManager();
        manager.LoadServices(solutions);

        // AutoStart olan servisleri başlat
        var allServices = solutions.SelectMany(s => s.Services).ToList();
        var autoStartNames = allServices
            .Where(s => s.AutoStart)
            .Select(s => s.Name)
            .ToList();

        if (autoStartNames.Count > 0)
        {
            Task.Run(async () =>
            {
                foreach (var name in autoStartNames)
                    await manager.StartAsync(name);
            });
        }

        Application.Run(new MainForm(manager));
    }
}
