using QuadClicker.Core;
using Xunit;

namespace QuadClicker.Tests;

public sealed class UpdateCheckerTests
{
    // ── NormalizeVersion ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("v1.2.3",  "1.2.3")]
    [InlineData("V1.2.3",  "1.2.3")]
    [InlineData(" 1.2.3 ", "1.2.3")]
    [InlineData("1.2.3",   "1.2.3")]
    [InlineData("",        "")]
    public void NormalizeVersion_StripsLeadingV(string input, string expected) =>
        Assert.Equal(expected, UpdateChecker.NormalizeVersion(input));

    // ── IsNewer ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0.1.1", "0.2.0", true)]
    [InlineData("0.1.1", "v0.2.0", true)]
    [InlineData("0.2.0", "0.2.1", true)]
    [InlineData("0.2.0", "1.0.0", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.2.0", "0.1.99", false)]
    [InlineData("1.0.0", "0.99.99", false)]
    [InlineData("0.2.0", "0.2.1-beta1", true)]   // pre-release stripped → 0.2.1 > 0.2.0
    [InlineData("0.2.0", "0.2.0-rc1",   false)]  // 0.2.0 == 0.2.0 after strip, NOT newer
    [InlineData("0.2.0", "garbage",     false)]
    [InlineData("garbage", "0.2.0",     false)]
    public void IsNewer_ComparesSemver(string current, string latest, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(current, latest));

    // ── ParseRelease ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseRelease_FindsAssetAndChecksum()
    {
        const string json = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/Quadstronaut/QuadClicker/releases/tag/v0.2.0",
          "assets": [
            { "name": "QuadClicker.exe",  "browser_download_url": "https://example.com/QuadClicker.exe" },
            { "name": "SHA256SUMS.txt",   "browser_download_url": "https://example.com/SHA256SUMS.txt" }
          ]
        }
        """;

        var result = UpdateChecker.ParseRelease(json, currentVersion: "0.1.1", assetName: "QuadClicker.exe");

        Assert.NotNull(result);
        Assert.True(result!.HasUpdate);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.Equal("https://example.com/QuadClicker.exe", result.AssetUrl);
        Assert.Equal("https://example.com/SHA256SUMS.txt",  result.Sha256SumsUrl);
        Assert.Equal("https://github.com/Quadstronaut/QuadClicker/releases/tag/v0.2.0", result.ReleaseNotesUrl);
    }

    [Fact]
    public void ParseRelease_ReturnsNull_WhenSameVersion()
    {
        const string json = """
        { "tag_name": "v0.1.1", "assets": [] }
        """;
        var result = UpdateChecker.ParseRelease(json, currentVersion: "0.1.1", assetName: "QuadClicker.exe");
        Assert.Null(result);
    }

    [Fact]
    public void ParseRelease_ReturnsNull_WhenOlderTag()
    {
        const string json = """
        { "tag_name": "v0.0.9", "assets": [] }
        """;
        var result = UpdateChecker.ParseRelease(json, currentVersion: "0.1.1", assetName: "QuadClicker.exe");
        Assert.Null(result);
    }

    [Fact]
    public void ParseRelease_ReturnsResult_EvenIfChecksumMissing()
    {
        // Updater later refuses to install without a checksum, but the *check* still reports an update.
        const string json = """
        {
          "tag_name": "v0.2.0",
          "assets": [
            { "name": "QuadClicker.exe", "browser_download_url": "https://example.com/QuadClicker.exe" }
          ]
        }
        """;
        var result = UpdateChecker.ParseRelease(json, currentVersion: "0.1.1", assetName: "QuadClicker.exe");

        Assert.NotNull(result);
        Assert.True(result!.HasUpdate);
        Assert.Equal("https://example.com/QuadClicker.exe", result.AssetUrl);
        Assert.Null(result.Sha256SumsUrl);
    }
}
