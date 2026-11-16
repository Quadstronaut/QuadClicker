using QuadClicker.Core;
using Xunit;

namespace QuadClicker.Tests;

/// <summary>Pure timing rules used by ClickEngine. No sleeping, no input.</summary>
public sealed class ClickTimingTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    // ── Double-click gap (F2) ─────────────────────────────────────────────────

    [Theory]
    [InlineData(500u, 50)]   // Windows default: well inside the 500 ms window
    [InlineData(900u, 50)]   // capped
    [InlineData(200u, 50)]   // 200/4 = 50
    [InlineData(100u, 25)]   // short system setting: a quarter of it
    [InlineData(2u,    1)]   // never 0
    [InlineData(0u,    1)]
    public void DoubleClickGap_IsAFractionOfTheSystemWindow(uint systemMs, int expected)
    {
        int gap = ClickTiming.DoubleClickGapMs(systemMs);
        Assert.Equal(expected, gap);
        if (systemMs >= 4) Assert.True(gap < systemMs);
    }

    // ── Stop conditions (F8) ──────────────────────────────────────────────────

    [Fact]
    public void LimitReached_ClickCount_TrueRightAfterLastClick()
    {
        Assert.False(ClickTiming.LimitReached(0, 1, TimeSpan.Zero, 0));
        Assert.True (ClickTiming.LimitReached(1, 1, TimeSpan.Zero, 0));
    }

    [Fact]
    public void LimitReached_Seconds()
    {
        Assert.False(ClickTiming.LimitReached(5, 0, Ms(999),  1));
        Assert.True (ClickTiming.LimitReached(5, 0, Ms(1000), 1));
    }

    [Fact]
    public void LimitReached_ZeroMeansUnlimited()
    {
        Assert.False(ClickTiming.LimitReached(int.MaxValue, 0, TimeSpan.FromDays(10), 0));
    }

    [Fact]
    public void WaitMs_IsCappedByStopAfterSeconds()
    {
        // --rate 5min --stop-after-seconds 10, first click at t=0:
        // the old loop slept 300 s; now it wakes at the 10 s limit.
        var deadline = ClickTiming.NextDeadline(TimeSpan.Zero, TimeSpan.FromMinutes(5), Ms(1));
        Assert.Equal(9_999, ClickTiming.WaitMs(Ms(1), deadline, 10));
    }

    [Fact]
    public void WaitMs_NoLimit_WaitsUntilDeadline()
    {
        Assert.Equal(300_000, ClickTiming.WaitMs(TimeSpan.Zero, TimeSpan.FromMinutes(5), 0));
    }

    [Fact]
    public void WaitMs_PastDeadline_IsZero()
    {
        Assert.Equal(0, ClickTiming.WaitMs(Ms(12), Ms(10), 0));
        Assert.Equal(0, ClickTiming.WaitMs(Ms(5_000), Ms(9_000), 4)); // limit already passed
    }

    [Fact]
    public void WaitMs_HugeLimitDoesNotOverflow()
    {
        Assert.Equal(100, ClickTiming.WaitMs(TimeSpan.Zero, Ms(100), 1e300));
    }

    // ── Rate accuracy (F9) ────────────────────────────────────────────────────

    [Fact]
    public void WaitMs_RoundsUp_NeverEndsEarly()
    {
        // 600/s = 1.667 ms. The old loop waited (int)1.667 = 1 ms.
        Assert.Equal(2, ClickTiming.WaitMs(TimeSpan.Zero, Ms(1000.0 / 600), 0));
    }

    [Fact]
    public void Deadlines_StayOnGrid_SoClickWorkDoesNotAccumulate()
    {
        // Simulate 600 clicks at 600/s where every click + wake-up overshoots by 0.4 ms.
        // The deadline grid absorbs the overshoot instead of adding it to each period.
        var period = Ms(1000.0 / 600);
        var deadline = TimeSpan.Zero;
        var now = TimeSpan.Zero;
        for (int i = 0; i < 600; i++)
        {
            now += Ms(0.4);                                   // click work
            deadline = ClickTiming.NextDeadline(deadline, period, now);
            now += Ms(ClickTiming.WaitMs(now, deadline, 0));  // wake no earlier than asked
            if (now < deadline) now = deadline;
        }
        // 600 periods = 1000 ms. Within one period plus the 1 ms rounding of the last wait.
        Assert.InRange(now.TotalMilliseconds, 1000 - 2, 1000 + 2);
    }

    [Fact]
    public void NextDeadline_AfterStall_ResyncsInsteadOfBursting()
    {
        var period = Ms(100);
        // Loop stalled: at t=1000 ms the previous deadline was t=100 ms.
        var next = ClickTiming.NextDeadline(Ms(100), period, Ms(1000));
        Assert.Equal(Ms(1000), next); // one click now, not nine catch-up clicks
    }

    [Fact]
    public void NextDeadline_SmallLateness_KeepsGrid()
    {
        var next = ClickTiming.NextDeadline(Ms(100), Ms(100), Ms(150));
        Assert.Equal(Ms(200), next);
    }
}
