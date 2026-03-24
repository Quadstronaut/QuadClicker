using QuadClicker.Core;
using QuadClicker.Models;
using QuadClicker.PInvoke;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
// Disambiguate: System.Windows.Input also has a MouseButton enum
using QcMouseButton = QuadClicker.Models.MouseButton;
using QcClickType   = QuadClicker.Models.ClickType;

namespace QuadClicker;

public partial class MainWindow : Window
{
    // ── State ─────────────────────────────────────────────────────────────────
    private CancellationTokenSource? _cts;
    private bool _isClicking;

    // ── Core services ─────────────────────────────────────────────────────────
    private readonly ClickEngine    _engine    = new();
    private readonly LocationPicker _picker    = new();
    private readonly TrayManager    _tray      = new();
    private HotkeyManager?          _hotkeys;
    private int                     _startHotkeyId = -1;
    private int                     _stopHotkeyId  = -1;

    // ── Hotkey capture state ──────────────────────────────────────────────────
    private TextBox? _capturingHotkeyBox;

    // ── Settings ──────────────────────────────────────────────────────────────
    private AppSettings Settings => ((App)Application.Current).Settings;

    // ─────────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _picker.LocationPicked  += OnLocationPicked;
        _picker.PickCancelled   += OnPickCancelled;
        _engine.ClickCountUpdated += OnClickCountUpdated;
        _engine.StatusChanged     += OnEngineStatusChanged;

