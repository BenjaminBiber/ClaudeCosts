using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCosts.App.Services;

/// <summary>Loads/saves <see cref="AppSettings"/> and locates the app data directory.</summary>
public sealed class SettingsService
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeCosts");

    public string SettingsPath { get; } = Path.Combine(AppDataDir, "settings.json");

    public static string PricingPath { get; } = Path.Combine(AppDataDir, "pricing.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions);
                if (s is not null) return s;
            }
        }
        catch
        {
            // corrupt settings → start from defaults
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // non-fatal: settings just won't persist this session
        }
    }
}
