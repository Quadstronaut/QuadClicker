# macOS + Linux Rectification Plan — Match Windows v0.1.0

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the Click Rate redesign and the Taneth color palette from the Windows app to the macOS (Swift / SwiftUI) and Linux (C++ / Qt6) tracks so all three apps have feature- and visual-parity at v0.1.0.

**Architecture:** Three completely separate native apps in a monorepo — no shared UI code. Each platform has its own `ClickRateParser`, `AppSettings`, and main window source. This plan duplicates the Windows v0.1.0 changes into the Swift and C++ codebases verbatim in spirit, adapted to each language's idioms. JSON settings keys stay identical across platforms (intentional — the same `settings.json` file format works on every OS).

**Tech Stack:** Swift 5.9 + SwiftUI + AppKit (macOS 13+); C++ 17 + Qt 6.2 (Ubuntu 22.04 / Fedora 38).

**Testing:** Deferred per user direction. Existing tests cover the old surface and remain green because all parser changes are additive. New units, settings migration, and UI logic are intentionally untested in this pass — add tests in a follow-up branch when you have a Mac and Linux build environment.

---

## Reference: what Windows v0.1.0 ships

Read these files first — every change in this plan exists in working form on the Windows side and you should crib from them directly:

- `windows/Core/ClickRateParser.cs` — extended parser with bounds constants
- `windows/Models/ClickRateMode.cs` — the new enum
- `windows/Models/AppSettings.cs` — schema + `LoadFromJson` + `MigrateLegacy`
- `windows/MainWindow.xaml` — XAML for the Mode-radio + dropdown + hint row
- `windows/MainWindow.xaml.cs` — `ClickRateMode_Changed`, `UpdateRateHint`, `FormatRate`/`FormatDelay`, `ComposeRateString`
- `windows/App.xaml` — Taneth palette (the brushes named `AccentBrush` etc.)
- `docs/superpowers/specs/2026-04-25-click-rate-redesign-design.md` — full design spec

## File structure

### macOS (existing files modified, two new files)

| File | Change |
|---|---|
| `macos/QuadClicker/Models/ClickRateMode.swift` | **Create** — enum |
| `macos/QuadClicker/Models/AppSettings.swift` | **Modify** — add mode field, canonical unit tags, migration in `load()` |
| `macos/QuadClicker/Core/ClickRateParser.swift` | **Modify** — add seconds / minutes / per-hour, bounds constants, bound enforcement |
| `macos/QuadClicker/ContentView.swift` | **Modify** — Color tokens (Taneth), Click Rate row redesign, hint logic, button foreground |
| `macos/QuadClicker.xcodeproj/project.pbxproj` | **Modify** — add the new `ClickRateMode.swift` file to the build target |

### Linux (existing files modified, one new file)

| File | Change |
|---|---|
| `linux/src/models/ClickRateMode.h` | **Create** — enum |
| `linux/src/models/AppSettings.h` | **Modify** — add `ClickRateMode mode` field |
| `linux/src/models/AppSettings.cpp` | **Modify** — JSON read/write of mode + canonical unit tags + migration |
| `linux/src/core/ClickRateParser.h` | **Modify** — add `MinDelayMs`/`MaxDelayMs` constants |
| `linux/src/core/ClickRateParser.cpp` | **Modify** — sec / min / per-hour, bound enforcement |
| `linux/src/MainWindow.h` | **Modify** — add Mode radio members, hint label, helper signatures |
| `linux/src/MainWindow.cpp` | **Modify** — Taneth stylesheet, click rate row redesign, hint logic |
| `linux/CMakeLists.txt` | **Modify** — add `src/models/ClickRateMode.h` to source list |

---

# Part 1 — macOS

### Task 1: Add `ClickRateMode` enum (Swift)

**Files:**
- Create: `macos/QuadClicker/Models/ClickRateMode.swift`

- [ ] **Step 1: Create the file**

```swift
// Models/ClickRateMode.swift
// QuadClicker — macOS

import Foundation

enum ClickRateMode: Int, Codable {
    case delay     = 0
    case frequency = 1
}
```

- [ ] **Step 2: Add the file to the Xcode build target**

Open `macos/QuadClicker.xcodeproj/project.pbxproj` and add a new file reference + build file entry for `Models/ClickRateMode.swift` next to the existing `Models/MouseButton.swift` entries. If you have Xcode installed, do this through the IDE (`File → Add Files to "QuadClicker"` → select `Models/ClickRateMode.swift`, target = QuadClicker). If you're scripting, mirror the four lines (`PBXBuildFile`, `PBXFileReference`, `PBXSourcesBuildPhase` entry, group child) used by `MouseButton.swift`.

- [ ] **Step 3: Commit**

```bash
git add macos/QuadClicker/Models/ClickRateMode.swift macos/QuadClicker.xcodeproj/project.pbxproj
git commit -m "macOS: add ClickRateMode enum"
```

---

### Task 2: Extend `AppSettings.swift` with mode + canonical units + migration

**Files:**
- Modify: `macos/QuadClicker/Models/AppSettings.swift`

- [ ] **Step 1: Replace the file's body**

```swift
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
```

- [ ] **Step 2: Commit**

```bash
git add macos/QuadClicker/Models/AppSettings.swift
git commit -m "macOS: AppSettings — add ClickRateMode + legacy migration"
```

---

### Task 3: Extend `ClickRateParser.swift` with seconds, minutes, per-hour, and bounds

**Files:**
- Modify: `macos/QuadClicker/Core/ClickRateParser.swift`

- [ ] **Step 1: Replace the file's body**

