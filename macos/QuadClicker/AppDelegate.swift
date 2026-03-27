// AppDelegate.swift
// QuadClicker — macOS
//
// NSApplicationDelegate.
// Hides the app from the Dock (LSUIElement=YES in Info.plist).
// Prompts for Accessibility permission (needed for global hotkeys and CGEventTap).

import AppKit
import Foundation

final class AppDelegate: NSObject, NSApplicationDelegate {

    /// Set by QuadClickerApp before applicationDidFinishLaunching fires.
    var settings: AppSettings = .load()

    func applicationDidFinishLaunching(_ notification: Notification) {
        // The app is a menu-bar-only app (LSUIElement); hide the Dock icon.
        // This is belt-and-suspenders: Info.plist already sets LSUIElement.
        NSApp.setActivationPolicy(.accessory)

        // Prompt for Accessibility permission if not already granted.
        // Without it, global hotkeys and the CGEventTap in LocationPicker won't work.
        promptForAccessibilityIfNeeded()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // Keep running after the main window is closed — tray icon stays active
        return false
    }

    func applicationWillTerminate(_ notification: Notification) {
        settings.save()
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    private func promptForAccessibilityIfNeeded() {
        guard !AXIsProcessTrusted() else { return }

        // Passing the prompt option triggers the system dialog.
        let options: NSDictionary = [kAXTrustedCheckOptionPrompt.takeUnretainedValue(): true]
        AXIsProcessTrustedWithOptions(options)
    }
}
