using QuadClicker.Core;
using QuadClicker.Models;
using System.Globalization;

namespace QuadClicker.Cli;

internal static class CliEntryPoint
{
    internal static int Run(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        if (args.Length == 1 && args[0] is "--version" or "-v")
        {
            var ver = typeof(CliEntryPoint).Assembly.GetName().Version;
            Console.WriteLine($"QuadClicker {ver?.ToString(3) ?? "unknown"}");
            return 0;
        }

        if (args.Length >= 1 && args[0] == "--check-update")
        {
            return RunCheckUpdate();
        }

        if (!TryParseArgs(args, out var session, out string parseError))
        {
            Console.Error.WriteLine($"Error: {parseError}");
            Console.Error.WriteLine("Run 'quadclicker --help' for usage.");
            return 1;
        }

        Console.WriteLine(
            $"QuadClicker | {(int)session!.ClickRate.TotalMilliseconds}ms delay | " +
            $"{session.Button} {session.ClickType} click | " +
            $"{(session.UseCurrentPosition ? "cursor position" : $"({session.X},{session.Y})")}");

        if (session.StopAfterClicks  > 0) Console.WriteLine($"  Stop after {session.StopAfterClicks} clicks");
        if (session.StopAfterSeconds > 0) Console.WriteLine($"  Stop after {session.StopAfterSeconds}s");
        if (session.IdleWaitSeconds  > 0) Console.WriteLine($"  Wait for {session.IdleWaitSeconds}s idle");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        return RunSession(session, cts);
    }

    /// <summary>
    /// Runs one headless session and maps the outcome to the documented exit code.
    /// Internal so tests can drive it with an already-cancelled source (no clicks).
    /// </summary>
    internal static int RunSession(ClickSession session, CancellationTokenSource cts)
    {
        var engine = new ClickEngine();
        int lastCount = 0;
        engine.ClickCountUpdated += count =>
        {
            lastCount = count;
            if (count % 50 == 0) Console.Write($"\rClicks: {count}   ");
        };
        engine.StatusChanged += status =>
        {
            if (status == EngineStatus.WaitingForIdle) Console.Write("\rWaiting for idle...   ");
            if (status == EngineStatus.Clicking)       Console.Write("\rClicking...           ");
        };

        try
        {
            engine.RunAsync(session, cts.Token).GetAwaiter().GetResult();
            // The engine returns normally when cancelled, so check the source:
            // Ctrl+C must exit 130 as documented, not 0.
            if (cts.IsCancellationRequested)
            {
                Console.WriteLine($"\nStopped. Total clicks: {lastCount}");
                return 130;
            }
            Console.WriteLine($"\nDone. Total clicks: {lastCount}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\nStopped. Total clicks: {lastCount}");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nRuntime error: {ex.Message}");
            return 2;
        }
    }

