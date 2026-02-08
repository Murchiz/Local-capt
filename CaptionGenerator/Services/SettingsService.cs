using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Models;

namespace CaptionGenerator.Services;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";

    // ⚡ Bolt Optimization: Cache JsonSerializerOptions to avoid repeated reflection overhead
    // and metadata generation on every serialization/deserialization call.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    // ⚡ Bolt Optimization: Cache the settings file path to avoid redundant environment
    // variable lookups, path combinations, and directory creation checks.
    private string? _settingsFilePath;

    private string GetSettingsFilePath()
    {
        if (_settingsFilePath != null) return _settingsFilePath;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "CaptionGenerator");
        Directory.CreateDirectory(appFolderPath);
        _settingsFilePath = Path.Combine(appFolderPath, SettingsFileName);
        return _settingsFilePath;
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        var filePath = GetSettingsFilePath();
        if (!File.Exists(filePath))
        {
            return new Settings();
        }

        // ⚡ Bolt Optimization: Stream the settings directly from disk using File.OpenRead
        // and JsonSerializer.DeserializeAsync. This avoids reading the entire file into
        // a large intermediate string, reducing memory allocations and pressure.
        try
        {
            using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<Settings>(stream, _jsonOptions) ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var filePath = GetSettingsFilePath();

        // ⚡ Bolt Optimization: Stream the settings directly to disk using File.Create
        // and JsonSerializer.SerializeAsync. This avoids serializing to a large intermediate
        // string before writing, minimizing RAM usage during persistence.
        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
    }
}
