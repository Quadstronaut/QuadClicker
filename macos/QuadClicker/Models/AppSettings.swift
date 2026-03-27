// Models/AppSettings.swift
// QuadClicker — macOS
//
// Persisted to ~/Library/Application Support/QuadClicker/settings.json.
// JSON keys match the Windows implementation for cross-platform compatibility.

import Foundation

final class AppSettings: Codable {
    // ── Click Rate ────────────────────────────────────────────────────────────
    var clickRateValue: String = "100"
    var clickRateUnit: String  = "ms"   // "ms" | "/s" | "/min"

    // ── Click Behaviour ───────────────────────────────────────────────────────
    var button: MouseButton   = .left
    var clickType: ClickType  = .single

    // ── Location ──────────────────────────────────────────────────────────────
    var useCurrentPosition: Bool = true
    var x: Int = 0
    var y: Int = 0

    // ── Stop Conditions ───────────────────────────────────────────────────────
    var stopAfterClicks: Int    = 0
    var stopAfterSeconds: Double = 0
    var idleWaitSeconds: Double  = 0

    // ── Window ────────────────────────────────────────────────────────────────
    var alwaysOnTop: Bool = false

    // ── Hotkeys ───────────────────────────────────────────────────────────────
    var startHotkeyText: String = ""
    var stopHotkeyText: String  = "F10"

    // ── Coding keys (match Windows JSON property names) ───────────────────────
    enum CodingKeys: String, CodingKey {
        case clickRateValue      = "ClickRateValue"
        case clickRateUnit       = "ClickRateUnit"
        case button              = "Button"
        case clickType           = "ClickType"
        case useCurrentPosition  = "UseCurrentPosition"
        case x                   = "X"
        case y                   = "Y"
        case stopAfterClicks     = "StopAfterClicks"
        case stopAfterSeconds    = "StopAfterSeconds"
        case idleWaitSeconds     = "IdleWaitSeconds"
        case alwaysOnTop         = "AlwaysOnTop"
        case startHotkeyText     = "StartHotkeyText"
        case stopHotkeyText      = "StopHotkeyText"
    }

    init() {}

    // ── Persistence ───────────────────────────────────────────────────────────

    private static var settingsURL: URL {
        let appSupport = FileManager.default.urls(
            for: .applicationSupportDirectory, in: .userDomainMask
        ).first!
        return appSupport
            .appendingPathComponent("QuadClicker", isDirectory: true)
            .appendingPathComponent("settings.json")
    }

    static func load() -> AppSettings {
        let url = settingsURL
        guard FileManager.default.fileExists(atPath: url.path) else {
            return AppSettings()
        }
        do {
            let data = try Data(contentsOf: url)
            let decoder = JSONDecoder()
            return try decoder.decode(AppSettings.self, from: data)
        } catch {
            // Corrupt or unreadable — fall back to defaults
            return AppSettings()
        }
    }

    func save() {
        let url = Self.settingsURL
        do {
            let dir = url.deletingLastPathComponent()
            try FileManager.default.createDirectory(
                at: dir, withIntermediateDirectories: true, attributes: nil
            )
            let encoder = JSONEncoder()
            encoder.outputFormatting = .prettyPrinted
            let data = try encoder.encode(self)
            try data.write(to: url, options: .atomic)
        } catch {
            // Non-fatal — settings loss is recoverable
        }
    }
}
