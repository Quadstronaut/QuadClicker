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
        // Monotonic (uptime) clock: wall-clock changes cannot stretch or cut the limit.
        let startTime = ProcessInfo.processInfo.systemUptime
        func elapsed() -> TimeInterval { ProcessInfo.processInfo.systemUptime - startTime }
        func limitReached() -> Bool {
            (session.stopAfterClicks > 0 && clicks >= session.stopAfterClicks) ||
            (session.stopAfterSeconds > 0 && elapsed() >= session.stopAfterSeconds)
        }

        while !token.isCancelled {
            // ── Stop conditions ───────────────────────────────────────────────
            if limitReached() { break }

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

            // Finish right after the last allowed click, not one full period later.
            if limitReached() { break }

            // ── Delay ─────────────────────────────────────────────────────────
            // One period, but never past the stop-after-seconds limit.
            if session.clickRate >= 0.001 && !token.isCancelled {
                var wait = session.clickRate
                if session.stopAfterSeconds > 0 {
                    wait = min(wait, max(0, session.stopAfterSeconds - elapsed()))
                }
                token.sleep(seconds: wait)
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

// ── ClickRunner ───────────────────────────────────────────────────────────────
// Owns "which run is current" (extracted from ContentView so it can be tested).
// Stop -> Start inside the old run's wind-down must not let the OLD run's cleanup
// touch the NEW run: only the current run reports its end, and every run has its
// own token so the new one stays cancellable.

final class ClickRunner {
    let engine: ClickEngine
    private let lock = NSLock()
    private var current: CancellationToken?

    /// Called on the main actor when the CURRENT run ends by itself (limit hit, error).
    /// Not called for runs stopped through `stop()` or superseded by a newer run.
    var onRunEnded: (@MainActor () -> Void)?
    /// Called on the main actor when the CURRENT run fails with an error.
    var onError: (@MainActor (Error) -> Void)?

    init(engine: ClickEngine) { self.engine = engine }

    var isRunning: Bool {
        lock.lock(); defer { lock.unlock() }
        return current != nil
    }

    func start(session: ClickSession) {
        let token = CancellationToken()
        lock.lock(); current = token; lock.unlock()

        Task {
            var failure: Error?
            do {
                try await engine.run(session: session, cancellationToken: token)
            } catch is CancellationError {
                // normal stop
            } catch {
                failure = error
            }
            // Only the run that is still current may report back. After Stop -> Start,
            // the older run finishes late; reporting here used to mark the NEW run
            // stopped while it kept clicking.
            await MainActor.run {
                guard self.finish(token) else { return }
                if let failure { self.onError?(failure) }
                self.onRunEnded?()
            }
        }
    }

    func stop() {
        lock.lock()
        let token = current
        current = nil
        lock.unlock()
        token?.cancel()
    }

    /// True (and clears the slot) only if `token` is still the current run.
    private func finish(_ token: CancellationToken) -> Bool {
        lock.lock(); defer { lock.unlock() }
        
        current = nil
        return true
    }
}
