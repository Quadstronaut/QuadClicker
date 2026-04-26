#pragma once

#include <QString>
#include <chrono>
#include <initializer_list>

namespace QuadClicker {

/// Parses user-supplied click rate strings into a millisecond delay.
///
/// Accepted formats:
///   bare integer           → treated as milliseconds
///   "100ms"                → milliseconds
///   "5s", "5sec"           → seconds
///   "2m", "2min"           → minutes
///   "10/s", "10cps"        → clicks per second
///   "10 times per second"  → clicks per second
///   "600/min", "600cpm"    → clicks per minute
///   "600 times per minute" → clicks per minute
///   "60/h", "60cph"        → clicks per hour
///   "60 times per hour"    → clicks per hour
///
/// Bounds: 1 ms ≤ delay ≤ 360 minutes.
class ClickRateParser {
public:
    static constexpr double MinDelayMs = 1.0;
    static constexpr double MaxDelayMs = 360.0 * 60'000.0;   // 360 minutes

    /// Returns true on success; populates \p delay and leaves \p error empty.
    /// Returns false on failure; populates \p error with a human-readable message.
    static bool tryParse(const QString& text,
                         std::chrono::milliseconds& delay,
                         QString& error);

private:
    static bool tryParsePositive(const QString& s, double& value);
    static bool buildDelay(double ms, std::chrono::milliseconds& delay, QString& error);
    static bool endsWithAny(const QString& t, std::initializer_list<const char*> suffixes);
    static QString stripFirstSuffix(const QString& t, std::initializer_list<const char*> suffixes);
};

} // namespace QuadClicker
