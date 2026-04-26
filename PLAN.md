# QuadClicker — Development Plan

**Author:** Kyle Green (Quadstronaut)
**Date:** 2026-03-23
**Status:** Living document — update as decisions solidify

---

## 1. Vision

QuadClicker is an open-source, native auto-clicker utility built to be the definitive tool in its category: fully configurable, scriptable via CLI, accessibility-first, and distributed everywhere. It is the flagship utility of Quadstronaut's sovereign cloud toolchain — built with full native implementations on Windows (WPF/C#), macOS (SwiftUI/Swift), and Linux (Qt6/C++) — never cross-platform frameworks, always platform-optimal. The goal is a tool that earns the trust of power users, accessibility communities, and automation engineers alike, distributed through every major package manager and backed by professional CI/CD pipelines, code signing, and a comprehensive test suite.

---

## 2. Platform Architecture

| Platform       | Language | Framework      | Input Injection API          | Tray Mechanism                        | Min OS Version         |
|----------------|----------|----------------|------------------------------|---------------------------------------|------------------------|
| Windows 10/11  | C#       | WPF / .NET 10  | `SendInput` (user32.dll)     | `NotifyIcon` (WPF/System.Windows.Forms) | Windows 10 22H2        |
| macOS          | Swift    | SwiftUI        | `CGEventPost` (CoreGraphics) | `NSStatusItem` (AppKit menu bar icon) | macOS 13 Ventura       |
| Linux          | C++      | Qt6            | XTest (X11) / uinput (Wayland) | `QSystemTrayIcon` / libappindicator  | Ubuntu 22.04 / Fedora 38 |

**Notes:**
- Windows target is `net10.0-windows`. Update `QuadClicker.csproj` from current `net8.0-windows`.
- macOS uses `CGEventPost` with `CGEventCreateMouseEvent` for sub-millisecond injection.
- Linux must support both X11 (XTest extension via `XSendEvent`) and Wayland (uinput kernel interface). Detect at runtime.
- No Electron, Avalonia, MAUI, or any cross-platform UI framework. Native per platform, always.

---

## 3. Repository Structure

Monorepo with platform subdirectories. Current flat structure is promoted into `/windows`.

```
QuadClicker/                        ← repo root
├── .github/
│   ├── workflows/
│   │   ├── build-windows.yml
│   │   ├── build-macos.yml
│   │   ├── build-linux.yml
│   │   └── release.yml             ← triggered on version tag push
│   └── ISSUE_TEMPLATE/
│       ├── bug_report.md
│       └── feature_request.md
├── windows/                        ← WPF / C# / .NET 10
│   ├── QuadClicker.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── Core/
│   │   ├── ClickEngine.cs          ← click loop, timing, cancellation
│   │   ├── ClickRateParser.cs      ← TryParseClickRate extracted to own class
│   │   ├── HotkeyManager.cs        ← RegisterHotKey / UnregisterHotKey
│   │   ├── IdleDetector.cs         ← GetLastInputInfo wrapper
│   │   ├── InputInjector.cs        ← SendInput P/Invoke, left/right/middle, single/double
│   │   ├── LocationPicker.cs       ← low-level mouse hook + overlay window
│   │   └── TrayManager.cs          ← NotifyIcon lifecycle
│   ├── Cli/
│   │   └── CliEntryPoint.cs        ← argument parsing, headless execution
│   ├── Models/
│   │   └── ClickSession.cs         ← immutable session config record
│   ├── PInvoke/
│   │   └── NativeMethods.cs        ← all P/Invoke declarations, structs
│   ├── Tests/
│   │   ├── QuadClicker.Tests.csproj
│   │   ├── ClickRateParserTests.cs
│   │   ├── ClickSessionTests.cs
│   │   └── CliArgumentTests.cs
│   └── Assets/
│       ├── icon.ico
│       └── tray-icon.ico
├── macos/                          ← SwiftUI / Swift
│   ├── QuadClicker.xcodeproj/
│   ├── QuadClicker/
│   │   ├── QuadClickerApp.swift    ← @main entry, AppDelegate
│   │   ├── ContentView.swift       ← main SwiftUI view
│   │   ├── Core/
│   │   │   ├── ClickEngine.swift
│   │   │   ├── ClickRateParser.swift
│   │   │   ├── InputInjector.swift ← CGEventPost
│   │   │   ├── IdleDetector.swift  ← IOHIDGetParameter / CGEventSourceSecondsSinceLastEventType
│   │   │   ├── HotkeyManager.swift ← CGEventTap / NSEvent global monitor
│   │   │   ├── LocationPicker.swift← full-screen NSWindow overlay
│   │   │   └── TrayManager.swift   ← NSStatusItem
│   │   ├── Cli/
│   │   │   └── CliEntryPoint.swift
│   │   ├── Models/
│   │   │   └── ClickSession.swift
│   │   └── Assets.xcassets/
│   └── QuadClickerTests/
│       ├── ClickRateParserTests.swift
│       ├── ClickSessionTests.swift
│       └── CliArgumentTests.swift
├── linux/                          ← Qt6 / C++
│   ├── CMakeLists.txt
│   ├── src/
│   │   ├── main.cpp
│   │   ├── MainWindow.cpp / .h
│   │   ├── core/
│   │   │   ├── ClickEngine.cpp / .h
│   │   │   ├── ClickRateParser.cpp / .h
│   │   │   ├── InputInjectorX11.cpp / .h   ← XTest
│   │   │   ├── InputInjectorUInput.cpp / .h← uinput
│   │   │   ├── InputInjectorFactory.cpp / .h← runtime selection
│   │   │   ├── IdleDetector.cpp / .h
│   │   │   ├── HotkeyManager.cpp / .h
│   │   │   ├── LocationPicker.cpp / .h
│   │   │   └── TrayManager.cpp / .h
│   │   ├── cli/
│   │   │   └── CliEntryPoint.cpp / .h
│   │   └── models/
│   │       └── ClickSession.h
│   ├── tests/
│   │   ├── CMakeLists.txt
│   │   ├── ClickRateParserTests.cpp
│   │   ├── ClickSessionTests.cpp
│   │   └── CliArgumentTests.cpp
│   └── assets/
│       ├── quadclicker.png
│       ├── quadclicker.desktop
│       └── quadclicker.appdata.xml
├── packaging/
│   ├── windows/
│   │   └── winget/
│   │       └── manifests/
│   │           └── quadstronaut.quadclicker.yaml
│   ├── macos/
│   │   └── homebrew/
│   │       └── quadclicker.rb       ← Homebrew cask formula
│   └── linux/
│       ├── debian/
│       │   ├── control
│       │   ├── changelog
│       │   └── rules
│       ├── snapcraft.yaml
│       └── flatpak/
│           └── io.quadstronaut.QuadClicker.yaml
├── ref_imgs/                        ← reference screenshots (already present)
├── CLAUDE.md
├── PLAN.md                          ← this file
├── CODE_SIGNING.md
├── README.md
├── LICENSE
└── .gitignore
```

