using QuadClicker.Models;
using Xunit;

namespace QuadClicker.Tests;

public sealed class ClickSessionTests
{
    private static ClickSession Default() => new(
        ClickRate:          TimeSpan.FromMilliseconds(100),
        Button:             MouseButton.Left,
        ClickType:          ClickType.Single,
        UseCurrentPosition: true,
        X: 0, Y: 0,
        StopAfterClicks:    0,
        StopAfterSeconds:   0,
        IdleWaitSeconds:    0);

    [Fact]
    public void DefaultSession_IsValid()
    {
        var s = Default();
        Assert.Equal(TimeSpan.FromMilliseconds(100), s.ClickRate);
        Assert.Equal(MouseButton.Left, s.Button);
        Assert.Equal(ClickType.Single, s.ClickType);
        Assert.True(s.UseCurrentPosition);
        Assert.Equal(0, s.StopAfterClicks);
    }

    [Fact]
    public void Session_IsImmutableRecord()
    {
        var s1 = Default();
        var s2 = s1 with { Button = MouseButton.Right };
        Assert.Equal(MouseButton.Left,  s1.Button);
        Assert.Equal(MouseButton.Right, s2.Button);
    }

    [Fact]
    public void Session_WithAllButtons_StoresCorrectly()
    {
        foreach (var btn in Enum.GetValues<MouseButton>())
        {
            var s = Default() with { Button = btn };
            Assert.Equal(btn, s.Button);
        }
    }

    [Fact]
    public void Session_DoubleClick_StoresCorrectly()
    {
        var s = Default() with { ClickType = ClickType.Double };
        Assert.Equal(ClickType.Double, s.ClickType);
    }

    [Fact]
    public void Session_FixedPosition_StoresCoordinates()
    {
        var s = Default() with { UseCurrentPosition = false, X = 500, Y = 300 };
        Assert.False(s.UseCurrentPosition);
        Assert.Equal(500, s.X);
        Assert.Equal(300, s.Y);
    }
}
