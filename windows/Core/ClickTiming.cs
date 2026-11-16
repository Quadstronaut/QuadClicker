namespace QuadClicker.Core;

/// <summary>
/// Pure timing decisions for the click loop. No clocks, no sleeping, no input:
/// the engine feeds in monotonic elapsed times, which keeps every rule unit-testable.
/// </summary>
public static class ClickTiming
{
    /// <summary>Upper bound for the pause between the two halves of a double click.</summary>
    public const int DoubleClickGapCapMs = 50;

    /// <summary>
    /// Pause between the two clicks of a double click. The OS double-click time is
    /// the MAXIMUM gap that still counts as a double click, so waiting that long
    /// lands on the edge of the window. Use a quarter of it, capped at 50 ms, never 0.
    /// </summary>
    public static int DoubleClickGapMs(uint systemDoubleClickTimeMs) =>
        (int)Math.Clamp(systemDoubleClickTimeMs / 4, 1u, (uint)DoubleClickGapCapMs);

    /// <summary>True once either stop condition is met. Zero means "no limit".</summary>
    public static bool LimitReached(int clicks, int stopAfterClicks, TimeSpan elapsed, double stopAfterSeconds) =>
        (stopAfterClicks  > 0 && clicks >= stopAfterClicks) ||
        (stopAfterSeconds > 0 && elapsed.TotalSeconds >= stopAfterSeconds);

    /// <summary>
    /// Deadline for the next click. Clicks are scheduled on a fixed grid
    /// (previous deadline + period), so time spent injecting the click and
    /// sub-millisecond rounding do not add up into a slower rate. If the loop
    /// fell more than a whole period behind (system stall), the grid restarts
    /// from now instead of firing a burst of catch-up clicks.
    /// </summary>
    public static TimeSpan NextDeadline(TimeSpan previousDeadline, TimeSpan period, TimeSpan now)
    {
        var next = previousDeadline + period;
        return now - next > period ? now : next;
    }

    /// <summary>
    /// Milliseconds to wait from <paramref name="now"/>: until the next deadline,
    /// but never past the stop-after-seconds limit. Rounded UP so the wait never
    /// ends before its target; 0 means "go now".
    /// </summary>
    public static int WaitMs(TimeSpan now, TimeSpan nextDeadline, double stopAfterSeconds)
    {
        // Work in double milliseconds: TimeSpan.FromSeconds would overflow on huge limits.
        double untilMs = nextDeadline.TotalMilliseconds;
        if (stopAfterSeconds > 0)
            untilMs = Math.Min(untilMs, stopAfterSeconds * 1000.0);
        double ms = untilMs - now.TotalMilliseconds;
        if (ms <= 0) return 0;
        return (int)Math.Min(Math.Ceiling(ms), int.MaxValue);
    }
}
