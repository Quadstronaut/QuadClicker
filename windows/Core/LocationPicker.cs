using QuadClicker.PInvoke;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuadClicker.Core;

/// <summary>
/// Shows a full-screen transparent overlay that lets the user click a point to capture its coordinates.
/// The selection click is swallowed (not forwarded to the underlying window).
/// ESC cancels the pick without changing stored coordinates.
/// </summary>
internal sealed class LocationPicker : IDisposable
{
    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private Window? _overlay;
    private bool _disposed;

    /// <summary>Raised on the UI thread when the user clicks a point.</summary>
    internal event Action<int, int>? LocationPicked;

    /// <summary>Raised on the UI thread when the pick is cancelled via ESC.</summary>
    internal event Action? PickCancelled;

    internal void BeginPick(Window owner)
    {
        owner.WindowState = WindowState.Minimized;

        // Wait for the minimize animation before showing the overlay
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(300);
            ShowOverlay(owner);
        });
    }

    private void ShowOverlay(Window owner)
    {
        _overlay = new Window
        {
            Background          = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            WindowState         = WindowState.Maximized,
            WindowStyle         = WindowStyle.None,
            AllowsTransparency  = true,
            Topmost             = true,
            Cursor              = Cursors.Cross,
            ShowInTaskbar       = false,
            Content             = new TextBlock
            {
                Text                = "Click to select location  |  ESC to cancel",
                Foreground          = Brushes.White,
                Background          = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                FontSize            = 14,
                FontFamily          = new FontFamily("Segoe UI"),
                Padding             = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(0, 40, 0, 0)
            }
        };

        _overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) CancelPick(owner);
        };

        _overlay.Show();

        _proc  = HookCallback;
        _hookId = SetHook(_proc);
    }

    private void CancelPick(Window owner)
    {
        Cleanup(owner);
        PickCancelled?.Invoke();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONUP)
        {
            // Unhook first — prevents re-entry
            var hookId = _hookId;
            _hookId = IntPtr.Zero;
            NativeMethods.UnhookWindowsHookEx(hookId);

            NativeMethods.GetCursorPos(out var p);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlay?.Close();
                _overlay = null;
                LocationPicked?.Invoke(p.X, p.Y);
            });

            return (IntPtr)1; // Swallow the click
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void Cleanup(Window owner)
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        Application.Current.Dispatcher.Invoke(() =>
        {
            _overlay?.Close();
            _overlay = null;
            owner.WindowState = WindowState.Normal;
            owner.Activate();
        });
    }

    private static IntPtr SetHook(NativeMethods.LowLevelMouseProc proc)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var module = process.MainModule;
        return module is not null
            ? NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc,
                NativeMethods.GetModuleHandle(module.ModuleName), 0)
            : IntPtr.Zero;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}
