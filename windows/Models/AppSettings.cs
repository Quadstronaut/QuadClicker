using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuadClicker.Models;

/// <summary>User preferences persisted to %APPDATA%\QuadClicker\settings.json.</summary>
public sealed class AppSettings
{
    public string      ClickRateValue      { get; set; } = "100";
    public string      ClickRateUnit       { get; set; } = "ms";
    public MouseButton Button              { get; set; } = MouseButton.Left;
    public ClickType   ClickType           { get; set; } = ClickType.Single;
    public bool        UseCurrentPosition  { get; set; } = true;
    public int         X                   { get; set; } = 0;
    public int         Y                   { get; set; } = 0;
    public int         StopAfterClicks     { get; set; } = 0;
    public int         StopAfterSeconds    { get; set; } = 0;
    public int         IdleWaitSeconds     { get; set; } = 0;
    public bool        AlwaysOnTop         { get; set; } = false;
    public string      StartHotkeyText     { get; set; } = "";
    public string      StopHotkeyText      { get; set; } = "F10";

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuadClicker", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch { /* Corrupt or missing — use defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* Non-fatal */ }
    }
}
