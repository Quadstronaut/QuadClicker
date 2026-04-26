# QuadClicker

**A fast, native, open-source auto-clicker for Windows, macOS, and Linux.**

[![Windows Build](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-windows.yml/badge.svg)](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-windows.yml)
[![macOS Build](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-macos.yml/badge.svg)](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-macos.yml)
[![Linux Build](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-linux.yml/badge.svg)](https://github.com/Quadstronaut/QuadClicker/actions/workflows/build-linux.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

---

## Overview

QuadClicker is the definitive open-source auto-clicker: fully configurable, scriptable via CLI, accessibility-first, and distributed everywhere. Built as a monorepo with three separate **native** applications — no cross-platform frameworks, ever.

| Platform | Language | Framework | Min OS |
|---|---|---|---|
| Windows 10/11 | C# / .NET 10 | WPF | Windows 10 22H2 |
| macOS | Swift | SwiftUI | macOS 13 Ventura |
| Linux | C++ | Qt6 | Ubuntu 22.04 / Fedora 38 |

---

## Features

- **Click Rate** — Pick **Delay** (`ms` / `sec` / `min`) or **Frequency** (per sec / per min / per hour) via radio. CLI also accepts free-form: `100ms`, `5s`, `2min`, `10/s`, `10cps`, `600/min`, `600cpm`, `60/h`, `60cph`, or a bare integer (ms). Bounds: 1 ms ≤ delay ≤ 360 min.
- **Mouse Button** — Left, Right, or Middle
- **Click Type** — Single or Double (uses OS double-click interval)
- **Location Modes**
  - Current cursor position
  - Fixed XY coordinate
  - Visual overlay picker — click anywhere on screen to capture coordinates
- **Stop Conditions** — After N clicks, after N seconds, or manual stop
- **Idle Detection** — Wait for N seconds of system idle before starting
- **Hotkeys** — Independently configurable start and stop hotkeys (global, works when minimized)
- **System Tray** — Minimize to tray; close button quits
- **Always on Top** — Optional float above all windows
- **Settings Persistence** — All settings saved and restored between launches
- **CLI Mode** — Same binary, full headless execution (see [CLI Reference](#cli-reference))
- **Dark Theme** — Native dark mode. Windows ships the new "Taneth" palette (deep-green hull, warm gold HUD accent); macOS and Linux still ship the original emerald-green palette in source until the redesign is ported.

---

## Repository Structure

```
QuadClicker/
├── windows/         ← WPF / C# / .NET 10
├── macos/           ← SwiftUI / Swift
├── linux/           ← Qt6 / C++
├── .github/
│   └── workflows/   ← CI/CD for all three platforms + release
├── ref_imgs/        ← Design reference screenshots
├── PLAN.md          ← Living design document
├── CODE_SIGNING.md  ← Signing and notarization guide
└── README.md
```

---

## Building

### Windows

**Requirements:** .NET 10 SDK, Windows 10 22H2+

```bash
# Build
dotnet build windows/QuadClicker.csproj -c Release

# Run tests
dotnet test windows/Tests/QuadClicker.Tests.csproj

# Publish self-contained single-file (matches release.yml)
dotnet publish windows/QuadClicker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/windows
```

### macOS

**Requirements:** Xcode 15+, macOS 13+, Apple Developer account (for signing/notarization)

```bash
# Open in Xcode
open macos/QuadClicker.xcodeproj

# Build from command line
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

### Linux

**Requirements:** Qt 6.2+, CMake 3.20+, GCC 11+ or Clang 13+, libXtst, libXss, libX11

**Verified:** Ubuntu 24.04 LTS (Qt 6.4.2, GCC 13.3) — including under WSL2 with WSLg.

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt install build-essential cmake ninja-build pkg-config \
                 qt6-base-dev qt6-base-dev-tools qt6-tools-dev \
                 libqt6svg6-dev libxtst-dev libxss-dev

# Configure and build
cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=Release linux/
cmake --build build

# Run tests
ctest --test-dir build --output-on-failure

# Install
sudo cmake --install build
```

---

## CLI Reference

When any argument other than `--minimized` is passed, the app runs headless with no GUI.

```
quadclicker [OPTIONS]
```

| Argument | Type | Default | Description |
|---|---|---|---|
| `--rate <value>` | string | required | Click rate. Formats: `100ms` · `5s` · `2min` · `10/s` · `600/min` · `60/h` (1 ms – 360 min) |
| `--button <left\|right\|middle>` | enum | `left` | Mouse button |
| `--type <single\|double>` | enum | `single` | Click type |
| `--location <x,y>` | int pair | cursor | Fixed screen coordinate |
| `--stop-after-clicks <n>` | int | 0 (unlimited) | Stop after N clicks |
| `--stop-after-seconds <n>` | float | 0 (unlimited) | Stop after N seconds |
| `--idle-wait <n>` | float | 0 (disabled) | Wait for N seconds of idle before starting |
| `--no-gui` | flag | auto | Force headless mode |
| `--minimized` | flag | off | Launch GUI minimized to tray |
| `--version` | flag | — | Print version and exit 0 |
| `--help` | flag | — | Print usage and exit 0 |

**Exit codes:** `0` success · `1` invalid argument · `2` runtime error · `130` Ctrl+C

**Examples:**

```bash
# Click at cursor, 10 times/second
quadclicker --rate 10/s

# Right-click at (500,300), stop after 100 clicks
quadclicker --rate 10/s --location 500,300 --button right --stop-after-clicks 100

# Double-click every 2 seconds for 30 seconds
quadclicker --rate 2000ms --type double --stop-after-seconds 30

# One click per minute, for an hour (using the new units)
quadclicker --rate 60/h --stop-after-seconds 3600

# One click every 5 minutes
quadclicker --rate 5min

# Click after 5 seconds of idle
quadclicker --rate 1000ms --idle-wait 5

# Launch GUI minimized to tray
quadclicker --minimized
```

---

## Design System

The Windows app ships the **Taneth** palette: a deep-green hull background with a warm gold HUD accent. macOS and Linux still ship the original emerald-green tokens in source — porting the new palette is tracked under the Phase 2/3 follow-ups.

| Token | Value | Usage |
|---|---|---|
| Accent | `#E8B547` | Start button, focus rings, active status (gold) |
| Accent Hover | `#F5C75A` | Hover on accent elements |
| Accent Pressed | `#B88A2A` | Pressed state |
| Accent Foreground | `#0A1410` | Dark text on accent fills |
| Danger | `#E04030` | Stop button, errors |
| Danger Hover | `#C8331E` | Hover on danger elements |
| Background | `#0A1410` | Window background |
| Surface | `#13211C` | Input backgrounds |
| Surface Elevated | `#1B2E27` | Raised surfaces (small buttons, hover items) |
| Border | `#2D5448` | Input borders |
| Text Primary | `#E8DCB0` | Main text |
| Text Secondary | `#7A9088` | Labels, helpers |
| Text Disabled | `#3D5048` | Disabled text |
| Status Waiting | `#5BA89A` | Idle wait indicator |

---

## Settings

Settings are persisted automatically on exit and loaded on launch. JSON format is identical across platforms for cross-platform compatibility.

| Platform | Path |
|---|---|
| Windows | `%APPDATA%\QuadClicker\settings.json` |
| macOS | `~/Library/Application Support/QuadClicker/settings.json` |
| Linux | `~/.config/quadclicker/settings.json` |

---

## Phase Status

| Phase | Description | Status |
|---|---|---|
| 0 | Monorepo restructure + CI/CD skeleton | ✅ Done |
| 1 | Windows WPF — released **v0.1.0** as a self-contained single-file EXE | ✅ Done |
| 2 | macOS SwiftUI — code-complete, **unverified** (never compiled or run; `build-macos.yml` is a no-op placeholder) | 🔨 Pending Mac + Xcode |
| 3 | Linux Qt6/C++ — **build verified** on Ubuntu 24.04 (Qt 6.4.2) under WSL2/WSLg: clean compile, all unit tests pass, CLI happy + parse-error paths exercised, GUI launches and renders. CI (`build-linux.yml`) now runs the real build. No `.deb` / AppImage published yet. | ✅ Built + tested |

### Known limitations

- The mode-based **Click Rate** redesign and the **Taneth** palette are now in all three platforms' source. Linux is verified end-to-end; macOS source has them but remains unbuilt pending an Xcode-equipped machine.
- Linux global hotkeys: `XGrabKey` (X11) is used; Wayland support is limited to compositors that expose `org.kde.kglobalaccel` (KDE) — GNOME Wayland users will not get global hotkeys until a compositor-side API exists.

---

## Releases / Downloads

Latest builds are published on GitHub Releases:

**[github.com/Quadstronaut/QuadClicker/releases/latest](https://github.com/Quadstronaut/QuadClicker/releases/latest)**

| Platform | Status | Notes |
|---|---|---|
| Windows x64 | `QuadClicker-win-x64.zip` | Self-contained single-file EXE — **no .NET install required**. Unzip and run. |
| macOS | Not yet released | Phase 2 build is unverified; no signed/notarized artifact yet. |
| Linux | Not yet released | Build verified on Ubuntu 24.04 (Qt 6.4.2). No signed `.deb` / `.rpm` / AppImage published yet — packaging is the next step. |

The Windows binary is currently **unsigned** — Windows SmartScreen will warn on first launch until an Authenticode certificate is in place (see `CODE_SIGNING.md`).

---

## Contributing

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push and open a Pull Request

Please open an issue first for major changes.

---

## License

Copyright © 2026 Quadstronaut (Kyle Green).
Licensed under the [GNU General Public License v3.0](LICENSE).

---

## Author

**Kyle Green (Quadstronaut)** — [github.com/Quadstronaut/QuadClicker](https://github.com/Quadstronaut/QuadClicker)

---

## Acknowledgements

Inspired by [Autoclick by Mahdi Bchatnia](https://mahdi.jp/apps/autoclick) (2011–2021, GNU GPLv2)
