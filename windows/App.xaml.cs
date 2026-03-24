using QuadClicker.Models;
using System.Windows;

namespace QuadClicker;

public partial class App : Application
{
    public AppSettings Settings { get; private set; } = new AppSettings();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = AppSettings.Load();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        // Settings are saved by MainWindow on close; this is a safety save
        Settings.Save();
    }
}
