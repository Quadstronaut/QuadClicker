using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuadClicker.Core;

/// <summary>
/// Result of a single GitHub-release check. <see cref="HasUpdate"/> is the only
/// success signal; on any network/parse/throttle failure callers receive a
/// result with <see cref="HasUpdate"/> = false. Failures never throw.
/// </summary>
public sealed record UpdateCheckResult(
    bool    HasUpdate,
    string  LatestVersion,
    string? AssetUrl,
    string? AssetName,
    string? Sha256SumsUrl,
    string? ReleaseNotesUrl);

public static class UpdateChecker
{
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/Quadstronaut/QuadClicker/releases/latest";

    /// <summary>
    /// Hits the GitHub releases API and returns whether a newer build exists.
    /// Silent on every error path — never throws, never blocks launch.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(
        string             currentVersion,
        string             windowsAssetName = "QuadClicker.exe",
        HttpClient?        httpClient       = null,
        CancellationToken  ct               = default)
    {
        var empty = new UpdateCheckResult(false, currentVersion, null, null, null, null);

        try
        {
            var http = httpClient ?? CreateHttpClient(currentVersion);
            using var resp = await http.GetAsync(ReleasesApiUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return empty;

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseRelease(json, currentVersion, windowsAssetName) ?? empty;
        }
        catch
        {
            // Any failure (no network, DNS, timeout, parse) → treat as no update.
            return empty;
        }
    }

    internal static HttpClient CreateHttpClient(string currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("QuadClicker", currentVersion));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>
    /// Parses a GitHub /releases/latest JSON payload. Public for tests.
    /// Returns null if no usable update is found.
    /// </summary>
    public static UpdateCheckResult? ParseRelease(string json, string currentVersion, string assetName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
        string? tag = tagEl.GetString();
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string latest = NormalizeVersion(tag);
        if (!IsNewer(currentVersion, latest)) return null;

        string? assetUrl     = null;
        string? sha256Url    = null;
        if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameEl)) continue;
                if (!asset.TryGetProperty("browser_download_url", out var urlEl)) continue;

                string? name = nameEl.GetString();
                string? url  = urlEl.GetString();
                if (name is null || url is null) continue;

                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                    assetUrl = url;
                else if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    sha256Url = url;
            }
        }

        string? notesUrl = root.TryGetProperty("html_url", out var notesEl) ? notesEl.GetString() : null;

        return new UpdateCheckResult(
            HasUpdate:      true,
            LatestVersion:  latest,
            AssetUrl:       assetUrl,
            AssetName:      assetName,
            Sha256SumsUrl:  sha256Url,
            ReleaseNotesUrl: notesUrl);
    }

    /// <summary>Strips a leading 'v' / 'V' and surrounding whitespace.</summary>
    public static string NormalizeVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        return s;
    }

    /// <summary>
    /// Returns true if <paramref name="latestRaw"/> is strictly greater than
    /// <paramref name="currentRaw"/> using Major.Minor.Patch comparison.
    /// Inputs may carry a leading 'v'. Malformed → returns false.
    /// </summary>
    public static bool IsNewer(string currentRaw, string latestRaw)
    {
        if (!TryParseSemver(currentRaw, out var cur))    return false;
        if (!TryParseSemver(latestRaw,  out var latest)) return false;

        if (latest.Major != cur.Major) return latest.Major > cur.Major;
        if (latest.Minor != cur.Minor) return latest.Minor > cur.Minor;
        return latest.Patch > cur.Patch;
    }

    private static bool TryParseSemver(string raw, out (int Major, int Minor, int Patch) parsed)
    {
        parsed = (0, 0, 0);
        var s = NormalizeVersion(raw);
        if (s.Length == 0) return false;

        // Strip pre-release / build metadata after Major.Minor.Patch.
        int dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        var parts = s.Split('.');
        if (parts.Length < 1 || parts.Length > 3) return false;

        int[] nums = new int[3];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int n) || n < 0) return false;
            nums[i] = n;
        }
        parsed = (nums[0], nums[1], nums[2]);
        return true;
    }
}
