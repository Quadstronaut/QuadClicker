// QuadClickerTests/LiveInputTests.swift
// QuadClicker — macOS
//
// QCLK-8: proofs for the macOS review findings that need REAL input events.
// Runs on the macos-26 CI runner, which grants event posting and listening.
// Each test is named after its finding. Events are posted through the app's own
// code and observed with a listen-only CGEvent tap.

import XCTest
import CoreGraphics
import AppKit
@testable import QuadClicker

// ── Event tap helper ──────────────────────────────────────────────────────────

/// Listen-only tap that records every left mouse down/up with its click state.
/// Lives on the main run loop, so tests pump it with `pump(_:)`.
private final class MouseTap {
    private let lock = NSLock()
    private var _downs: [Int64] = []
    private var _ups: [Int64] = []
    private var port: CFMachPort?
    private var source: CFRunLoopSource?

    var downs: [Int64] { lock.lock(); defer { lock.unlock() }; return _downs }
    var ups: [Int64]   { lock.lock(); defer { lock.unlock() }; return _ups }

    func record(down: Bool, state: Int64) {
        lock.lock(); defer { lock.unlock() }
        if down { _downs.append(state) } else { _ups.append(state) }
    }

    init?() {
        let mask: CGEventMask = (1 << CGEventType.leftMouseDown.rawValue)
                              | (1 << CGEventType.leftMouseUp.rawValue)
        let callback: CGEventTapCallBack = { _, type, event, ref in
            let tap = Unmanaged<MouseTap>.fromOpaque(ref!).takeUnretainedValue()
            tap.record(down: type == .leftMouseDown,
                       state: event.getIntegerValueField(.mouseEventClickState))
            return Unmanaged.passUnretained(event)
        }
        guard let p = CGEvent.tapCreate(
            tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly,
            eventsOfInterest: mask, callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else { return nil }
        port = p
        source = CFMachPortCreateRunLoopSource(nil, p, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: p, enable: true)
    }

    func close() {
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let port { CGEvent.tapEnable(tap: port, enable: false) }
    }
}

/// Diagnostics go to a file the CI job prints afterwards (xcodebuild drops test stdout).
private func diag(_ line: String) {
    let url = URL(fileURLWithPath: "/tmp/qclk-diag.txt")
    let data = (line + "
").data(using: .utf8)!
    if let h = try? FileHandle(forWritingTo: url) { h.seekToEndOfFile(); h.write(data); h.closeFile() }
    else { try? data.write(to: url) }
}

/// Run the main run loop so tap callbacks and main-actor hops are delivered.
private func pump(_ seconds: TimeInterval) {
    RunLoop.main.run(until: Date(timeIntervalSinceNow: seconds))
}

private func parkCursor() {
    CGWarpMouseCursorPosition(CGPoint(x: 600, y: 400))
}

// ── Tests ─────────────────────────────────────────────────────────────────────

final class LiveInputTests: XCTestCase {

    private var tap: MouseTap!

    override func setUpWithError() throws {
        tap = try XCTUnwrap(MouseTap(), "event tap could not be created: runner cannot observe input")
        diag("setUp AX=\(AXIsProcessTrusted()) listen=\(CGPreflightListenEventAccess()) post=\(CGPreflightPostEventAccess()) bundle=\(Bundle.main.bundleIdentifier ?? "?") exe=\(Bundle.main.executablePath ?? "?")")
        parkCursor()
        pump(0.2)
    }

    override func tearDown() {
        tap.close()
    }

    // F3: the second click of a double click carries click state 2.
    func test_F3_doubleClick_secondDownHasClickState2() throws {
        let done = expectation(description: "click returned")
        DispatchQueue.global().async {
            try? InputInjector.click(button: .left, clickType: .double_)
            done.fulfill()
        }
        wait(for: [done], timeout: 5)
        pump(0.5)
        XCTAssertEqual(tap.downs, [1, 2], "mouse-down click states of one double click")
        XCTAssertEqual(tap.ups, [1, 2], "mouse-up click states of one double click")
    }

    // F1: Stop then Start inside the old run's wind-down (double mode).
    func test_F1_stopThenQuickStart_newRunSurvivesOldRunCleanup() {
        let runner = ClickRunner(engine: ClickEngine())
        var endedReports = 0
        runner.onRunEnded = { endedReports += 1 }
        let session = ClickSession(
            clickRate: 0.02, button: .left, clickType: .double_,
            useCurrentPosition: true, x: 0, y: 0,
            stopAfterClicks: 0, stopAfterSeconds: 0, idleWaitSeconds: 0
        )

        runner.start(session: session)
        pump(0.3)
        XCTAssertGreaterThan(tap.downs.count, 0, "first run clicks")

        runner.stop()
        runner.start(session: session)          // well inside 0.5 s of the stop
        pump(0.4)                                // old run's cleanup lands in here
        let afterCleanup = tap.downs.count
        pump(0.4)

        XCTAssertEqual(endedReports, 0, "old run's cleanup must not report the new run as ended")
        XCTAssertTrue(runner.isRunning, "new run must still be the current run")
        XCTAssertGreaterThan(tap.downs.count, afterCleanup, "new run keeps clicking")

        runner.stop()                            // new run must be cancellable
        pump(0.4)                                // let an in-flight click drain
        let settled = tap.downs.count
        pump(0.6)
        XCTAssertEqual(tap.downs.count, settled, "clicks cease after the stop")
        XCTAssertFalse(runner.isRunning)
    }

    // F14: idle time is measured against ANY input event, so a posted click resets it.
    func test_F14_idleResetsAfterAnyInputEvent() {
        let down = CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown,
                           mouseCursorPosition: CGPoint(x: 600, y: 400), mouseButton: .left)!
        let up = CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp,
                         mouseCursorPosition: CGPoint(x: 600, y: 400), mouseButton: .left)!
        down.post(tap: .cghidEventTap)
        up.post(tap: .cghidEventTap)
        pump(0.2)
        let idle = IdleDetector.getIdleTime()
        let nullIdle = CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: .null)
        diag("F14 idle(any)=\(idle) idle(null)=\(nullIdle)")
        XCTAssertLessThan(idle, 1.0, "idle time must reset after an input event")
    }

    // F16 (partial): registration works when trusted; no silent failure when not.
    func test_F16_hotkeyRegistrationMatchesAccessibilityTrust() {
        let manager = HotkeyManager()
        let token = manager.register(spec: HotkeySpec(key: "F10", modifierFlags: [])) {}
        diag("F16 AXIsProcessTrusted=\(AXIsProcessTrusted()) registered=\(token != nil)")
        XCTAssertEqual(token != nil, AXIsProcessTrusted())
        if let token { manager.unregister(token) }
    }
}

