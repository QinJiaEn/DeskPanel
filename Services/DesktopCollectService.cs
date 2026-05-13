using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeskPanel.Models;

namespace DeskPanel.Services;

public class CollectResult
{
    public int TotalItems { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<FileEntry> NewEntries { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, int> CategoryStats { get; set; } = new(); // key=categoryName, value=count
    public bool UsedAi { get; set; }
}

public static class DesktopCollectService
{
    /// <summary>
    /// Scan desktop for files AND directories, excluding system items.
    /// </summary>
    public static (List<string> Files, List<string> Directories) ScanDesktopItems()
    {
        var files = new List<string>();
        var directories = new List<string>();
        var desktopPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var dir in desktopPaths.Distinct())
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                // Scan files (exclude only .ini and hidden files, include .lnk shortcuts)
                foreach (var file in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".ini") continue;
                    var attr = File.GetAttributes(file);
                    if (attr.HasFlag(FileAttributes.Hidden)) continue;
                    files.Add(file);
                }

                // Scan directories (exclude system hidden folders)
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var dirName = Path.GetFileName(subDir);
                    // Skip known system folders
                    if (dirName.StartsWith("$") || dirName == "System Volume Information") continue;
                    var attr = File.GetAttributes(subDir);
                    if (attr.HasFlag(FileAttributes.Hidden)) continue;
                    directories.Add(subDir);
                }
            }
            catch { }
        }
        return (files, directories);
    }

    /// <summary>
    /// Smart collect: auto-categorize files by extension, handle directories too.
    /// When a specific category is selected, all items go there.
    /// When "全部" is selected, smart-match each file to its category.
    /// If AI mode is active (and no forced category), uses OpenAI to categorize.
    /// </summary>
    public static CollectResult SmartCollectDesktop(List<Category> categories, Category? forcedCategory = null, Action<string>? onProgress = null)
    {
        var result = new CollectResult();
        var (files, directories) = ScanDesktopItems();
        result.TotalItems = files.Count + directories.Count;

        // ── AI mode: batch-categorize all files via DeepSeek ──
        Dictionary<string, string>? aiMap = null;
        if (forcedCategory == null)
        {
            var settings = SettingsService.Current;
            Console.WriteLine($"[AI] AiMode={settings.AiMode}, OpenAiKey={!string.IsNullOrWhiteSpace(settings.OpenAiKey)}, BaseUrl={settings.AiBaseUrl}, Model={settings.AiModel}");
            if (settings.AiMode && !string.IsNullOrWhiteSpace(settings.OpenAiKey))
            {
                onProgress?.Invoke("AI 正在分析文件...");
                try
                {
                    var fileNames = files.Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
                    Console.WriteLine($"[AI] 开始调用 AiCategorizationService, 文件数={fileNames.Count}");
                    aiMap = AiCategorizationService.CategorizeAsync(
                        fileNames,
                        categories.Select(c => c.Name).ToList(),
                        settings.OpenAiKey).GetAwaiter().GetResult();
                    result.UsedAi = true;
                    Console.WriteLine($"[AI] AI 分类成功, 返回 {aiMap.Count} 个映射");
                    onProgress?.Invoke($"AI 分析完成，共 {fileNames.Count} 个文件");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI] 异常: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"[AI] 堆栈: {ex.StackTrace}");
                    result.Errors.Add($"[AI] 分类失败，回退到规则分类: {ex.Message}");
                    onProgress?.Invoke("AI 分析失败，回退到规则分类");
                }
            }
            else
            {
                Console.WriteLine("[AI] 跳过 AI 分类（AiMode 未开启或 ApiKey 未配置）");
            }
        }
        else
        {
            Console.WriteLine("[AI] 跳过 AI 分类（指定了强制分类）");
        }

        // Collect files
        foreach (var file in files)
        {
            try
            {
                Category targetCat;
                if (forcedCategory != null)
                {
                    targetCat = forcedCategory;
                }
                else if (aiMap != null && aiMap.TryGetValue(Path.GetFileName(file), out var aiCatName))
                {
                    // Use AI-suggested category
                    targetCat = categories.FirstOrDefault(c =>
                        c.Name.Equals(aiCatName, StringComparison.OrdinalIgnoreCase))
                        ?? categories.First();
                }
                else
                {
                    // Smart categorization: try to match by extension
                    var suggestedId = FileOperationService.GetSuggestedCategoryId(file, categories);
                    targetCat = suggestedId != null
                        ? categories.First(c => c.Id == suggestedId)
                        : categories.First(); // fallback to first category
                }

                var entry = FileOperationService.MoveFile(file, targetCat);
                result.NewEntries.Add(entry);
                result.SuccessCount++;

                // Track per-category stats
                var catName = targetCat.Name;
                if (!result.CategoryStats.ContainsKey(catName))
                    result.CategoryStats[catName] = 0;
                result.CategoryStats[catName]++;
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        // Collect directories (always use first category unless forced)
        foreach (var dir in directories)
        {
            try
            {
                Category targetCat;
                if (forcedCategory != null)
                {
                    targetCat = forcedCategory;
                }
                else
                {
                    targetCat = categories.First(); // directories go to first category by default
                }

                var entry = FileOperationService.MoveDirectory(dir, targetCat);
                result.NewEntries.Add(entry);
                result.SuccessCount++;

                var catName = targetCat.Name;
                if (!result.CategoryStats.ContainsKey(catName))
                    result.CategoryStats[catName] = 0;
                result.CategoryStats[catName]++;
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"[文件夹] {Path.GetFileName(dir)}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Legacy compatibility: collect all desktop files into a single category.
    /// </summary>
    public static CollectResult CollectDesktopFiles(Category targetCategory)
    {
        return SmartCollectDesktop(
            DataService.GetCategories(),
            forcedCategory: targetCategory);
    }
}