---

## 4. Feature Specification

### 4.1 Click Rate

**Description:** User specifies how fast clicks occur.

**Input:** Text field (numeric value) + dropdown unit selector.

| Unit Option     | Format Example | Stored As       |
|-----------------|----------------|-----------------|
| Milliseconds    | `100`          | delay = 100ms   |
| Clicks/second   | `10`           | delay = 100ms   |
| Clicks/minute   | `600`          | delay = 100ms   |

**Parser (`ClickRateParser`):**
- Accepts bare integer (treated as ms), `100ms`, `10/s`, `600/min`, `10 times per second`, `600 times per minute`
- Returns `TimeSpan` (not `int`), enabling sub-millisecond future precision
- Returns a `Result<TimeSpan, string>` type — error message included on failure
- Minimum rate: 1ms (enforce in parser, show error if < 1ms requested)
- Maximum rate: no enforced max, but document OS limits (~1000 clicks/s on Windows with `SendInput`)

**Acceptance Criteria:**
- All six format variants parse correctly with unit tests
- Invalid input shows inline error label (not a modal dialog)
- Parser is platform-shared logic (same algorithm ported to each platform)

---

### 4.2 Click Location

Three mutually exclusive modes, selectable via radio button / segmented control:

**Mode A — Current Cursor Position**
- No additional input required
- Click fires at wherever the cursor is at the moment of injection
- Default mode on launch

**Mode B — Fixed XY Coordinate**
- Two numeric text fields: X, Y (screen pixels, origin top-left)
- Cursor is moved to (X, Y) before each click via `SetCursorPos` / `CGWarpMouseCursorPosition` / `XWarpPointer`
- Fields are disabled when Mode A is active

**Mode C — Pick Location (Visual Overlay Picker)**
- Button: "Pick Location"
- App window minimizes (Windows) / hides (macOS) / minimizes (Linux)
- Full-screen transparent overlay appears with crosshair cursor and instruction text
- User left-clicks anywhere on screen; coordinates are captured and populated into X/Y fields
- Overlay closes, app window restores
- The capturing click is swallowed (not forwarded to underlying window)
- ESC key cancels picker without changing stored coordinates

**Acceptance Criteria:**
- Mode B fields are disabled in Mode A; Pick button is disabled in Mode A
- Overlay is always-on-top and covers all monitors (full virtual desktop)
- Picker swallows the selection click; does not trigger a click in the underlying app
- ESC cancels without side effects

---

### 4.3 Mouse Button Selection

Dropdown or segmented control: **Left** (default), **Right**, **Middle**

**Windows P/Invoke flags:**
- Left: `MOUSEEVENTF_LEFTDOWN` (0x0002) + `MOUSEEVENTF_LEFTUP` (0x0004)
- Right: `MOUSEEVENTF_RIGHTDOWN` (0x0008) + `MOUSEEVENTF_RIGHTUP` (0x0010)
- Middle: `MOUSEEVENTF_MIDDLEDOWN` (0x0020) + `MOUSEEVENTF_MIDDLEUP` (0x0040)

**macOS CGEvent types:** `kCGEventLeftMouseDown/Up`, `kCGEventRightMouseDown/Up`, `kCGEventOtherMouseDown/Up` (button 2)

**Linux XTest:** `XTestFakeButtonEvent(display, 1/2/3, True/False, CurrentTime)`

**Acceptance Criteria:**
- All three buttons inject correctly and verifiably on each platform
- Integration test: inject click, verify via hook/listener that the correct button event was received

---

### 4.4 Click Type

Radio button / toggle: **Single** (default), **Double**

- Double click = two click sequences injected with system double-click interval between them
  - Windows: `GetDoubleClickTime()` (user32.dll)
  - macOS: `NSEvent.doubleClickInterval`
  - Linux: `XGetDefault(display, "Net", "DoubleClickTime")` or xsettings

**Acceptance Criteria:**
- Double click is recognized as a double-click by the OS (e.g., selecting a word in a text editor)
- Interval is read from OS, not hardcoded

---

### 4.5 Stop Conditions

Three independent conditions — any that trigger first wins. All default to disabled (0 = unlimited).

**Condition 1 — Stop After N Clicks**
- Integer field, 0 = unlimited
- Counter increments after each successful `SendInput` / `CGEventPost` / `XTestFakeButtonEvent`

**Condition 2 — Stop After N Seconds**
- Integer or float field, 0 = unlimited
- Timer starts when clicking begins (after idle wait, if any)

**Condition 3 — Manual Stop**
- Stop button in UI
- Stop hotkey (separately configurable — see 4.7)

**Acceptance Criteria:**
- All three conditions halt the loop cleanly via cancellation token / equivalent mechanism
- UI reflects stopped state immediately after halt
- Stopping is graceful: no partial click events left pending

---

### 4.6 Idle Detection

