#pragma once

#include <QString>
#include <chrono>

namespace QuadClicker {

/// Parses user-supplied click rate strings into a millisecond delay.
///
/// Accepted formats:
///   bare integer           → treated as milliseconds
///   "100ms"                → milliseconds
///   "10/s", "10cps"        → clicks per second
///   "10 times per second"  → clicks per second
///   "600/min", "600cpm"    → clicks per minute
///   "600 times per minute" → clicks per minute
///
/// Minimum enforced delay: 1ms.
class ClickRateParser {
public:
    /// Returns true on success; populates \p delay and leaves \p error empty.
    /// Returns false on failure; populates \p error with a human-readable message.
    static bool tryParse(const QString& text,
                         std::chrono::milliseconds& delay,
                         QString& error);

private:
    static bool tryParsePositive(const QString& s, double& value);
};

} // namespace QuadClicker
