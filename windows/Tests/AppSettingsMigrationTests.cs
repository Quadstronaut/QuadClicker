using QuadClicker.Models;
using Xunit;

namespace QuadClicker.Tests;

public sealed class AppSettingsMigrationTests
{
    // ── Legacy → canonical migration ─────────────────────────────────────────

    [Fact]
    public void LegacyMs_LoadsAsDelayMs()
    {
        // Old schema: ClickRateUnit "ms", no ClickRateMode field.
        const string json = """
        {
          "ClickRateValue": "100",
          "ClickRateUnit": "ms"
        }
        """;
        var s = AppSettings.LoadFromJson(json);
        Assert.Equal(ClickRateMode.Delay, s.ClickRateMode);
        Assert.Equal("ms", s.ClickRateUnit);
        Assert.Equal("100", s.ClickRateValue);
    }

    [Fact]
    public void LegacyPerSec_MigratesToFrequencyPerSec()
    {
        const string json = """
        {
          "ClickRateValue": "10",
          "ClickRateUnit": "/s"
        }
        """;
        var s = AppSettings.LoadFromJson(json);
        Assert.Equal(ClickRateMode.Frequency, s.ClickRateMode);
        Assert.Equal("per_sec", s.ClickRateUnit);
        Assert.Equal("10", s.ClickRateValue);
    }

    [Fact]
    public void LegacyPerMin_MigratesToFrequencyPerMin()
    {
        const string json = """
        {
          "ClickRateValue": "600",
          "ClickRateUnit": "/min"
        }
        """;
        var s = AppSettings.LoadFromJson(json);
        Assert.Equal(ClickRateMode.Frequency, s.ClickRateMode);
        Assert.Equal("per_min", s.ClickRateUnit);
        Assert.Equal("600", s.ClickRateValue);
    }

    // ── Idempotence: new schema round-trips unchanged ────────────────────────

    [Theory]
    [InlineData("ms")]
    [InlineData("sec")]
    [InlineData("min")]
    public void NewSchema_DelayUnits_PreservedExactly(string unit)
    {
        string json = $$"""
        {
          "ClickRateMode": 0,
          "ClickRateValue": "5",
          "ClickRateUnit": "{{unit}}"
        }
        """;
        var s = AppSettings.LoadFromJson(json);
        Assert.Equal(ClickRateMode.Delay, s.ClickRateMode);
        Assert.Equal(unit, s.ClickRateUnit);
    }

    [Theory]
    [InlineData("per_sec")]
    [InlineData("per_min")]
    [InlineData("per_hour")]
    public void NewSchema_FrequencyUnits_PreservedExactly(string unit)
    {
        string json = $$"""
        {
          "ClickRateMode": 1,
          "ClickRateValue": "5",
          "ClickRateUnit": "{{unit}}"
        }
        """;
        var s = AppSettings.LoadFromJson(json);
        Assert.Equal(ClickRateMode.Frequency, s.ClickRateMode);
        Assert.Equal(unit, s.ClickRateUnit);
    }

    // ── Empty / minimal JSON ─────────────────────────────────────────────────

    [Fact]
    public void EmptyJson_ReturnsDefaults()
    {
        var s = AppSettings.LoadFromJson("{}");
        Assert.Equal(ClickRateMode.Delay, s.ClickRateMode);
        Assert.Equal("ms",  s.ClickRateUnit);
        Assert.Equal("100", s.ClickRateValue);
    }
}
