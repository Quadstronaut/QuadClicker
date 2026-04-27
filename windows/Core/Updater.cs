using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace QuadClicker.Core;

/// <summary>
/// Downloads a new build, verifies its SHA256 against the release's
/// SHA256SUMS.txt, writes a one-shot PowerShell staging script that swaps
/// the running .exe after the current process exits, and spawns the script.
/// </summary>
public static class Updater
{
    public sealed class StagingFailure : Exception
    {
        public StagingFailure(string message) : base(message) { }
    }

    /// <summary>
    /// End-to-end: download asset + checksums, verify, generate the staging
    /// script, spawn it, and return the script path. Caller is responsible for
    /// shutting down the application after this returns successfully.
    /// </summary>
    public static async Task<string> DownloadAndStageAsync(
        UpdateCheckResult  result,
        string             currentExePath,
        HttpClient?        httpClient = null,
        CancellationToken  ct         = default)
    {
        if (!result.HasUpdate)               throw new StagingFailure("No update available.");
        if (string.IsNullOrEmpty(result.AssetUrl))
                                             throw new StagingFailure("Release has no Windows asset.");
        if (string.IsNullOrEmpty(result.Sha256SumsUrl))
                                             throw new StagingFailure("Release is missing SHA256SUMS.txt — refusing to update.");

        string version = UpdateChecker.NormalizeVersion(result.LatestVersion);
        var http = httpClient ?? UpdateChecker.CreateHttpClient(version);

        string tempDir = Path.Combine(Path.GetTempPath(), "QuadClicker-update");
        Directory.CreateDirectory(tempDir);
        string stagedExe = Path.Combine(tempDir, $"QuadClicker-{version}.exe");

        // 1. Download asset + checksum file
        await DownloadFileAsync(http, result.AssetUrl!,      stagedExe,                              ct).ConfigureAwait(false);
        string sumsText = await DownloadStringAsync(http, result.Sha256SumsUrl!, ct).ConfigureAwait(false);

        // 2. Verify SHA256
        string? expected = ParseSha256Sums(sumsText, result.AssetName ?? "QuadClicker.exe");
        if (string.IsNullOrEmpty(expected))
            throw new StagingFailure($"SHA256SUMS.txt does not list {result.AssetName}.");

        string actual = ComputeSha256Hex(stagedExe);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new StagingFailure("Checksum mismatch — refusing to install. (Expected " + expected + ", got " + actual + ")");

        // 3. Write staging script + spawn
        string scriptPath = Path.Combine(tempDir, "stage.ps1");
        int    pid        = Environment.ProcessId;
        string script     = BuildStagingScript(pid, stagedExe, currentExePath, version);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        SpawnStagingScript(scriptPath);
        return scriptPath;
    }

    // ── Pure helpers (unit-tested) ────────────────────────────────────────────

    /// <summary>
    /// Parses a SHA256SUMS.txt file (one line per file, "<hex>  <filename>").
    /// Returns the hex digest for the requested filename, or null if absent.
    /// Public for tests.
    /// </summary>
    public static string? ParseSha256Sums(string content, string filename)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(filename)) return null;

        foreach (var rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Split on whitespace; first token = hash, last token = filename
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string hash = parts[0].TrimStart('*');
            string name = parts[^1].TrimStart('*');

            if (string.Equals(name, filename, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(name), filename, StringComparison.OrdinalIgnoreCase))
            {
                return hash.ToLowerInvariant();
            }
        }
        return null;
    }

    /// <summary>
    /// Generates the PowerShell script that waits for the current process to
    /// exit, copies the staged exe over the live one, relaunches with
    /// <c>--post-update</c>, and deletes itself. Public for tests.
    /// </summary>
    public static string BuildStagingScript(int pid, string sourceExe, string targetExe, string newVersion)
    {
        // Single-quote escape: PowerShell single-quoted strings only need '' to escape '.
        string esc(string s) => s.Replace("'", "''");

        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("try {");
        sb.AppendLine($"    $pidToWait = {pid}");
        sb.AppendLine($"    $source    = '{esc(sourceExe)}'");
        sb.AppendLine($"    $target    = '{esc(targetExe)}'");
        sb.AppendLine($"    $version   = '{esc(newVersion)}'");
        sb.AppendLine();
        sb.AppendLine("    # Wait up to 30s for the previous process to exit");
        sb.AppendLine("    $deadline = (Get-Date).AddSeconds(30)");
        sb.AppendLine("    while (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) {");
        sb.AppendLine("        if ((Get-Date) -gt $deadline) { throw \"Timed out waiting for PID $pidToWait\" }");
        sb.AppendLine("        Start-Sleep -Milliseconds 200");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    Copy-Item -LiteralPath $source -Destination $target -Force");
        sb.AppendLine("    Start-Process -FilePath $target -ArgumentList @('--post-update', $version)");
        sb.AppendLine("} catch {");
        sb.AppendLine("    # Fail silently — user still has the old binary");
        sb.AppendLine("} finally {");
        sb.AppendLine("    Start-Sleep -Milliseconds 100");
        sb.AppendLine("    Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string GetRunningExePath() =>
        Process.GetCurrentProcess().MainModule?.FileName
            ?? Assembly.GetEntryAssembly()?.Location
            ?? throw new StagingFailure("Cannot determine running exe path.");

    // ── IO helpers ────────────────────────────────────────────────────────────

    private static async Task DownloadFileAsync(HttpClient http, string url, string targetPath, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        using var fs   = File.Create(targetPath);
        using var net  = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await net.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    private static async Task<string> DownloadStringAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static void SpawnStagingScript(string scriptPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow  = true,
        };
        Process.Start(psi);
    }
}
