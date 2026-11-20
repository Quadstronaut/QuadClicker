// Core/IdleDetector.swift
// QuadClicker — macOS
//
// Reports how long the HID system has been idle using CoreGraphics.
// This is the macOS equivalent of Windows GetLastInputInfo.

import CoreGraphics
import Foundation

enum IdleDetector {
    /// kCGAnyInputEventType, i.e. ((CGEventType)(~0)). Exposed so tests can compare it to `.null`.
    static let anyInputEventType = CGEventType(rawValue: ~0)!

    /// Returns the number of seconds since the last user input event.
    /// Returns 0 on failure (treated as "not idle").
    static func getIdleTime() -> TimeInterval {
        // kCGAnyInputEventType covers keyboard + mouse + stylus etc.
        // It is ((CGEventType)(~0)) and has no Swift case; `.null` is
        // kCGEventNull (0), which measured time since a null event, not user input.
        let idle = CGEventSource.secondsSinceLastEventType(
            .hidSystemState,
            eventType: anyInputEventType
        )
        // CGEventSource returns a negative value on some error paths
        return max(0, idle)
    }
}
