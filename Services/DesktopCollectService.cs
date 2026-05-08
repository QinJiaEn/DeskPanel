using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeskPanel.Models;

namespace DeskPanel.Services;

public class CollectResult
{
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<FileEntry> NewEntries { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public static class DesktopCollectService
{
    public static List<string> ScanDesktopFiles()
    {
        var files = new List<string>();
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
                // Get files (exclude .lnk shortcuts and hidden files)
                foreach (var file in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".lnk" || ext == ".ini") continue;
                    var attr = File.GetAttributes(file);
                    if (attr.HasFlag(FileAttributes.Hidden)) continue;
                    files.Add(file);
                }
            }
            catch { }
        }
        return files;
    }

    public static CollectResult CollectDesktopFiles(Category targetCategory)
    {
        var result = new CollectResult();
        var files = ScanDesktopFiles();
        result.TotalFiles = files.Count;

        foreach (var file in files)
        {
            try
            {
                var entry = FileOperationService.MoveFile(file, targetCategory);
                result.NewEntries.Add(entry);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return result;
    }
}
