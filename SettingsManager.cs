using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mif;

public static class SettingsManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mif",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            string path = SettingsPath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    if (loaded.GifColorCount is < 2 or > 256)
                        loaded.GifColorCount = 256;
                    return loaded;
                }
            }
        }
        catch { /* ignore */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            string path = SettingsPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch { /* ignore */ }
    }
}