        _tray.ShowWindowRequested     += RestoreFromTray;
        _tray.ToggleClickingRequested += () => Dispatcher.Invoke(StartStopBtn_Click, null!, null!);
        _tray.QuitRequested           += () => Dispatcher.Invoke(Quit);
        _tray.Show();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);

        _hotkeys = new HotkeyManager(helper.Handle);
        LoadSettings();
    }

    // ── WndProc ───────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _hotkeys is not null)
        {
            int id = wParam.ToInt32();
            if (_hotkeys.HandleMessage(id))
                handled = true;
        }
        return IntPtr.Zero;
    }

    // ── UI Events ─────────────────────────────────────────────────────────────

    private void StartStopBtn_Click(object? sender, RoutedEventArgs? e)
    {
        if (_isClicking) StopClicking();
        else             StartClicking();
    }

    private async void StartClicking()
    {
        ClearError();
        if (!TryBuildSession(out var session, out string error))
        {
            ShowError(error);
            return;
        }

        _isClicking = true;
        SetButtonState(isClicking: true);
        _cts = new CancellationTokenSource();
        _tray.SetActiveState(true);

        try
        {
            await _engine.RunAsync(session!, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowError($"Error: {ex.Message}"));
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                StopClicking();
                SetStatus(EngineStatus.Stopped);
            });
        }
    }

    private void StopClicking()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isClicking = false;
        SetButtonState(isClicking: false);
        _tray.SetActiveState(false);
    }

    private void PickBtn_Click(object sender, RoutedEventArgs e) =>
        _picker.BeginPick(this);

    private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e) =>
        Topmost = AlwaysOnTopBox.IsChecked == true;

    // ── Hotkey capture ────────────────────────────────────────────────────────

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkeyBox = (TextBox)sender;
        _capturingHotkeyBox.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        box.BorderBrush = (Brush)FindResource("BorderBrush");
        _capturingHotkeyBox = null;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingHotkeyBox is null) return;
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            _capturingHotkeyBox.Text = string.Empty;
            _capturingHotkeyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            ReRegisterHotkeys();
            return;
        }

        // Ignore lone modifier keys
        if (e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
                  or Key.LeftAlt  or Key.RightAlt  or Key.LWin or Key.RWin or Key.System)
            return;

        var modifiers = Keyboard.Modifiers;
        string text = BuildHotkeyText(e.Key, modifiers);

        // Prevent both boxes having the same hotkey
        bool isSameAsOther = _capturingHotkeyBox == StartHotkeyBox
            ? text == StopHotkeyBox.Text
            : text == StartHotkeyBox.Text;

        if (isSameAsOther)
        {
            ShowError("Start and stop hotkeys cannot be the same.");
            return;
        }

        ClearError();
        _capturingHotkeyBox.Text = text;
        _capturingHotkeyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        ReRegisterHotkeys();
    }

    // ── Window Close → Tray ───────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true; // Intercept; hide to tray instead
        Hide();
    }

    private void RestoreFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void Quit()
    {
        SaveSettings();
        _hotkeys?.Dispose();
        _picker.Dispose();
        _tray.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        Application.Current.Shutdown();
    }

    // ── Engine callbacks ──────────────────────────────────────────────────────

    private void OnClickCountUpdated(int count)
    {
        Dispatcher.Invoke(() =>
        {
            ClickCountLabel.Visibility = Visibility.Visible;
            ClickCountLabel.Text = $"Clicks: {count}";
        });
    }

    private void OnEngineStatusChanged(EngineStatus status)
    {
        Dispatcher.Invoke(() => SetStatus(status));
    }

    private void OnLocationPicked(int x, int y)
    {
        XBox.Text = x.ToString();
        YBox.Text = y.ToString();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnPickCancelled()
    {
        WindowState = WindowState.Normal;
        Activate();
    }

    // ── Build session ─────────────────────────────────────────────────────────

    private bool TryBuildSession(out ClickSession? session, out string error)
    {
        session = null;
        error   = string.Empty;

        string rateText = ClickRateValueBox.Text.Trim();
        string unit     = (ClickRateUnitBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ms";
        string combined = unit == "ms" ? rateText + "ms" : rateText + unit;

        if (!ClickRateParser.TryParse(combined, out var delay, out error))
            return false;

        if (!int.TryParse(StopClicksBox.Text, out int stopClicks) || stopClicks < 0)
        { error = "Stop after (clicks) must be a non-negative integer."; return false; }

        if (!double.TryParse(StopSecondsBox.Text, out double stopSeconds) || stopSeconds < 0)
        { error = "Stop after (seconds) must be a non-negative number."; return false; }

        if (!double.TryParse(IdleBox.Text, out double idleWait) || idleWait < 0)
        { error = "Idle wait must be a non-negative number."; return false; }

        bool useCurrentPos = LocCurrent.IsChecked == true;
        int x = 0, y = 0;
        if (!useCurrentPos)
        {
            if (!int.TryParse(XBox.Text, out x) || !int.TryParse(YBox.Text, out y))
            { error = "X and Y coordinates must be valid integers."; return false; }
        }

        var button = BtnRight.IsChecked == true ? QcMouseButton.Right
                   : BtnMiddle.IsChecked == true ? QcMouseButton.Middle
                   : QcMouseButton.Left;

        var clickType = TypeDouble.IsChecked == true ? QcClickType.Double : QcClickType.Single;

        session = new ClickSession(delay, button, clickType, useCurrentPos, x, y,
                                   stopClicks, stopSeconds, idleWait);
        return true;
    }

    // ── Hotkey registration ───────────────────────────────────────────────────

    private void ReRegisterHotkeys()
    {
        if (_hotkeys is null) return;

        if (_startHotkeyId >= 0) { _hotkeys.Unregister(_startHotkeyId); _startHotkeyId = -1; }
        if (_stopHotkeyId  >= 0) { _hotkeys.Unregister(_stopHotkeyId);  _stopHotkeyId  = -1; }

        if (TryParseHotkeyText(StartHotkeyBox.Text, out uint smod, out uint svk))
        {
            _startHotkeyId = _hotkeys.Register(smod, svk, () =>
                Dispatcher.Invoke(() => { if (!_isClicking) StartClicking(); }));
        }

        if (TryParseHotkeyText(StopHotkeyBox.Text, out uint emod, out uint evk))
        {
            _stopHotkeyId = _hotkeys.Register(emod, evk, () =>
                Dispatcher.Invoke(() => { if (_isClicking) StopClicking(); }));
        }
    }

    private static string BuildHotkeyText(Key key, ModifierKeys mods)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Alt))      parts.Add("Alt");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static bool TryParseHotkeyText(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+');
        string keyPart = parts[^1];

        foreach (var mod in parts[..^1])
        {
            modifiers |= mod.Trim().ToUpperInvariant() switch
            {
                "CTRL"  => 0x0002u,
                "SHIFT" => 0x0004u,
                "ALT"   => 0x0001u,
                _       => 0u
            };
        }

        if (!Enum.TryParse<Key>(keyPart.Trim(), ignoreCase: true, out var parsedKey)) return false;
        var converter = new KeyConverter();
        var str = converter.ConvertToString(parsedKey);
        // Map Key enum to VK code via KeyInterop
        vk = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
        return vk != 0;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void SetButtonState(bool isClicking)
    {
        if (isClicking)
        {
            StartStopBtn.Content = "STOP";
            StartStopBtn.Style   = (Style)FindResource("StopButtonStyle");
        }
        else
        {
            StartStopBtn.Content = "START";
            StartStopBtn.Style   = (Style)FindResource("StartButtonStyle");
            ClickCountLabel.Visibility = Visibility.Collapsed;
        }
    }

    private void SetStatus(EngineStatus status)
    {
        switch (status)
        {
            case EngineStatus.Clicking:
                StatusDot.Fill   = (Brush)FindResource("AccentBrush");
                StatusLabel.Text = "Clicking";
                StatusLabel.Foreground = (Brush)FindResource("AccentBrush");
                break;
            case EngineStatus.WaitingForIdle:
                StatusDot.Fill   = (Brush)FindResource("StatusWaitingBrush");
                StatusLabel.Text = "Waiting for idle…";
                StatusLabel.Foreground = (Brush)FindResource("StatusWaitingBrush");
                break;
            default:
                StatusDot.Fill   = (Brush)FindResource("TextDisabledBrush");
                StatusLabel.Text = "Stopped";
                StatusLabel.Foreground = (Brush)FindResource("TextSecondaryBrush");
                break;
        }
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text       = msg;
        ErrorLabel.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorLabel.Text       = string.Empty;
        ErrorLabel.Visibility = Visibility.Collapsed;
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        var s = Settings;
        ClickRateValueBox.Text = s.ClickRateValue;

        foreach (ComboBoxItem item in ClickRateUnitBox.Items)
            if ((string)item.Tag == s.ClickRateUnit) item.IsSelected = true;

        BtnLeft.IsChecked   = s.Button == QcMouseButton.Left;
        BtnRight.IsChecked  = s.Button == QcMouseButton.Right;
        BtnMiddle.IsChecked = s.Button == QcMouseButton.Middle;

        TypeSingle.IsChecked = s.ClickType == QcClickType.Single;
        TypeDouble.IsChecked = s.ClickType == QcClickType.Double;

        LocCurrent.IsChecked = s.UseCurrentPosition;
        LocFixed.IsChecked   = !s.UseCurrentPosition;
        XBox.Text = s.X.ToString();
        YBox.Text = s.Y.ToString();

        StopClicksBox.Text  = s.StopAfterClicks.ToString();
        StopSecondsBox.Text = s.StopAfterSeconds.ToString();
        IdleBox.Text        = s.IdleWaitSeconds.ToString();

        AlwaysOnTopBox.IsChecked = s.AlwaysOnTop;
        Topmost = s.AlwaysOnTop;

        StartHotkeyBox.Text = s.StartHotkeyText;
        StopHotkeyBox.Text  = s.StopHotkeyText;

        ReRegisterHotkeys();
    }

    private void SaveSettings()
    {
        var s = Settings;
        s.ClickRateValue = ClickRateValueBox.Text;
        s.ClickRateUnit  = (ClickRateUnitBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ms";

        s.Button     = BtnRight.IsChecked == true ? QcMouseButton.Right
                     : BtnMiddle.IsChecked == true ? QcMouseButton.Middle
                     : QcMouseButton.Left;
        s.ClickType  = TypeDouble.IsChecked == true ? QcClickType.Double : QcClickType.Single;

        s.UseCurrentPosition = LocCurrent.IsChecked == true;
        int.TryParse(XBox.Text, out int px); s.X = px;
        int.TryParse(YBox.Text, out int py); s.Y = py;

        int.TryParse(StopClicksBox.Text, out int sc); s.StopAfterClicks = sc;
        double.TryParse(StopSecondsBox.Text, out double ss); s.StopAfterSeconds = (int)ss;
        double.TryParse(IdleBox.Text,        out double iw); s.IdleWaitSeconds  = (int)iw;

        s.AlwaysOnTop    = AlwaysOnTopBox.IsChecked == true;
        s.StartHotkeyText = StartHotkeyBox.Text;
        s.StopHotkeyText  = StopHotkeyBox.Text;

        s.Save();
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettings();
        _hotkeys?.Dispose();
        _picker.Dispose();
        _tray.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnClosed(e);
    }
}
