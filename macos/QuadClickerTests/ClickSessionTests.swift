// QuadClickerTests/ClickSessionTests.swift
// QuadClicker — macOS
//
// Port of Windows ClickSessionTests.cs.

import XCTest
@testable import QuadClicker

final class ClickSessionTests: XCTestCase {

    // ── Helpers ───────────────────────────────────────────────────────────────

    private func defaultSession() -> ClickSession {
        ClickSession(
            clickRate: 0.100,            // 100 ms
            button: .left,
            clickType: .single,
            useCurrentPosition: true,
            x: 0,
            y: 0,
            stopAfterClicks: 0,
            stopAfterSeconds: 0,
            idleWaitSeconds: 0
        )
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    func test_defaultSession_isValid() {
        let s = defaultSession()
        XCTAssertEqual(s.clickRate, 0.100, accuracy: 0.0001)
        XCTAssertEqual(s.button, .left)
        XCTAssertEqual(s.clickType, .single)
        XCTAssertTrue(s.useCurrentPosition)
        XCTAssertEqual(s.stopAfterClicks, 0)
    }

    func test_session_isValueType_copiesIndependently() {
        // Swift structs are value types — modifying a copy does not affect the original
        let s1 = defaultSession()
        var s2 = s1
        // Simulate "with" by creating a new instance from s2's values but with button changed
        s2 = ClickSession(
            clickRate: s1.clickRate,
            button: .right,
            clickType: s1.clickType,
            useCurrentPosition: s1.useCurrentPosition,
            x: s1.x, y: s1.y,
            stopAfterClicks: s1.stopAfterClicks,
            stopAfterSeconds: s1.stopAfterSeconds,
            idleWaitSeconds: s1.idleWaitSeconds
        )
        XCTAssertEqual(s1.button, .left)
        XCTAssertEqual(s2.button, .right)
    }

    func test_session_allButtonsStoreCorrectly() {
        for btn in MouseButton.allCases {
            let s = ClickSession(
                clickRate: 0.100, button: btn, clickType: .single,
                useCurrentPosition: true, x: 0, y: 0,
                stopAfterClicks: 0, stopAfterSeconds: 0, idleWaitSeconds: 0
            )
            XCTAssertEqual(s.button, btn)
        }
    }

    func test_session_doubleClick_storesCorrectly() {
        let s = ClickSession(
            clickRate: 0.100, button: .left, clickType: .double_,
            useCurrentPosition: true, x: 0, y: 0,
            stopAfterClicks: 0, stopAfterSeconds: 0, idleWaitSeconds: 0
        )
        XCTAssertEqual(s.clickType, .double_)
    }

    func test_session_fixedPosition_storesCoordinates() {
        let s = ClickSession(
            clickRate: 0.100, button: .left, clickType: .single,
            useCurrentPosition: false, x: 500, y: 300,
            stopAfterClicks: 0, stopAfterSeconds: 0, idleWaitSeconds: 0
        )
        XCTAssertFalse(s.useCurrentPosition)
        XCTAssertEqual(s.x, 500)
        XCTAssertEqual(s.y, 300)
    }

    func test_session_stopConditions_storeCorrectly() {
        let s = ClickSession(
            clickRate: 0.100, button: .left, clickType: .single,
            useCurrentPosition: true, x: 0, y: 0,
            stopAfterClicks: 200, stopAfterSeconds: 60, idleWaitSeconds: 10
        )
        XCTAssertEqual(s.stopAfterClicks, 200)
        XCTAssertEqual(s.stopAfterSeconds, 60, accuracy: 0.001)
        XCTAssertEqual(s.idleWaitSeconds, 10, accuracy: 0.001)
    }
}
