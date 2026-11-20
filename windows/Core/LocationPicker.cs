using QuadClicker.PInvoke;
using System.IO;
using System.Runtime.InteropServices;
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
    private CancellationTokenSource? _pickCts;
    private bool _disposed;

    /// <summary>Raised on the UI thread when the user clicks a point.</summary>
    internal event Action<int, int>? LocationPicked;

    /// <summary>Raised on the UI thread when the pick is cancelled via ESC.</summary>
    internal event Action? PickCancelled;

    internal void BeginPick(Window owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Cancel any in-progress pick delay
        _pickCts?.Cancel();
        _pickCts?.Dispose();
        _pickCts = new CancellationTokenSource();
        var cts = _pickCts;

        Log("BeginPick: minimizing owner");
        owner.WindowState = WindowState.Minimized;

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await Task.Delay(300, cts.Token);
                ShowOverlay(owner);
            }
            catch (OperationCanceledException) { Log("BeginPick: cancelled during delay"); }
            catch (Exception ex) { Log($"BeginPick: exception {ex.GetType().Name}: {ex.Message}"); }
        });
    }

    private void ShowOverlay(Window owner)
    {
        Log("ShowOverlay: creating overlay window");
        var hintLabel = new TextBlock
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
        };

        // Span the whole virtual desktop (all monitors), not just the monitor a
        // maximized window would land on.
        _overlay = new Window
        {
            Background            = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left                  = SystemParameters.VirtualScreenLeft,
            Top                   = SystemParameters.VirtualScreenTop,
            Width                 = SystemParameters.VirtualScreenWidth,
            Height                = SystemParameters.VirtualScreenHeight,
            WindowStyle           = WindowStyle.None,
            AllowsTransparency    = true,
            Topmost               = true,
            Cursor                = Cursors.Cross,
            ShowInTaskbar         = false,
            Content               = hintLabel
        };

        _overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) CancelPick(owner);
        };

        // Our own pick/cancel paths clear _overlay BEFORE calling Close(). If the
        // overlay is still current here, something else closed it (e.g. Alt+F4):
        // treat that as a cancel so the global hook is removed and the owner restored.
        _overlay.Closed += (sender, _) =>
        {
            if (!ReferenceEquals(sender, _overlay)) return;
            _overlay = null;
            CancelPick(owner);
        };

        _overlay.Show();
        Log("ShowOverlay: overlay shown");

        _proc   = HookCallback;
        _hookId = SetHook(_proc);
        int err = Marshal.GetLastWin32Error();
        Log($"ShowOverlay: SetHook returned 0x{_hookId.ToInt64():X}, GetLastError={err}");

        if (_hookId == IntPtr.Zero)
        {
            hintLabel.Text       = "Hook install failed — see %APPDATA%\\QuadClicker\\picker.log. ESC to cancel.";
            hintLabel.Foreground = Brushes.Red;
        }
    }

    private void CancelPick(Window owner)
    {
        Cleanup(owner);
        PickCancelled?.Invoke();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Swallow the button-down too. Passing it on while swallowing the up left
        // whatever window was under the cursor with a button stuck down.
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
            return (IntPtr)1;

        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONUP)
        {
            Log($"HookCallback: WM_LBUTTONUP captured");

            // Unhook first — prevents re-entry
            var hookId = _hookId;
            _hookId = IntPtr.Zero;
            NativeMethods.UnhookWindowsHookEx(hookId);

            NativeMethods.GetCursorPos(out var p);
            Log($"HookCallback: cursor at ({p.X}, {p.Y}), dispatching");

            // BeginInvoke (async) — never block inside a low-level hook callback
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Log("HookCallback: dispatcher action running, closing overlay and raising LocationPicked");
                CloseOverlay();
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
            CloseOverlay();
            // Show BEFORE restoring. BeginPick minimized the owner and MainWindow hides
            // itself when minimized. Setting WindowState on a hidden window only stores
            // the value, so a later Show() could bring the window back still minimized.
            owner.Show();
            owner.WindowState = WindowState.Normal;
            owner.Activate();
        });
    }

    // Clear the field first, then close: the overlay's Closed handler uses
    // "still current" to tell an outside close (Alt+F4) from our own.
    private void CloseOverlay()
    {
        var overlay = _overlay;
        _overlay = null;
        overlay?.Close();
    }

    private static IntPtr SetHook(NativeMethods.LowLevelMouseProc proc)
    {
        // Pass NULL — Win32 substitutes the EXE's HMODULE. Works for both
        // single-file (bundled) and regular publish modes; Marshal.GetHINSTANCE
        // returns -1 in single-file mode because managed assemblies are
        // embedded rather than loaded from disk.
        return NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc,
            NativeMethods.GetModuleHandle(null), 0);
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────
    // Writes to %APPDATA%\QuadClicker\picker.log so we can see what happens
    // across the BeginPick → ShowOverlay → SetHook → HookCallback → dispatch
    // chain when picking misbehaves on a user's machine.
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuadClicker", "picker.log");

    private static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { /* never let diagnostics break the picker */ }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pickCts?.Cancel();
            _pickCts?.Dispose();
            _pickCts = null;

            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                CloseOverlay();
            });

            _disposed = true;
        }
    }
}