**Description:** Delay start of clicking until system has been idle for N seconds. 0 = disabled (start immediately).

**Windows:** `GetLastInputInfo` → compare `dwTime` to `GetTickCount()`
**macOS:** `CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateHIDSystemState, kCGAnyInputEventType)`
**Linux:** Use `/proc/interrupts` or `XScreenSaverQueryInfo` (X11) or `org.freedesktop.ScreenSaver` D-Bus interface (Wayland)

**Behavior:**
- Idle poll interval: 100ms (same as current implementation)
- While waiting for idle: display status "Waiting for idle..." in UI
- Cancellation during idle wait exits cleanly

**Acceptance Criteria:**
- Moving the mouse or pressing a key resets the idle timer (behavior is OS-native, not simulated)
- Status label reflects current state: Idle, Waiting, Clicking, Stopped

---

### 4.7 Hotkeys

Two separately configurable hotkeys: **Start hotkey** and **Stop hotkey**. They cannot be the same key combination.

**Capture UI:** A "Press a key..." text field that records the next key press as the hotkey. Modifier keys (Ctrl, Alt, Shift, Win/Cmd) are supported in combination.

**Defaults:**
- Start hotkey: none (must be configured by user, or use Start button)
- Stop hotkey: `F10` (existing behavior, kept as default)

**Windows:** `RegisterHotKey` / `UnregisterHotKey` (user32.dll), WM_HOTKEY in WndProc
**macOS:** `CGEventTap` or `NSEvent.addGlobalMonitorForEvents(matching:handler:)` — requires Accessibility permission
**Linux:** `XGrabKey` (X11) / `libxkbcommon` shortcut monitoring

**Permissions:**
- macOS: prompt for Accessibility permission on first launch if hotkeys are configured. Show clear explanation dialog before requesting.
- Linux: document that `XGrabKey` requires X11; Wayland global shortcuts require compositor support (KDE: `org.kde.kglobalaccel`, GNOME: limited).

**Acceptance Criteria:**
- Start and stop hotkeys function when app is in background / minimized to tray
- Assigning the same key to both shows inline validation error and prevents registration
- Hotkeys persist across app restarts (saved to settings)

---

### 4.8 System Tray

**Behavior:**
- Closing the window minimizes to tray (does not quit)
- Tray icon menu: Show Window, Start/Stop, Quit
- Tray icon visual state: static (idle) vs animated/green (clicking active)
- Optional: "Start minimized to tray" launch flag

**Windows:** `System.Windows.Forms.NotifyIcon` or `Hardcodet.Wpf.TaskbarNotification` NuGet package
**macOS:** `NSStatusItem` with `NSMenu` — lives in menu bar, not Dock
**Linux:** `QSystemTrayIcon` (Qt6 built-in), falls back gracefully if no tray is available

**Acceptance Criteria:**
- App does not appear in taskbar when minimized to tray (Windows)
- App is not in Dock when running menu-bar-only mode (macOS, optional toggle)
- Clicking tray icon restores window on all platforms
- Quitting from tray performs clean shutdown (unhook, unregister, cancel tasks)

---

### 4.9 Always On Top

**Description:** Optional checkbox/toggle. When enabled, the app window floats above all other windows.

**Windows:** `window.Topmost = true`
**macOS:** `window.level = .floating`
**Linux:** `setWindowFlags(Qt::WindowStaysOnTopHint)`

**Acceptance Criteria:**
- Always on top persists across sessions (saved to settings)
- Toggling while app is running takes effect immediately

---

### 4.10 CLI Mode

See Section 5 for full specification.

**Behavior:** When any recognized CLI argument is passed, the app launches in headless mode — no GUI window is created. The process runs, performs clicks, and exits. Output goes to stdout/stderr.

**Acceptance Criteria:**
- `--help` prints usage and exits 0
- `--version` prints version string and exits 0
- Invalid argument combination prints error to stderr and exits 1
- GUI never opens in CLI mode
- All GUI features are available via CLI

---

### 4.11 Settings Persistence

All user-configured values persist between launches via platform-appropriate storage:

- **Windows:** `%APPDATA%\QuadClicker\settings.json`
- **macOS:** `~/Library/Application Support/QuadClicker/settings.json` (or `UserDefaults` for native feel)
- **Linux:** `~/.config/quadclicker/settings.json` (XDG Base Directory Spec)

**Persisted values:** click rate, click rate unit, location mode, X/Y coordinates, button selection, click type, idle time, stop-after-clicks, stop-after-seconds, start hotkey, stop hotkey, always-on-top, minimize-to-tray.

---

## 5. CLI Interface Specification

The CLI is a first-class interface. Every option available in the GUI has a CLI equivalent.

### 5.1 Entry Point

```
quadclicker [OPTIONS]
```

When called with no arguments, the GUI launches. When called with any of the below options, headless mode activates.

### 5.2 Full Argument Reference

| Argument                        | Type        | Default       | Description                                                      |
|---------------------------------|-------------|---------------|------------------------------------------------------------------|
| `--rate <value>`                | string      | required      | Click rate. Formats: `100ms`, `10/s`, `600/min`                  |
| `--button <left\|right\|middle>`| enum        | `left`        | Mouse button to click                                            |
| `--type <single\|double>`       | enum        | `single`      | Click type                                                       |
| `--location <x,y>`             | int pair    | cursor        | Fixed screen coordinate. Omit for current cursor position        |
| `--stop-after-clicks <n>`       | int         | 0 (unlimited) | Stop after N clicks                                              |
| `--stop-after-seconds <n>`      | float       | 0 (unlimited) | Stop after N seconds                                             |
| `--idle-wait <n>`               | float       | 0 (disabled)  | Wait until system idle for N seconds before starting            |
| `--no-gui`                      | flag        | auto-detected | Force headless mode (redundant when other args present)          |
| `--minimized`                   | flag        | off           | Launch GUI minimized to tray                                     |
| `--version`                     | flag        | —             | Print version and exit 0                                         |
| `--help`                        | flag        | —             | Print usage and exit 0                                           |

