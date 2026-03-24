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
}
