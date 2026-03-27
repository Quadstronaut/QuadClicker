// QuadClickerTests/ClickRateParserTests.swift
// QuadClicker — macOS
//
// Port of Windows ClickRateParserTests.cs.
// Uses Swift Testing framework (available Xcode 16 / Swift 5.10+).
// Falls back to XCTest-compatible structure for broader compatibility.

import XCTest
@testable import QuadClicker

final class ClickRateParserTests: XCTestCase {

    // ── Millisecond inputs ────────────────────────────────────────────────────

    func test_milliseconds_100ms() {
        assertSuccess("100ms", expectedSeconds: 0.100)
    }

    func test_milliseconds_100ms_withSpace() {
        assertSuccess("100 ms", expectedSeconds: 0.100)
    }

    func test_milliseconds_1ms() {
        assertSuccess("1ms", expectedSeconds: 0.001)
    }

    func test_milliseconds_250ms() {
        assertSuccess("250ms", expectedSeconds: 0.250)
    }

    func test_milliseconds_1000ms() {
        assertSuccess("1000ms", expectedSeconds: 1.000)
    }

    func test_milliseconds_pointFive_fails() {
        // 0.5ms < 1ms minimum — must fail
        assertFailure("0.5ms")
    }

    // ── Clicks per second ─────────────────────────────────────────────────────

    func test_clicksPerSecond_10() {
        assertSuccess("10/s", expectedSeconds: 0.100)
    }

    func test_clicksPerSecond_1() {
        assertSuccess("1/s", expectedSeconds: 1.000)
    }

    func test_clicksPerSecond_100() {
        assertSuccess("100/s", expectedSeconds: 0.010)
    }

    func test_timesPerSecond() {
        assertSuccess("10 times per second", expectedSeconds: 0.100)
    }

    func test_cps() {
        assertSuccess("10cps", expectedSeconds: 0.100)
    }

    // ── Clicks per minute ─────────────────────────────────────────────────────

    func test_clicksPerMinute_600() {
        assertSuccess("600/min", expectedSeconds: 0.100)
    }

    func test_clicksPerMinute_60() {
        assertSuccess("60/min", expectedSeconds: 1.000)
    }

    func test_timesPerMinute() {
        assertSuccess("600 times per minute", expectedSeconds: 0.100)
    }

    func test_cpm() {
        assertSuccess("600cpm", expectedSeconds: 0.100)
    }

    // ── Bare integer / decimal → milliseconds ─────────────────────────────────

    func test_bareInteger_100() {
        assertSuccess("100", expectedSeconds: 0.100)
    }

    func test_bareInteger_1() {
        assertSuccess("1", expectedSeconds: 0.001)
    }

    func test_bareInteger_500() {
        assertSuccess("500", expectedSeconds: 0.500)
    }

    // ── Invalid inputs ────────────────────────────────────────────────────────

    func test_empty_fails() {
        assertFailure("")
    }

    func test_whitespaceOnly_fails() {
        assertFailure("   ")
    }

    func test_alphabetic_fails() {
        assertFailure("abc")
    }

    func test_zeroMs_fails() {
        assertFailure("0ms")
    }

    func test_negativeMs_fails() {
        assertFailure("-1ms")
    }

    func test_zeroPerSecond_fails() {
        assertFailure("0/s")
    }

    func test_fooPerSecond_fails() {
        assertFailure("foo/s")
    }

    func test_exceedingMaxRate_fails() {
        // 2000/s = 0.5 ms delay < 1 ms minimum
        assertFailure("2000/s")
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private func assertSuccess(_ input: String, expectedSeconds: Double,
                                file: StaticString = #file, line: UInt = #line) {
        let result = ClickRateParser.parse(input)
        switch result {
        case .success(let interval):
            XCTAssertEqual(interval, expectedSeconds, accuracy: 0.0001,
                           "Input '\(input)' → \(interval)s, expected \(expectedSeconds)s",
                           file: file, line: line)
        case .failure(let err):
            XCTFail("Expected success for '\(input)', got error: \(err)", file: file, line: line)
        }
    }

    private func assertFailure(_ input: String,
                                file: StaticString = #file, line: UInt = #line) {
        let result = ClickRateParser.parse(input)
        switch result {
        case .success(let interval):
            XCTFail("Expected failure for '\(input)', got \(interval)s", file: file, line: line)
        case .failure(let err):
            XCTAssertFalse(err.isEmpty, "Error message must not be empty for '\(input)'",
                           file: file, line: line)
        }
    }
}
