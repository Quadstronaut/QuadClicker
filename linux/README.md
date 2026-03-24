# QuadClicker — Linux

**Status: Phase 3 — Not yet started.**

## Planned Stack

| | |
|---|---|
| Language | C++ |
| Framework | Qt6 (Widgets) |
| Input injection (X11) | XTest extension (`XTestFakeButtonEvent`) |
| Input injection (Wayland) | uinput kernel interface |
| Runtime detection | `QGuiApplication::platformName() == "wayland"` |
| System tray | `QSystemTrayIcon` with `libappindicator` fallback |
| Hotkeys (X11) | `XGrabKey` |
| Hotkeys (Wayland) | KDE `org.kde.kglobalaccel` D-Bus / GNOME limited |
| Min OS | Ubuntu 22.04 / Fedora 38 |

## Phase 3 Deliverables

See `PLAN.md § Phase 3` in the repo root for full specification.

## Distribution Targets

- AppImage (portable)
- `.deb` package (Debian/Ubuntu)
- Snap
- Flatpak (`io.quadstronaut.QuadClicker`)
