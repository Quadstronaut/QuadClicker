// Core/InputInjector.swift
// QuadClicker — macOS
//
// Injects mouse click events via CGEventPost (CoreGraphics).
// Exact port of Windows InputInjector.cs.
//
// Requires Accessibility permission (TCC) when targeting another application.
// CGEventPost(.cghidEventTap, …) works without it for most cases.

import CoreGraphics
import AppKit
import Foundation

enum InputInjector {

    /// Errors thrown by click injection.
    enum InjectionError: Error, LocalizedError {
        case cgEventCreationFailed(String)

        var errorDescription: String? {
            switch self {
            case .cgEventCreationFailed(let detail):
                return "CGEventCreate failed: \(detail). Input may be blocked (Accessibility permission required)."
            }
        }
    }

    /// Inject a click at the current cursor position (or at the session's fixed coordinate if
    /// the caller already moved the cursor via CGWarpMouseCursorPosition).
    ///
    /// - Parameters:
    ///   - button:    Which mouse button to press.
    ///   - clickType: Single or double.
    ///   - isCancelled: Closure returning true if the operation should abort mid-double-click.
    static func click(
        button: MouseButton,
        clickType: ClickType,
        isCancelled: () -> Bool = { false }
    ) throws {
        try sendSingleClick(button: button)

        if clickType == .double_ && !isCancelled() {
            // Use the system double-click interval (same as Windows GetDoubleClickTime)
            let interval = NSEvent.doubleClickInterval
            Thread.sleep(forTimeInterval: interval)
            if !isCancelled() {
                try sendSingleClick(button: button)
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static func sendSingleClick(button: MouseButton) throws {
        let pos = currentMousePosition()
        let (downType, upType, cgButton) = eventTypes(for: button)

        guard let downEvent = CGEvent(
            mouseEventSource: nil,
            mouseType: downType,
            mouseCursorPosition: pos,
            mouseButton: cgButton
        ) else {
            throw InjectionError.cgEventCreationFailed("mouseDown event for \(button)")
        }

        guard let upEvent = CGEvent(
            mouseEventSource: nil,
            mouseType: upType,
            mouseCursorPosition: pos,
            mouseButton: cgButton
        ) else {
            throw InjectionError.cgEventCreationFailed("mouseUp event for \(button)")
        }

        downEvent.post(tap: .cghidEventTap)
        upEvent.post(tap: .cghidEventTap)
    }

    private static func currentMousePosition() -> CGPoint {
        // CGEventSourceGetLocalEventsSuppressionInterval is not what we want here;
        // read the actual HID cursor position.
        NSEvent.mouseLocation.flipped
    }

    private static func eventTypes(
        for button: MouseButton
    ) -> (CGEventType, CGEventType, CGMouseButton) {
        switch button {
        case .left:
            return (.leftMouseDown, .leftMouseUp, .left)
        case .right:
            return (.rightMouseDown, .rightMouseUp, .right)
        case .middle:
            // CGMouseButton.center == button index 2
            return (.otherMouseDown, .otherMouseUp, .center)
        }
    }
}

// ── NSPoint → CGPoint with flipped y-axis ────────────────────────────────────
// AppKit uses bottom-left origin; CoreGraphics uses top-left origin.
private extension NSPoint {
    /// Convert from AppKit (bottom-left) coordinates to CG (top-left) coordinates.
    var flipped: CGPoint {
        guard let screen = NSScreen.main else {
            return CGPoint(x: x, y: y)
        }
        let screenHeight = screen.frame.height
        return CGPoint(x: x, y: screenHeight - y)
    }
}
