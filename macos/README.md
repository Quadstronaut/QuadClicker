# QuadClicker — macOS

**Status: Phase 2 — Code-complete, verified on a CI runner only.** `build-macos.yml` (`macos-26`) compiles the app, runs the unit tests, and runs real-input checks (QCLK-8): XCTest posts real mouse events through the app's `InputInjector` and observes them with an event tap (double-click state, Stop-to-Start run handoff, idle reset, hotkey registration), and a script sends SIGINT to the built CLI binary and checks it exits 130 within 2 s. The runner is headless (one 1024x768 screen), and the job grants itself Accessibility/Input permissions in the runner's TCC database; the real permission prompt, the hotkey capture field, multi-monitor layout and the GUI are still unverified on a physical Mac. No signed or notarized binary has been released.

## Stack

| | |
|---|---|
| Language | Swift |
| Framework | SwiftUI |
| Input injection | `CGEventPost` (CoreGraphics) |
| Settings persistence | `~/Library/Application Support/QuadClicker/settings.json` |
| Menu bar | `NSStatusItem` (AppKit) |
| Hotkeys | `NSEvent.addGlobalMonitorForEvents` — requires Accessibility permission |
| Idle detection | `CGEventSourceSecondsSinceLastEventType` |
| Min OS | macOS 13 Ventura |

## Building

Requires Xcode 15+ on macOS 13+. See `CODE_SIGNING.md` for signing and notarization requirements.

```bash
open macos/QuadClicker.xcodeproj

# Command-line build
xcodebuild -project macos/QuadClicker.xcodeproj \
           -scheme QuadClicker \
           -configuration Release \
           -derivedDataPath artifacts/macos

# Run tests
xcodebuild test -project macos/QuadClicker.xcodeproj \
                -scheme QuadClickerTests \
                -destination 'platform=macOS'
```

> **Note:** `CGEventPost` and global hotkeys require Accessibility permission. The app prompts on first launch when hotkeys are configured. Code signing and notarization are required for distribution — see `CODE_SIGNING.md`.

See `PLAN.md § Phase 2` in the repo root for full specification.