### 5.3 Examples

```bash
# Click at current cursor position, 10 times per second, until manually stopped
quadclicker --rate 10/s

# Click at coordinate (500, 300) with right mouse button, stop after 100 clicks
quadclicker --rate 10/s --location 500,300 --button right --stop-after-clicks 100

# Double-click at (960, 540) every 2 seconds, stop after 30 seconds
quadclicker --rate 500ms --type double --location 960,540 --stop-after-seconds 30

# Click at 1000ms intervals, but only after 5 seconds of system idle
quadclicker --rate 1000ms --idle-wait 5

# Click as fast as possible (1ms rate), 50 clicks, middle button
quadclicker --rate 1ms --button middle --stop-after-clicks 50

# Launch GUI minimized to tray
quadclicker --minimized
```

### 5.4 Exit Codes

| Code | Meaning                                  |
|------|------------------------------------------|
| 0    | Success / clean stop                     |
| 1    | Invalid argument or argument combination |
| 2    | Runtime error (OS refused input injection) |
| 130  | Interrupted by signal (SIGINT / Ctrl+C)  |

---

## 6. UI/UX Specification

### 6.1 Color Palette

| Role                   | Hex       | Usage                                                         |
|------------------------|-----------|---------------------------------------------------------------|
| Accent / Primary       | `#50C878` | Start button active state, focus rings, radio selected dot, active status indicator |
| Accent Hover           | `#3DAF62` | Hover state for accent-colored elements                       |
| Accent Pressed         | `#2E9150` | Pressed/active state                                          |
| Background             | `#1A1A1A` | Window background                                             |
| Surface                | `#242424` | Card / panel / input backgrounds                              |
| Surface Elevated       | `#2E2E2E` | Hover over surface elements                                   |
| Border                 | `#3A3A3A` | Input field borders, separators                               |
| Border Focus           | `#50C878` | Input field border when focused                               |
| Text Primary           | `#F0F0F0` | Main readable text                                            |
| Text Secondary         | `#9A9A9A` | Labels, helper text, placeholders                             |
| Text Disabled          | `#555555` | Disabled controls                                             |
| Danger / Stop          | `#E05252` | Stop button active state, error messages                      |
| Danger Hover           | `#C43C3C` | Stop button hover                                             |
| Status Waiting         | `#E0A030` | "Waiting for idle..." indicator                               |

### 6.2 Window Dimensions

- **Default size:** 420px wide × 360px tall (compact, suitable for always-on-top overlay)
- **Minimum size:** 380px wide × 320px tall (not resizable smaller)
- **Resizable:** Yes, but layout constrains growth gracefully
- Title bar: native platform title bar, dark mode

### 6.3 Layout — All Platforms (Logical)

```
┌─────────────────────────────────────────────┐
│  QuadClicker                    [─] [□] [×] │
├─────────────────────────────────────────────┤
│  Click Rate:  [___100___] [ms ▼]            │
│                                             │
│  Button:  ● Left   ○ Right   ○ Middle       │
│  Type:    ● Single  ○ Double                │
│                                             │
│  Location:                                  │
│  ● Current Position                         │
│  ○ Fixed Coordinate  [  X  ] [  Y  ] [Pick] │
│                                             │
│  ──────────── Stop Conditions ──────────── │
│  After clicks:   [___0___]  (0 = unlimited) │
│  After seconds:  [___0___]  (0 = unlimited) │
│                                             │
│  ──────────── Advanced ─────────────────── │
│  Idle wait (s):  [___0___]                  │
│  Always on top:  [☐]                        │
│                                             │
│  Hotkeys:  Start [___none___]  Stop [_F10_] │
│                                             │
│  Status:  ● Stopped                         │
│                                             │
│  [████████ START ████████████████████████] │
└─────────────────────────────────────────────┘
```

### 6.4 Windows WPF Implementation Notes

- Use `ResourceDictionary` for all colors (no hardcoded brushes in XAML after Phase 1)
- All colors reference named resources: `{StaticResource AccentBrush}`, `{StaticResource BackgroundBrush}`, etc.
- Dark mode: set `Window.Background` to `#1A1A1A`; rely on `ResourceDictionary` theming, not system theme detection
- `NotifyIcon` context menu uses custom drawn icons if feasible; fallback to monochrome icon
- `ControlTemplate` overrides for `TextBox`, `Button`, `RadioButton`, `CheckBox` to match dark theme
- Start button: background `#50C878`, text white, bold; hover `#3DAF62`; active (clicking): background `#E05252` (becomes Stop button)
- Input field border radius: 4px (use `Border` wrapping `TextBox`)
- Typography: Segoe UI, 13px for labels, 14px for inputs, 16px bold for Start/Stop button

### 6.5 macOS SwiftUI Implementation Notes

- Use `.preferredColorScheme(.dark)` on the root view
- `NSStatusItem` in menu bar; optionally hide from Dock via `LSUIElement = YES` in Info.plist (user-togglable)
- Accent color defined in `Assets.xcassets` as a named color set (`AccentColor`: `#50C878`)
- Use `@AppStorage` for settings persistence backed by `UserDefaults`
- Picker overlay: `NSWindow` with `NSWindowStyleMask.borderless`, `level = .screenSaver`, covers all screens
- Request Accessibility permission with `AXIsProcessTrustedWithOptions` on first hotkey configuration
- Minimum deployment: macOS 13 (for `NavigationSplitView` and `.toolbar` APIs)

### 6.6 Linux Qt6 Implementation Notes

