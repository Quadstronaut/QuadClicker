using QuadClicker.Cli;
using QuadClicker.PInvoke;

namespace QuadClicker;

public static class Program
{
    // Flags that may appear alongside the GUI without triggering headless CLI mode.
    private static readonly HashSet<string> GuiCompatibleFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--minimized",
        "--no-update-check",
        "--post-update",
    };

    [STAThread]
    public static int Main(string[] args)
    {
        bool minimized       = args.Contains("--minimized",       StringComparer.OrdinalIgnoreCase);
        bool noUpdateCheck   = args.Contains("--no-update-check", StringComparer.OrdinalIgnoreCase);
        string? postUpdateVersion = ExtractPostUpdateVersion(args);

        // CLI mode: triggered by any flag that isn't strictly GUI-compatible.
        // GuiCompatibleFlags handles the lone-flag case; --post-update may also
        // carry a positional version arg directly after it, so accept that too.
        bool isCli = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (GuiCompatibleFlags.Contains(a))
            {
                if (a.Equals("--post-update", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    i++; // consume version arg
                }
                continue;
            }
            isCli = true;
            break;
        }

        if (isCli)
            return CliEntryPoint.Run(args);

        // GUI mode — detach from the console window that Exe output type allocates
        NativeMethods.FreeConsole();

        var app = new App();
        if (minimized)             app.Properties["StartMinimized"]    = true;
        if (noUpdateCheck)         app.Properties["NoUpdateCheck"]     = true;
        if (postUpdateVersion is not null)
                                    app.Properties["PostUpdateVersion"] = postUpdateVersion;

        app.InitializeComponent();
        return app.Run();
    }

    private static string? ExtractPostUpdateVersion(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--post-update", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) return args[i + 1];
            return string.Empty; // flag present without a version
        }
        return null;
    }
}