// ── Tests that need no event posting ──────────────────────────────────────────

final class FixDecisionTests: XCTestCase {

    // F4: Y flip uses the PRIMARY screen (frames[0]) height, never another screen's.
    func test_F4_flipUsesPrimaryScreenHeight() {
        let primary   = CGRect(x: 0,    y: 0,    width: 1920, height: 1080)
        let secondary = CGRect(x: 1920, y: 0, width: 2560, height: 1440)
        let p = InputInjector.cgPoint(fromCocoa: NSPoint(x: 100, y: 200),
                                      screenFrames: [primary, secondary])
        XCTAssertEqual(p.x, 100)
        XCTAssertEqual(p.y, 1080 - 200, "must flip against the primary height (1080), not 1440")
        // Same point with the screens listed the other way round flips against 1440.
        let q = InputInjector.cgPoint(fromCocoa: NSPoint(x: 100, y: 200),
                                      screenFrames: [secondary, primary])
        XCTAssertEqual(q.y, 1440 - 200)
    }

    // F15: quitting saves the CURRENT settings, not the startup snapshot.
    func test_F15_quitSavesChangedSettings() throws {
        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("qclk8-\(UUID().uuidString)/settings.json")
        AppSettings.settingsURLOverride = tmp
        let shared = AppState.shared.settings
        let original = shared.clickRateValue
        defer {
            AppSettings.settingsURLOverride = nil
            shared.clickRateValue = original
            try? FileManager.default.removeItem(at: tmp.deletingLastPathComponent())
        }

        let delegate = AppDelegate()          // starts with a startup snapshot
        delegate.adoptSharedSettings()        // what QuadClickerApp does at launch
        shared.clickRateValue = "777"         // the user edits settings in the UI
        delegate.applicationWillTerminate(Notification(name: NSApplication.willTerminateNotification))

        XCTAssertEqual(AppSettings.load().clickRateValue, "777")
    }

    // F16: the missing-permission notice shows exactly when hotkeys silently fail.
    func test_F16_accessibilityNoticeDecision() {
        XCTAssertTrue(HotkeyManager.needsAccessibilityNotice(wanted: true, anyRegistered: false, trusted: false))
        XCTAssertFalse(HotkeyManager.needsAccessibilityNotice(wanted: true, anyRegistered: false, trusted: true))
        XCTAssertFalse(HotkeyManager.needsAccessibilityNotice(wanted: true, anyRegistered: true, trusted: false))
        XCTAssertFalse(HotkeyManager.needsAccessibilityNotice(wanted: false, anyRegistered: false, trusted: false))
    }
}
