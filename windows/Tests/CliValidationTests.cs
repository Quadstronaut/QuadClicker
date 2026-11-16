using QuadClicker.Cli;
using QuadClicker.Core;
using QuadClicker.Models;
using Xunit;

namespace QuadClicker.Tests;

/// <summary>CLI argument validation and exit-code mapping. Never sends input.</summary>
public sealed class CliValidationTests
{
    private static bool Parse(out ClickSession? s, out string err, params string[] args) =>
        CliEntryPoint.TryParseArgs(args, out s, out err);

    [Theory]
    [InlineData("--stop-after-clicks",  "-1")]
    [InlineData("--stop-after-seconds", "-10")]
    [InlineData("--stop-after-seconds", "NaN")]
    [InlineData("--stop-after-seconds", "Infinity")]
    [InlineData("--idle-wait",          "-5")]
    [InlineData("--idle-wait",          "NaN")]
    [InlineData("--idle-wait",          "1,5")]   // comma decimal is rejected, not read as 15
    public void Limits_RejectNegativeOrNonFinite(string flag, string value)
    {
        Assert.False(Parse(out _, out string err, "--rate", "100ms", flag, value));
        Assert.Contains("non-negative", err);
    }

    [Fact]
    public void Limits_AcceptZeroAndDecimals()
    {
        Assert.True(Parse(out var s, out _, "--rate", "100ms",
            "--stop-after-clicks", "0", "--stop-after-seconds", "1.5", "--idle-wait", "0"));
        Assert.Equal(0,   s!.StopAfterClicks);
        Assert.Equal(1.5, s.StopAfterSeconds);
        Assert.Equal(0,   s.IdleWaitSeconds);
    }

    [Theory]
    [InlineData("doubleclick")]
    [InlineData("dbl")]
    [InlineData("triple")]
    public void Type_UnknownValue_IsRejected(string type)
    {
        Assert.False(Parse(out _, out string err, "--rate", "100ms", "--type", type));
        Assert.Contains("single, double", err);
    }

    [Theory]
    [InlineData("double", ClickType.Double)]
    [InlineData("Double", ClickType.Double)]
    [InlineData("SINGLE", ClickType.Single)]
    public void Type_KnownValue_IsCaseInsensitive(string type, ClickType expected)
    {
        Assert.True(Parse(out var s, out _, "--rate", "100ms", "--type", type));
        Assert.Equal(expected, s!.ClickType);
    }

    [Fact]
    public void RunSession_Cancelled_Returns130()
    {
        // Pre-cancelled source plus an unreachable idle wait: the engine returns at
        // the idle-wait cancellation check before it could ever send a click.
        var session = new ClickSession(TimeSpan.FromMilliseconds(100), MouseButton.Left,
            ClickType.Single, true, 0, 0, 0, 0, IdleWaitSeconds: 1_000_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Equal(130, CliEntryPoint.RunSession(session, cts));
    }
}
