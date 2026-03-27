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

    private static bool TryParseArgs(string[] args, out ClickSession? session, out string error)
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
                    clickType = val.ToLower() == "double" ? Models.ClickType.Double : Models.ClickType.Single;
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
                    if (!int.TryParse(val, out stopClicks))
                    { error = "--stop-after-clicks must be an integer."; return false; }
                    break;
                }
                case "--stop-after-seconds":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (!double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out stopSeconds))
                    { error = "--stop-after-seconds must be a number."; return false; }
                    break;
                }
                case "--idle-wait":
                {
                    if (!TryNext(out var val)) { error = $"Missing value after '{arg}'"; return false; }
                    if (!double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out idleWait))
                    { error = "--idle-wait must be a number."; return false; }
                    break;
                }
                case "--no-gui":
                case "--minimized":
                    break; // Handled at the program entry level

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

    private static void PrintHelp() => Console.WriteLine("""
        Usage: quadclicker [OPTIONS]

        When run without arguments, launches the GUI.

        Options:
          --rate <value>               Click rate. Formats: 100ms | 10/s | 600/min  [required in CLI mode]
          --button <left|right|middle> Mouse button to click (default: left)
          --type <single|double>       Click type (default: single)
          --location <x,y>             Fixed screen coordinate (default: current cursor)
          --stop-after-clicks <n>      Stop after N clicks (0 = unlimited)
          --stop-after-seconds <n>     Stop after N seconds (0 = unlimited)
          --idle-wait <n>              Wait for N seconds of system idle before starting
          --no-gui                     Force headless mode
          --minimized                  Launch GUI minimized to tray
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
        """);
}
