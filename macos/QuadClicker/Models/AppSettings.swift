// Models/AppSettings.swift
// QuadClicker — macOS
//
// Persisted to ~/Library/Application Support/QuadClicker/settings.json.
// JSON keys match the Windows implementation for cross-platform compatibility.

import Foundation

final class AppSettings: Codable {
    // ── Click Rate ────────────────────────────────────────────────────────────
    // Mode determines which set of unit tags is valid for clickRateUnit.
    //   .delay     → "ms", "sec", "min"
    //   .frequency → "per_sec", "per_min", "per_hour"
    var clickRateMode: ClickRateMode = .delay
    var clickRateValue: String       = "100"
    var clickRateUnit: String        = "ms"

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
        case clickRateMode       = "ClickRateMode"
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

    // Custom init from decoder so missing `ClickRateMode` doesn't fail decoding.
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        clickRateMode      = (try? c.decode(ClickRateMode.self, forKey: .clickRateMode))     ?? .delay
        clickRateValue     = (try? c.decode(String.self,        forKey: .clickRateValue))    ?? "100"
        clickRateUnit      = (try? c.decode(String.self,        forKey: .clickRateUnit))     ?? "ms"
        button             = (try? c.decode(MouseButton.self,   forKey: .button))            ?? .left
        clickType          = (try? c.decode(ClickType.self,     forKey: .clickType))         ?? .single
        useCurrentPosition = (try? c.decode(Bool.self,          forKey: .useCurrentPosition))?? true
        x                  = (try? c.decode(Int.self,           forKey: .x))                 ?? 0
        y                  = (try? c.decode(Int.self,           forKey: .y))                 ?? 0
        stopAfterClicks    = (try? c.decode(Int.self,           forKey: .stopAfterClicks))   ?? 0
        stopAfterSeconds   = (try? c.decode(Double.self,        forKey: .stopAfterSeconds))  ?? 0
        idleWaitSeconds    = (try? c.decode(Double.self,        forKey: .idleWaitSeconds))   ?? 0
        alwaysOnTop        = (try? c.decode(Bool.self,          forKey: .alwaysOnTop))       ?? false
        startHotkeyText    = (try? c.decode(String.self,        forKey: .startHotkeyText))   ?? ""
        stopHotkeyText     = (try? c.decode(String.self,        forKey: .stopHotkeyText))    ?? "F10"
        migrateLegacy()
    }

    // ── Migration: legacy "/s" / "/min" → canonical Frequency tags ────────────
    private func migrateLegacy() {
        switch clickRateUnit {
        case "/s":
            clickRateMode = .frequency
            clickRateUnit = "per_sec"
        case "/min":
            clickRateMode = .frequency
            clickRateUnit = "per_min"
        case "ms":
            clickRateMode = .delay
        default:
            break
        }
    }

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
