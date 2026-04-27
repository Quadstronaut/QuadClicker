using QuadClicker.Core;
using QuadClicker.Models;
using QuadClicker.PInvoke;
using System.Reflection;
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

    // ── Click rate UI state ───────────────────────────────────────────────────
    private bool _rateUiReady;

    private static readonly (string Tag, string Display)[] DelayUnits =
    {
        ("ms",  "ms"),
        ("sec", "seconds"),
        ("min", "minutes"),
    };

    private static readonly (string Tag, string Display)[] FrequencyUnits =
    {
        ("per_sec",  "per second"),
        ("per_min",  "per minute"),
        ("per_hour", "per hour"),
    };

    // ── Settings ──────────────────────────────────────────────────────────────
    private AppSettings Settings => ((App)Application.Current).Settings;

    // ── Self-update state ─────────────────────────────────────────────────────
    private UpdateCheckResult? _pendingUpdate;
    private static readonly TimeSpan UpdateCheckMinInterval = TimeSpan.FromHours(6);

    // ─────────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");
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
        ShowPostUpdateMessageIfRequested();
        _ = TryStartUpdateCheckAsync();
    }

    // ── Self-update ───────────────────────────────────────────────────────────

    private void ShowPostUpdateMessageIfRequested()
    {
        var app = (App)Application.Current;
        if (!app.Properties.Contains("PostUpdateVersion")) return;

        string ver = app.Properties["PostUpdateVersion"] as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ver))
            ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

        UpdateBannerHeadline.Text = $"Updated to v{ver}";
        UpdateBannerSubtext.Text  = "Your settings were preserved.";
        UpdateBtn.Visibility           = Visibility.Collapsed;
        UpdateSkipVersionBtn.Visibility = Visibility.Collapsed;
        UpdateSkipBtn.Content     = "Dismiss";
        UpdateBanner.Visibility   = Visibility.Visible;
    }

    private async Task TryStartUpdateCheckAsync()
    {
        var app = (App)Application.Current;
        if (app.Properties.Contains("NoUpdateCheck")) return;
        if (!Settings.UpdateCheckEnabled)             return;

        if (DateTime.UtcNow - Settings.LastCheckUtc < UpdateCheckMinInterval) return;

        string current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        UpdateCheckResult result;
        try
        {
            result = await Task.Run(() => UpdateChecker.CheckAsync(current));
        }
        catch
        {
            return; // never block the UI on a failed check
        }

        Settings.LastCheckUtc = DateTime.UtcNow;

        if (!result.HasUpdate) return;
        if (!string.IsNullOrEmpty(Settings.SkippedVersion) &&
            string.Equals(Settings.SkippedVersion, result.LatestVersion, StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.Invoke(() => ShowUpdateBanner(result));
    }

    private void ShowUpdateBanner(UpdateCheckResult result)
    {
        _pendingUpdate = result;
        UpdateBannerHeadline.Text = $"QuadClicker v{result.LatestVersion} is available";
        UpdateBannerSubtext.Text  = "Click Update to download and install automatically.";
        UpdateBtn.Visibility           = Visibility.Visible;
        UpdateSkipVersionBtn.Visibility = Visibility.Visible;
        UpdateSkipBtn.Content     = "Skip";
        UpdateBanner.Visibility   = Visibility.Visible;
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null) return;

        UpdateBtn.IsEnabled       = false;
        UpdateSkipBtn.IsEnabled   = false;
        UpdateSkipVersionBtn.IsEnabled = false;
        UpdateBannerSubtext.Text  = "Downloading…";

        try
        {
            string exe = Updater.GetRunningExePath();
            await Task.Run(() => Updater.DownloadAndStageAsync(_pendingUpdate, exe));
            UpdateBannerSubtext.Text = "Installing — the app will restart.";
            // Persist before shutdown so the new build sees the latest settings.
            SaveSettings();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateBannerHeadline.Text = "Update failed";
            UpdateBannerSubtext.Text  = ex.Message;
            UpdateBtn.IsEnabled       = true;
            UpdateSkipBtn.IsEnabled   = true;
            UpdateSkipVersionBtn.IsEnabled = true;
        }
    }

    private void UpdateSkip_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
    }

    private void UpdateSkipVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not null)
        {
            Settings.SkippedVersion = _pendingUpdate.LatestVersion;
            Settings.Save();
        }
        UpdateBanner.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
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
        // Allow close — OnClosed handles cleanup and app exits via OnMainWindowClose
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
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

    private void Quit() => Close(); // OnClosed handles cleanup

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
        // Window_StateChanged hid the window when BeginPick minimized it; Show() is needed to undo that.
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnPickCancelled()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ── Build session ─────────────────────────────────────────────────────────

    private bool TryBuildSession(out ClickSession? session, out string error)
    {
        session = null;
        error   = string.Empty;

        if (!ClickRateParser.TryParse(ComposeRateString(), out var delay, out error))
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

    // ── Click rate UI ─────────────────────────────────────────────────────────

    private void ClickRateMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        bool isFrequency = ModeFrequency.IsChecked == true;
        var units = isFrequency ? FrequencyUnits : DelayUnits;
        string fallback = isFrequency ? "per_sec" : "ms";

        // Try to keep the user's saved unit if it belongs to the new mode; otherwise fallback.
        string desired = (ClickRateUnitBox.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;
        PopulateUnitBox(units, desired, fallback);
        UpdateRateHint();
    }

    private void ClickRateInput_Changed(object sender, TextChangedEventArgs e) =>
        UpdateRateHint();

    private void ClickRateUnit_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateRateHint();

    private void PopulateUnitBox((string Tag, string Display)[] units, string desiredTag, string fallbackTag)
    {
        ClickRateUnitBox.Items.Clear();
        ComboBoxItem? toSelect = null;
        ComboBoxItem? fallback = null;
        foreach (var (tag, display) in units)
        {
            var item = new ComboBoxItem { Content = display, Tag = tag };
            ClickRateUnitBox.Items.Add(item);
            if (tag == desiredTag) toSelect = item;
            if (tag == fallbackTag) fallback = item;
        }
        (toSelect ?? fallback ?? (ComboBoxItem)ClickRateUnitBox.Items[0]!).IsSelected = true;
    }

    private string ComposeRateString()
    {
        string value = ClickRateValueBox.Text.Trim();
        string unit  = (ClickRateUnitBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ms";
        // Translate canonical tag → parser DSL suffix.
        return unit switch
        {
            "ms"       => value + "ms",
            "sec"      => value + "s",
            "min"      => value + "min",
            "per_sec"  => value + "/s",
            "per_min"  => value + "/min",
            "per_hour" => value + "/h",
            _          => value + "ms",
        };
    }

    private void UpdateRateHint()
    {
        if (!_rateUiReady || RateHintLabel == null) return;

        if (string.IsNullOrWhiteSpace(ClickRateValueBox.Text))
        {
            RateHintLabel.Text = string.Empty;
            return;
        }

        if (!ClickRateParser.TryParse(ComposeRateString(), out var delay, out _))
        {
            RateHintLabel.Foreground = (Brush)FindResource("TextSecondaryBrush");
            RateHintLabel.Text = string.Empty;
            return;
        }

        bool isDelayMode = ModeDelay.IsChecked == true;
        double cps = 1000.0 / delay.TotalMilliseconds;
        bool veryFast = cps > 100.0;

        string conversion = isDelayMode ? FormatRate(cps) : FormatDelay(delay);

        if (veryFast)
        {
            RateHintLabel.Foreground = (Brush)FindResource("DangerBrush");
            RateHintLabel.Text = $"⚠ Very fast — input may not register reliably  (≈ {conversion})";
        }
        else
        {
            RateHintLabel.Foreground = (Brush)FindResource("TextSecondaryBrush");
            RateHintLabel.Text = $"≈ {conversion}";
        }
    }

    private static string FormatRate(double cps)
    {
        if (cps >= 1.0)
            return $"{Trim(cps)} clicks/sec";
        double cpm = cps * 60.0;
        if (cpm >= 1.0)
            return $"{Trim(cpm)} clicks/min";
        double cph = cps * 3600.0;
        return $"{Trim(cph)} clicks/hour";
    }

    private static string FormatDelay(TimeSpan delay)
    {
        double ms = delay.TotalMilliseconds;
        if (ms < 1000)         return $"{Trim(ms)} ms between clicks";
        if (ms < 60_000)       return $"{Trim(ms / 1000.0)} sec between clicks";
        if (ms < 3_600_000)    return $"{Trim(ms / 60_000.0)} min between clicks";
        return $"{Trim(ms / 3_600_000.0)} hours between clicks";
    }

    private static string Trim(double v)
    {
        // Show integers without ".0", otherwise up to 2 decimals.
        if (Math.Abs(v - Math.Round(v)) < 0.005)
            return ((long)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

        // Mode + units must be set BEFORE wiring rate hint, to avoid handler thrash.
        if (s.ClickRateMode == ClickRateMode.Frequency)
        {
            ModeFrequency.IsChecked = true;
            PopulateUnitBox(FrequencyUnits, s.ClickRateUnit, fallbackTag: "per_sec");
        }
        else
        {
            ModeDelay.IsChecked = true;
            PopulateUnitBox(DelayUnits, s.ClickRateUnit, fallbackTag: "ms");
        }
        ClickRateValueBox.Text = s.ClickRateValue;
        _rateUiReady = true;
        UpdateRateHint();

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
        s.ClickRateMode  = ModeFrequency.IsChecked == true ? ClickRateMode.Frequency : ClickRateMode.Delay;
        s.ClickRateValue = ClickRateValueBox.Text;
        s.ClickRateUnit  = (ClickRateUnitBox.SelectedItem as ComboBoxItem)?.Tag as string
                           ?? (s.ClickRateMode == ClickRateMode.Frequency ? "per_sec" : "ms");

        s.Button     = BtnRight.IsChecked == true ? QcMouseButton.Right
                     : BtnMiddle.IsChecked == true ? QcMouseButton.Middle
                     : QcMouseButton.Left;
        s.ClickType  = TypeDouble.IsChecked == true ? QcClickType.Double : QcClickType.Single;

        s.UseCurrentPosition = LocCurrent.IsChecked == true;
        int.TryParse(XBox.Text, out int px); s.X = px;
        int.TryParse(YBox.Text, out int py); s.Y = py;

        int.TryParse(StopClicksBox.Text, out int sc); s.StopAfterClicks = sc;
        double.TryParse(StopSecondsBox.Text, out double ss); s.StopAfterSeconds = ss;
        double.TryParse(IdleBox.Text,        out double iw); s.IdleWaitSeconds  = iw;

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
