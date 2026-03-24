using System.Globalization;

namespace QuadClicker.Core;

/// <summary>
/// Parses user-supplied click rate strings into a <see cref="TimeSpan"/> delay.
/// Accepted formats: bare integer (ms), "100ms", "10/s", "10cps", "10 times per second",
/// "600/min", "600cpm", "600 times per minute".
/// </summary>
public static class ClickRateParser
{
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
            if (TryParsePositive(text[..^2].Trim(), out double ms) && ms >= 1)
            {
                delay = TimeSpan.FromMilliseconds(ms);
                return true;
            }
            error = "Millisecond value must be a number ≥ 1.";
            return false;
        }

        // ── Clicks/second: "10/s", "10cps", "10 times per second" ────────────
        if (text.EndsWith("/s") || text.EndsWith("cps") || text.Contains("times per second"))
        {
            string num = text.Replace("times per second", "")
                             .Replace("/s", "").Replace("cps", "").Trim();
            if (TryParsePositive(num, out double tps))
            {
                double ms = 1000.0 / tps;
                if (ms < 1) { error = "Rate exceeds maximum — minimum delay is 1ms."; return false; }
                delay = TimeSpan.FromMilliseconds(ms);
                return true;
            }
            error = "Clicks-per-second value must be a positive number.";
            return false;
        }

        // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────
        if (text.EndsWith("/min") || text.EndsWith("cpm") || text.Contains("times per minute"))
        {
            string num = text.Replace("times per minute", "")
                             .Replace("/min", "").Replace("cpm", "").Trim();
            if (TryParsePositive(num, out double tpm))
            {
                double ms = 60000.0 / tpm;
                if (ms < 1) { error = "Rate exceeds maximum — minimum delay is 1ms."; return false; }
                delay = TimeSpan.FromMilliseconds(ms);
                return true;
            }
            error = "Clicks-per-minute value must be a positive number.";
            return false;
        }

        // ── Bare integer/decimal → milliseconds ───────────────────────────────
        if (TryParsePositive(text, out double bare) && bare >= 1)
        {
            delay = TimeSpan.FromMilliseconds(bare);
            return true;
        }

        error = "Invalid format. Examples: 100ms  |  10/s  |  600/min";
        return false;
    }

    private static bool TryParsePositive(string s, out double value) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value) && value > 0;
}
