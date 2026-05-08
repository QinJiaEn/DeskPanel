using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace DeskPanel.Models;

public class AppSettings
{
    // === Appearance ===
    public string AccentColor { get; set; } = "#0078D4";
    public double Opacity { get; set; } = 0.95;
    public int ThemeMode { get; set; } = 0; // 0=Light, 1=Dark, 2=Follow system

    // === Behavior ===
    public int HotkeyModifiers { get; set; } = 1; // MOD_ALT
    public uint HotkeyKey { get; set; } = 0xC0; // VK_OEM_3 (`)
    public bool AutoStart { get; set; } = false;
    public string StoragePath { get; set; } = @"F:\DeskPanel\files\";

    // === Panel ===
    public double PanelWidth { get; set; } = 960;
    public double PanelHeight { get; set; } = 620;

    // === Computed ===
    [JsonIgnore]
    public int EffectiveThemeMode => ThemeMode switch
    {
        2 => IsSystemDark() ? 1 : 0,
        _ => ThemeMode
    };

    [JsonIgnore]
    public static AppSettings Default => new();

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is 0;
        }
        catch { return false; }
    }
}
