# QuadClicker — macOS

**Status: Phase 2 — Code-complete, UNVERIFIED.** All Swift source files are written and present in this directory, but the app has never been compiled or run — an Xcode-equipped Mac is required. The `build-macos.yml` CI workflow is a no-op placeholder and produces no artifact. No signed or notarized binary has been released.

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
