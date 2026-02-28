using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuadClicker
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isClicking = false;
        private const int HotkeyId = 0x79; // F10 virtual key / hotkey ID
        private const uint VK_F10 = 0x79;
        private HwndSource? _source;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);
            RegisterHotKey(helper.Handle, HotkeyId, (uint)ModifierKeys.None, VK_F10);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                if (_isClicking)
                {
                    StopClicking();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isClicking)
            {
                StopClicking();
            }
            else
            {
                StartClicking();
            }
        }

        private async void StartClicking()
        {
            // Validate and parse all inputs before starting
            if (!TryParseClickRate(ClickRateTextBox.Text, out int delay))
            {
                MessageBox.Show(
                    "Invalid click rate. Use formats like: 100ms, 10 times per second, 600 times per minute.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(IdleTimeTextBox.Text, out int idleTime) || idleTime < 0)
            {
                MessageBox.Show("Idle Time must be a non-negative integer.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(StopAfterClicksTextBox.Text, out int stopAfterClicks) || stopAfterClicks < 0)
            {
                MessageBox.Show("Stop After (clicks) must be a non-negative integer.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(StopAfterSecondsTextBox.Text, out int stopAfterSeconds) || stopAfterSeconds < 0)
            {
                MessageBox.Show("Stop After (seconds) must be a non-negative integer.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool useCurrentPosition = CurrentPositionRadioButton.IsChecked == true;
            int x = 0, y = 0;
            if (!useCurrentPosition)
            {
                if (!int.TryParse(XCoordinateTextBox.Text, out x) ||
                    !int.TryParse(YCoordinateTextBox.Text, out y))
                {
                    MessageBox.Show("X and Y coordinates must be valid integers.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _isClicking = true;
            StartStopButton.Content = "Stop";
            StartStopButton.Background = Brushes.Red;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                await Task.Run(() =>
                    ClickLoop(delay, idleTime, stopAfterClicks, stopAfterSeconds,
                              useCurrentPosition, x, y, token), token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopped via button or hotkey — do nothing
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                // Always restore UI on the UI thread
                Dispatcher.Invoke(StopClicking);
            }
        }

        /// <summary>
        /// Resets the UI to the stopped state. Safe to call multiple times.
        /// Must be called on the UI thread.
        /// </summary>
        private void StopClicking()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
            _isClicking = false;
            StartStopButton.Content = "Start";
            StartStopButton.Background = Brushes.Green;
        }

        private void ClickLoop(int delay, int idleTime, int stopAfterClicks, int stopAfterSeconds,
                               bool useCurrentPosition, int x, int y, CancellationToken token)
        {
            int clicks = 0;
            DateTime startTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                if (stopAfterClicks > 0 && clicks >= stopAfterClicks) break;
                if (stopAfterSeconds > 0 && (DateTime.Now - startTime).TotalSeconds >= stopAfterSeconds) break;

                // Wait for system idle if required
                if (idleTime > 0)
                {
                    uint idleThresholdMs = (uint)(idleTime * 1000);
                    while (GetIdleTime() < idleThresholdMs && !token.IsCancellationRequested)
                    {
                        token.WaitHandle.WaitOne(100);
                    }
                    if (token.IsCancellationRequested) break;
                }

                if (!useCurrentPosition)
                {
                    SetCursorPos(x, y);
                }

                Click();
                clicks++;

                // Delay between clicks — exit early on cancellation
                if (delay > 0)
                {
                    token.WaitHandle.WaitOne(delay);
                }
            }
        }

        /// <summary>
        /// Parses the click rate string into a delay in milliseconds.
        /// Accepted formats: "100ms", "10 times per second", "600 times per minute".
        /// Returns false if the input cannot be parsed.
        /// </summary>
        private bool TryParseClickRate(string text, out int delayMs)
        {
            delayMs = 100; // safe default
            text = text.Trim().ToLowerInvariant();

            if (text.EndsWith("ms"))
            {
                string numPart = text[..^2].Trim();
                if (int.TryParse(numPart, out int ms) && ms > 0)
                {
                    delayMs = ms;
                    return true;
                }
                return false;
            }

            if (text.Contains("times per second"))
            {
                string numPart = text.Replace("times per second", "").Trim();
                if (double.TryParse(numPart, out double tps) && tps > 0)
                {
                    delayMs = (int)(1000.0 / tps);
                    return true;
                }
                return false;
            }

            if (text.Contains("times per minute"))
            {
                string numPart = text.Replace("times per minute", "").Trim();
                if (double.TryParse(numPart, out double tpm) && tpm > 0)
                {
                    delayMs = (int)(60000.0 / tpm);
                    return true;
                }
                return false;
            }

            // Last-chance: bare integer treated as milliseconds
            if (int.TryParse(text, out int bare) && bare > 0)
            {
                delayMs = bare;
                return true;
            }

            return false;
        }

        private void Click()
        {
            INPUT mouseDownInput = new INPUT
            {
                Type = 0 // INPUT_MOUSE
            };
            mouseDownInput.Data.Mouse.Flags = 0x0002; // MOUSEEVENTF_LEFTDOWN

            INPUT mouseUpInput = new INPUT
            {
                Type = 0 // INPUT_MOUSE
            };
            mouseUpInput.Data.Mouse.Flags = 0x0004; // MOUSEEVENTF_LEFTUP

            INPUT[] inputs = new INPUT[] { mouseDownInput, mouseUpInput };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);
            return unchecked((uint)Environment.TickCount - lastInput.dwTime);
        }

        // ── Pick-Location ──────────────────────────────────────────────────────

        private LowLevelMouseProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private Window? _tempWindow;

        private async void PickLocationButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            await Task.Delay(300); // Give the window time to minimise before showing overlay

            Application.Current.Dispatcher.Invoke(() =>
            {
                _tempWindow = new Window
                {
                    Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), // Near-transparent but hit-testable
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Topmost = true,
                    Cursor = Cursors.Cross,
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = "Click anywhere to select that location",
                        Foreground = Brushes.White,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        FontSize = 16,
                        Padding = new Thickness(12, 8, 12, 8),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 40, 0, 0)
                    }
                };
                _tempWindow.Show();
            });

            // Install the low-level mouse hook after the overlay is shown
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_LBUTTONUP = 0x0202;
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONUP)
            {
                // Unhook immediately so we don't fire again
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;

                GetCursorPos(out POINT p);

                Dispatcher.Invoke(() =>
                {
                    XCoordinateTextBox.Text = p.X.ToString();
                    YCoordinateTextBox.Text = p.Y.ToString();
                    _tempWindow?.Close();
                    _tempWindow = null;
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                });

                return (IntPtr)1; // Swallow this click so it doesn't land on whatever is underneath
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                return SetWindowsHookEx(14 /* WH_MOUSE_LL */, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up the hotkey and hook
            UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
            _source?.RemoveHook(HwndHook);

            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            base.OnClosed(e);
        }

        #region P/Invoke

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion
    }
}