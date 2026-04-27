using QuadClicker.Core;
using Xunit;

namespace QuadClicker.Tests;

public sealed class UpdaterTests
{
    // ── ParseSha256Sums ───────────────────────────────────────────────────────

    [Fact]
    public void ParseSha256Sums_ReturnsHashForExactFilename()
    {
        const string content = """
        abc1234567890abc1234567890abc1234567890abc1234567890abc1234567890ab  QuadClicker.exe
        deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefde  SOURCE.zip
        """;
        var hash = Updater.ParseSha256Sums(content, "QuadClicker.exe");
        Assert.Equal("abc1234567890abc1234567890abc1234567890abc1234567890abc1234567890ab", hash);
    }

    [Fact]
    public void ParseSha256Sums_IsCaseInsensitiveOnFilename()
    {
        const string content = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF  quadclicker.exe";
        var hash = Updater.ParseSha256Sums(content, "QuadClicker.exe");
        Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", hash);
    }

    [Fact]
    public void ParseSha256Sums_HandlesBinaryAsterisk()
    {
        // sha256sum -b emits "<hash> *<filename>"
        const string content = "1234567890123456789012345678901234567890123456789012345678901234 *QuadClicker.exe";
        var hash = Updater.ParseSha256Sums(content, "QuadClicker.exe");
        Assert.Equal("1234567890123456789012345678901234567890123456789012345678901234", hash);
    }

    [Fact]
    public void ParseSha256Sums_IgnoresCommentsAndBlankLines()
    {
        const string content = """

        # This is a comment
        cafebabecafebabecafebabecafebabecafebabecafebabecafebabecafebabe  QuadClicker.exe

        """;
        var hash = Updater.ParseSha256Sums(content, "QuadClicker.exe");
        Assert.Equal("cafebabecafebabecafebabecafebabecafebabecafebabecafebabecafebabe", hash);
    }

    [Fact]
    public void ParseSha256Sums_ReturnsNullWhenAbsent()
    {
        const string content = "abc123  OtherFile.zip";
        Assert.Null(Updater.ParseSha256Sums(content, "QuadClicker.exe"));
    }

    [Fact]
    public void ParseSha256Sums_ReturnsNullForEmptyInput()
    {
        Assert.Null(Updater.ParseSha256Sums("",   "QuadClicker.exe"));
        Assert.Null(Updater.ParseSha256Sums("\n", "QuadClicker.exe"));
    }

    // ── BuildStagingScript ────────────────────────────────────────────────────

    [Fact]
    public void BuildStagingScript_ContainsExpectedPidWaitAndCopy()
    {
        string script = Updater.BuildStagingScript(
            pid: 4242,
            sourceExe: @"C:\Temp\QuadClicker-update\QuadClicker-0.2.0.exe",
            targetExe: @"C:\Program Files\QuadClicker\QuadClicker.exe",
            newVersion: "0.2.0");

        Assert.Contains("$pidToWait = 4242",                                              script);
        Assert.Contains(@"'C:\Temp\QuadClicker-update\QuadClicker-0.2.0.exe'",            script);
        Assert.Contains(@"'C:\Program Files\QuadClicker\QuadClicker.exe'",               script);
        Assert.Contains("Get-Process -Id $pidToWait",                                    script);
        Assert.Contains("Copy-Item",                                                     script);
        Assert.Contains("Start-Process",                                                 script);
        Assert.Contains("--post-update",                                                 script);
        Assert.Contains("Remove-Item",                                                   script);
    }

    [Fact]
    public void BuildStagingScript_EscapesSingleQuotesInPaths()
    {
        // PowerShell single-quoted strings escape ' as ''.
        string script = Updater.BuildStagingScript(
            pid: 1,
            sourceExe: @"C:\Users\O'Brien\update.exe",
            targetExe: @"C:\Apps\QuadClicker.exe",
            newVersion: "0.2.0");

        Assert.Contains(@"'C:\Users\O''Brien\update.exe'", script);
    }

    [Fact]
    public void BuildStagingScript_HasErrorActionAndCleanup()
    {
        string script = Updater.BuildStagingScript(1, @"a", @"b", "0.2.0");
        Assert.Contains("$ErrorActionPreference = 'Stop'", script);
        Assert.Contains("try {",                           script);
        Assert.Contains("} catch {",                       script);
        Assert.Contains("} finally {",                     script);
    }
}
