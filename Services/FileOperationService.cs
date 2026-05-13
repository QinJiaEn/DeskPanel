using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DeskPanel.Models;

namespace DeskPanel.Services;

public static class FileOperationService
{
    public static string GetCategoryPath(string categoryName)
    {
        var storageRoot = SettingsService.Current.StoragePath;
        var path = Path.Combine(storageRoot, SanitizeFolderName(categoryName));
        Directory.CreateDirectory(path);
        return path;
    }

    public static FileEntry StoreFile(string sourcePath, Category targetCategory)
    {
        var destDir = GetCategoryPath(targetCategory.Name);
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDir, fileName);

        // Handle duplicate names
        destPath = GetUniqueFilePath(destPath);

        // Copy file to storage
        File.Copy(sourcePath, destPath, overwrite: false);

        var fileInfo = new FileInfo(destPath);
        return new FileEntry
        {
            FileName = Path.GetFileName(destPath),
            OriginalPath = sourcePath,
            StoredPath = destPath,
            CategoryId = targetCategory.Id,
            AddedTime = DateTime.Now,
            FileSize = fileInfo.Length
        };
    }

    public static FileEntry MoveFile(string sourcePath, Category targetCategory)
    {
        // Route directories to MoveDirectory
        if (Directory.Exists(sourcePath))
            return MoveDirectory(sourcePath, targetCategory);

        var destDir = GetCategoryPath(targetCategory.Name);
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDir, fileName);
        destPath = GetUniqueFilePath(destPath);

        File.Move(sourcePath, destPath);

        var fileInfo = new FileInfo(destPath);
        return new FileEntry
        {
            FileName = Path.GetFileName(destPath),
            OriginalPath = sourcePath,
            StoredPath = destPath,
            CategoryId = targetCategory.Id,
            AddedTime = DateTime.Now,
            FileSize = fileInfo.Length
        };
    }

    public static void OpenFile(FileEntry entry)
    {
        if (entry.IsMissing)
        {
            var itemType = entry.IsDirectory ? "文件夹" : "文件";
            MessageBox.Show($"{itemType}不存在:\n{entry.StoredPath}", $"{itemType}丢失",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = entry.StoredPath,
            UseShellExecute = true
        });
    }

    public static void DeleteFile(FileEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (Directory.Exists(entry.StoredPath))
                Directory.Delete(entry.StoredPath, recursive: true);
        }
        else
        {
            if (File.Exists(entry.StoredPath))
                File.Delete(entry.StoredPath);
        }
    }

    public static void MoveFileToCategory(FileEntry entry, Category newCategory)
    {
        if (entry.IsDirectory)
        {
            if (!Directory.Exists(entry.StoredPath))
                throw new DirectoryNotFoundException($"文件夹不存在: {entry.StoredPath}");

            var destDir = GetCategoryPath(newCategory.Name);
            var destPath = Path.Combine(destDir, Path.GetFileName(entry.StoredPath));
            destPath = GetUniqueDirectoryPath(destPath);

            Directory.Move(entry.StoredPath, destPath);
            entry.StoredPath = destPath;
            entry.CategoryId = newCategory.Id;
        }
        else
        {
            if (!File.Exists(entry.StoredPath))
                throw new FileNotFoundException("文件不存在", entry.StoredPath);

            var destDir = GetCategoryPath(newCategory.Name);
            var destPath = Path.Combine(destDir, Path.GetFileName(entry.StoredPath));
            destPath = GetUniqueFilePath(destPath);

            File.Move(entry.StoredPath, destPath);
            entry.StoredPath = destPath;
            entry.CategoryId = newCategory.Id;
        }
    }

    public static bool RenameFile(FileEntry entry, string newName)
    {
        if (entry.IsDirectory)
        {
            if (!Directory.Exists(entry.StoredPath))
                return false;

            var dir = Path.GetDirectoryName(entry.StoredPath)!;
            var destPath = Path.Combine(dir, newName);
            destPath = GetUniqueDirectoryPath(destPath);

            Directory.Move(entry.StoredPath, destPath);
            entry.StoredPath = destPath;
            entry.FileName = Path.GetFileName(destPath);
            return true;
        }
        else
        {
            if (!File.Exists(entry.StoredPath))
                return false;

            var dir = Path.GetDirectoryName(entry.StoredPath)!;
            var destPath = Path.Combine(dir, newName);
            destPath = GetUniqueFilePath(destPath);

            File.Move(entry.StoredPath, destPath);
            entry.StoredPath = destPath;
            entry.FileName = Path.GetFileName(destPath);
            return true;
        }
    }

    private static string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
        } while (File.Exists(newPath));
        return newPath;
    }

    private static string SanitizeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "未分类" : name;
    }

    // ── Smart categorization ──────────────────────────────────────

    public static string? GetSuggestedCategoryId(string filePath, List<Category> categories)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        if (string.IsNullOrEmpty(ext)) return null;

        // Map extension to category name keyword
        string? keyword = ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" => "图片",
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".pdf" or ".txt" or ".md" => "文档",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => "音乐",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" => "视频",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "压缩",
            ".exe" or ".msi" or ".bat" or ".cmd" or ".lnk" => "工具",
            ".cs" or ".py" or ".js" or ".ts" or ".java" or ".json" or ".xml" or ".html" or ".css" or ".cpp" or ".c" => "代码",
            _ => null
        };

        if (keyword == null) return null;

        // Find matching category by name keyword
        var match = categories.FirstOrDefault(c =>
            c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    // ── Folder/directory operations ────────────────────────────────

    public static FileEntry MoveDirectory(string sourcePath, Category targetCategory)
    {
        var destDir = GetCategoryPath(targetCategory.Name);
        var dirName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDir, dirName);
        destPath = GetUniqueDirectoryPath(destPath);

        // Same drive? Instant Directory.Move — no copying needed
        if (Path.GetPathRoot(sourcePath) == Path.GetPathRoot(destPath))
        {
            Directory.Move(sourcePath, destPath);
        }
        else
        {
            CopyDirectoryRecursive(sourcePath, destPath);
            Directory.Delete(sourcePath, recursive: true);
        }

        return new FileEntry
        {
            FileName = Path.GetFileName(destPath),
            OriginalPath = sourcePath,
            StoredPath = destPath,
            CategoryId = targetCategory.Id,
            AddedTime = DateTime.Now,
            FileSize = GetDirectorySize(destPath),
            IsDirectory = true
        };
    }

    // ── Restore to desktop ─────────────────────────────────────────

    public static bool RestoreToDesktop(FileEntry entry)
    {
        if (entry.IsMissing)
            return false;

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // Prefer original path if its parent directory still exists
        string destDir;
        if (!string.IsNullOrEmpty(entry.OriginalPath))
        {
            var origDir = Path.GetDirectoryName(entry.OriginalPath)!;
            destDir = Directory.Exists(origDir) ? origDir : desktopPath;
        }
        else
        {
            destDir = desktopPath;
        }

        string destPath = Path.Combine(destDir, entry.FileName);
        destPath = GetUniqueFilePath(destPath);

        if (entry.IsDirectory)
        {
            // Same drive? Instant Directory.Move — no copying needed
            if (Path.GetPathRoot(entry.StoredPath) == Path.GetPathRoot(destPath))
            {
                Directory.Move(entry.StoredPath, destPath);
            }
            else
            {
                CopyDirectoryRecursive(entry.StoredPath, destPath);
                Directory.Delete(entry.StoredPath, recursive: true);
            }
        }
        else
        {
            File.Move(entry.StoredPath, destPath);
        }

        entry.StoredPath = destPath;
        return true;
    }

    // ── Directory helpers ──────────────────────────────────────────

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        // Parallel copy files within this directory using large-buffer streams
        var files = Directory.GetFiles(sourceDir);
        Parallel.ForEach(files, file =>
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            CopyFileLarge(file, destFile);
        });

        // Process subdirectories recursively
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }

    /// <summary>High-speed file copy with 1MB buffer for large files.</summary>
    private static void CopyFileLarge(string source, string dest)
    {
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read,
            FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        using var dst = new FileStream(dest, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
        src.CopyTo(dst);
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            Parallel.ForEach(files, file =>
            {
                try
                {
                    var len = new FileInfo(file).Length;
                    Interlocked.Add(ref size, len);
                }
                catch { }
            });
        }
        catch { }
        return size;
    }

    private static string GetUniqueDirectoryPath(string path)
    {
        if (!Directory.Exists(path)) return path;

        var parent = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(parent, $"{name} ({counter})");
            counter++;
        } while (Directory.Exists(newPath));
        return newPath;
    }
}
