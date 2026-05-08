using System;
using System.IO;
using System.Text.Json.Serialization;

namespace DeskPanel.Models;

public class FileEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public DateTime AddedTime { get; set; } = DateTime.Now;
    public long FileSize { get; set; }

    [JsonIgnore]
    public bool IsMissing => !System.IO.File.Exists(StoredPath);

    [JsonIgnore]
    public string FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
    };

    [JsonIgnore]
    public string Tooltip => $"文件: {FileName}\n大小: {FileSizeDisplay}\n添加: {AddedTime:yyyy-MM-dd HH:mm}\n{(IsMissing ? "⚠ 文件已丢失" : StoredPath)}";
}
