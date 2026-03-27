using System.Drawing;
using System.Windows.Forms;

namespace QuadClicker.Core;

/// <summary>Manages the system tray NotifyIcon lifecycle.</summary>
internal sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _disposed;

    internal event Action? ShowWindowRequested;
    internal event Action? ToggleClickingRequested;
    internal event Action? QuitRequested;

    internal TrayManager()
    {
        _icon = new NotifyIcon
        {
            Text    = "QuadClicker",
            Visible = false,
            Icon    = SystemIcons.Application
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Window",   null, (_, _) => ShowWindowRequested?.Invoke());
        menu.Items.Add("Start / Stop",  null, (_, _) => ToggleClickingRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit",          null, (_, _) => QuitRequested?.Invoke());

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick     += (_, _) => ShowWindowRequested?.Invoke();
    }

    internal void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _icon.Visible = true;
    }

    internal void Hide()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _icon.Visible = false;
    }

    internal void SetActiveState(bool isClicking)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _icon.Text = isClicking ? "QuadClicker — Clicking" : "QuadClicker";
        // TODO: swap to animated icon when assets are available
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _disposed = true;
        }
    }
}
