using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Models;

namespace CaptionGenerator.Services;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private string? _cachedFilePath;

    private string GetSettingsFilePath()
    {
        if (_cachedFilePath != null) return _cachedFilePath;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "CaptionGenerator");

        if (!Directory.Exists(appFolderPath))
        {
            Directory.CreateDirectory(appFolderPath);
        }

        _cachedFilePath = Path.Combine(appFolderPath, SettingsFileName);
        return _cachedFilePath;
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        var filePath = GetSettingsFilePath();
        if (!File.Exists(filePath))
        {
            return new Settings();
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            // FIX: Pass the AppJsonContext.Default.Settings explicitly
            return await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.Settings) ?? new Settings();
        }
        catch (Exception)
        {
            return new Settings();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var filePath = GetSettingsFilePath();
        using var stream = File.Create(filePath);

        // FIX: Pass the AppJsonContext.Default.Settings explicitly
        await JsonSerializer.SerializeAsync(stream, settings, AppJsonContext.Default.Settings);
    }
}