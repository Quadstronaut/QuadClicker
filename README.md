# QuadClicker

**A fast and configurable auto-clicker for Windows.**

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Contributing](#contributing)
- [License](#license)
- [Author](#author)
- [Acknowledgements](#acknowledgements)

---

## Overview

`QuadClicker` is a utility for automating mouse clicks on Windows. It is a WPF application written in C# and targeting .NET 8. It provides a simple UI to configure click rate, location, and stopping conditions.

Developed by Quadstronaut (Kyle Green).

## Features

*   **Configurable Click Rate:** Set the click interval in milliseconds, or as a frequency in clicks per second or per minute.
*   **Flexible Click Location:**
    *   Click at the current mouse cursor position.
    *   Click at a fixed, specified X/Y coordinate.
    *   Use the "Pick Location" tool to visually select a point on the screen.
*   **Automatic Stop Conditions:**
    *   Stop after a defined number of clicks.
    *   Stop after a set duration in seconds.
*   **Idle Detection:** Configure the auto-clicker to only run when the system has been idle for a specified time.
*   **Emergency Hotkey:** Instantly stop the auto-clicker at any time by pressing the `F10` key.

## Getting Started

To build and run this project, you will need the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or a compatible version of Visual Studio.

### Building from the Command Line

1.  Clone the repository:
    ```sh
    git clone https://github.com/Quadstronaut/QuadClicker.git
    ```
2.  Navigate to the project directory:
    ```sh
    cd QuadClicker
    ```
3.  Build the project:
    ```sh
    dotnet build -c Release
    ```
4.  Run the application:
    The executable will be located in `bin/Release/net8.0-windows/`.
    ```sh
    ./bin/Release/net8.0-windows/QuadClicker.exe
    ```

### Building with Visual Studio

1.  Clone the repository.
2.  Open the `QuadClicker.csproj` file in Visual Studio.
3.  Set the build configuration to "Release".
4.  Build the solution (F6).
5.  Run the application (F5).

## Usage

1.  **Configure Click Rate:** Enter a value in the "Click Rate" textbox.
    *   `100ms` (milliseconds)
    *   `10 times per second`
    *   `600 times per minute`
2.  **Choose Click Location:**
    *   Select "Current Position" to click wherever your mouse is.
    *   Select "Specific Position", then either manually enter the X and Y coordinates or use the "Pick Location" button to select a point on the screen.
3.  **Set Optional Conditions:**
    *   **Idle Time:** Set a number of seconds the system must be idle before clicking begins.
    *   **Stop After (clicks):** Set a maximum number of clicks.
    *   **Stop After (seconds):** Set a maximum duration for the clicking session.
4.  **Start/Stop:**
    *   Click the "Start" button to begin auto-clicking.
    *   Click the "Stop" button or press `F10` to end the session.

---

## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1.  Fork the Project
2.  Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the Branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

---

## License

Copyright © 2026 Quadstronaut (Kyle Green).

This project is licensed under the terms of the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007. See the `LICENSE` file in the project root for the full license text.

---

## Author

**Kyle Green (Quadstronaut)**

*   Project Link: [https://github.com/Quadstronaut/QuadClicker](https://github.com/Quadstronaut/QuadClicker)

---

## Acknowledgements

*   Inspired by [Autoclick by Mahdi Bchatnia](https://mahdi.jp/apps/autoclick) (2011-2021 - GNU GPLv2)
*   Thanks to all contributors who help improve this project.

---

**Happy Clicking! 🖱️**