```swift
// Core/ClickRateParser.swift
// QuadClicker — macOS

import Foundation

enum ClickRateParser {
    static let minDelayMs: Double = 1.0
    static let maxDelayMs: Double = 360.0 * 60_000.0   // 360 minutes

    /// Parse `text` into a delay TimeInterval (seconds).
    static func parse(_ text: String) -> Result<TimeInterval, String> {
        let t = text.trimmingCharacters(in: .whitespaces).lowercased()

        if t.isEmpty {
            return .failure("Click rate is required.")
        }

        // ── Milliseconds: "100ms" ─────────────────────────────────────────────
        if t.hasSuffix("ms") {
            let num = String(t.dropLast(2)).trimmingCharacters(in: .whitespaces)
            if let ms = parsePositive(num) {
                return buildDelay(ms)
            }
            return .failure("Millisecond value must be a positive number.")
        }

        // ── Minutes: "2m", "2min", "2minutes" ─────────────────────────────────
        if endsWithAny(t, ["minutes", "minute", "mins", "min", "m"])
           && !t.contains("per minute")
           && !t.hasSuffix("/min")
           && !t.hasSuffix("cpm")
        {
            let num = stripFirstSuffix(t, ["minutes", "minute", "mins", "min", "m"])
            if let mins = parsePositive(num) {
                return buildDelay(mins * 60_000.0)
            }
            return .failure("Minutes value must be a positive number.")
        }

        // ── Seconds: "5s", "5sec", "5seconds" ─────────────────────────────────
        if endsWithAny(t, ["seconds", "second", "secs", "sec", "s"])
           && !t.contains("per second")
           && !t.hasSuffix("/s")
           && !t.hasSuffix("cps")
        {
            let num = stripFirstSuffix(t, ["seconds", "second", "secs", "sec", "s"])
            if let secs = parsePositive(num) {
                return buildDelay(secs * 1000.0)
            }
            return .failure("Seconds value must be a positive number.")
        }

        // ── Clicks/second: "10/s", "10cps", "10 times per second" ────────────
        if t.hasSuffix("/s") || t.hasSuffix("cps") || t.contains("times per second") {
            let num = t
                .replacingOccurrences(of: "times per second", with: "")
                .replacingOccurrences(of: "/s", with: "")
                .replacingOccurrences(of: "cps", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tps = parsePositive(num) {
                return buildDelay(1000.0 / tps)
            }
            return .failure("Clicks-per-second value must be a positive number.")
        }

        // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────
        if t.hasSuffix("/min") || t.hasSuffix("cpm") || t.contains("times per minute") {
            let num = t
                .replacingOccurrences(of: "times per minute", with: "")
                .replacingOccurrences(of: "/min", with: "")
                .replacingOccurrences(of: "cpm", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tpm = parsePositive(num) {
                return buildDelay(60_000.0 / tpm)
            }
            return .failure("Clicks-per-minute value must be a positive number.")
        }

        // ── Clicks/hour: "60/h", "60cph", "60 times per hour" ────────────────
        if t.hasSuffix("/h") || t.hasSuffix("cph") || t.contains("times per hour") {
            let num = t
                .replacingOccurrences(of: "times per hour", with: "")
                .replacingOccurrences(of: "/h", with: "")
                .replacingOccurrences(of: "cph", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tph = parsePositive(num) {
                return buildDelay(3_600_000.0 / tph)
            }
            return .failure("Clicks-per-hour value must be a positive number.")
        }

        // ── Bare integer/decimal → milliseconds ───────────────────────────────
        if let bare = parsePositive(t) {
            return buildDelay(bare)
        }

        return .failure("Invalid format. Examples: 100ms  |  5s  |  10/s  |  600/min  |  60/h")
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static func buildDelay(_ ms: Double) -> Result<TimeInterval, String> {
        if !ms.isFinite { return .failure("Rate is not a finite number.") }
        if ms < minDelayMs {
            return .failure("Rate exceeds maximum — minimum delay is 1 ms (1000 clicks/sec).")
        }
        if ms > maxDelayMs {
            return .failure("Delay exceeds maximum of 360 minutes.")
        }
        return .success(ms / 1000.0)
    }

    private static func parsePositive(_ s: String) -> Double? {
        let trimmed = s.trimmingCharacters(in: .whitespaces)
        guard let v = Double(trimmed), v > 0 else { return nil }
        return v
    }

    private static func endsWithAny(_ text: String, _ suffixes: [String]) -> Bool {
        for s in suffixes where text.hasSuffix(s) { return true }
        return false
    }

    private static func stripFirstSuffix(_ text: String, _ suffixes: [String]) -> String {
        for s in suffixes where text.hasSuffix(s) {
            return String(text.dropLast(s.count)).trimmingCharacters(in: .whitespaces)
        }
        return text.trimmingCharacters(in: .whitespaces)
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add macos/QuadClicker/Core/ClickRateParser.swift
git commit -m "macOS: parser — sec / min / per-hour units + bounds"
```

---

### Task 4: Apply the Taneth color palette in `ContentView.swift`

**Files:**
- Modify: `macos/QuadClicker/ContentView.swift` (the `Color` extension at the top)

- [ ] **Step 1: Replace the design-token comment block at lines 4–9 and the static `Color` extension at lines 29–43**

Find this block at the top of the file:

```swift
// Design tokens:
//   Background     #1A1A1A   Surface        #242424   SurfaceElevated #2E2E2E
//   Border         #3A3A3A   TextPrimary    #F0F0F0   TextSecondary   #9A9A9A
//   TextDisabled   #555555   Accent         #50C878   AccentHover     #3DAF62
//   AccentPressed  #2E9150   Danger         #E05252   StatusWaiting   #E0A030
```

Replace with:

```swift
// Design tokens (Taneth palette — deep-green hull with gold HUD accent):
//   Background      #0A1410   Surface          #13211C   SurfaceElevated #1B2E27
//   Border          #2D5448   TextPrimary      #E8DCB0   TextSecondary   #7A9088
//   TextDisabled    #3D5048   Accent           #E8B547   AccentHover     #F5C75A
//   AccentPressed   #B88A2A   AccentForeground #0A1410   Danger          #E04030
//   DangerHover     #C8331E   StatusWaiting    #5BA89A
```

And replace the static `Color` extension (the block declaring `qcBackground`, etc.) with:

```swift
extension Color {
    static let qcBackground       = Color(hex: "#0A1410")
    static let qcSurface          = Color(hex: "#13211C")
    static let qcSurfaceElevated  = Color(hex: "#1B2E27")
    static let qcBorder           = Color(hex: "#2D5448")
    static let qcTextPrimary      = Color(hex: "#E8DCB0")
    static let qcTextSecondary    = Color(hex: "#7A9088")
    static let qcTextDisabled     = Color(hex: "#3D5048")
    static let qcAccent           = Color(hex: "#E8B547")
    static let qcAccentHover      = Color(hex: "#F5C75A")
    static let qcAccentPressed    = Color(hex: "#B88A2A")
    static let qcAccentForeground = Color(hex: "#0A1410")
    static let qcDanger           = Color(hex: "#E04030")
    static let qcDangerHover      = Color(hex: "#C8331E")
    static let qcStatusWaiting    = Color(hex: "#5BA89A")
}
```

- [ ] **Step 2: Update `MainActionButtonStyle` to use `qcAccentForeground` for text on the start button**

Find:

```swift
struct MainActionButtonStyle: ButtonStyle {
    var isDanger: Bool

    func makeBody(configuration: Configuration) -> some View {
        let base    = isDanger ? Color.qcDanger         : Color.qcAccent
        let pressed = isDanger ? Color.qcDangerHover   : Color.qcAccentPressed

        configuration.label
            .font(.system(size: 15, weight: .semibold))
            .foregroundColor(.white)
```

Replace `.foregroundColor(.white)` with:

```swift
            .foregroundColor(isDanger ? .white : Color.qcAccentForeground)
```

- [ ] **Step 3: Build (if you have Xcode) to confirm no symbol references break**

```bash
xcodebuild -project macos/QuadClicker.xcodeproj -scheme QuadClicker -configuration Release -derivedDataPath /tmp/qc-build build
```

