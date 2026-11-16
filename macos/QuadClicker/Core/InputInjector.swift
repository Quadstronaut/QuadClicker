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
        try sendSingleClick(button: button, clickState: 1)

        if clickType == .double_ && !isCancelled() {
            // NSEvent.doubleClickInterval is the MAXIMUM gap that still counts as a
            // double click; sleeping that long landed on the edge of the window.
            // Use a quarter of it, capped at 50 ms (same rule as Windows/Linux).
            let gap = min(0.05, NSEvent.doubleClickInterval / 4)
            Thread.sleep(forTimeInterval: gap)
            if !isCancelled() {
                // Click state 2 is what makes AppKit report clickCount == 2.
                try sendSingleClick(button: button, clickState: 2)
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static func sendSingleClick(button: MouseButton, clickState: Int64) throws {
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

        // Without this field apps derive clickCount 1 for every event, so a
        // "double" was two single clicks.
        downEvent.setIntegerValueField(.mouseEventClickState, value: clickState)
        upEvent.setIntegerValueField(.mouseEventClickState, value: clickState)

        downEvent.post(tap: .cghidEventTap)
        upEvent.post(tap: .cghidEventTap)
    }

    private static func currentMousePosition() -> CGPoint {
        // A fresh CGEvent's location is the cursor in CG global coordinates
        // (top-left origin of the primary display), exactly what CGEvent posting
        // expects, on every monitor. The fallback flips against the PRIMARY screen.
        CGEvent(source: nil)?.location ?? NSEvent.mouseLocation.flipped
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
    /// Global Cocoa coordinates are relative to the PRIMARY screen (screens[0]),
    /// not NSScreen.main (the key window's screen, which may be a secondary
    /// display of a different height).
    var flipped: CGPoint {
        guard let primary = NSScreen.screens.first else {
            return CGPoint(x: x, y: y)
        }
        return CGPoint(x: x, y: primary.frame.maxY - y)
    }
}
