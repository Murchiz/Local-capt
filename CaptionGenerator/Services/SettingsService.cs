using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Models;

namespace CaptionGenerator.Services;

using System;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";

    private string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "CaptionGenerator");
        Directory.CreateDirectory(appFolderPath);
        return Path.Combine(appFolderPath, SettingsFileName);
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        var filePath = GetSettingsFilePath();
        if (!File.Exists(filePath))
        {
            return new Settings();
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var filePath = GetSettingsFilePath();
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
}