- `QApplication::setStyle("Fusion")` with custom palette for dark mode
- Palette: `QPalette::Window` = `#1A1A1A`, `QPalette::Button` = `#242424`, `QPalette::Highlight` = `#50C878`
- `QSystemTrayIcon` with `QMenu` — fallback: if `QSystemTrayIcon::isSystemTrayAvailable()` returns false, disable tray feature and show a note in UI
- Location picker: `QWidget` with `Qt::FramelessWindowHint | Qt::WindowStaysOnTopHint | Qt::Tool`, fullscreen, semi-transparent
- Build targets: dynamic linking preferred; static linking for portable AppImage variant
- Detect X11 vs Wayland at runtime: `QGuiApplication::platformName() == "wayland"` → use uinput; else XTest

---

## 7. Phase Plan

### Phase 0: Repo Restructure + CI/CD Skeleton

**Goal:** Establish monorepo layout, move existing code, wire up empty CI pipelines that build successfully.

**Deliverables:**
1. Move all existing WPF files into `/windows/` subdirectory
2. Update `QuadClicker.csproj` target from `net8.0-windows` to `net10.0-windows`
3. Create placeholder `macos/` and `linux/` directories with minimal build files (Xcode project stub, empty `CMakeLists.txt`)
4. `.github/workflows/build-windows.yml` — builds `/windows` on push to `master` and all PRs
5. `.github/workflows/build-macos.yml` — builds `/macos` on macOS runner (placeholder until Phase 2 code exists)
6. `.github/workflows/build-linux.yml` — builds `/linux` on ubuntu runner (placeholder)
7. `.github/workflows/release.yml` — triggered on `v*` tag; uploads artifacts from all three build jobs
8. Update `README.md` to reflect monorepo structure and multi-platform roadmap
9. Update `CLAUDE.md` with post-interview content

**Dependencies:** None

**Acceptance Criteria:**
- `dotnet build` passes from `/windows` directory
- All three workflow files exist and trigger without errors (build jobs may produce empty artifacts in placeholders)
- No code from `/windows` at repo root

---

### Phase 1: Windows WPF — Feature-Complete Rewrite

**Goal:** Rewrite the Windows app from a monolithic `MainWindow.xaml.cs` to a properly layered architecture with all specified features.

**Deliverables:**

1. **Architecture refactor:**
   - Extract `ClickRateParser` to `Core/ClickRateParser.cs` — returns `TimeSpan`, full unit tests
   - Extract `InputInjector` to `Core/InputInjector.cs` — supports left/right/middle, single/double
   - Extract `ClickEngine` to `Core/ClickEngine.cs` — encapsulates loop, cancellation, stop conditions
   - Extract all P/Invoke to `PInvoke/NativeMethods.cs`
   - Extract `HotkeyManager` to `Core/HotkeyManager.cs` — two-hotkey support, validation
   - Extract `IdleDetector` to `Core/IdleDetector.cs`
   - Extract `LocationPicker` to `Core/LocationPicker.cs`
   - Extract `TrayManager` to `Core/TrayManager.cs`
   - Create `Models/ClickSession.cs` as immutable record

2. **New features (not in current code):**
   - Mouse button selection UI (Left/Right/Middle)
   - Click type selection UI (Single/Double) — `GetDoubleClickTime()` for interval
   - Configurable start hotkey (capture UI)
   - Configurable stop hotkey (replaces hardcoded F10)
   - System tray minimize (window close → tray, not exit)
   - Always-on-top toggle
   - Settings persistence to `%APPDATA%\QuadClicker\settings.json`
   - Status label showing current state (Stopped / Waiting / Clicking)
   - Inline validation errors (no modal dialogs for input errors)

3. **UI rewrite:**
   - Full dark mode using `ResourceDictionary`
   - Emerald green accent (`#50C878`) on all interactive elements
   - Layout per Section 6.3

4. **CLI mode:**
   - Detect CLI args in `Program.cs` (set `OutputType` to `Exe`, not `WinExe`, with `[STAThread]`)
   - Parse args, build `ClickSession`, run `ClickEngine` without GUI
   - All arguments from Section 5.2

5. **Test project:**
   - `windows/Tests/QuadClicker.Tests.csproj` (xUnit)
   - `ClickRateParserTests.cs` — all format variants, edge cases
   - `ClickSessionTests.cs` — validation logic
   - `CliArgumentTests.cs` — all argument parsing paths

**Dependencies:** Phase 0 complete

**Acceptance Criteria:**
- All existing features work at parity with pre-rewrite behavior
- All new features function per Section 4 acceptance criteria
- Test project runs `dotnet test` green
- CI passes on push

---

### Phase 2: macOS SwiftUI — Feature-Complete Native App

**Goal:** Build the macOS app from scratch in Swift/SwiftUI with full feature parity.

**Deliverables:**

1. `QuadClicker.xcodeproj` with SwiftUI app target and test target
2. All `Core/` Swift classes: `ClickEngine`, `ClickRateParser`, `InputInjector` (CGEventPost), `IdleDetector`, `HotkeyManager`, `LocationPicker`, `TrayManager`
3. `ContentView.swift` — full UI matching layout in Section 6.3, dark mode, emerald accent
4. `NSStatusItem` menu bar integration
5. Accessibility permission prompt with explanation dialog
6. Settings via `@AppStorage` / `UserDefaults`
7. `CliEntryPoint.swift` — headless CLI mode via `CommandLine.arguments`
8. XCTest suite with same logical test coverage as Windows
9. `.github/workflows/build-macos.yml` updated to actually build and run tests

**Dependencies:** Phase 1 complete (feature spec is frozen at that point)

**Acceptance Criteria:**
- App notarized and signed with Apple Developer ID (see CODE_SIGNING.md)
- All features in Section 4 work on macOS 13+
- `quadclicker --rate 10/s --location 500,300` works headless from Terminal
- XCTest suite passes in CI

---

### Phase 3: Linux Qt6/C++ — Feature-Complete Native App

**Goal:** Build the Linux app with Qt6 and C++, supporting both X11 and Wayland.

**Deliverables:**

