using System.Globalization;

namespace QuadClicker.Core;

/// <summary>
/// Parses user-supplied click rate strings into a <see cref="TimeSpan"/> delay.
/// Accepted formats:
///   Delay:     "100", "100ms", "5s" / "5sec" / "5 seconds", "2min" / "2m" / "2 minutes"
///   Frequency: "10/s" / "10cps" / "10 times per second",
///              "600/min" / "600cpm" / "600 times per minute",
///              "60/h" / "60cph" / "60 times per hour"
/// Bounds: 1 ms ≤ delay ≤ 360 min. Frequencies that would produce a delay outside
/// this range are rejected with a descriptive error.
/// </summary>
public static class ClickRateParser
{
    public const double MinDelayMs = 1.0;
    public const long   MaxDelayMs = 360L * 60_000;  // 360 minutes

    public static bool TryParse(string text, out TimeSpan delay, out string error)
    {
        delay = TimeSpan.FromMilliseconds(100);
        error = string.Empty;
        text  = text?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            error = "Click rate is required.";
            return false;
        }

        // ── Milliseconds: "100ms" ─────────────────────────────────────────────
        if (text.EndsWith("ms"))
        {
            if (TryParsePositive(StripSuffix(text, "ms"), out double ms))
                return TryBuildDelay(ms, out delay, out error);
            error = "Millisecond value must be a positive number.";
            return false;
        }

        // ── Minutes: "2min", "2 minutes", "2m" ────────────────────────────────
        // Order: longest suffix first. Guards skip frequency forms that share
        // a final "m" or "min" (cpm, /min, "times per minute").
        if (EndsWithAny(text, "minutes", "minute", "mins", "min", "m") &&
            !text.Contains("per minute") && !text.EndsWith("/min") && !text.EndsWith("cpm"))
        {
            string num = StripFirstMatchingSuffix(text, "minutes", "minute", "mins", "min", "m");
            if (TryParsePositive(num, out double minutes))
                return TryBuildDelay(minutes * 60_000.0, out delay, out error);
            error = "Minutes value must be a positive number.";
            return false;
        }

        // ── Seconds: "5s", "5sec", "5 seconds" ────────────────────────────────
        // Guards skip /s, cps, "times per second" frequency forms.
        if (EndsWithAny(text, "seconds", "second", "secs", "sec", "s") &&
            !text.Contains("per second") && !text.EndsWith("/s") && !text.EndsWith("cps"))
        {
            string num = StripFirstMatchingSuffix(text, "seconds", "second", "secs", "sec", "s");
            if (TryParsePositive(num, out double seconds))
                return TryBuildDelay(seconds * 1000.0, out delay, out error);
            error = "Seconds value must be a positive number.";
            return false;
        }

        // ── Clicks/second: "10/s", "10cps", "10 times per second" ────────────
        if (text.EndsWith("/s") || text.EndsWith("cps") || text.Contains("times per second"))
        {
            string num = text.Replace("times per second", "")
                             .Replace("/s", "").Replace("cps", "").Trim();
            if (TryParsePositive(num, out double tps))
                return TryBuildDelay(1000.0 / tps, out delay, out error);
            error = "Clicks-per-second value must be a positive number.";
            return false;
        }

        // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────
        if (text.EndsWith("/min") || text.EndsWith("cpm") || text.Contains("times per minute"))
        {
            string num = text.Replace("times per minute", "")
                             .Replace("/min", "").Replace("cpm", "").Trim();
            if (TryParsePositive(num, out double tpm))
                return TryBuildDelay(60_000.0 / tpm, out delay, out error);
            error = "Clicks-per-minute value must be a positive number.";
            return false;
        }

        // ── Clicks/hour: "60/h", "60cph", "60 times per hour" ────────────────
        if (text.EndsWith("/h") || text.EndsWith("cph") || text.Contains("times per hour"))
        {
            string num = text.Replace("times per hour", "")
                             .Replace("/h", "").Replace("cph", "").Trim();
            if (TryParsePositive(num, out double tph))
                return TryBuildDelay(3_600_000.0 / tph, out delay, out error);
            error = "Clicks-per-hour value must be a positive number.";
            return false;
        }

        // ── Bare integer/decimal → milliseconds ───────────────────────────────
        if (TryParsePositive(text, out double bare))
            return TryBuildDelay(bare, out delay, out error);

        error = "Invalid format. Examples: 100ms  |  5s  |  10/s  |  600/min  |  60/h";
        return false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryBuildDelay(double ms, out TimeSpan delay, out string error)
    {
        delay = TimeSpan.Zero;
        if (double.IsNaN(ms) || double.IsInfinity(ms))
        {
            error = "Rate is not a finite number.";
            return false;
        }
        if (ms < MinDelayMs)
        {
            error = "Rate exceeds maximum — minimum delay is 1 ms (1000 clicks/sec).";
            return false;
        }
        if (ms > MaxDelayMs)
        {
            error = "Delay exceeds maximum of 360 minutes.";
            return false;
        }
        delay = TimeSpan.FromMilliseconds(ms);
        error = string.Empty;
        return true;
    }

    private static bool TryParsePositive(string s, out double value) =>
        double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) && value > 0;

    private static string StripSuffix(string text, string suffix) =>
        text.EndsWith(suffix) ? text[..^suffix.Length].Trim() : text.Trim();

    private static bool EndsWithAny(string text, params string[] suffixes)
    {
        foreach (var s in suffixes)
            if (text.EndsWith(s)) return true;
        return false;
    }

    private static string StripFirstMatchingSuffix(string text, params string[] suffixes)
    {
        foreach (var s in suffixes)
            if (text.EndsWith(s)) return text[..^s.Length].Trim();
        return text.Trim();
    }
}
