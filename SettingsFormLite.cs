using System.Diagnostics;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace MuteMic;

// Lite edition only: plain-WinForms equivalent of SettingsWindow.xaml, no WPF at all.
// Tab layout and stock system look inspired by the classic xenolightning AudioSwitcher —
// default WinForms colors throughout, no custom theming.
internal sealed class SettingsFormLite : Form
{
    private sealed record DeviceItem(Guid Id, string Name) { public override string ToString() => Name; }
    private sealed record PositionOption(OverlayPosition Value, string Label) { public override string ToString() => Label; }
    private sealed record KeyOption(Keys Key, string Label) { public override string ToString() => Label; }

    private readonly CoreAudioController _controller;
    private readonly AppSettings _settings;
    private bool _isLoading = true;

    public event Action? SettingsChanged;

    private readonly ComboBox _micCombo = NewCombo();
    private readonly ComboBox _inputCombo = NewCombo();
    private readonly ComboBox _outputCombo = NewCombo();
    private readonly CheckBox _forceOnStartup = NewCheck("Force on app start");
    private readonly CheckBox _watchChanges = NewCheck("Watch && notify");
    private readonly Label _watchChangesHint = NewValueLabel();
    private readonly CheckBox _ctrlCheck = NewCheck("Ctrl");
    private readonly CheckBox _altCheck = NewCheck("Alt");
    private readonly CheckBox _shiftCheck = NewCheck("Shift");
    private readonly ComboBox _keyCombo = NewCombo();
    private readonly CheckBox _showToast = NewCheck("Show toast overlay");
    private readonly CheckBox _showBorder = NewCheck("Show screen border glow");
    private readonly CheckBox _persistToggle = NewCheck("Keep overlay visible while muted");
    private readonly ComboBox _positionCombo = NewCombo();
    private readonly TrackBar _scaleSlider = NewSlider(50, 200);
    private readonly Label _scaleValue = NewValueLabel();
    private readonly TrackBar _thicknessSlider = NewSlider(2, 16);
    private readonly Label _thicknessValue = NewValueLabel();
    private readonly CheckBox _autoStart = NewCheck("Start with Windows");
    private readonly FlowLayoutPanel _monitorPanel = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly List<(Screen Screen, CheckBox Box)> _monitorChecks = [];
    private readonly Label _versionLabel = NewValueLabel();