1. `CMakeLists.txt` targeting Qt6 (Widgets, Core, DBus)
2. All `src/core/` C++ classes matching architecture
3. `InputInjectorFactory` — runtime detection of X11 vs Wayland, instantiates correct injector
4. `MainWindow.cpp` — Qt6 dark palette, emerald accent, layout per Section 6.3
5. `QSystemTrayIcon` integration with graceful fallback
6. Hotkey via `XGrabKey` (X11) and D-Bus global shortcuts (KDE Wayland)
7. CLI mode via `argc/argv` parsing in `main.cpp`
8. AppImage build target for portable distribution
9. `.deb` packaging files in `packaging/linux/debian/`
10. CMake test target with Google Test or Catch2 unit tests
11. `.github/workflows/build-linux.yml` updated to build and test

**Dependencies:** Phase 2 complete

**Acceptance Criteria:**
- Builds on Ubuntu 22.04 and Fedora 38 in CI
- X11 and Wayland injection both work (tested in CI with virtual framebuffer / headless input)
- All features in Section 4 work
- AppImage is self-contained (ldd shows no unexpected external deps)
- Unit tests pass

**Status (2026-04-25):** Build is **verified** on Ubuntu 24.04 LTS (Qt 6.4.2, GCC 13.3) under WSL2/WSLg. All three test suites pass (`ClickRateParserTests`, `ClickSessionTests`, `CliArgumentTests`). CLI mode handles happy-path (`--rate`, `--stop-after-clicks`, `--location`) and parse-error paths (exit 1 on bad arg) correctly. GUI launches and renders the Taneth palette. CI workflow `build-linux.yml` now runs the real build. **Remaining:** Wayland (`uinput`) injection path is unexercised under WSL — needs a real Linux desktop session. Fedora build, AppImage / `.deb` packaging not yet done.

---

### Phase 4: Distribution — Package Managers, Code Signing, Releases

**Goal:** Make QuadClicker installable from every major package manager. Establish code signing.

**Deliverables:**

1. `CODE_SIGNING.md` — step-by-step instructions for Kyle on:
   - **Windows:** Obtain EV (Extended Validation) code signing certificate from DigiCert or Sectigo. Sign `.exe` with `signtool.exe`. Store cert in GitHub Secrets as base64 PFX. CI signs on release builds.
   - **macOS:** Apple Developer ID Application certificate. Use `codesign --deep --force --sign`. Notarize with `notarytool`. Store API key in GitHub Secrets.
   - **Linux:** GPG key generation, signing `.deb` packages with `dpkg-sig`. Publish GPG public key to keyservers. SHA256 checksums in release assets.

2. **winget manifest:**
   - `packaging/windows/winget/manifests/quadstronaut.quadclicker.yaml`
   - Submit PR to `microsoft/winget-pkgs` on each release

3. **Homebrew cask:**
   - `packaging/macos/homebrew/quadclicker.rb`
   - Fork `homebrew/homebrew-cask` and submit PR, or maintain `Quadstronaut/homebrew-quadclicker` tap

4. **apt / .deb:**
   - Build `.deb` via CI, publish to GitHub Releases
   - Optional: host a PPA on Launchpad for `apt add-repository` support

5. **Snap:**
   - `packaging/linux/snapcraft.yaml`
   - Publish to Snap Store (snapcraft.io)

6. **Flatpak:**
   - `packaging/linux/flatpak/io.quadstronaut.QuadClicker.yaml`
   - Publish to Flathub (submit to `flathub/flathub` repo)

7. **Release workflow** (`release.yml`):
   - Trigger on `v*` tag (e.g., `v1.0.0`)
   - Build all three platforms
   - Code-sign each artifact
   - Create GitHub Release with:
     - `QuadClicker-windows-x64.exe` (self-contained, signed)
     - `QuadClicker-macos-universal.dmg` (universal binary, notarized)
     - `QuadClicker-linux-x86_64.AppImage` (portable)
     - `quadclicker_1.0.0_amd64.deb`
     - SHA256 checksum file

**Dependencies:** Phases 1, 2, 3 complete

---

### Phase 5: Polish + Test Coverage Across All Platforms

**Goal:** Achieve production-quality polish, full test coverage, and zero known regressions.

**Deliverables:**

1. **Test coverage targets:**
   - Unit tests: 90%+ line coverage on all `Core/` logic (platform-independent algorithms)
   - Integration tests: click injection verified via OS-level event listener on each platform
   - CLI argument parsing: all valid + invalid combinations tested

2. **Accessibility audit:**
   - Windows: NVDA/Narrator compatibility — all controls have accessible names
   - macOS: VoiceOver — all controls labeled in SwiftUI accessibility modifiers
   - Linux: AT-SPI2 via Qt accessibility bridge

3. **Performance audit:**
   - Measure actual click interval precision at 1ms, 10ms, 100ms rates on each platform
   - Document real achievable precision in README
   - Use high-resolution timers: `Stopwatch` (Windows), `mach_absolute_time` (macOS), `CLOCK_MONOTONIC` (Linux)

4. **UX polish:**
   - Keyboard navigation: Tab order correct on all platforms
   - All error states communicated via inline labels, never modals
   - Status bar shows: click count, elapsed time, current rate, current state

5. **Documentation:**
   - Update `README.md` with full feature list, installation instructions per platform, screenshots
   - `CONTRIBUTING.md` with build instructions for each platform
   - Man page for CLI (`quadclicker.1`) — installed by `.deb` and Homebrew

6. **Final CI validation:**
   - All three build-platform workflows green
   - Release workflow dry-run (without actual GitHub Release) passes

**Dependencies:** Phase 4 complete

---

## 8. CI/CD Pipeline Design

All pipelines use GitHub Actions. Trigger matrix: push to `master`, pull requests to `master`, and `v*` tags.

### 8.1 `build-windows.yml`

