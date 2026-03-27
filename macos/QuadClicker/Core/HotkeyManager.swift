// Core/HotkeyManager.swift
// QuadClicker — macOS
//
// Registers and dispatches global hotkeys using NSEvent global monitors.
// Requires Accessibility permission (TCC: com.apple.private.accessibility.inspection).

import AppKit
import Foundation

// ── Hotkey representation ─────────────────────────────────────────────────────

struct HotkeySpec: Equatable {
    let key: String              // e.g. "F10", "A"
    let modifierFlags: NSEvent.ModifierFlags

    /// Modifiers as displayable string, e.g. "Ctrl+Shift+F10"
    var displayText: String {
        var parts: [String] = []
        if modifierFlags.contains(.control) { parts.append("Ctrl") }
        if modifierFlags.contains(.shift)   { parts.append("Shift") }
        if modifierFlags.contains(.option)  { parts.append("Alt") }
        if modifierFlags.contains(.command) { parts.append("Cmd") }
        parts.append(key)
        return parts.joined(separator: "+")
    }

    /// Parse a display text string back into a HotkeySpec, e.g. "Ctrl+F10".
    static func parse(_ text: String) -> HotkeySpec? {
        guard !text.trimmingCharacters(in: .whitespaces).isEmpty else { return nil }
        let parts = text.split(separator: "+").map { String($0).trimmingCharacters(in: .whitespaces) }
        guard let keyPart = parts.last, !keyPart.isEmpty else { return nil }

        var flags: NSEvent.ModifierFlags = []
        for mod in parts.dropLast() {
            switch mod.uppercased() {
            case "CTRL":  flags.insert(.control)
            case "SHIFT": flags.insert(.shift)
            case "ALT":   flags.insert(.option)
            case "CMD":   flags.insert(.command)
            default: break
            }
        }
        return HotkeySpec(key: keyPart, modifierFlags: flags)
    }
}

// ── Manager ───────────────────────────────────────────────────────────────────

final class HotkeyManager {

    private var monitors: [Any] = []
    private let lock = NSLock()

    deinit {
        unregisterAll()
    }

    // ── Registration ─────────────────────────────────────────────────────────

    /// Register a global hotkey. Returns an opaque token that can be passed to `unregister(_:)`.
    /// Returns nil if Accessibility permission is not granted or the monitor could not be installed.
    @discardableResult
    func register(spec: HotkeySpec, handler: @escaping () -> Void) -> Any? {
        guard AXIsProcessTrusted() else { return nil }

        let monitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { event in
            guard let chars = event.charactersIgnoringModifiers else { return }
            let keyMatch = chars.lowercased() == spec.key.lowercased()
                        || event.keyCode == keyCodeForName(spec.key)

            // Normalise the relevant modifier flags for comparison
            let relevantMask: NSEvent.ModifierFlags = [.control, .shift, .option, .command]
            let eventMods = event.modifierFlags.intersection(relevantMask)
            let wantedMods = spec.modifierFlags.intersection(relevantMask)

            if keyMatch && eventMods == wantedMods {
                DispatchQueue.main.async { handler() }
            }
        }

        if let monitor {
            lock.lock()
            monitors.append(monitor)
            lock.unlock()
        }
        return monitor
    }

    /// Unregister a previously registered hotkey by its token.
    func unregister(_ token: Any) {
        NSEvent.removeMonitor(token)
        lock.lock()
        monitors.removeAll { $0 as AnyObject === token as AnyObject }
        lock.unlock()
    }

    /// Unregister all hotkeys.
    func unregisterAll() {
        lock.lock()
        let all = monitors
        monitors.removeAll()
        lock.unlock()
        for m in all { NSEvent.removeMonitor(m) }
    }
}

// ── Key name → key code mapping ───────────────────────────────────────────────
// Covers the most common hotkey keys. Function keys F1–F20 and letters are handled
// by name comparison in the monitor callback via `charactersIgnoringModifiers`.

private func keyCodeForName(_ name: String) -> CGKeyCode {
    let map: [String: CGKeyCode] = [
        "F1": 122, "F2": 120, "F3": 99,  "F4": 118,
        "F5": 96,  "F6": 97,  "F7": 98,  "F8": 100,
        "F9": 101, "F10": 109, "F11": 103, "F12": 111,
        "F13": 105, "F14": 107, "F15": 113, "F16": 106,
        "F17": 64,  "F18": 79,  "F19": 80,  "F20": 90,
        "RETURN": 36, "ENTER": 76, "ESCAPE": 53, "DELETE": 51,
        "TAB": 48, "SPACE": 49, "HOME": 115, "END": 119,
        "PAGEUP": 116, "PAGEDOWN": 121,
        "LEFT": 123, "RIGHT": 124, "DOWN": 125, "UP": 126,
        "0": 29, "1": 18, "2": 19, "3": 20, "4": 21,
        "5": 23, "6": 22, "7": 26, "8": 28, "9": 25,
    ]
    return map[name.uppercased()] ?? 0xFFFF
}
