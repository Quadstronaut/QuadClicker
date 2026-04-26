# QuadClicker — Linux

**Status: Phase 3 — build verified.** Compiles clean on Ubuntu 24.04 (Qt 6.4.2, GCC 13.3) including under WSL2 with WSLg. All unit tests pass; CLI happy and parse-error paths exercised; GUI launches and renders the Taneth palette correctly. No signed `.deb` / AppImage published yet.

## Stack

| | |
|---|---|
| Language | C++17 |
| Framework | Qt6 (Widgets, Core, Gui, Concurrent, DBus) |
| Build | CMake ≥ 3.20 + Ninja |
| Input injection (X11) | XTest extension (`XTestFakeButtonEvent`) |
| Input injection (Wayland) | uinput kernel interface |
| Runtime detection | `QGuiApplication::platformName() == "wayland"` |
| Idle detection | `XScreenSaverQueryInfo` (X11) / `org.freedesktop.ScreenSaver` D-Bus (Wayland) |
| Hotkeys (X11) | `XGrabKey` |
| Hotkeys (Wayland) | KDE `org.kde.kglobalaccel` D-Bus / GNOME limited |
| System tray | `QSystemTrayIcon` |
| Min OS | Ubuntu 22.04 / Fedora 38 |

## Build

```bash
sudo apt install build-essential cmake ninja-build pkg-config \
                 qt6-base-dev qt6-base-dev-tools qt6-tools-dev \
                 libqt6svg6-dev libxtst-dev libxss-dev

cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=Release linux/
cmake --build build
ctest --test-dir build --output-on-failure
```

## CLI smoke test

```bash
./build/quadclicker --version
./build/quadclicker --rate 100ms --stop-after-clicks 5 --location 800,500
```

## WSL2 / WSLg notes

- `wsl --install Ubuntu-24.04` provisions everything needed; WSLg supplies `DISPLAY=:0` and `WAYLAND_DISPLAY=wayland-0` automatically.
- The Qt app builds and runs as expected; the GUI renders through XWayland.
- XTEST events injected by the engine are not visible to other XInput observers under XWayland — this is a WSLg compositor quirk, not a bug in the engine. Click delivery on a real Linux desktop is unaffected.
- `uinput` is unavailable in WSL (no `/dev/uinput` from the host); use a real Linux session to exercise the Wayland injection path.

## Distribution Targets (planned)

- AppImage (portable)
- `.deb` package (Debian/Ubuntu)
- Snap
- Flatpak (`io.quadstronaut.QuadClicker`)

See `PLAN.md § Phase 3` and `§ Phase 4` in the repo root for full specification.
