using System;
using System.Text.Json.Serialization;

namespace DeskPanel.Models;

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#89b4fa";
    public int Order { get; set; }

    [JsonIgnore]
    public int FileCount { get; set; }
}
