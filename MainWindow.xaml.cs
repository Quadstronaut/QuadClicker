using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isClicking = false;
        private const int F10 = 0x79;
        private HwndSource _source;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source.AddHook(HwndHook);
            RegisterHotKey(new WindowInteropHelper(this).Handle, F10, (uint)ModifierKeys.None, F10);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == F10)
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
            _isClicking = true;
            StartStopButton.Content = "Stop";
            StartStopButton.Background = Brushes.Red;
            _cancellationTokenSource = new CancellationTokenSource();

            int delay = ParseClickRate(ClickRateTextBox.Text);
            int idleTime = int.Parse(IdleTimeTextBox.Text);
            int stopAfterClicks = int.Parse(StopAfterClicksTextBox.Text);
            int stopAfterSeconds = int.Parse(StopAfterSecondsTextBox.Text);
            bool useCurrentPosition = CurrentPositionRadioButton.IsChecked == true;
            int x = 0, y = 0;
            if (!useCurrentPosition)
            {
                int.TryParse(XCoordinateTextBox.Text, out x);
                int.TryParse(YCoordinateTextBox.Text, out y);
            }

            try
            {
                await Task.Run(() => ClickLoop(delay, idleTime, stopAfterClicks, stopAfterSeconds, useCurrentPosition, x, y, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled
            }
            finally
            {
                StopClicking();
            }
        }

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

        private void ClickLoop(int delay, int idleTime, int stopAfterClicks, int stopAfterSeconds, bool useCurrentPosition, int x, int y, CancellationToken token)
        {
            int clicks = 0;
            DateTime startTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                if (stopAfterClicks > 0 && clicks >= stopAfterClicks) break;
                if (stopAfterSeconds > 0 && (DateTime.Now - startTime).TotalSeconds >= stopAfterSeconds) break;

                if (idleTime > 0)
                {
                    if (GetIdleTime() < idleTime * 1000)
                    {
                        Task.Delay(100, token).Wait(token);
                        continue;
                    }
                }

                if (!useCurrentPosition)
                {
                    SetCursorPos(x, y);
                }

                Click();
                clicks++;
                Task.Delay(delay, token).Wait(token);
            }
        }

        private int ParseClickRate(string text)
        {
            text = text.ToLower();
            if (text.EndsWith("ms"))
            {
                return int.Parse(text.Replace("ms", ""));
            }
            else if (text.Contains("times per second"))
            {
                double times = double.Parse(text.Split(' ')[0]);
                return (int)(1000 / times);
            }
            else if (text.Contains("times per minute"))
            {
                double times = double.Parse(text.Split(' ')[0]);
                return (int)(60000 / times);
            }
            return 100; // Default to 100ms
        }

        private void Click()
        {
            INPUT mouseDownInput = new INPUT();
            mouseDownInput.Type = 0; // INPUT_MOUSE
            mouseDownInput.Data.Mouse.Flags = 0x0002; // MOUSEEVENTF_LEFTDOWN

            INPUT mouseUpInput = new INPUT();
            mouseUpInput.Type = 0; // INPUT_MOUSE
            mouseUpInput.Data.Mouse.Flags = 0x0004; // MOUSEEVENTF_LEFTUP

            INPUT[] inputs = new INPUT[] { mouseDownInput, mouseUpInput };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInPut = new LASTINPUTINFO();
            lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
            GetLastInputInfo(ref lastInPut);

            return ((uint)Environment.TickCount - lastInPut.dwTime);
        }

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private Window _tempWindow;

        private async void PickLocationButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            await Task.Delay(200); 

            _proc = HookCallback;
            _hookID = SetHook(_proc);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _tempWindow = new Window
                {
                    Background = Brushes.Transparent,
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Topmost = true,
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = "Click to select location",
                        Foreground = Brushes.White,
                        Background = Brushes.Black,
                        Padding = new Thickness(5)
                    }
                };
                _tempWindow.Show();
            });
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)0x0202) // WM_LBUTTONUP
            {
                UnhookWindowsHookEx(_hookID);
                this.WindowState = WindowState.Normal;

                POINT p = new POINT();
                GetCursorPos(out p);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    XCoordinateTextBox.Text = p.X.ToString();
                    YCoordinateTextBox.Text = p.Y.ToString();
                    _tempWindow.Close();
                });
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(14, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            UnregisterHotKey(new WindowInteropHelper(this).Handle, F10);
            _source.RemoveHook(HwndHook);
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
        static extern bool GetCursorPos(out POINT lpPoint);

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

        private delegate void LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }


        #endregion
    }
}
