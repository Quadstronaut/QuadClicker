using System.IO;
using System.Text.Json;

namespace QuadClicker.Models;

/// <summary>User preferences persisted to %APPDATA%\QuadClicker\settings.json.</summary>
public sealed class AppSettings
{
    // ── Click rate ────────────────────────────────────────────────────────────
    // Mode determines which set of units is valid for ClickRateUnit.
    //   Delay:     "ms", "sec", "min"
    //   Frequency: "per_sec", "per_min", "per_hour"
    public ClickRateMode ClickRateMode  { get; set; } = ClickRateMode.Delay;
    public string        ClickRateValue { get; set; } = "100";
    public string        ClickRateUnit  { get; set; } = "ms";

    // ── Click behavior ────────────────────────────────────────────────────────
    public MouseButton Button             { get; set; } = MouseButton.Left;
    public ClickType   ClickType          { get; set; } = ClickType.Single;
    public bool        UseCurrentPosition { get; set; } = true;
    public int         X                  { get; set; } = 0;
    public int         Y                  { get; set; } = 0;
    public int         StopAfterClicks    { get; set; } = 0;
    public double      StopAfterSeconds   { get; set; } = 0;
    public double      IdleWaitSeconds    { get; set; } = 0;
    public bool        AlwaysOnTop        { get; set; } = false;
    public string      StartHotkeyText    { get; set; } = "";
    public string      StopHotkeyText     { get; set; } = "F10";

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
                return LoadFromJson(File.ReadAllText(SettingsPath));
        }
        catch { /* Corrupt or unreadable — fall back to defaults */ }
        return new AppSettings();
    }

    /// <summary>Deserializes JSON and runs forward-only legacy migration. Public for tests.</summary>
    public static AppSettings LoadFromJson(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        settings.MigrateLegacy();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* Non-fatal — settings loss is recoverable */ }
    }

    /// <summary>
    /// Translates pre-redesign unit values ("/s", "/min") into the new (Mode, Unit) shape.
    /// Idempotent: canonical values pass through unchanged.
    /// </summary>
    private void MigrateLegacy()
    {
        switch (ClickRateUnit)
        {
            case "/s":
                ClickRateMode = ClickRateMode.Frequency;
                ClickRateUnit = "per_sec";
                break;
            case "/min":
                ClickRateMode = ClickRateMode.Frequency;
                ClickRateUnit = "per_min";
                break;
            case "ms":
                ClickRateMode = ClickRateMode.Delay;
                break;
        }
    }
}
