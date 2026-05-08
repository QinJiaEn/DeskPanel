using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DeskPanel.Models;

namespace DeskPanel.Services;

public static class FileOperationService
{
    private static readonly string StorageRoot = @"F:\DeskPanel\files";

    public static string GetCategoryPath(string categoryName)
    {
        var path = Path.Combine(StorageRoot, SanitizeFolderName(categoryName));
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
        if (!File.Exists(entry.StoredPath))
        {
            MessageBox.Show($"文件不存在:\n{entry.StoredPath}", "文件丢失",
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
        if (File.Exists(entry.StoredPath))
            File.Delete(entry.StoredPath);
    }

    public static void MoveFileToCategory(FileEntry entry, Category newCategory)
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

    public static bool RenameFile(FileEntry entry, string newName)
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
}
