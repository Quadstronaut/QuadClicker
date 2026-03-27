// Models/ClickSession.swift
// QuadClicker — macOS

import Foundation

/// Immutable configuration for a single clicking session.
struct ClickSession {
    /// Delay between click events (seconds). Minimum 0.001 (1 ms).
    let clickRate: TimeInterval

    let button: MouseButton
    let clickType: ClickType

    /// When true the click lands at the current cursor position; when false at (x, y).
    let useCurrentPosition: Bool
    let x: Int
    let y: Int

    /// Stop after this many clicks. 0 = unlimited.
    let stopAfterClicks: Int

    /// Stop after this many seconds. 0 = unlimited.
    let stopAfterSeconds: Double

    /// Wait this many seconds of system idle before starting the loop. 0 = disabled.
    let idleWaitSeconds: Double
}
