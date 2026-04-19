# Dev Service Manager

.NET 8 Windows Forms tabanlı local servis yönetim uygulaması.

## Proje Yapısı

```
DevManager/
├── Configuration/
│   └── ConfigurationLoader.cs     # services.json okuma, örnek config üretme
├── Forms/
│   └── MainForm.cs                # Ana pencere, tüm panelleri birleştirir
├── Helpers/
│   ├── AppTheme.cs                # Renkler ve tasarım sabitleri
│   ├── HealthChecker.cs           # HTTP GET health check
│   └── PortChecker.cs             # TCP port kontrolü
├── Managers/
│   └── ServiceManager.cs          # Tüm servislerin orkestratörü
├── Models/
│   ├── LogEntry.cs                # Log satırı modeli
│   ├── ServiceDefinition.cs       # Config'den gelen servis tanımı
│   └── ServiceState.cs            # Runtime durum modeli
├── Services/
│   └── ProcessRunner.cs           # Tek servis için process yaşam döngüsü
├── UserControls/
│   ├── LogPanel.cs                # Canlı log görüntüleyici
│   ├── ServiceDetailPanel.cs      # Sağ panel: bilgi + butonlar
│   └── ServiceListItem.cs         # Sol panel: servis kart bileşeni
├── Program.cs                     # Giriş noktası
├── services.json                  # Servis tanımları (düzenlenebilir)
└── DevManager.csproj
```

## Kurulum ve Derleme

```bash
# Gereksinim: .NET 8 SDK (Windows)
dotnet restore
dotnet build
dotnet run
```

## services.json Yapısı

```json
[
  {
    "name": "auth-service",
    "workingDirectory": "C:\\Projects\\services\\auth-service",
    "command": "npm",
    "arguments": "run dev",
    "port": 4000,
    "healthCheckUrl": "http://localhost:4000/health",
    "autoStart": false
  }
]
```

### Alan Açıklamaları

| Alan | Açıklama |
|------|----------|
| `name` | Servis görünen adı |
| `workingDirectory` | Process'in başlatılacağı klasör |
| `command` | Çalıştırılacak program (`npm`, `dotnet`, `node`, vb.) |
| `arguments` | Komut argümanları |
| `port` | Servisin dinlediği port (TCP kontrolü için) |
| `healthCheckUrl` | Opsiyonel HTTP health check adresi |
| `autoStart` | Uygulama açılırken otomatik başlat |

## Mimari Kararlar

- **ServiceManager**: Tüm servislerin merkezi yöneticisi. UI sadece bu sınıfla konuşur.
- **ProcessRunner**: Her servis için ayrı instance. stdout/stderr async okunur, UI thread bloke edilmez.
- **Polling**: Her 5 saniyede port + health check ile servis durumu doğrulanır.
- **Log buffer**: Servis başına son 5000 satır tutulur.
- **Kapanış**: Çalışan servisler için kullanıcıya onay sorulur.
