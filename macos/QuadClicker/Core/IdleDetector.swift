// Core/IdleDetector.swift
// QuadClicker — macOS
//
// Reports how long the HID system has been idle using CoreGraphics.
// This is the macOS equivalent of Windows GetLastInputInfo.

import CoreGraphics
import Foundation

enum IdleDetector {
    /// Returns the number of seconds since the last user input event.
    /// Returns 0 on failure (treated as "not idle").
    static func getIdleTime() -> TimeInterval {
        // kCGAnyInputEventType covers keyboard + mouse + stylus etc.
        let idle = CGEventSource.secondsSinceLastEventType(
            .hidSystemState,
            eventType: .null   // .null == kCGAnyInputEventType (value 0)
        )
        // CGEventSource returns a negative value on some error paths
        return max(0, idle)
    }
}
