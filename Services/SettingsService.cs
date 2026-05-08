using System;
using System.IO;
using System.Text.Json;
using DeskPanel.Models;

namespace DeskPanel.Services;

public static class SettingsService
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    public static AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return AppSettings.Default;

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                   ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public static void Save(AppSettings settings)
    {
        // Backup before write
        if (File.Exists(FilePath))
        {
            var bakPath = FilePath + ".bak";
            File.Copy(FilePath, bakPath, overwrite: true);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
        _current = settings;
    }
}
