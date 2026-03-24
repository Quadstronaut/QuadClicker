using QuadClicker.Cli;
using QuadClicker.PInvoke;

namespace QuadClicker;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool minimized = args.Contains("--minimized");

        // CLI mode: any argument other than --minimized triggers headless execution
        bool isCli = args.Length > 0 && !minimized;
        if (isCli)
            return CliEntryPoint.Run(args);

        // GUI mode — detach from the console window that Exe output type allocates
        NativeMethods.FreeConsole();

        var app = new App();
        if (minimized)
            app.Properties["StartMinimized"] = true;

        app.InitializeComponent();
        return app.Run();
    }
}
