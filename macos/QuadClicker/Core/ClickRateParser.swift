// Core/ClickRateParser.swift
// QuadClicker — macOS

import Foundation

enum ClickRateParser {
    static let minDelayMs: Double = 1.0
    static let maxDelayMs: Double = 360.0 * 60_000.0   // 360 minutes

    /// Parse `text` into a delay TimeInterval (seconds).
    static func parse(_ text: String) -> Result<TimeInterval, String> {
        let t = text.trimmingCharacters(in: .whitespaces).lowercased()

        if t.isEmpty {
            return .failure("Click rate is required.")
        }

        // ── Milliseconds: "100ms" ─────────────────────────────────────────────
        if t.hasSuffix("ms") {
            let num = String(t.dropLast(2)).trimmingCharacters(in: .whitespaces)
            if let ms = parsePositive(num) {
                return buildDelay(ms)
            }
            return .failure("Millisecond value must be a positive number.")
        }

        // ── Minutes: "2m", "2min", "2minutes" ─────────────────────────────────
        if endsWithAny(t, ["minutes", "minute", "mins", "min", "m"])
           && !t.contains("per minute")
           && !t.hasSuffix("/min")
           && !t.hasSuffix("cpm")
        {
            let num = stripFirstSuffix(t, ["minutes", "minute", "mins", "min", "m"])
            if let mins = parsePositive(num) {
                return buildDelay(mins * 60_000.0)
            }
            return .failure("Minutes value must be a positive number.")
        }

        // ── Seconds: "5s", "5sec", "5seconds" ─────────────────────────────────
        if endsWithAny(t, ["seconds", "second", "secs", "sec", "s"])
           && !t.contains("per second")
           && !t.hasSuffix("/s")
           && !t.hasSuffix("cps")
        {
            let num = stripFirstSuffix(t, ["seconds", "second", "secs", "sec", "s"])
            if let secs = parsePositive(num) {
                return buildDelay(secs * 1000.0)
            }
            return .failure("Seconds value must be a positive number.")
        }

        // ── Clicks/second: "10/s", "10cps", "10 times per second" ────────────
        if t.hasSuffix("/s") || t.hasSuffix("cps") || t.contains("times per second") {
            let num = t
                .replacingOccurrences(of: "times per second", with: "")
                .replacingOccurrences(of: "/s", with: "")
                .replacingOccurrences(of: "cps", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tps = parsePositive(num) {
                return buildDelay(1000.0 / tps)
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
                return buildDelay(60_000.0 / tpm)
            }
            return .failure("Clicks-per-minute value must be a positive number.")
        }

        // ── Clicks/hour: "60/h", "60cph", "60 times per hour" ────────────────
        if t.hasSuffix("/h") || t.hasSuffix("cph") || t.contains("times per hour") {
            let num = t
                .replacingOccurrences(of: "times per hour", with: "")
                .replacingOccurrences(of: "/h", with: "")
                .replacingOccurrences(of: "cph", with: "")
                .trimmingCharacters(in: .whitespaces)
            if let tph = parsePositive(num) {
                return buildDelay(3_600_000.0 / tph)
            }
            return .failure("Clicks-per-hour value must be a positive number.")
        }

        // ── Bare integer/decimal → milliseconds ───────────────────────────────
        if let bare = parsePositive(t) {
            return buildDelay(bare)
        }

        return .failure("Invalid format. Examples: 100ms  |  5s  |  10/s  |  600/min  |  60/h")
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static func buildDelay(_ ms: Double) -> Result<TimeInterval, String> {
        if !ms.isFinite { return .failure("Rate is not a finite number.") }
        if ms < minDelayMs {
            return .failure("Rate exceeds maximum — minimum delay is 1 ms (1000 clicks/sec).")
        }
        if ms > maxDelayMs {
            return .failure("Delay exceeds maximum of 360 minutes.")
        }
        return .success(ms / 1000.0)
    }

    private static func parsePositive(_ s: String) -> Double? {
        let trimmed = s.trimmingCharacters(in: .whitespaces)
        guard let v = Double(trimmed), v > 0 else { return nil }
        return v
    }

    private static func endsWithAny(_ text: String, _ suffixes: [String]) -> Bool {
        for s in suffixes where text.hasSuffix(s) { return true }
        return false
    }

    private static func stripFirstSuffix(_ text: String, _ suffixes: [String]) -> String {
        for s in suffixes where text.hasSuffix(s) {
            return String(text.dropLast(s.count)).trimmingCharacters(in: .whitespaces)
        }
        return text.trimmingCharacters(in: .whitespaces)
    }
}
