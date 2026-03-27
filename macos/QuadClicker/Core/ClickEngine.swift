// Core/ClickEngine.swift
// QuadClicker — macOS
//
// Exact port of Windows ClickEngine.cs.
// Runs the click loop on a background thread.
// All callbacks are invoked on the background thread — callers must dispatch to main if needed.

import Foundation
import CoreGraphics

// ── Status enum ───────────────────────────────────────────────────────────────

enum EngineStatus {
    case stopped
    case waitingForIdle
    case clicking
}

// ── Engine ────────────────────────────────────────────────────────────────────

final class ClickEngine {

    // Callbacks (called on background thread)
    var onClickCountUpdated: ((Int) -> Void)?
    var onStatusChanged: ((EngineStatus) -> Void)?

    // ── Public API ────────────────────────────────────────────────────────────

    /// Run the click loop asynchronously. Returns when the loop exits (stop condition or cancellation).
    /// Throws `CancellationError` if cancelled.
    func run(session: ClickSession, cancellationToken: CancellationToken) async throws {
        try await Task.detached(priority: .userInitiated) { [weak self] in
            guard let self else { return }
            try self.loop(session: session, token: cancellationToken)
        }.value
    }

    // ── Private loop ─────────────────────────────────────────────────────────

    private func loop(session: ClickSession, token: CancellationToken) throws {
        var clicks = 0

        // ── Idle wait (once, BEFORE the click loop) ───────────────────────────
        if session.idleWaitSeconds > 0 {
            onStatusChanged?(.waitingForIdle)
            let threshold = session.idleWaitSeconds

            while IdleDetector.getIdleTime() < threshold {
                if token.isCancelled {
                    onStatusChanged?(.stopped)
                    return
                }
                Thread.sleep(forTimeInterval: 0.1)
            }

            if token.isCancelled {
                onStatusChanged?(.stopped)
                return
            }
        }

        onStatusChanged?(.clicking)
        let startTime = Date()

        while !token.isCancelled {
            // ── Stop conditions ───────────────────────────────────────────────
            if session.stopAfterClicks > 0 && clicks >= session.stopAfterClicks { break }
            if session.stopAfterSeconds > 0 && Date().timeIntervalSince(startTime) >= session.stopAfterSeconds { break }

            // ── Position ──────────────────────────────────────────────────────
            if !session.useCurrentPosition {
                // Move cursor to fixed coordinate (CG top-left origin)
                CGWarpMouseCursorPosition(CGPoint(x: session.x, y: session.y))
            }

            // ── Click ─────────────────────────────────────────────────────────
            do {
                try InputInjector.click(
                    button: session.button,
                    clickType: session.clickType,
                    isCancelled: { token.isCancelled }
                )
            } catch {
                // Propagate injection errors up through the Task
                onStatusChanged?(.stopped)
                throw error
            }

            clicks += 1
            onClickCountUpdated?(clicks)

            // ── Delay ─────────────────────────────────────────────────────────
            if session.clickRate >= 0.001 && !token.isCancelled {
                token.sleep(seconds: session.clickRate)
            }
        }

        onStatusChanged?(.stopped)
    }
}

// ── CancellationToken ─────────────────────────────────────────────────────────
// Lightweight cancellation handle. Thread-safe via DispatchSemaphore.

final class CancellationToken {
    private let _lock = NSLock()
    private var _cancelled = false
    private let _semaphore = DispatchSemaphore(value: 0)

    var isCancelled: Bool {
        _lock.lock()
        defer { _lock.unlock() }
        return _cancelled
    }

    func cancel() {
        _lock.lock()
        let wasAlreadyCancelled = _cancelled
        _cancelled = true
        _lock.unlock()
        if !wasAlreadyCancelled {
            // Unblock any sleeping thread
            _semaphore.signal()
        }
    }

    /// Block the calling thread for `seconds`, or return early if cancelled.
    func sleep(seconds: TimeInterval) {
        let deadline = DispatchTime.now() + seconds
        _ = _semaphore.wait(timeout: deadline)
        // After wakeup: either timed out (normal) or signalled (cancelled). Either way, return.
    }
}