    public SettingsFormLite(CoreAudioController controller, AppSettings settings)
    {
        _controller = controller;
        _settings = settings;

        Text = "Kuro Mute & Force Mic — Settings";
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 460);
        try { Icon = TrayApp.LoadIcon("app.ico"); } catch { /* best-effort window icon */ }

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildMicTab());
        tabs.TabPages.Add(BuildForceAudioTab());
        tabs.TabPages.Add(BuildOverlayTab());
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildAboutTab());
        Controls.Add(tabs);

        LoadDevices();
        LoadHotkey();
        LoadOverlay();
        LoadAbout();
        _isLoading = false;
    }

    private TabPage BuildMicTab()
    {
        var page = NewPage("Mic");
        var layout = NewFieldLayout();
        int row = 0;

        AddField(layout, ref row, "Microphone to mute:", _micCombo);
        AddRow(layout, ref row, NewLabel("Hotkey:"), null);
        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        hotkeyRow.Controls.AddRange([_ctrlCheck, _altCheck, _shiftCheck, _keyCombo]);
        layout.Controls.Add(hotkeyRow, 0, row); layout.SetColumnSpan(hotkeyRow, 2); row++;

        _micCombo.SelectedIndexChanged += OnChanged;
        _ctrlCheck.CheckedChanged += OnChanged;
        _altCheck.CheckedChanged += OnChanged;
        _shiftCheck.CheckedChanged += OnChanged;
        _keyCombo.SelectedIndexChanged += OnChanged;

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildForceAudioTab()
    {
        var page = NewPage("Force Audio");
        var layout = NewFieldLayout();
        int row = 0;

        AddField(layout, ref row, "Force default input:", _inputCombo);
        AddField(layout, ref row, "Force default output:", _outputCombo);
        AddRow(layout, ref row, _forceOnStartup, null);

        _watchChangesHint.Text = "Notify when default audio changes";
        _watchChangesHint.Margin = new Padding(20, 0, 0, 4);
        var watchGroup = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        watchGroup.Controls.Add(_watchChanges);
        watchGroup.Controls.Add(_watchChangesHint);
        AddRow(layout, ref row, watchGroup, null);

        _inputCombo.SelectedIndexChanged += OnChanged;
        _outputCombo.SelectedIndexChanged += OnChanged;
        _forceOnStartup.CheckedChanged += OnChanged;
        _watchChanges.CheckedChanged += OnChanged;

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildOverlayTab()
    {
        var page = NewPage("Overlay");
        var layout = NewFieldLayout();
        int row = 0;

        AddRow(layout, ref row, _showToast, null);
        AddRow(layout, ref row, _showBorder, null);
        AddRow(layout, ref row, _persistToggle, null);
        AddField(layout, ref row, "Position:", _positionCombo);

        var scaleRow = new FlowLayoutPanel { AutoSize = true };
        scaleRow.Controls.AddRange([_scaleSlider, _scaleValue]);
        AddRow(layout, ref row, NewLabel("Scale:"), scaleRow);

        var thicknessRow = new FlowLayoutPanel { AutoSize = true };
        thicknessRow.Controls.AddRange([_thicknessSlider, _thicknessValue]);
        AddRow(layout, ref row, NewLabel("Border thickness:"), thicknessRow);

        AddRow(layout, ref row, NewLabel("Monitors:"), null);
        layout.Controls.Add(_monitorPanel, 0, row); layout.SetColumnSpan(_monitorPanel, 2); row++;

        _showToast.CheckedChanged += OnChanged;
        _showBorder.CheckedChanged += OnChanged;
        _persistToggle.CheckedChanged += OnChanged;
        _positionCombo.SelectedIndexChanged += OnChanged;
        _scaleSlider.ValueChanged += (_, _) => _scaleValue.Text = $"{_scaleSlider.Value}%";
        _scaleSlider.MouseUp += OnChanged;
        _scaleSlider.KeyUp += OnChanged;
        _thicknessSlider.ValueChanged += (_, _) => _thicknessValue.Text = $"{_thicknessSlider.Value}px";
        _thicknessSlider.MouseUp += OnChanged;
        _thicknessSlider.KeyUp += OnChanged;

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildGeneralTab()
    {
        var page = NewPage("General");
        var layout = NewFieldLayout();
        int row = 0;

        AddRow(layout, ref row, _autoStart, null);
        _autoStart.CheckedChanged += OnChanged;

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildAboutTab()
    {
        var page = NewPage("About");
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16),
            WrapContents = false,
        };

        var title = NewLabel("Kuro Mute & Force Mic");
        title.Font = new Font("Segoe UI Semibold", 12f);
        layout.Controls.Add(title);
        layout.Controls.Add(_versionLabel);
        layout.Controls.Add(new Panel { Height = 12 });
        layout.Controls.Add(NewLink("GitHub Repository", "https://github.com/Kurohiko-id/Kuro-Mute-Force-Mic-Input"));
        layout.Controls.Add(NewLink("Support / Donate", "https://saweria.co/Kurohiko"));
        layout.Controls.Add(NewLink("Top Up", "https://kurohikotopup.com"));

        page.Controls.Add(layout);
        return page;
    }

    private void LoadDevices()
    {
        var captures = _controller.GetCaptureDevices(DeviceState.Active).Select(d => new DeviceItem(d.Id, d.FullName)).ToList();
        var playbacks = _controller.GetPlaybackDevices(DeviceState.Active).Select(d => new DeviceItem(d.Id, d.FullName)).ToList();

        _micCombo.DataSource = captures;
        _inputCombo.DataSource = captures.ToList();
        _outputCombo.DataSource = playbacks;

        _micCombo.SelectedItem = captures.FirstOrDefault(c => c.Id == _settings.MicDeviceId) ?? captures.FirstOrDefault();
        _inputCombo.SelectedItem = captures.FirstOrDefault(c => c.Id == _settings.DefaultInputId) ?? captures.FirstOrDefault();
        _outputCombo.SelectedItem = playbacks.FirstOrDefault(c => c.Id == _settings.DefaultOutputId) ?? playbacks.FirstOrDefault();

        _forceOnStartup.Checked = _settings.ForceAudioOnStartup;
        _watchChanges.Checked = _settings.WatchAudioChanges;
    }

    private void LoadHotkey()
    {
        var keyOptions = BuildKeyOptions().ToList();
        _keyCombo.DataSource = keyOptions;
        _keyCombo.SelectedItem = keyOptions.FirstOrDefault(k => k.Key == (Keys)_settings.HotkeyKey) ?? keyOptions[0];

        _ctrlCheck.Checked = (_settings.HotkeyModifiers & HotkeyManager.MOD_CONTROL) != 0;
        _altCheck.Checked = (_settings.HotkeyModifiers & HotkeyManager.MOD_ALT) != 0;
        _shiftCheck.Checked = (_settings.HotkeyModifiers & HotkeyManager.MOD_SHIFT) != 0;
    }

    private void LoadOverlay()
    {
        _showToast.Checked = _settings.ShowToastOverlay;
        _showBorder.Checked = _settings.ShowBorderGlow;
        _persistToggle.Checked = _settings.OverlayPersistWhileMuted;

        PositionOption[] positions =
        [
            new(OverlayPosition.TopLeft, "Top Left"), new(OverlayPosition.TopCenter, "Top Center"), new(OverlayPosition.TopRight, "Top Right"),
            new(OverlayPosition.BottomLeft, "Bottom Left"), new(OverlayPosition.BottomCenter, "Bottom Center"), new(OverlayPosition.BottomRight, "Bottom Right"),
        ];
        _positionCombo.DataSource = positions;
        _positionCombo.SelectedItem = positions.FirstOrDefault(p => p.Value == _settings.OverlayPosition) ?? positions[4];

        _scaleSlider.Value = Math.Clamp((int)(_settings.OverlayScale * 100), _scaleSlider.Minimum, _scaleSlider.Maximum);
        _scaleValue.Text = $"{_scaleSlider.Value}%";

        _thicknessSlider.Value = Math.Clamp(_settings.BorderThickness, _thicknessSlider.Minimum, _thicknessSlider.Maximum);
        _thicknessValue.Text = $"{_thicknessSlider.Value}px";

        _monitorPanel.Controls.Clear();
        _monitorChecks.Clear();
        foreach (var screen in Screen.AllScreens)
        {
            var label = $"{(screen.Primary ? "Primary monitor" : "Monitor")} · {screen.Bounds.Width}x{screen.Bounds.Height}";
            var box = NewCheck(label);
            box.Checked = _settings.OverlayAllMonitors || _settings.OverlayMonitorIds.Contains(screen.DeviceName);
            box.CheckedChanged += OnChanged;
            _monitorChecks.Add((screen, box));
            _monitorPanel.Controls.Add(box);
        }

        _autoStart.Checked = _settings.AutoStart;
    }

    private void LoadAbout()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        _versionLabel.Text = version is null ? "dev build" : $"v{version.Major}.{version.Minor}.{version.Build} (Lite)";
    }

    private void OnChanged(object? sender, EventArgs e) => SaveAndApply();

    private void SaveAndApply()
    {
        if (_isLoading)
            return;

        _settings.MicDeviceId = (_micCombo.SelectedItem as DeviceItem)?.Id;
        _settings.DefaultInputId = (_inputCombo.SelectedItem as DeviceItem)?.Id;
        _settings.DefaultOutputId = (_outputCombo.SelectedItem as DeviceItem)?.Id;
        _settings.ForceAudioOnStartup = _forceOnStartup.Checked;
        _settings.WatchAudioChanges = _watchChanges.Checked;

        _settings.HotkeyModifiers =
            (_ctrlCheck.Checked ? HotkeyManager.MOD_CONTROL : 0u) |
            (_altCheck.Checked ? HotkeyManager.MOD_ALT : 0u) |
            (_shiftCheck.Checked ? HotkeyManager.MOD_SHIFT : 0u);
        _settings.HotkeyKey = (uint)((_keyCombo.SelectedItem as KeyOption)?.Key ?? Keys.M);

        _settings.ShowToastOverlay = _showToast.Checked;
        _settings.ShowBorderGlow = _showBorder.Checked;
        _settings.OverlayPersistWhileMuted = _persistToggle.Checked;
        _settings.OverlayPosition = (_positionCombo.SelectedItem as PositionOption)?.Value ?? OverlayPosition.BottomCenter;
        _settings.OverlayScale = _scaleSlider.Value / 100.0;
        _settings.BorderThickness = _thicknessSlider.Value;

        var allChecked = _monitorChecks.All(m => m.Box.Checked);
        _settings.OverlayAllMonitors = allChecked;
        _settings.OverlayMonitorIds = allChecked ? [] : _monitorChecks.Where(m => m.Box.Checked).Select(m => m.Screen.DeviceName).ToList();

        _settings.AutoStart = _autoStart.Checked;

        _settings.Save();
        SettingsChanged?.Invoke();
    }

    private static IEnumerable<KeyOption> BuildKeyOptions()
    {
        for (char c = 'A'; c <= 'Z'; c++)
            yield return new KeyOption(Enum.Parse<Keys>(c.ToString()), c.ToString());
        for (var d = 0; d <= 9; d++)
            yield return new KeyOption(Enum.Parse<Keys>("D" + d), d.ToString());
        for (var f = 1; f <= 24; f++)
            yield return new KeyOption(Enum.Parse<Keys>("F" + f), "F" + f);

        (Keys Key, string Label)[] symbols =
        [
            (Keys.OemMinus, "-"), (Keys.Oemplus, "="), (Keys.Oemcomma, ","), (Keys.OemPeriod, "."),
            (Keys.OemQuestion, "/"), (Keys.OemSemicolon, ";"), (Keys.OemQuotes, "'"), (Keys.OemOpenBrackets, "["),
            (Keys.OemCloseBrackets, "]"), (Keys.OemPipe, "\\"), (Keys.Oemtilde, "`"),
        ];
        foreach (var s in symbols)
            yield return new KeyOption(s.Key, s.Label);
    }

    // --- small control-building helpers, kept local so layout stays declarative-ish ---

    private static TabPage NewPage(string title) => new(title) { Padding = new Padding(16) };

    private static TableLayoutPanel NewFieldLayout()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private static void AddField(TableLayoutPanel layout, ref int row, string labelText, Control control)
    {
        layout.RowCount = row + 1;
        layout.Controls.Add(NewLabel(labelText), 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(control, 1, row);
        row++;
    }

    private static void AddRow(TableLayoutPanel layout, ref int row, Control a, Control? b)
    {
        layout.RowCount = row + 1;
        layout.Controls.Add(a, 0, row);
        if (b != null)
            layout.Controls.Add(b, 1, row);
        row++;
    }

    private static Label NewLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 6, 8, 6) };
    private static Label NewValueLabel() => new() { AutoSize = true, Margin = new Padding(6, 6, 0, 6) };

    private static CheckBox NewCheck(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 4, 12, 4),
    };

    private static ComboBox NewCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 220,
    };

    private static TrackBar NewSlider(int min, int max) => new() { Minimum = min, Maximum = max, Width = 180, TickStyle = TickStyle.None };

    private static LinkLabel NewLink(string text, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return link;
    }
}
