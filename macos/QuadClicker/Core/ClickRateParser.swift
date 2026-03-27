// Core/ClickRateParser.swift
// QuadClicker — macOS
//
// Exact port of Windows ClickRateParser.cs.
// Parses user-supplied click rate strings into a TimeInterval (seconds) delay.
//
// Accepted formats:
//   bare integer/decimal  → milliseconds (e.g. "100" → 100 ms)
//   "100ms"               → milliseconds
//   "10/s"                → clicks per second
//   "10cps"               → clicks per second
//   "10 times per second" → clicks per second
//   "600/min"             → clicks per minute
//   "600cpm"              → clicks per minute
//   "600 times per minute"→ clicks per minute
//
// Minimum delay: 1 ms (0.001 seconds). All inputs below this are rejected.

import Foundation

enum ClickRateParser {

    /// Parse `text` into a delay TimeInterval (seconds).
    /// Returns `.success(interval)` on success, `.failure(message)` on error.
    static func parse(_ text: String) -> Result<TimeInterval, String> {
        let t = text.trimmingCharacters(in: .whitespaces).lowercased()

        if t.isEmpty {
            return .failure("Click rate is required.")
        }

        // ── Milliseconds: "100ms" ─────────────────────────────────────────────
        if t.hasSuffix("ms") {
            let num = String(t.dropLast(2)).trimmingCharacters(in: .whitespaces)
            if let ms = parsePositive(num), ms >= 1 {
                return .success(ms / 1000.0)
            }
            return .failure("Millisecond value must be a number ≥ 1.")
        }

        // ── Clicks/second: "10/s", "10cps", "10 times per second" ────────────
        if t.hasSuffix("/s") || t.hasSuffix("cps") || t.contains("times per second") {
            let num = t
                .replacingOccurrences(of: "times per second", with: "")
                .replacingOccurrences(of: "/s", with: "")
                .replacingOccurrences(of: "cps", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tps = parsePositive(num) {
                let ms = 1000.0 / tps
                if ms < 1 { return .failure("Rate exceeds maximum — minimum delay is 1ms.") }
                return .success(ms / 1000.0)
            }
            return .failure("Clicks-per-second value must be a positive number.")
        }

        // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────
        if t.hasSuffix("/min") || t.hasSuffix("cpm") || t.contains("times per minute") {
            let num = t
                .replacingOccurrences(of: "times per minute", with: "")
                .replacingOccurrences(of: "/min", with: "")
                .replacingOccurrences(of: "cpm", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tpm = parsePositive(num) {
                let ms = 60000.0 / tpm
                if ms < 1 { return .failure("Rate exceeds maximum — minimum delay is 1ms.") }
                return .success(ms / 1000.0)
            }
            return .failure("Clicks-per-minute value must be a positive number.")
        }

        // ── Bare integer/decimal → milliseconds ───────────────────────────────
        if let bare = parsePositive(t), bare >= 1 {
            return .success(bare / 1000.0)
        }

        return .failure("Invalid format. Examples: 100ms  |  10/s  |  600/min")
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static func parsePositive(_ s: String) -> Double? {
        guard let v = Double(s), v > 0 else { return nil }
        return v
    }
}