    internal static bool TryParseArgs(string[] args, out ClickSession? session, out string error)
    {
        session = null;
        error   = string.Empty;

        TimeSpan           rate          = TimeSpan.Zero;
        Models.MouseButton button        = Models.MouseButton.Left;
        Models.ClickType   clickType     = Models.ClickType.Single;
        bool               useCurrentPos = true;
        int                x = 0, y     = 0;
        int                stopClicks    = 0;
        double             stopSeconds   = 0;
        double             idleWait      = 0;
        bool               hasRate       = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            // Inline helper — cannot capture `out` param `error` in local function, so returns bool
            // and the caller sets error on false return.
            bool TryNext(out string val)
            {
                if (i + 1 < args.Length) { val = args[++i]; return true; }
                val = string.Empty;
                return false;
            }

            switch (arg.ToLowerInvariant())
            {
                case "--rate":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (!ClickRateParser.TryParse(val, out rate, out error)) return false;
                    hasRate = true;
                    break;
                }
                case "--button":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (val.ToLower() is not ("left" or "right" or "middle"))
                    { error = $"Unknown button '{val}'. Use: left, right, middle."; return false; }
                    button = val.ToLower() switch
                    {
                        "right"  => Models.MouseButton.Right,
                        "middle" => Models.MouseButton.Middle,
                        _        => Models.MouseButton.Left
                    };
                    break;
                }
                case "--type":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    string type = val.ToLowerInvariant();
                    if (type is not ("single" or "double"))
                    { error = $"Unknown click type '{val}'. Use: single, double."; return false; }
                    clickType = type == "double" ? Models.ClickType.Double : Models.ClickType.Single;
                    break;
                }
                case "--location":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    var parts = val.Split(',');
                    if (parts.Length != 2 ||
                        !int.TryParse(parts[0].Trim(), out x) ||
                        !int.TryParse(parts[1].Trim(), out y))
                    { error = "Location must be 'X,Y' (e.g. 500,300)."; return false; }
                    useCurrentPos = false;
                    break;
                }
                case "--stop-after-clicks":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    // A negative limit would silently mean "unlimited" in the engine.
                    if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out stopClicks) || stopClicks < 0)
                    { error = "--stop-after-clicks must be a non-negative integer."; return false; }
                    break;
                }
                case "--stop-after-seconds":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (!TryParseNonNegative(val, out stopSeconds))
                    { error = "--stop-after-seconds must be a non-negative number."; return false; }
                    break;
                }
                case "--idle-wait":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (!TryParseNonNegative(val, out idleWait))
                    { error = "--idle-wait must be a non-negative number."; return false; }
                    break;
                }
                case "--no-gui":
                case "--minimized":
                case "--no-update-check":
                    break; // Handled at the program entry level

                case "--post-update":
                    // The relaunched-after-update flag carries an optional version arg.
                    // We don't act on it in CLI mode but accept it so it isn't a parse error.
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) i++;
                    break;

                default:
                    error = $"Unknown argument: '{arg}'";
                    return false;
            }
        }

        if (!hasRate) { error = "--rate is required in CLI mode."; return false; }

        session = new ClickSession(rate, button, clickType, useCurrentPos, x, y,
                                   stopClicks, stopSeconds, idleWait);
        return true;
    }

    // Finite and >= 0. NaN/Infinity/negatives would make the engine's "> 0" checks
    // silently disable the limit. Float (not Any) so "1,5" is rejected, not read as 15.
    private static bool TryParseNonNegative(string s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value) && value >= 0;

    private static int RunCheckUpdate()
    {
        var ver = typeof(CliEntryPoint).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        try
        {
            var result = QuadClicker.Core.UpdateChecker.CheckAsync(ver).GetAwaiter().GetResult();
            if (result.HasUpdate)
            {
                Console.WriteLine($"Update available: v{result.LatestVersion} (current v{ver})");
                if (!string.IsNullOrEmpty(result.ReleaseNotesUrl))
                    Console.WriteLine($"  {result.ReleaseNotesUrl}");
                return 0;
            }
            Console.WriteLine($"Up to date (v{ver})");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update check failed: {ex.Message}");
            return 2;
        }
    }

    private static void PrintHelp() => Console.WriteLine("""
        Usage: quadclicker [OPTIONS]

        When run without arguments, launches the GUI.

        Options:
          --rate <value>               Click rate. Formats: 100ms | 5s | 2min | 10/s | 600/min | 60/h  [required in CLI mode]
          --button <left|right|middle> Mouse button to click (default: left)
          --type <single|double>       Click type (default: single)
          --location <x,y>             Fixed screen coordinate (default: current cursor)
          --stop-after-clicks <n>      Stop after N clicks, N >= 0 (0 = unlimited)
          --stop-after-seconds <n>     Stop after N seconds, N >= 0 (0 = unlimited)
          --idle-wait <n>              Wait for N seconds of system idle before starting (N >= 0)
          --no-gui                     Force headless mode
          --minimized                  Launch GUI minimized to tray
          --no-update-check            Disable the launch-time GitHub update check (GUI only)
          --check-update               Check for an update, print result, exit 0
          --version                    Print version and exit 0
          --help                       Print this help and exit 0

        Exit codes:
          0   Success / clean stop
          1   Invalid argument
          2   Runtime error
          130 Ctrl+C / interrupted

        Examples:
          quadclicker --rate 10/s
          quadclicker --rate 10/s --location 500,300 --button right --stop-after-clicks 100
          quadclicker --rate 500ms --type double --stop-after-seconds 30
          quadclicker --rate 1ms --button middle --stop-after-clicks 50
          quadclicker --rate 5s --stop-after-clicks 10
          quadclicker --rate 60/h --stop-after-seconds 3600
        """);
}
