// QuadClickerApp.swift
// QuadClicker — macOS
//
// @main entry point.
// Detects CLI mode: if any argument beyond the executable path and --minimized is
// present, runs headlessly via CliEntryPoint and exits.

import SwiftUI
import AppKit

@main
struct QuadClickerApp: App {

    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    init() {
        // ── CLI mode detection ─────────────────────────────────────────────────
        // CommandLine.arguments[0] is always the executable path.
        // --minimized is the only argument that is valid in GUI mode.
        let rawArgs = Array(CommandLine.arguments.dropFirst())
        let isMinimized = rawArgs.count == 1
            && rawArgs[0].lowercased() == "--minimized"

        let isCli = !rawArgs.isEmpty && !isMinimized

        if isCli {
            // Run headlessly, then terminate with the appropriate exit code.
            let code = CliEntryPoint.run(args: rawArgs)
            exit(code)
        }

        // GUI mode — load settings before the window appears
        let settings = AppSettings.load()
        appDelegate.settings = settings

        if isMinimized {
            // The window will be created hidden; the tray icon is always visible.
            // We signal this via a shared flag that ContentView observes.
            AppState.shared.startMinimized = true
        }
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(AppState.shared)
        }
        .windowStyle(.hiddenTitleBar)
        .commands {
            // Remove standard menu items that don't apply to a tray app
            CommandGroup(replacing: .newItem) {}
        }
    }
}

// ── AppState ──────────────────────────────────────────────────────────────────
// Process-wide observable state shared between the app, tray, and window.

final class AppState: ObservableObject {
    static let shared = AppState()

    /// Loaded once at startup.
    let settings: AppSettings = .load()

    /// When true, the window starts hidden (--minimized flag).
    var startMinimized: Bool = false

    private init() {}
}
