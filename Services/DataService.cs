using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DeskPanel.Models;

namespace DeskPanel.Services;

public class AppData
{
    public List<Category> Categories { get; set; } = new();
    public List<FileEntry> Files { get; set; } = new();
}

public static class DataService
{
    private static readonly string AppDir = Path.GetDirectoryName(
        System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    public static readonly string DataFilePath = Path.Combine(AppDir, "data.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static AppData? _cache;

    public static AppData Load()
    {
        if (_cache != null) return _cache;

        if (!File.Exists(DataFilePath))
        {
            _cache = CreateDefaultData();
            Save();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(DataFilePath);
            _cache = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? CreateDefaultData();
        }
        catch
        {
            _cache = CreateDefaultData();
        }
        return _cache;
    }

    public static void Save()
    {
        if (_cache == null) return;

        // Backup before write
        if (File.Exists(DataFilePath))
        {
            var bakPath = DataFilePath + ".bak";
            File.Copy(DataFilePath, bakPath, overwrite: true);
        }

        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        File.WriteAllText(DataFilePath, json);
    }

    public static List<Category> GetCategories()
    {
        var data = Load();
        return data.Categories.OrderBy(c => c.Order).ToList();
    }

    public static void SaveCategories(List<Category> categories)
    {
        var data = Load();
        data.Categories = categories;
        Save();
    }

    public static void AddCategory(Category category)
    {
        var data = Load();
        data.Categories.Add(category);
        Save();
    }

    public static void RemoveCategory(string categoryId)
    {
        var data = Load();
        data.Categories.RemoveAll(c => c.Id == categoryId);
        // Move files to first category or create "uncategorized"
        var firstCat = data.Categories.FirstOrDefault();
        if (firstCat == null)
        {
            firstCat = new Category { Name = "未分类", Color = "#6c7086", Order = 0 };
            data.Categories.Add(firstCat);
        }
        foreach (var file in data.Files.Where(f => f.CategoryId == categoryId))
        {
            file.CategoryId = firstCat.Id;
        }
        Save();
    }

    public static List<FileEntry> GetFiles()
    {
        var data = Load();
        return data.Files;
    }

    public static void AddFile(FileEntry entry)
    {
        var data = Load();
        data.Files.Add(entry);
        Save();
    }

    public static void RemoveFile(string fileId)
    {
        var data = Load();
        data.Files.RemoveAll(f => f.Id == fileId);
        Save();
    }

    public static void UpdateFile(FileEntry entry)
    {
        var data = Load();
        var idx = data.Files.FindIndex(f => f.Id == entry.Id);
        if (idx >= 0)
            data.Files[idx] = entry;
        Save();
    }

    public static int GetFileCount(string categoryId)
    {
        var data = Load();
        return data.Files.Count(f => f.CategoryId == categoryId);
    }

    private static AppData CreateDefaultData()
    {
        return new AppData
        {
            Categories = new List<Category>
            {
                new() { Name = "工作", Color = "#89b4fa", Order = 0 },
                new() { Name = "工具", Color = "#a6e3a1", Order = 1 },
                new() { Name = "临时", Color = "#f9e2af", Order = 2 },
                new() { Name = "图片", Color = "#f38ba8", Order = 3 },
                new() { Name = "文档", Color = "#cba6f7", Order = 4 },
            },
            Files = new List<FileEntry>()
        };
    }
}
