using System.IO;
using System.Text.Json;
using HypixelStatsOverlay.Models;

namespace HypixelStatsOverlay.Services;

public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HypixelStatsOverlay");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // fall through to defaults on any read/parse failure
        }

        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