Expected: build succeeds. (If you don't have Xcode, defer this until macOS hardware is available.)

- [ ] **Step 4: Commit**

```bash
git add macos/QuadClicker/ContentView.swift
git commit -m "macOS: apply Taneth palette (deep-green hull, gold accent)"
```

---

### Task 5: Redesign the Click Rate row in `ContentView.swift`

**Files:**
- Modify: `macos/QuadClicker/ContentView.swift`

- [ ] **Step 1: Add Mode + canonical-unit state to `ClickerViewModel`**

Find the `@Published var clickRateUnit: String` line and replace the two click-rate state lines:

```swift
@Published var clickRateValue: String = "100"
@Published var clickRateUnit: String  = "ms"   // "ms" | "/s" | "/min"
```

with:

```swift
@Published var clickRateMode: ClickRateMode = .delay
@Published var clickRateValue: String       = "100"
@Published var clickRateUnit: String        = "ms"   // canonical Tag — see DelayUnits / FrequencyUnits below
@Published var rateHintText: String         = ""
@Published var rateHintIsWarning: Bool      = false
```

- [ ] **Step 2: Add unit lists and `composeRateString()` to `ClickerViewModel`**

Add these as static members of `ClickerViewModel` (place just below the closing `}` of `setupCallbacks` or anywhere inside the class):

```swift
static let delayUnits: [(tag: String, display: String)] = [
    ("ms",  "ms"),
    ("sec", "seconds"),
    ("min", "minutes"),
]

static let frequencyUnits: [(tag: String, display: String)] = [
    ("per_sec",  "per second"),
    ("per_min",  "per minute"),
    ("per_hour", "per hour"),
]

func currentUnits() -> [(tag: String, display: String)] {
    clickRateMode == .frequency ? Self.frequencyUnits : Self.delayUnits
}

/// Translate canonical (mode, value, unitTag) → parser DSL string.
func composeRateString() -> String {
    let v = clickRateValue.trimmingCharacters(in: .whitespaces)
    switch clickRateUnit {
    case "ms":       return v + "ms"
    case "sec":      return v + "s"
    case "min":      return v + "min"
    case "per_sec":  return v + "/s"
    case "per_min":  return v + "/min"
    case "per_hour": return v + "/h"
    default:         return v + "ms"
    }
}
```

- [ ] **Step 3: Update `loadSettings` and `saveSettings` to read/write the mode + canonical unit**

Replace `clickRateValue = settings.clickRateValue` / `clickRateUnit = settings.clickRateUnit` lines in `loadSettings()` with:

```swift
clickRateMode  = settings.clickRateMode
clickRateValue = settings.clickRateValue
clickRateUnit  = settings.clickRateUnit
```

And in `saveSettings()` replace the corresponding two lines with:

```swift
settings.clickRateMode  = clickRateMode
settings.clickRateValue = clickRateValue
settings.clickRateUnit  = clickRateUnit
```

- [ ] **Step 4: Replace `buildSession()`'s rate-composition lines**

Find:

```swift
let combined = clickRateUnit == "ms"
    ? (clickRateValue + "ms")
    : (clickRateValue + clickRateUnit)

let rate: TimeInterval
switch ClickRateParser.parse(combined) {
case .success(let r): rate = r
case .failure(let e): errorMessage = e; return nil
}
```

Replace with:

```swift
let rate: TimeInterval
switch ClickRateParser.parse(composeRateString()) {
case .success(let r): rate = r
case .failure(let e): errorMessage = e; return nil
}
```

- [ ] **Step 5: Add `updateRateHint()` to `ClickerViewModel`**

Add this method to `ClickerViewModel`:

```swift
func updateRateHint() {
    if clickRateValue.trimmingCharacters(in: .whitespaces).isEmpty {
        rateHintText = ""; rateHintIsWarning = false; return
    }
    switch ClickRateParser.parse(composeRateString()) {
    case .failure:
        rateHintText = ""; rateHintIsWarning = false
    case .success(let secs):
        let ms = secs * 1000.0
        let cps = 1000.0 / ms
        let veryFast = cps > 100.0
        let conv: String = (clickRateMode == .delay) ? Self.formatRate(cps: cps)
                                                     : Self.formatDelay(ms: ms)
        if veryFast {
            rateHintText = "⚠ Very fast — input may not register reliably  (≈ \(conv))"
            rateHintIsWarning = true
        } else {
            rateHintText = "≈ \(conv)"
            rateHintIsWarning = false
        }
    }
}

private static func formatRate(cps: Double) -> String {
    if cps >= 1.0       { return "\(trim(cps)) clicks/sec" }
    let cpm = cps * 60.0
    if cpm >= 1.0       { return "\(trim(cpm)) clicks/min" }
    return "\(trim(cps * 3600.0)) clicks/hour"
}

private static func formatDelay(ms: Double) -> String {
    if ms < 1000        { return "\(trim(ms)) ms between clicks" }
    if ms < 60_000      { return "\(trim(ms / 1000.0)) sec between clicks" }
    if ms < 3_600_000   { return "\(trim(ms / 60_000.0)) min between clicks" }
    return "\(trim(ms / 3_600_000.0)) hours between clicks"
}

private static func trim(_ v: Double) -> String {
    if abs(v - v.rounded()) < 0.005 {
        return String(Int64(v.rounded()))
    }
    return String(format: "%.2f", v).replacingOccurrences(of: #"0+$"#, with: "", options: .regularExpression)
                                    .replacingOccurrences(of: #"\.$"#, with: "", options: .regularExpression)
}
```

- [ ] **Step 6: Add a Mode-changed handler to `ClickerViewModel`**

Add this method:

```swift
/// Called when the Mode segmented control changes. Falls back to the matching mode's first unit
/// if the previously-selected tag isn't valid in the new mode.
func clickRateModeChanged() {
    let valid = currentUnits().map { $0.tag }
    if !valid.contains(clickRateUnit) {
        clickRateUnit = clickRateMode == .frequency ? "per_sec" : "ms"
    }
    updateRateHint()
}
```

- [ ] **Step 7: Replace the `clickRateRow` view**

Find:

```swift
private var clickRateRow: some View {
    HStack {
        label("Click Rate:")
            .frame(width: 155, alignment: .leading)
        HStack(spacing: 6) {
            QCTextField(text: $vm.clickRateValue)
                .frame(width: 80)
                .help("Enter a number. Unit selected to the right.")
            Picker("", selection: $vm.clickRateUnit) {
                Text("ms").tag("ms")
                Text("/s").tag("/s")
                Text("/min").tag("/min")
            }
            .pickerStyle(.menu)
            .frame(width: 80)
            .help("Unit for the click rate value")
            .background(Color.qcSurface)
            .cornerRadius(4)
        }
    }
}
```

Replace with:

```swift
private var clickRateRow: some View {
    HStack(alignment: .top) {
        label("Click Rate:")
            .frame(width: 155, alignment: .leading)
            .padding(.top, 4)
        VStack(alignment: .leading, spacing: 4) {
            // Mode picker
            HStack(spacing: 16) {
                radioButton(label: "Delay", isSelected: vm.clickRateMode == .delay) {
                    vm.clickRateMode = .delay
                    vm.clickRateModeChanged()
                }
                radioButton(label: "Frequency", isSelected: vm.clickRateMode == .frequency) {
                    vm.clickRateMode = .frequency
                    vm.clickRateModeChanged()
                }
            }
            // Value + Unit
            HStack(spacing: 6) {
                QCTextField(text: $vm.clickRateValue)
                    .frame(width: 80)
                    .help("Enter a number. Unit selected on the right.")
                    .onChange(of: vm.clickRateValue) { _ in vm.updateRateHint() }
                Picker("", selection: $vm.clickRateUnit) {
                    ForEach(vm.currentUnits(), id: \.tag) { u in
                        Text(u.display).tag(u.tag)
                    }
                }
                .pickerStyle(.menu)
                .frame(width: 110)
                .help("Unit for the click rate value")
                .background(Color.qcSurface)
                .cornerRadius(4)
                .onChange(of: vm.clickRateUnit) { _ in vm.updateRateHint() }
            }
            // Hint
            Text(vm.rateHintText)
                .font(.system(size: 11))
                .foregroundColor(vm.rateHintIsWarning ? Color.qcDanger : Color.qcTextSecondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}
```

- [ ] **Step 8: Trigger an initial hint render after `loadSettings`**

In `init(settings:)` of `ClickerViewModel`, after the existing `loadSettings()` and `setupCallbacks()` calls, add:

```swift
updateRateHint()
```

- [ ] **Step 9: Bump the window height to accommodate the new two-row Click Rate block**

Find:

```swift
.frame(width: 420, height: 480)
```

Replace with:

```swift
.frame(width: 420, height: 510)
```

And in `configureWindow()` find:

```swift
window.setContentSize(NSSize(width: 420, height: 480))
```

Replace with:

```swift
window.setContentSize(NSSize(width: 420, height: 510))
```

- [ ] **Step 10: Build and smoke-launch (if you have Xcode)**

```bash
xcodebuild -project macos/QuadClicker.xcodeproj -scheme QuadClicker -configuration Release -derivedDataPath /tmp/qc-build build
```

Then run the resulting `.app` and verify:
1. Click Rate row shows `Delay  Frequency` radios and a unit dropdown that swaps when you toggle.
2. Typing a value updates the `≈ X clicks/sec` hint live.
3. Typing a value with the per-sec unit greater than 100 turns the hint red with the `⚠ Very fast` warning.
4. Settings persist across app restart.

If you don't have Xcode, mark this checkbox skipped and revisit on real hardware.

- [ ] **Step 11: Commit**

```bash
git add macos/QuadClicker/ContentView.swift
git commit -m "macOS: redesign Click Rate row — mode + live hint + warning"
```

---

# Part 2 — Linux

### Task 6: Add `ClickRateMode` enum (C++)

**Files:**
- Create: `linux/src/models/ClickRateMode.h`

- [ ] **Step 1: Create the file**

```cpp
#pragma once

namespace QuadClicker {

enum class ClickRateMode {
    Delay     = 0,
    Frequency = 1,
};

} // namespace QuadClicker
```

- [ ] **Step 2: Add the file to CMakeLists**

Open `linux/CMakeLists.txt`, find the `add_executable(quadclicker ...)` (or `add_library`) target, and add `src/models/ClickRateMode.h` to the source list next to `src/models/ClickType.h`. (Even though it's a header-only enum, listing it makes IDE indexing and `make install` headers consistent with the rest of the codebase.)

- [ ] **Step 3: Commit**

```bash
git add linux/src/models/ClickRateMode.h linux/CMakeLists.txt
git commit -m "Linux: add ClickRateMode enum"
```

---

### Task 7: Extend `AppSettings` with mode + migration (Linux)

**Files:**
- Modify: `linux/src/models/AppSettings.h`
- Modify: `linux/src/models/AppSettings.cpp`

- [ ] **Step 1: Add the field and helper to `AppSettings.h`**

In the `class AppSettings` body, add `#include "ClickRateMode.h"` at the top of the include list and insert this field at the very top of the public member list:

```cpp
ClickRateMode clickRateMode{ClickRateMode::Delay};
```

So the public members start:

```cpp
public:
    ClickRateMode clickRateMode{ClickRateMode::Delay};
    QString       clickRateValue{QStringLiteral("100")};
    QString       clickRateUnit{QStringLiteral("ms")};
    // … rest unchanged
```

- [ ] **Step 2: Update `AppSettings.cpp` to read/write the mode and run legacy migration**

In `AppSettings::load()`, immediately after the line that decodes `clickRateUnit` (`s.clickRateUnit = obj[...]`), insert:

```cpp
        if (obj.contains(QLatin1String("ClickRateMode"))) {
            int v = obj[QLatin1String("ClickRateMode")].toInt(0);
            s.clickRateMode = (v == 1) ? ClickRateMode::Frequency : ClickRateMode::Delay;
        }
```

Then, just before `return s;` at the end of `load()`, add legacy migration:

```cpp
    // Legacy migration: pre-redesign settings used "/s" and "/min" as unit values
    // with no ClickRateMode field. Translate them to the new canonical shape.
    if (s.clickRateUnit == QLatin1String("/s")) {
        s.clickRateMode = ClickRateMode::Frequency;
        s.clickRateUnit = QStringLiteral("per_sec");
    } else if (s.clickRateUnit == QLatin1String("/min")) {
        s.clickRateMode = ClickRateMode::Frequency;
        s.clickRateUnit = QStringLiteral("per_min");
    } else if (s.clickRateUnit == QLatin1String("ms")) {
        s.clickRateMode = ClickRateMode::Delay;
    }
```

In `AppSettings::save()`, in the `QJsonObject obj;` build-up, insert this line just before the `obj[QLatin1String("ClickRateValue")]` line:

```cpp
        obj[QLatin1String("ClickRateMode")]      = static_cast<int>(clickRateMode);
```

- [ ] **Step 3: Commit**

```bash
git add linux/src/models/AppSettings.h linux/src/models/AppSettings.cpp
git commit -m "Linux: AppSettings — ClickRateMode + legacy migration"
```

---

### Task 8: Extend `ClickRateParser` with sec / min / per-hour and bounds (Linux)

**Files:**
- Modify: `linux/src/core/ClickRateParser.h`
- Modify: `linux/src/core/ClickRateParser.cpp`

- [ ] **Step 1: Add bounds constants to the header**

In `ClickRateParser.h`, replace the body of the `class ClickRateParser` declaration with:

```cpp
class ClickRateParser {
public:
    static constexpr double MinDelayMs = 1.0;
    static constexpr double MaxDelayMs = 360.0 * 60'000.0;   // 360 minutes

    /// Returns true on success; populates \p delay and leaves \p error empty.
    /// Returns false on failure; populates \p error with a human-readable message.
    static bool tryParse(const QString& text,
                         std::chrono::milliseconds& delay,
                         QString& error);

private:
    static bool tryParsePositive(const QString& s, double& value);
    static bool buildDelay(double ms, std::chrono::milliseconds& delay, QString& error);
    static bool endsWithAny(const QString& t, std::initializer_list<const char*> suffixes);
    static QString stripFirstSuffix(const QString& t, std::initializer_list<const char*> suffixes);
};
```

Add `#include <initializer_list>` to the header's include list.

- [ ] **Step 2: Replace `ClickRateParser.cpp` with the extended implementation**

```cpp
#include "ClickRateParser.h"

#include <cmath>

namespace QuadClicker {

bool ClickRateParser::tryParsePositive(const QString& s, double& value)
{
    bool ok = false;
    value = s.trimmed().toDouble(&ok);
    return ok && value > 0.0;
}

bool ClickRateParser::buildDelay(double ms,
                                  std::chrono::milliseconds& delay,
                                  QString& error)
{
    if (!std::isfinite(ms)) {
        error = QStringLiteral("Rate is not a finite number.");
        return false;
    }
    if (ms < MinDelayMs) {
        error = QStringLiteral("Rate exceeds maximum — minimum delay is 1 ms (1000 clicks/sec).");
        return false;
    }
    if (ms > MaxDelayMs) {
        error = QStringLiteral("Delay exceeds maximum of 360 minutes.");
        return false;
    }
    delay = std::chrono::milliseconds(static_cast<long long>(ms));
    error.clear();
    return true;
}

bool ClickRateParser::endsWithAny(const QString& t, std::initializer_list<const char*> suffixes)
{
    for (auto s : suffixes) {
        if (t.endsWith(QLatin1String(s))) return true;
    }
    return false;
}

QString ClickRateParser::stripFirstSuffix(const QString& t, std::initializer_list<const char*> suffixes)
{
    for (auto s : suffixes) {
        QLatin1String suf(s);
        if (t.endsWith(suf)) {
            return t.left(t.size() - suf.size()).trimmed();
        }
    }
    return t.trimmed();
}

bool ClickRateParser::tryParse(const QString& text,
                                std::chrono::milliseconds& delay,
                                QString& error)
{
    delay = std::chrono::milliseconds(100);
    error.clear();

    QString t = text.trimmed().toLower();

    if (t.isEmpty()) {
        error = QStringLiteral("Click rate is required.");
        return false;
    }

    // ── Milliseconds: "100ms" ──────────────────────────────────────────────────
    if (t.endsWith(QLatin1String("ms"))) {
        QString num = t.chopped(2).trimmed();
        double ms = 0.0;
        if (tryParsePositive(num, ms)) {
            return buildDelay(ms, delay, error);
        }
        error = QStringLiteral("Millisecond value must be a positive number.");
        return false;
    }

    // ── Minutes: "2m", "2min", "2minutes" ─────────────────────────────────────
    if (endsWithAny(t, {"minutes", "minute", "mins", "min", "m"})
        && !t.contains(QLatin1String("per minute"))
        && !t.endsWith(QLatin1String("/min"))
        && !t.endsWith(QLatin1String("cpm")))
    {
        QString num = stripFirstSuffix(t, {"minutes", "minute", "mins", "min", "m"});
        double mins = 0.0;
        if (tryParsePositive(num, mins)) {
            return buildDelay(mins * 60'000.0, delay, error);
        }
        error = QStringLiteral("Minutes value must be a positive number.");
        return false;
    }

    // ── Seconds: "5s", "5sec", "5seconds" ─────────────────────────────────────
    if (endsWithAny(t, {"seconds", "second", "secs", "sec", "s"})
        && !t.contains(QLatin1String("per second"))
        && !t.endsWith(QLatin1String("/s"))
        && !t.endsWith(QLatin1String("cps")))
    {
        QString num = stripFirstSuffix(t, {"seconds", "second", "secs", "sec", "s"});
        double secs = 0.0;
        if (tryParsePositive(num, secs)) {
            return buildDelay(secs * 1000.0, delay, error);
        }
        error = QStringLiteral("Seconds value must be a positive number.");
        return false;
    }

    // ── Clicks/second: "10/s", "10cps", "10 times per second" ─────────────────
    if (t.endsWith(QLatin1String("/s")) ||
        t.endsWith(QLatin1String("cps")) ||
        t.contains(QLatin1String("times per second")))
    {
        QString num = t;
        num.replace(QLatin1String("times per second"), QString())
           .replace(QLatin1String("/s"),               QString())
           .replace(QLatin1String("cps"),              QString())
           .replace(QLatin1String(" "),                QString());
        double tps = 0.0;
        if (tryParsePositive(num, tps)) {
            return buildDelay(1000.0 / tps, delay, error);
        }
        error = QStringLiteral("Clicks-per-second value must be a positive number.");
        return false;
    }

    // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────────
    if (t.endsWith(QLatin1String("/min")) ||
        t.endsWith(QLatin1String("cpm")) ||
        t.contains(QLatin1String("times per minute")))
    {
        QString num = t;
        num.replace(QLatin1String("times per minute"), QString())
           .replace(QLatin1String("/min"),             QString())
           .replace(QLatin1String("cpm"),              QString())
           .replace(QLatin1String(" "),                QString());
        double tpm = 0.0;
        if (tryParsePositive(num, tpm)) {
            return buildDelay(60'000.0 / tpm, delay, error);
        }
        error = QStringLiteral("Clicks-per-minute value must be a positive number.");
        return false;
    }

    // ── Clicks/hour: "60/h", "60cph", "60 times per hour" ─────────────────────
    if (t.endsWith(QLatin1String("/h")) ||
        t.endsWith(QLatin1String("cph")) ||
        t.contains(QLatin1String("times per hour")))
    {
        QString num = t;
        num.replace(QLatin1String("times per hour"), QString())
           .replace(QLatin1String("/h"),             QString())
           .replace(QLatin1String("cph"),            QString())
           .replace(QLatin1String(" "),              QString());
        double tph = 0.0;
        if (tryParsePositive(num, tph)) {
            return buildDelay(3'600'000.0 / tph, delay, error);
        }
        error = QStringLiteral("Clicks-per-hour value must be a positive number.");
        return false;
    }

    // ── Bare integer/decimal → milliseconds ───────────────────────────────────
    double bare = 0.0;
    if (tryParsePositive(t, bare)) {
        return buildDelay(bare, delay, error);
    }

    error = QStringLiteral("Invalid format. Examples: 100ms  |  5s  |  10/s  |  600/min  |  60/h");
    return false;
}

} // namespace QuadClicker
```

- [ ] **Step 3: Commit**

```bash
git add linux/src/core/ClickRateParser.h linux/src/core/ClickRateParser.cpp
git commit -m "Linux: parser — sec / min / per-hour units + bounds"
```

---

### Task 9: Apply the Taneth stylesheet (Linux)

**Files:**
- Modify: `linux/src/MainWindow.cpp` (the `CSS_WINDOW` raw-string at lines 19–142, plus the inline `setStyleSheet` calls scattered through the file)

- [ ] **Step 1: Replace `CSS_WINDOW`**

Replace the entire `CSS_WINDOW` raw-string literal (lines 19–142) with:

```cpp
// ── Colour constants (Taneth palette — deep-green hull, gold HUD accent) ──────
static const char* CSS_WINDOW = R"(
QMainWindow, QWidget#centralWidget {
    background-color: #0A1410;
}
QLabel {
    color: #E8DCB0;
    font-size: 13px;
}
QLineEdit {
    background-color: #13211C;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 6px;
    font-size: 13px;
}
QLineEdit:focus {
    border-color: #E8B547;
}
QLineEdit:disabled {
    color: #3D5048;
    background-color: #0A1410;
}
QComboBox {
    background-color: #13211C;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 6px;
    font-size: 13px;
}
QComboBox:focus {
    border-color: #E8B547;
}
QComboBox QAbstractItemView {
    background-color: #13211C;
    color: #E8DCB0;
    selection-background-color: #E8B547;
    selection-color: #0A1410;
}
QRadioButton {
    color: #E8DCB0;
    font-size: 13px;
    spacing: 6px;
}
QRadioButton::indicator {
    width: 14px;
    height: 14px;
}
QRadioButton::indicator:checked {
    background-color: #E8B547;
    border: 2px solid #E8B547;
    border-radius: 7px;
}
QRadioButton::indicator:unchecked {
    background-color: #13211C;
    border: 2px solid #2D5448;
    border-radius: 7px;
}
QCheckBox {
    color: #E8DCB0;
    font-size: 13px;
    spacing: 6px;
}
QCheckBox::indicator {
    width: 14px;
    height: 14px;
}
QCheckBox::indicator:checked {
    background-color: #E8B547;
    border: 2px solid #E8B547;
    border-radius: 2px;
}
QCheckBox::indicator:unchecked {
    background-color: #13211C;
    border: 2px solid #2D5448;
    border-radius: 2px;
}
QPushButton#smallBtn {
    background-color: #1B2E27;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 8px;
    font-size: 12px;
}
QPushButton#smallBtn:hover {
    background-color: #13211C;
    border-color: #E8B547;
}
QPushButton#smallBtn:pressed {
    background-color: #0A1410;
}
QPushButton#startBtn {
    background-color: #E8B547;
    color: #0A1410;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: bold;
    padding: 8px;
}
QPushButton#startBtn:hover {
    background-color: #F5C75A;
}
QPushButton#startBtn:pressed {
    background-color: #B88A2A;
}
QPushButton#stopBtn {
    background-color: #E04030;
    color: #FFFFFF;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: bold;
    padding: 8px;
}
QPushButton#stopBtn:hover {
    background-color: #C8331E;
}
QPushButton#stopBtn:pressed {
    background-color: #A8281A;
}
)";
```

- [ ] **Step 2: Update inline color hexcodes scattered through `MainWindow.cpp`**

Search for and replace each occurrence below (these are inline `setStyleSheet` calls and section-separator borders that bypass the global stylesheet):

| Old hex | New hex | Where |
|---|---|---|
| `#E05252` | `#E04030` | error label color (~line 241) |
| `#3A3A3A` | `#2D5448` | section separator divider colors (~lines 459, 469) |
| `#9A9A9A` | `#7A9088` | section separator label color and hotkey labels (~lines 463, 554, 568) |
| `#1A1A1A` | `#0A1410` | section separator label background (the inline `background-color`) |
| `#50C878` | `#E8B547` | hotkey-edit border when capturing (~line 598) |

You can do these with `Grep` then `Edit replace_all=true`. Verify after each replacement that the line still compiles (the surrounding context shouldn't change).

- [ ] **Step 3: Update the status-color logic**

Find the `setStatus(EngineStatus status)` body (around lines 920–950) where dot and label colors are set with inline stylesheets per state. Replace any `#50C878` with `#E8B547` (Clicking accent), any `#E0A030` with `#5BA89A` (WaitingForIdle), any `#9A9A9A` with `#7A9088` (Stopped/disabled text), and any `#555555` with `#3D5048` (TextDisabled).

- [ ] **Step 4: Build**

```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release linux/
cmake --build build --parallel
```

Expected: builds clean.

- [ ] **Step 5: Commit**

```bash
git add linux/src/MainWindow.cpp
git commit -m "Linux: apply Taneth palette (deep-green hull, gold accent)"
```

---

### Task 10: Redesign the Click Rate row (Linux)

**Files:**
- Modify: `linux/src/MainWindow.h`
- Modify: `linux/src/MainWindow.cpp`

- [ ] **Step 1: Add new widget members and helper signatures to `MainWindow.h`**

In the private `Widgets` block (after `m_clickRateUnitBox`), add:

```cpp
QButtonGroup* m_rateModeGroup{nullptr};
QRadioButton* m_modeDelay{nullptr};
QRadioButton* m_modeFrequency{nullptr};
QLabel*       m_rateHintLabel{nullptr};
bool          m_rateUiReady{false};
```

In the private helper signatures section (after `installHotkeyCaptureFilter`), add:

```cpp
void onClickRateModeChanged();
void onClickRateInputChanged();
void populateUnitBox(const QString& desiredTag, const QString& fallbackTag);
QString composeRateString() const;
void updateRateHint();
static QString formatRate(double cps);
static QString formatDelay(double ms);
static QString trimNumber(double v);
```

Make sure `#include "models/ClickRateMode.h"` is added to the existing include block.

- [ ] **Step 2: Replace `buildClickRateRow()` in `MainWindow.cpp`**

Find the existing `buildClickRateRow` (~lines 295–325) and replace with:

```cpp
QWidget* MainWindow::buildClickRateRow()
{
    auto* host = new QWidget(this);
    auto* root = new QHBoxLayout(host);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    auto* lbl = new QLabel(QStringLiteral("Click Rate:"), host);
    lbl->setFixedWidth(155);
    lbl->setAlignment(Qt::AlignTop | Qt::AlignLeft);
    root->addWidget(lbl);

    auto* col = new QVBoxLayout();
    col->setContentsMargins(0, 0, 0, 0);
    col->setSpacing(2);

    // Mode radios
    auto* modeRow = new QHBoxLayout();
    modeRow->setSpacing(16);
    modeRow->setContentsMargins(0, 0, 0, 0);
    m_modeDelay     = new QRadioButton(QStringLiteral("Delay"), host);
    m_modeFrequency = new QRadioButton(QStringLiteral("Frequency"), host);
    m_rateModeGroup = new QButtonGroup(host);
    m_rateModeGroup->addButton(m_modeDelay,     0);
    m_rateModeGroup->addButton(m_modeFrequency, 1);
    m_modeDelay->setChecked(true);
    modeRow->addWidget(m_modeDelay);
    modeRow->addWidget(m_modeFrequency);
    modeRow->addStretch();
    col->addLayout(modeRow);

    // Value + Unit
    auto* valRow = new QHBoxLayout();
    valRow->setSpacing(6);
    valRow->setContentsMargins(0, 0, 0, 0);
    m_clickRateValueEdit = new QLineEdit(QStringLiteral("100"), host);
    m_clickRateValueEdit->setFixedWidth(80);
    m_clickRateValueEdit->setToolTip(
        QStringLiteral("Enter a number. Unit selected on the right."));
    valRow->addWidget(m_clickRateValueEdit);

    m_clickRateUnitBox = new QComboBox(host);
    m_clickRateUnitBox->setFixedWidth(110);
    m_clickRateUnitBox->setToolTip(QStringLiteral("Unit for the click rate value"));
    valRow->addWidget(m_clickRateUnitBox);
    valRow->addStretch();
    col->addLayout(valRow);

    // Hint
    m_rateHintLabel = new QLabel(QString(), host);
    m_rateHintLabel->setStyleSheet(QStringLiteral("color: #7A9088; font-size: 11px;"));
    m_rateHintLabel->setWordWrap(true);
    col->addWidget(m_rateHintLabel);

    root->addLayout(col, /*stretch=*/1);

    // Wiring (deferred — populateUnitBox + signal hookups happen in loadSettings)
    connect(m_modeDelay,     &QRadioButton::toggled, this, [this](bool on) {
        if (on) onClickRateModeChanged();
    });
    connect(m_modeFrequency, &QRadioButton::toggled, this, [this](bool on) {
        if (on) onClickRateModeChanged();
    });
    connect(m_clickRateValueEdit, &QLineEdit::textChanged,
            this, &MainWindow::onClickRateInputChanged);
    connect(m_clickRateUnitBox, QOverload<int>::of(&QComboBox::currentIndexChanged),
            this, [this](int){ onClickRateInputChanged(); });

    return host;
}
```

- [ ] **Step 3: Add the new helper bodies at the bottom of `MainWindow.cpp`** (just above the closing namespace)

```cpp
void MainWindow::onClickRateModeChanged()
{
    if (!m_modeFrequency || !m_modeDelay) return;
    bool isFreq = m_modeFrequency->isChecked();
    QString prevTag = m_clickRateUnitBox->currentData().toString();
    QString fallback = isFreq ? QStringLiteral("per_sec") : QStringLiteral("ms");
    populateUnitBox(prevTag, fallback);
    if (m_rateUiReady) updateRateHint();
}

void MainWindow::onClickRateInputChanged()
{
    if (m_rateUiReady) updateRateHint();
}

void MainWindow::populateUnitBox(const QString& desiredTag, const QString& fallbackTag)
{
    bool wasReady = m_rateUiReady;
    m_rateUiReady = false;          // suppress hint rebuild during repopulation

    m_clickRateUnitBox->clear();
    bool isFreq = m_modeFrequency && m_modeFrequency->isChecked();
    if (isFreq) {
        m_clickRateUnitBox->addItem(QStringLiteral("per second"), QStringLiteral("per_sec"));
        m_clickRateUnitBox->addItem(QStringLiteral("per minute"), QStringLiteral("per_min"));
        m_clickRateUnitBox->addItem(QStringLiteral("per hour"),   QStringLiteral("per_hour"));
    } else {
        m_clickRateUnitBox->addItem(QStringLiteral("ms"),       QStringLiteral("ms"));
        m_clickRateUnitBox->addItem(QStringLiteral("seconds"),  QStringLiteral("sec"));
        m_clickRateUnitBox->addItem(QStringLiteral("minutes"),  QStringLiteral("min"));
    }
    int idx = m_clickRateUnitBox->findData(desiredTag);
    if (idx < 0) idx = m_clickRateUnitBox->findData(fallbackTag);
    if (idx < 0) idx = 0;
    m_clickRateUnitBox->setCurrentIndex(idx);

    m_rateUiReady = wasReady;
}

QString MainWindow::composeRateString() const
{
    QString v = m_clickRateValueEdit->text().trimmed();
    QString u = m_clickRateUnitBox->currentData().toString();
    if (u == QLatin1String("ms"))       return v + QLatin1String("ms");
    if (u == QLatin1String("sec"))      return v + QLatin1String("s");
    if (u == QLatin1String("min"))      return v + QLatin1String("min");
    if (u == QLatin1String("per_sec"))  return v + QLatin1String("/s");
    if (u == QLatin1String("per_min"))  return v + QLatin1String("/min");
    if (u == QLatin1String("per_hour")) return v + QLatin1String("/h");
    return v + QLatin1String("ms");
}

void MainWindow::updateRateHint()
{
    if (!m_rateHintLabel) return;

    QString v = m_clickRateValueEdit->text().trimmed();
    if (v.isEmpty()) {
        m_rateHintLabel->setText(QString());
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
        return;
    }

    std::chrono::milliseconds delay{};
    QString err;
    if (!ClickRateParser::tryParse(composeRateString(), delay, err)) {
        m_rateHintLabel->setText(QString());
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
        return;
    }

    double ms = static_cast<double>(delay.count());
    double cps = 1000.0 / ms;
    bool isDelay = m_modeDelay && m_modeDelay->isChecked();
    bool veryFast = cps > 100.0;
    QString conv = isDelay ? formatRate(cps) : formatDelay(ms);

    if (veryFast) {
        m_rateHintLabel->setText(
            QStringLiteral("⚠ Very fast — input may not register reliably  (≈ %1)")
                .arg(conv));
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #E04030; font-size: 11px;"));
    } else {
        m_rateHintLabel->setText(QStringLiteral("≈ %1").arg(conv));
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
    }
}

QString MainWindow::formatRate(double cps)
{
    if (cps >= 1.0) return QStringLiteral("%1 clicks/sec").arg(trimNumber(cps));
    double cpm = cps * 60.0;
    if (cpm >= 1.0) return QStringLiteral("%1 clicks/min").arg(trimNumber(cpm));
    return QStringLiteral("%1 clicks/hour").arg(trimNumber(cps * 3600.0));
}

QString MainWindow::formatDelay(double ms)
{
    if (ms < 1000.0)      return QStringLiteral("%1 ms between clicks").arg(trimNumber(ms));
    if (ms < 60'000.0)    return QStringLiteral("%1 sec between clicks").arg(trimNumber(ms / 1000.0));
    if (ms < 3'600'000.0) return QStringLiteral("%1 min between clicks").arg(trimNumber(ms / 60'000.0));
    return QStringLiteral("%1 hours between clicks").arg(trimNumber(ms / 3'600'000.0));
}

QString MainWindow::trimNumber(double v)
{
    if (std::abs(v - std::round(v)) < 0.005) {
        return QString::number(static_cast<long long>(std::round(v)));
    }
    QString s = QString::number(v, 'f', 2);
    while (s.endsWith(QLatin1Char('0'))) s.chop(1);
    if (s.endsWith(QLatin1Char('.')))    s.chop(1);
    return s;
}
```

- [ ] **Step 4: Update `loadSettings` and `saveSettings`**

In `MainWindow::loadSettings()`, find:

```cpp
m_clickRateValueEdit->setText(s.clickRateValue);
int unitIdx = m_clickRateUnitBox->findData(s.clickRateUnit);
if (unitIdx >= 0) m_clickRateUnitBox->setCurrentIndex(unitIdx);
```

Replace with:

```cpp
m_rateUiReady = false;
if (s.clickRateMode == ClickRateMode::Frequency) {
    m_modeFrequency->setChecked(true);
    populateUnitBox(s.clickRateUnit, QStringLiteral("per_sec"));
} else {
    m_modeDelay->setChecked(true);
    populateUnitBox(s.clickRateUnit, QStringLiteral("ms"));
}
m_clickRateValueEdit->setText(s.clickRateValue);
m_rateUiReady = true;
updateRateHint();
```

In `MainWindow::saveSettings()`, find:

```cpp
s.clickRateValue = m_clickRateValueEdit->text();
```

…and add immediately above and below it:

```cpp
s.clickRateMode  = m_modeFrequency->isChecked()
                     ? ClickRateMode::Frequency
                     : ClickRateMode::Delay;
s.clickRateValue = m_clickRateValueEdit->text();
s.clickRateUnit  = m_clickRateUnitBox->currentData().toString();
```

Remove the existing `s.clickRateUnit = m_clickRateUnitBox->currentData().toString();` line later in the function so it isn't set twice.

- [ ] **Step 5: Update `tryBuildSession()` to use `composeRateString()`**

Find:

```cpp
QString rateText = m_clickRateValueEdit->text().trimmed();
QString unit = m_clickRateUnitBox->currentData().toString();
QString combined = (unit == QLatin1String("ms"))
                   ? rateText + QLatin1String("ms")
                   : rateText + unit;

std::chrono::milliseconds delay;
if (!ClickRateParser::tryParse(combined, delay, error)) return false;
```

Replace with:

```cpp
std::chrono::milliseconds delay;
if (!ClickRateParser::tryParse(composeRateString(), delay, error)) return false;
```

- [ ] **Step 6: Bump window height in the constructor**

Find:

```cpp
setFixedSize(420, 480);
```

Replace with:

```cpp
setFixedSize(420, 510);
```

- [ ] **Step 7: Build**

```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release linux/
cmake --build build --parallel
```

Expected: builds clean. If you get an undefined-reference link error for `ClickRateMode`, double-check Task 6 Step 2 added the header to the source list.

- [ ] **Step 8: Smoke-launch (if you have Linux + Qt6 on the box)**

```bash
./build/quadclicker
```

Verify:
1. Click Rate row shows the Mode radios and the dropdown contents swap.
2. Hint updates live and turns red for >100/s rates.
3. Settings persist across restart, including the mode.

- [ ] **Step 9: Commit**

```bash
git add linux/src/MainWindow.h linux/src/MainWindow.cpp
git commit -m "Linux: redesign Click Rate row — mode + live hint + warning"
```

---

# Final wrap-up

### Task 11: Open a PR

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feat/mac-linux-rectify
```

- [ ] **Step 2: Open the PR**

```bash
gh pr create --title "Mac + Linux rectify — port Click Rate redesign and Taneth palette" --body "$(cat <<'EOF'
## Summary

Brings the macOS (Swift / SwiftUI) and Linux (C++ / Qt 6) tracks up to feature- and visual-parity with the Windows v0.1.0 release. Tests are deferred per request.

### macOS
- `ClickRateMode` enum + `AppSettings` mode field with legacy migration.
- Parser extended with sec / min / per-hour units and 1 ms – 360 min bound enforcement.
- `ContentView` redesigned: Mode picker, swap-on-mode unit dropdown, live conversion hint, "Very fast" warning at >100/s.
- Color extension switched to the Taneth palette; START button text uses `qcAccentForeground`.
- Window height bumped 480 → 510 to fit the new two-row Click Rate block.

### Linux
- `ClickRateMode` enum header.
- `AppSettings` reads/writes the mode and runs legacy `/s` / `/min` migration.
- Parser extended with sec / min / per-hour units and bound enforcement.
- `MainWindow`: Mode radios, dynamic unit combo, live hint label, warning state.
- Global stylesheet swapped to the Taneth palette; inline color references updated.
- Window height bumped 480 → 510.

## Out of scope
- Tests for the new units / migration / UI in mac and linux — deferred.
- macOS notarization / code signing — still requires the Apple Developer account.
- Real CI builds for mac / linux — the workflows remain placeholder no-ops; flip them on once you can run xcodebuild / cmake on real hardware.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Wait for CI green and merge**

The mac/linux CI workflows are placeholder no-ops, so they'll pass instantly. The Windows workflow will also pass since none of these changes touch Windows code. Squash-merge.

---

## Self-review notes

Spec coverage:
- All Windows v0.1.0 features (mode-based input, sec/min/hour units, 1 ms – 360 min bounds, live conversion hint, very-fast warning, settings migration) are covered for both platforms.
- Taneth palette is fully ported to both platforms (macOS Color tokens; Linux QSS + inline overrides).
- Tests deferred per user direction.

Cross-platform consistency:
- JSON keys in settings remain identical (`ClickRateMode`, `ClickRateValue`, `ClickRateUnit`).
- Canonical unit tags match Windows: `ms`, `sec`, `min`, `per_sec`, `per_min`, `per_hour`.
- Bounds constants are identical (1 ms / 360 min).

Things explicitly NOT done by this plan (good — they were ruled out as scope creep):
- Wiring real CI builds for mac/linux.
- Releasing mac/linux artifacts.
- Code signing / notarization.
- Porting the Click Rate redesign to the mac/linux *CLI help text* (left as a small follow-up — the parser handles the new formats already).
- Linux AppSettings JSON serializes `Button` and `ClickType` as **strings** while Windows serializes them as **integers**. This is a pre-existing cross-platform compatibility wart, not introduced by this plan, and outside scope.
