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

- **Click Rate** — Enter any format: `100ms`, `10/s`, `10cps`, `600/min`, `600cpm`, or a bare integer (ms)
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
- **Dark Theme** — Native dark mode with emerald green accent on all platforms

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

# Publish self-contained
dotnet publish windows/QuadClicker.csproj -c Release -r win-x64 --self-contained false -o artifacts/windows
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

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt install qt6-base-dev libxt-dev libxtst-dev libxss-dev cmake build-essential

# Configure and build
cmake -B build -DCMAKE_BUILD_TYPE=Release linux/
cmake --build build --parallel

# Run tests
cd build && ctest --output-on-failure

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
| `--rate <value>` | string | required | Click rate. Formats: `100ms` · `10/s` · `600/min` |
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

# Click after 5 seconds of idle
quadclicker --rate 1000ms --idle-wait 5

# Launch GUI minimized to tray
quadclicker --minimized
```

---

## Design System

| Token | Value | Usage |
|---|---|---|
| Accent | `#50C878` | Start button, focus rings, active status |
| Accent Hover | `#3DAF62` | Hover state |
| Accent Pressed | `#2E9150` | Pressed state |
| Danger | `#E05252` | Stop button, errors |
| Background | `#1A1A1A` | Window background |
| Surface | `#242424` | Input backgrounds |
| Border | `#3A3A3A` | Input borders |
| Text Primary | `#F0F0F0` | Main text |
| Text Secondary | `#9A9A9A` | Labels, helpers |
| Status Waiting | `#E0A030` | Idle wait indicator |

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
| 1 | Windows WPF — feature-complete | ✅ Done |
| 2 | macOS SwiftUI — feature-complete | 🔨 Code written, needs Mac + Xcode to build |
| 3 | Linux Qt6/C++ — feature-complete | 🔨 Code written, needs Linux + Qt6 to build |

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
