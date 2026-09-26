using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Wpf.Ui.Controls;

namespace MuteMic;

internal partial class SettingsWindow : FluentWindow
{
    private sealed record DeviceItem(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record KeyOption(System.Windows.Forms.Keys Key, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly System.Windows.Media.Brush DotUnselectedFill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3a, 0x3d, 0x44));
    private static readonly System.Windows.Media.Brush DotUnselectedStroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x56, 0x59, 0x5f));
    private static readonly System.Windows.Media.Brush DotSelectedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7c, 0x96, 0xff));

    private readonly CoreAudioController _controller;
    private readonly AppSettings _settings;
    private readonly List<(System.Windows.Forms.Screen Screen, System.Windows.Controls.CheckBox Box)> _monitorChecks = [];
    private OverlayPosition _selectedPosition;
    private IDisposable? _liveMuteSub;
    private bool _isLoading = true;

    // Fires after every change is written to disk, so the tray app can re-apply live
    // (re-register the hotkey, rebuild overlays, etc.) without a Save button.
    public event Action? SettingsChanged;

    public SettingsWindow(CoreAudioController controller, AppSettings settings)
    {
        InitializeComponent();
        _controller = controller;
        _settings = settings;

        LoadDevices();
        LoadHotkey();
        LoadOverlay();
        UpdateHomeSummary();
        LoadAbout();
        _isLoading = false;

        Closed += (_, _) => _liveMuteSub?.Dispose();
    }

    private void OnChanged(object sender, RoutedEventArgs e) => SaveAndApply();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SaveAndApply();

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = (string)((Wpf.Ui.Controls.Button)sender).Tag;
        HomeSection.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        MicSection.Visibility = tag == "mic" ? Visibility.Visible : Visibility.Collapsed;
        ForceAudioSection.Visibility = tag == "force" ? Visibility.Visible : Visibility.Collapsed;
        OverlaySection.Visibility = tag == "overlay" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;

        foreach (var button in new[] { NavHomeButton, NavMicButton, NavForceAudioButton, NavOverlayButton, NavAboutButton })
            button.Appearance = ReferenceEquals(button, sender) ? ControlAppearance.Secondary : ControlAppearance.Transparent;
    }

    private void LoadAbout()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "dev build" : $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void GitHubCard_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/Kurohiko-id/Kuro-Mute-Force-Mic-Input");
    private void SaweriaCard_Click(object sender, RoutedEventArgs e) => OpenUrl("https://saweria.co/Kurohiko");
    private void TopUpCard_Click(object sender, RoutedEventArgs e) => OpenUrl("https://kurohikotopup.com");

    private void LoadDevices()
    {
        var captures = _controller.GetCaptureDevices(DeviceState.Active).Select(d => new DeviceItem(d.Id, d.FullName)).ToList();
        var playbacks = _controller.GetPlaybackDevices(DeviceState.Active).Select(d => new DeviceItem(d.Id, d.FullName)).ToList();

        MicCombo.ItemsSource = captures;
        InputCombo.ItemsSource = captures;
        OutputCombo.ItemsSource = playbacks;

        MicCombo.SelectedItem = captures.FirstOrDefault(c => c.Id == _settings.MicDeviceId) ?? captures.FirstOrDefault();
        InputCombo.SelectedItem = captures.FirstOrDefault(c => c.Id == _settings.DefaultInputId) ?? captures.FirstOrDefault();
        OutputCombo.SelectedItem = playbacks.FirstOrDefault(c => c.Id == _settings.DefaultOutputId) ?? playbacks.FirstOrDefault();

        ForceOnStartupToggle.IsChecked = _settings.ForceAudioOnStartup;
        WatchChangesToggle.IsChecked = _settings.WatchAudioChanges;
    }

    private void LoadHotkey()
    {
        var keyOptions = BuildKeyOptions().ToList();
        KeyCombo.ItemsSource = keyOptions;
        KeyCombo.SelectedItem = keyOptions.FirstOrDefault(k => k.Key == (System.Windows.Forms.Keys)_settings.HotkeyKey) ?? keyOptions[0];

        CtrlCheck.IsChecked = (_settings.HotkeyModifiers & HotkeyManager.MOD_CONTROL) != 0;
        AltCheck.IsChecked = (_settings.HotkeyModifiers & HotkeyManager.MOD_ALT) != 0;
        ShiftCheck.IsChecked = (_settings.HotkeyModifiers & HotkeyManager.MOD_SHIFT) != 0;
    }

    private void LoadOverlay()
    {
        ShowToastToggle.IsChecked = _settings.ShowToastOverlay;
        ShowBorderToggle.IsChecked = _settings.ShowBorderGlow;
        PersistToggle.IsChecked = _settings.OverlayPersistWhileMuted;

        _selectedPosition = _settings.OverlayPosition;
        UpdatePositionDots();

        ScaleSlider.Value = Math.Clamp(_settings.OverlayScale * 100, ScaleSlider.Minimum, ScaleSlider.Maximum);
        ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
        ScaleSlider.ValueChanged += (_, _) => ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
        // Save on drag-release / key-adjust, not on every ValueChanged tick, so dragging doesn't hammer disk.
        ScaleSlider.PreviewMouseUp += (_, _) => SaveAndApply();
        ScaleSlider.KeyUp += (_, _) => SaveAndApply();

        ThicknessSlider.Value = Math.Clamp(_settings.BorderThickness, ThicknessSlider.Minimum, ThicknessSlider.Maximum);
        ThicknessValueText.Text = $"{(int)ThicknessSlider.Value}px";
        ThicknessSlider.ValueChanged += (_, _) => ThicknessValueText.Text = $"{(int)ThicknessSlider.Value}px";
        ThicknessSlider.PreviewMouseUp += (_, _) => SaveAndApply();
        ThicknessSlider.KeyUp += (_, _) => SaveAndApply();

        MonitorPanel.Children.Clear();
        _monitorChecks.Clear();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var label = $"{(screen.Primary ? "Primary monitor" : "Monitor")} · {screen.Bounds.Width}×{screen.Bounds.Height}";
            var box = new System.Windows.Controls.CheckBox
            {
                Content = label,
                Margin = new Thickness(0, 4, 0, 4),
                IsChecked = _settings.OverlayAllMonitors || _settings.OverlayMonitorIds.Contains(screen.DeviceName),
            };
            box.Checked += OnChanged;
            box.Unchecked += OnChanged;
            _monitorChecks.Add((screen, box));
            MonitorPanel.Children.Add(box);
        }

        AutoStartToggle.IsChecked = _settings.AutoStart;
    }

    private void PositionDot_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedPosition = Enum.Parse<OverlayPosition>((string)((Ellipse)sender).Tag);
        UpdatePositionDots();
        SaveAndApply();
    }

    private void UpdatePositionDots()
    {
        (Ellipse Dot, OverlayPosition Position)[] dots =
        [
            (PosTopLeft, OverlayPosition.TopLeft), (PosTopCenter, OverlayPosition.TopCenter), (PosTopRight, OverlayPosition.TopRight),
            (PosBottomLeft, OverlayPosition.BottomLeft), (PosBottomCenter, OverlayPosition.BottomCenter), (PosBottomRight, OverlayPosition.BottomRight),
        ];
        foreach (var (dot, position) in dots)
        {
            var selected = position == _selectedPosition;
            dot.Fill = selected ? DotSelectedBrush : DotUnselectedFill;
            dot.Stroke = selected ? DotSelectedBrush : DotUnselectedStroke;
        }
    }

    private void UpdateHomeSummary()
    {
        var micName = (MicCombo.SelectedItem as DeviceItem)?.Name ?? "not selected";
        StatusText.Text = $"Mic: {micName}";

        var mods = new List<string>();
        if (CtrlCheck.IsChecked == true) mods.Add("Ctrl");
        if (AltCheck.IsChecked == true) mods.Add("Alt");
        if (ShiftCheck.IsChecked == true) mods.Add("Shift");
        mods.Add((KeyCombo.SelectedItem as KeyOption)?.Label ?? "M");
        HotkeySummaryText.Text = string.Join(" + ", mods);

        var modes = new List<string>();
        if (ShowToastToggle.IsChecked == true) modes.Add("Toast");
        if (ShowBorderToggle.IsChecked == true) modes.Add("Screen border");
        OverlaySummaryText.Text = modes.Count > 0 ? string.Join(" + ", modes) : "Off (no overlay while muted)";
    }

    private void SaveAndApply()
    {
        if (_isLoading)
            return;

        _settings.MicDeviceId = (MicCombo.SelectedItem as DeviceItem)?.Id;
        _settings.DefaultInputId = (InputCombo.SelectedItem as DeviceItem)?.Id;
        _settings.DefaultOutputId = (OutputCombo.SelectedItem as DeviceItem)?.Id;
        _settings.ForceAudioOnStartup = ForceOnStartupToggle.IsChecked == true;
        _settings.WatchAudioChanges = WatchChangesToggle.IsChecked == true;

        _settings.HotkeyModifiers =
            (CtrlCheck.IsChecked == true ? HotkeyManager.MOD_CONTROL : 0u) |
            (AltCheck.IsChecked == true ? HotkeyManager.MOD_ALT : 0u) |
            (ShiftCheck.IsChecked == true ? HotkeyManager.MOD_SHIFT : 0u);
        _settings.HotkeyKey = (uint)((KeyCombo.SelectedItem as KeyOption)?.Key ?? System.Windows.Forms.Keys.M);

        _settings.ShowToastOverlay = ShowToastToggle.IsChecked == true;
        _settings.ShowBorderGlow = ShowBorderToggle.IsChecked == true;
        _settings.OverlayPersistWhileMuted = PersistToggle.IsChecked == true;
        _settings.OverlayPosition = _selectedPosition;
        _settings.OverlayScale = ScaleSlider.Value / 100.0;
        _settings.BorderThickness = (int)ThicknessSlider.Value;

        var allChecked = _monitorChecks.All(m => m.Box.IsChecked == true);
        _settings.OverlayAllMonitors = allChecked;
        _settings.OverlayMonitorIds = allChecked
            ? []
            : _monitorChecks.Where(m => m.Box.IsChecked == true).Select(m => m.Screen.DeviceName).ToList();

        _settings.AutoStart = AutoStartToggle.IsChecked == true;

        _settings.Save();
        UpdateHomeSummary();
        SettingsChanged?.Invoke();
    }

    private static IEnumerable<KeyOption> BuildKeyOptions()
    {
        for (char c = 'A'; c <= 'Z'; c++)
            yield return new KeyOption(Enum.Parse<System.Windows.Forms.Keys>(c.ToString()), c.ToString());

        for (var d = 0; d <= 9; d++)
            yield return new KeyOption(Enum.Parse<System.Windows.Forms.Keys>("D" + d), d.ToString());

        for (var f = 1; f <= 24; f++)
            yield return new KeyOption(Enum.Parse<System.Windows.Forms.Keys>("F" + f), "F" + f);

        (System.Windows.Forms.Keys Key, string Label)[] symbols =
        [
            (System.Windows.Forms.Keys.OemMinus, "-"), (System.Windows.Forms.Keys.Oemplus, "="),
            (System.Windows.Forms.Keys.Oemcomma, ","), (System.Windows.Forms.Keys.OemPeriod, "."),
            (System.Windows.Forms.Keys.OemQuestion, "/"), (System.Windows.Forms.Keys.OemSemicolon, ";"),
            (System.Windows.Forms.Keys.OemQuotes, "'"), (System.Windows.Forms.Keys.OemOpenBrackets, "["),
            (System.Windows.Forms.Keys.OemCloseBrackets, "]"), (System.Windows.Forms.Keys.OemPipe, "\\"),
            (System.Windows.Forms.Keys.Oemtilde, "`"),
        ];
        foreach (var s in symbols)
            yield return new KeyOption(s.Key, s.Label);
    }
}