```yaml
# Trigger: push, PR, tag
# Runner: windows-latest (Windows Server 2022)

steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '10.0.x'
  - name: Restore
    run: dotnet restore windows/QuadClicker.csproj
  - name: Build
    run: dotnet build windows/QuadClicker.csproj -c Release --no-restore
  - name: Test
    run: dotnet test windows/Tests/QuadClicker.Tests.csproj --no-build -c Release
  - name: Publish self-contained
    if: startsWith(github.ref, 'refs/tags/v')
    run: dotnet publish windows/QuadClicker.csproj -c Release -r win-x64 --self-contained true -o dist/windows
  - name: Sign (release only)
    if: startsWith(github.ref, 'refs/tags/v')
    # Uses SIGNING_CERT_BASE64 and SIGNING_CERT_PASSWORD secrets
    run: |
      $pfxPath = [System.IO.Path]::GetTempFileName() + ".pfx"
      [System.IO.File]::WriteAllBytes($pfxPath, [System.Convert]::FromBase64String($env:SIGNING_CERT_BASE64))
      & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.x\x64\signtool.exe" sign /f $pfxPath /p $env:SIGNING_CERT_PASSWORD /tr http://timestamp.digicert.com /td sha256 /fd sha256 dist/windows/QuadClicker.exe
  - uses: actions/upload-artifact@v4
    with:
      name: windows-build
      path: dist/windows/QuadClicker.exe
```

### 8.2 `build-macos.yml`

```yaml
# Trigger: push, PR, tag
# Runner: macos-14 (Apple Silicon)

steps:
  - uses: actions/checkout@v4
  - name: Select Xcode
    run: sudo xcode-select -s /Applications/Xcode_15.x.app
  - name: Build
    run: xcodebuild -project macos/QuadClicker.xcodeproj -scheme QuadClicker -configuration Release build
  - name: Test
    run: xcodebuild -project macos/QuadClicker.xcodeproj -scheme QuadClickerTests test
  - name: Archive + Export (release only)
    if: startsWith(github.ref, 'refs/tags/v')
    run: |
      xcodebuild -project macos/QuadClicker.xcodeproj -scheme QuadClicker -configuration Release archive -archivePath build/QuadClicker.xcarchive
      xcodebuild -exportArchive -archivePath build/QuadClicker.xcarchive -exportOptionsPlist macos/ExportOptions.plist -exportPath dist/macos
  - name: Notarize (release only)
    if: startsWith(github.ref, 'refs/tags/v')
    # Uses APPLE_API_KEY, APPLE_API_KEY_ID, APPLE_API_ISSUER secrets
    run: xcrun notarytool submit dist/macos/QuadClicker.dmg --key $APPLE_API_KEY --key-id $APPLE_API_KEY_ID --issuer $APPLE_API_ISSUER --wait
  - uses: actions/upload-artifact@v4
    with:
      name: macos-build
      path: dist/macos/QuadClicker.dmg
```

### 8.3 `build-linux.yml`

```yaml
# Trigger: push, PR, tag
# Runner: ubuntu-22.04

steps:
  - uses: actions/checkout@v4
  - name: Install dependencies
    run: |
      sudo apt-get update
      sudo apt-get install -y cmake ninja-build qt6-base-dev qt6-base-private-dev libxtst-dev libxss-dev libdbus-1-dev googletest
  - name: Configure
    run: cmake -S linux -B linux/build -G Ninja -DCMAKE_BUILD_TYPE=Release
  - name: Build
    run: cmake --build linux/build
  - name: Test
    run: cd linux/build && ctest --output-on-failure
  - name: Build AppImage (release only)
    if: startsWith(github.ref, 'refs/tags/v')
    run: |
      # Use linuxdeployqt or appimagetool
      ./tools/linuxdeployqt linux/build/quadclicker -appimage
  - name: Build .deb (release only)
    if: startsWith(github.ref, 'refs/tags/v')
    run: |
      cmake --install linux/build --prefix dist/linux/deb/usr
      cp -r packaging/linux/debian dist/linux/deb/DEBIAN
      dpkg-deb --build dist/linux/deb dist/linux/quadclicker_${{ github.ref_name }}_amd64.deb
  - uses: actions/upload-artifact@v4
    with:
      name: linux-build
      path: dist/linux/
```

### 8.4 `release.yml`

```yaml
# Trigger: push of v* tag only

jobs:
  build-windows:
    uses: ./.github/workflows/build-windows.yml
  build-macos:
    uses: ./.github/workflows/build-macos.yml
  build-linux:
    uses: ./.github/workflows/build-linux.yml

  publish:
    needs: [build-windows, build-macos, build-linux]
    runs-on: ubuntu-latest
    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
      - name: Generate checksums
        run: sha256sum windows-build/* macos-build/* linux-build/* > SHA256SUMS.txt
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            windows-build/QuadClicker.exe
            macos-build/QuadClicker.dmg
            linux-build/*.AppImage
            linux-build/*.deb
            SHA256SUMS.txt
          generate_release_notes: true
```

### 8.5 Secrets Required

| Secret Name              | Used In         | Description                                          |
|--------------------------|-----------------|------------------------------------------------------|
| `SIGNING_CERT_BASE64`    | build-windows   | Base64-encoded PFX file for EV code signing cert     |
| `SIGNING_CERT_PASSWORD`  | build-windows   | Password for the PFX                                 |
| `APPLE_API_KEY`          | build-macos     | App Store Connect API key file content (base64)      |
| `APPLE_API_KEY_ID`       | build-macos     | App Store Connect API key ID                         |
| `APPLE_API_ISSUER`       | build-macos     | App Store Connect issuer ID                          |
| `GPG_PRIVATE_KEY`        | release         | GPG private key for signing Linux packages           |
| `GPG_PASSPHRASE`         | release         | Passphrase for GPG key                               |

---

## 9. Testing Strategy

### 9.1 Unit Tests (All Platforms)

Test the following in isolation, with no OS or UI dependencies:

