using QuadClicker.Core;
using QuadClicker.Models;
using Xunit;

namespace QuadClicker.Tests;

/// <summary>Tests for CLI argument parsing via ClickRateParser (public surface).</summary>
public sealed class CliArgumentTests
{
    // These tests exercise the parser through the same path the CLI uses.

    [Theory]
    [InlineData("100ms",  100)]
    [InlineData("10/s",   100)]
    [InlineData("600/min", 100)]
    public void Rate_AllFormats_ParseCorrectly(string rate, double expectedMs)
    {
        Assert.True(ClickRateParser.TryParse(rate, out var delay, out _));
        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void Rate_Missing_ReturnsError()
    {
        bool ok = ClickRateParser.TryParse("", out _, out string err);
        Assert.False(ok);
        Assert.NotEmpty(err);
    }

    [Fact]
    public void ClickSession_AllFields_RoundTrip()
    {
        var session = new ClickSession(
            ClickRate:          TimeSpan.FromMilliseconds(200),
            Button:             MouseButton.Right,
            ClickType:          ClickType.Double,
            UseCurrentPosition: false,
            X:                  640,
            Y:                  480,
            StopAfterClicks:    100,
            StopAfterSeconds:   30,
            IdleWaitSeconds:    5);

        Assert.Equal(200,             session.ClickRate.TotalMilliseconds);
        Assert.Equal(MouseButton.Right, session.Button);
        Assert.Equal(ClickType.Double, session.ClickType);
        Assert.False(session.UseCurrentPosition);
        Assert.Equal(640, session.X);
        Assert.Equal(480, session.Y);
        Assert.Equal(100, session.StopAfterClicks);
        Assert.Equal(30,  session.StopAfterSeconds);
        Assert.Equal(5,   session.IdleWaitSeconds);
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public void MouseButton_AllValues_AreValid(MouseButton btn)
    {
        var s = new ClickSession(TimeSpan.FromMilliseconds(100), btn,
            ClickType.Single, true, 0, 0, 0, 0, 0);
        Assert.Equal(btn, s.Button);
    }
}
