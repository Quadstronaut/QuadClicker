// QuadClickerTests/CliArgumentTests.swift
// QuadClicker — macOS
//
// Port of Windows CliArgumentTests.cs.
// Tests the public surface used by the CLI (ClickRateParser and ClickSession).

import XCTest
@testable import QuadClicker

final class CliArgumentTests: XCTestCase {

    // ── Rate formats parse correctly ──────────────────────────────────────────

    func test_rate_100ms_parsesCorrectly() {
        assertRateSuccess("100ms", expectedSeconds: 0.100)
    }

    func test_rate_10perSecond_parsesCorrectly() {
        assertRateSuccess("10/s", expectedSeconds: 0.100)
    }

    func test_rate_600perMinute_parsesCorrectly() {
        assertRateSuccess("600/min", expectedSeconds: 0.100)
    }

    // ── Missing / empty rate ──────────────────────────────────────────────────

    func test_rate_empty_returnsError() {
        let result = ClickRateParser.parse("")
        switch result {
        case .success:
            XCTFail("Expected failure for empty rate string")
        case .failure(let err):
            XCTAssertFalse(err.isEmpty)
        }
    }

    // ── ClickSession round-trip ───────────────────────────────────────────────

    func test_clickSession_allFields_roundTrip() {
        let session = ClickSession(
            clickRate: 0.200,         // 200 ms
            button: .right,
            clickType: .double_,
            useCurrentPosition: false,
            x: 640,
            y: 480,
            stopAfterClicks: 100,
            stopAfterSeconds: 30,
            idleWaitSeconds: 5
        )

        XCTAssertEqual(session.clickRate, 0.200, accuracy: 0.0001)
        XCTAssertEqual(session.button, .right)
        XCTAssertEqual(session.clickType, .double_)
        XCTAssertFalse(session.useCurrentPosition)
        XCTAssertEqual(session.x, 640)
        XCTAssertEqual(session.y, 480)
        XCTAssertEqual(session.stopAfterClicks, 100)
        XCTAssertEqual(session.stopAfterSeconds, 30, accuracy: 0.001)
        XCTAssertEqual(session.idleWaitSeconds, 5, accuracy: 0.001)
    }

    // ── All MouseButton values are valid ──────────────────────────────────────

    func test_mouseButton_left_isValid() {
        assertButton(.left)
    }

    func test_mouseButton_right_isValid() {
        assertButton(.right)
    }

    func test_mouseButton_middle_isValid() {
        assertButton(.middle)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private func assertRateSuccess(_ input: String, expectedSeconds: Double,
                                    file: StaticString = #file, line: UInt = #line) {
        switch ClickRateParser.parse(input) {
        case .success(let interval):
            XCTAssertEqual(interval, expectedSeconds, accuracy: 0.0001,
                           file: file, line: line)
        case .failure(let err):
            XCTFail("Expected success for '\(input)', got: \(err)", file: file, line: line)
        }
    }

    private func assertButton(_ btn: MouseButton,
                               file: StaticString = #file, line: UInt = #line) {
        let s = ClickSession(
            clickRate: 0.100, button: btn, clickType: .single,
            useCurrentPosition: true, x: 0, y: 0,
            stopAfterClicks: 0, stopAfterSeconds: 0, idleWaitSeconds: 0
        )
        XCTAssertEqual(s.button, btn, file: file, line: line)
    }
}