| Module              | Test Cases                                                                                                  |
|---------------------|-------------------------------------------------------------------------------------------------------------|
| `ClickRateParser`   | All 6 format variants; boundary (1ms min); invalid inputs (negative, zero, NaN, empty, garbage string)      |
| `ClickSession`      | Construction with valid args; validation rejects conflicting stop conditions (0 clicks + 0 seconds is valid) |
| CLI argument parser | All flags; missing required args; conflicting args (`--button invalid`); `--help` exits 0; `--version`      |
| `HotkeyManager`     | Same-key assignment rejected; null/empty hotkey clears registration                                         |

**Windows:** xUnit + FluentAssertions (`dotnet test`)
**macOS:** XCTest (`xcodebuild test`)
**Linux:** Google Test or Catch2 (`ctest`)

### 9.2 Integration Tests

These require an actual OS environment (run in CI with virtual display where needed):

| Test                        | Windows                       | macOS                          | Linux                                      |
|-----------------------------|-------------------------------|--------------------------------|--------------------------------------------|
| Click injection — left      | Create listener window, verify WM_LBUTTONDOWN received | NSEvent global monitor | XRecordInterceptData via XRECORD extension |
| Click injection — right     | Same with WM_RBUTTONDOWN      | kCGEventRightMouseDown         | XRecordInterceptData                       |
| Click injection — middle    | Same with WM_MBUTTONDOWN      | kCGEventOtherMouseDown         | XRecordInterceptData                       |
| Double click detection      | Verify WM_LBUTTONDBLCLK       | Monitor doubleClick flag       | Count consecutive button events            |
| Stop after N clicks         | Run engine for 10 clicks; assert click count == 10     | Same                           | Same                                       |
| Stop after N seconds        | Run engine for 1s; assert elapsed < 1.5s               | Same                           | Same                                       |
| CLI end-to-end              | Run `QuadClicker.exe --rate 10/s --stop-after-clicks 5`; assert 5 clicks injected | Same with binary | Same |

**Linux CI:** Use `Xvfb` virtual framebuffer for X11 tests. Run as: `Xvfb :99 -screen 0 1024x768x24 & DISPLAY=:99 ctest`

### 9.3 Timing / Precision Tests

- At each rate (1ms, 10ms, 100ms, 1000ms), measure actual inter-click intervals over 100 iterations
- Assert: mean interval within 10% of target; max deviation < 2x target
- Document results in README under "Performance"
- These are informational rather than hard-pass/fail in CI (OS scheduling is non-deterministic)

### 9.4 Accessibility Tests

- **Windows:** Run UIAutomation traversal; verify all interactive controls have `AutomationProperties.Name` set
- **macOS:** Run `xcrun accessibility-inspector` in CI (or snapshot test accessible elements)
- **Linux:** Use `at-spi-bus-launcher` + `pyatspi2` to verify all Qt controls are reachable via AT-SPI2

---

## 10. Open Questions

These require Kyle's input before or during implementation. Items marked **[BLOCKER]** must be resolved before the affected phase begins.

1. **[BLOCKER — Phase 4]** App bundle identifier for macOS. Required for code signing and notarization. Suggested: `io.quadstronaut.QuadClicker`. Confirm or specify alternative.

2. **[BLOCKER — Phase 4]** Legal entity name for code signing certificates. The Windows EV cert and Apple Developer ID both require a registered legal name. Is this filed under "Kyle Green" (individual) or under a company name? The "sovereign cloud company" — does it have a registered name yet?

3. **[BLOCKER — Phase 4]** Homebrew strategy: maintain a personal tap (`Quadstronaut/homebrew-quadclicker`) at launch, then submit to `homebrew/homebrew-cask` once the tool reaches sufficient adoption? Or skip personal tap and go directly to official cask? (Official cask requires >30 day old GitHub repo and some usage evidence.)

4. **[Phase 1]** Finalize exact accent hex. `#50C878` (standard "emerald green") is assumed. Confirm this is the exact brand color or provide an alternative.

5. **[Phase 1]** Should the app support **multiple simultaneous click targets** (e.g., click location A then B then C in sequence)? This was not mentioned in the interview but is a natural extension. Confirming scope is "no" for v1.

6. **[Phase 1]** Should **click rate have a sub-millisecond input mode**? E.g., `0.5ms` / `2000/s`. The OS (Windows `SendInput`, macOS `CGEventPost`) may not honor sub-1ms intervals reliably. Recommended: allow input, document OS floor, do not artificially block it.

7. **[Phase 2]** macOS: should the app live **only in the menu bar** (no Dock icon, `LSUIElement = YES`) or in both? Suggested default: menu bar only with an option in settings to show Dock icon. Confirm.

8. **[Phase 3]** Wayland global hotkeys: GNOME does not expose a public D-Bus API for registering global shortcuts as of GNOME 45. KDE does via `org.kde.kglobalaccel`. Decision needed: show a warning on GNOME Wayland that global hotkeys are unavailable, or ship without hotkey support on that combination?

9. **[Phase 4]** Flatpak sandbox: `CGEventPost` equivalent (uinput) requires `/dev/uinput` device access. Flatpak's sandbox does not grant this by default. Flatpak portal (`org.freedesktop.portal.RemoteDesktop`) can be used instead but requires user consent on each session. Is Flatpak a priority or can it be deferred post-v1?

10. **[Phase 5]** App name branding: binary is `quadclicker`, app display name is "QuadClicker". Should the window title and tray tooltip include a tagline (e.g., "QuadClicker — Auto Clicker") or just "QuadClicker"?

11. **[Phase 0]** Repository rename / organization: is this staying at `github.com/Quadstronaut/QuadClicker` or moving to an organization account tied to the sovereign cloud company? This affects package manager submission URLs and can be painful to change post-distribution.

12. **[General]** License: currently GPL v3. Confirm this is intentional for an open-source tool in a commercial product line. GPL v3 means any derivative work must also be GPL v3. MIT or Apache 2.0 would be more permissive if Kyle wants others to embed or extend the CLI without copyleft obligations. This is a significant decision.

---

*End of PLAN.md — last updated 2026-03-23*
