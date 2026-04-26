using QuadClicker.Core;
using Xunit;

namespace QuadClicker.Tests;

public sealed class ClickRateParserTests
{
    // ── Valid inputs ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("100ms",   100)]
    [InlineData("100 ms",  100)]
    [InlineData("1ms",     1)]
    [InlineData("250ms",   250)]
    [InlineData("1000ms",  1000)]
    [InlineData("0.5ms",   0)]  // rounds down to 0 — actually this is < 1, should fail
    public void Milliseconds_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        // 0.5ms is < 1 so it should fail — handled by the conditional below
        if (expectedMs <= 0)
        {
            Assert.False(ClickRateParser.TryParse(input, out _, out _));
            return;
        }
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    [Theory]
    [InlineData("10/s",               100)]
    [InlineData("1/s",                1000)]
    [InlineData("100/s",              10)]
    [InlineData("10 times per second", 100)]
    [InlineData("10cps",              100)]
    public void ClicksPerSecond_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    [Theory]
    [InlineData("600/min",                100)]
    [InlineData("60/min",                 1000)]
    [InlineData("600 times per minute",   100)]
    [InlineData("600cpm",                 100)]
    public void ClicksPerMinute_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    [Theory]
    [InlineData("100",   100)]
    [InlineData("1",     1)]
    [InlineData("500",   500)]
    public void BareInteger_TreatedAsMilliseconds(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    // ── Invalid inputs ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0ms")]
    [InlineData("-1ms")]
    [InlineData("0/s")]
    [InlineData("foo/s")]
    public void InvalidInput_ReturnsFalseWithError(string input)
    {
        bool ok = ClickRateParser.TryParse(input, out _, out string err);
        Assert.False(ok);
        Assert.NotEmpty(err);
    }

    [Fact]
    public void ExceedingMaxRate_ReturnsFalse()
    {
        // 2000/s = 0.5ms delay, which is < 1ms minimum
        bool ok = ClickRateParser.TryParse("2000/s", out _, out string err);
        Assert.False(ok);
        Assert.NotEmpty(err);
    }

    // ── New: Seconds ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("5s",         5_000)]
    [InlineData("5 s",        5_000)]
    [InlineData("5sec",       5_000)]
    [InlineData("5 sec",      5_000)]
    [InlineData("5secs",      5_000)]
    [InlineData("5second",    5_000)]
    [InlineData("5seconds",   5_000)]
    [InlineData("0.5sec",     500)]
    public void Seconds_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    // ── New: Minutes ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2m",        120_000)]
    [InlineData("2 m",       120_000)]
    [InlineData("2min",      120_000)]
    [InlineData("2 min",     120_000)]
    [InlineData("2mins",     120_000)]
    [InlineData("2minute",   120_000)]
    [InlineData("2minutes",  120_000)]
    [InlineData("0.5min",    30_000)]
    public void Minutes_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    // ── New: Per-hour frequency ──────────────────────────────────────────────

    [Theory]
    [InlineData("60/h",                 60_000)]
    [InlineData("60cph",                60_000)]
    [InlineData("60 times per hour",    60_000)]
    [InlineData("3600/h",               1_000)]   // 3600 clicks/hour = 1 click/sec
    public void ClicksPerHour_ValidInput_ReturnsCorrectDelay(string input, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(input, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    // ── Boundary: max delay 360 minutes ──────────────────────────────────────

    [Fact]
    public void MaxDelay_AcceptedAtBoundary()
    {
        Assert.True(ClickRateParser.TryParse("360min", out var delay, out _));
        Assert.Equal(360.0 * 60_000, delay.TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void MaxDelay_RejectedAboveBoundary()
    {
        Assert.False(ClickRateParser.TryParse("361min", out _, out string err));
        Assert.NotEmpty(err);
    }

    [Fact]
    public void VeryLowFrequency_RejectedWhenDelayExceedsMax()
    {
        // 0.001/h would be 3.6e9 ms = ~1000 hours → way over 360 min
        Assert.False(ClickRateParser.TryParse("0.001/h", out _, out string err));
        Assert.NotEmpty(err);
    }

    // ── Boundary: max rate 1000/s ────────────────────────────────────────────

    [Fact]
    public void MaxRate_AcceptedAtBoundary()
    {
        Assert.True(ClickRateParser.TryParse("1000/s", out var delay, out _));
        Assert.Equal(1.0, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void MaxRate_RejectedAboveBoundary()
    {
        Assert.False(ClickRateParser.TryParse("1001/s", out _, out string err));
        Assert.NotEmpty(err);
    }

    // ── Disambiguation: frequency forms with shared trailing letters ─────────

    [Fact]
    public void PerMinute_NotConfusedWithMinutes()
    {
        // "10/min" must parse as frequency, not be eaten by the minutes path.
        Assert.True(ClickRateParser.TryParse("10/min", out var delay, out _));
        Assert.Equal(6_000, delay.TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void Cpm_NotConfusedWithMinutes()
    {
        Assert.True(ClickRateParser.TryParse("600cpm", out var delay, out _));
        Assert.Equal(100, delay.TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void TimesPerMinute_NotConfusedWithMinutes()
    {
        Assert.True(ClickRateParser.TryParse("600 times per minute", out var delay, out _));
        Assert.Equal(100, delay.TotalMilliseconds, precision: 0);
    }
}
