# QuadClicker — macOS

**Status: Phase 2 — Not yet started.**

## Planned Stack

| | |
|---|---|
| Language | Swift |
| Framework | SwiftUI |
| Input injection | `CGEventPost` (CoreGraphics) |
| Menu bar | `NSStatusItem` (AppKit) |
| Hotkeys | `CGEventTap` / `NSEvent.addGlobalMonitorForEvents` |
| Min OS | macOS 13 Ventura |

## Phase 2 Deliverables

See `PLAN.md § Phase 2` in the repo root for full specification.

Requires:
- Apple Developer account (for notarization) — see `CODE_SIGNING.md`
- Xcode 15+ on macOS 13+
