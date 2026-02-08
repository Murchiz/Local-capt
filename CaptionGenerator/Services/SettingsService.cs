using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Models;

namespace CaptionGenerator.Services;

using System;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";

    // ⚡ Bolt Optimization: Cache JsonSerializerOptions to avoid reflection overhead and repeated allocations.
    // In .NET, reusing options allows the serializer to cache metadata for the target types.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private string? _cachedFilePath;

    private string GetSettingsFilePath()
    {
        if (_cachedFilePath != null) return _cachedFilePath;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "CaptionGenerator");

        // ⚡ Bolt Optimization: Only call Directory.CreateDirectory if the folder doesn't exist.
        // While CreateDirectory is relatively fast, avoiding unnecessary system calls is good practice.
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
            // ⚡ Bolt Optimization: Stream directly from the file to avoid allocating a large intermediate string.
            // This reduces memory footprint and GC pressure, especially for larger settings files.
            using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<Settings>(stream, _jsonOptions) ?? new Settings();
        }
        catch (Exception)
        {
            // If the settings file is corrupted or cannot be read, return default settings.
            return new Settings();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var filePath = GetSettingsFilePath();

        // ⚡ Bolt Optimization: Stream directly to the file instead of serializing to an intermediate string.
        // This is significantly more memory-efficient and faster for I/O.
        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
    }
}
