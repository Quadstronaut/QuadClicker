#pragma once

#include <algorithm>
#include <chrono>

/// Pure timing rules for the click loop (GUI engine and CLI). No sleeping, no
/// input, no clock reads: callers pass in steady_clock times, which keeps every
/// rule unit-testable.
namespace QuadClicker::ClickTiming {

using Clock = std::chrono::steady_clock;

/// Pause between the two clicks of a double click. The system double-click time
/// is the MAXIMUM gap that still counts as a double click (GTK and Qt default to
/// 400 ms), so waiting 500 ms produced two single clicks. Use a quarter of it,
/// capped at 50 ms, never 0.
inline int doubleClickGapMs(int systemDoubleClickMs)
{
    return std::clamp(systemDoubleClickMs / 4, 1, 50);
}

/// True once either stop condition is met. Zero means "no limit".
inline bool limitReached(int clicks, int stopAfterClicks,
                         double elapsedSeconds, double stopAfterSeconds)
{
    return (stopAfterClicks  > 0   && clicks >= stopAfterClicks) ||
           (stopAfterSeconds > 0.0 && elapsedSeconds >= stopAfterSeconds);
}

/// When to wake for the next click: one period from now, but never later than
/// the stop-after-seconds limit (so a 5 min period cannot overrun a 10 s limit).
inline Clock::time_point nextWake(Clock::time_point now, Clock::duration period,
                                  Clock::time_point start, double stopAfterSeconds)
{
    if (stopAfterSeconds > 0.0) {
        // Compare in double seconds: a huge limit would overflow Clock::duration.
        double remaining = stopAfterSeconds - std::chrono::duration<double>(now - start).count();
        if (remaining < std::chrono::duration<double>(period).count()) {
            return now + std::chrono::duration_cast<Clock::duration>(
                             std::chrono::duration<double>(std::max(remaining, 0.0)));
        }
    }
    return now + period;
}

} // namespace QuadClicker::ClickTiming
