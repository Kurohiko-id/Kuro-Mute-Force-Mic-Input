using System.Diagnostics;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;

namespace MuteMic;

internal sealed partial class TrayApp : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly CoreAudioController _controller = new();
    private readonly NotifyIcon _tray;
    private readonly HotkeyManager _hotkey = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private List<OverlayForm> _overlays = [];
    private List<BorderGlowForm> _borders = [];
    private readonly DeviceChangeToast _deviceToast = new();
    private readonly Icon _icon = LoadIcon("app.ico");
    private IDisposable? _muteSub;
    private IDisposable? _audioWatchSub;

    public TrayApp()
    {
        var menu = BuildMenu();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "Kuro Mute & Force Mic",
            ContextMenuStrip = menu,
        };
        // Windows 11's systray overflow flyout can steal foreground focus after showing
        // the menu, so the strip never sees itself deactivated and won't auto-close on an
        // outside click. Re-assert foreground on the strip itself right after it's shown.
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                SetForegroundWindow(menu.Handle);
        };

        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _ = _deviceToast.Handle;

        ApplyAllSettings();
        _ = CheckForUpdate(manual: false);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Check for Updates", null, (_, _) => _ = CheckForUpdate(manual: true));
        menu.Items.Add("Report Bug...", null, (_, _) => ReportBug());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private async Task CheckForUpdate(bool manual)
    {
        var update = await UpdateChecker.CheckAsync();
        if (update is { } info)
        {
            _deviceToast.ShowMessage(
                $"Update available: v{info.Version}",
                "Download",
                () => Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true }));
        }
        else if (manual)
        {
            _deviceToast.ShowMessage("You're up to date.", "OK", () => { });
        }
    }

    private static void ReportBug()
    {
        var version = typeof(TrayApp).Assembly.GetName().Version;
        var body = $"""
            **App version:** {version}
            **OS:** {Environment.OSVersion.VersionString}
            **.NET:** {Environment.Version}

            Describe the bug here:
            """;
        var url = "https://github.com/Kurohiko-id/Kuro-Mute-Force-Mic-Input/issues/new"
            + $"?labels=bug&title={Uri.EscapeDataString("Bug: ")}&body={Uri.EscapeDataString(body)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // OpenSettings() lives in TrayApp.Full.cs (WPF SettingsWindow) or TrayApp.Lite.cs
    // (WinForms SettingsFormLite) — whichever one the edition's .csproj compiles in.

    // Settings now autosave, so this re-applies everything live after every single
    // change (hotkey checkbox, toggle, slider, ...) rather than once on a Save click.
    // ponytail: re-applies the whole set each time rather than diffing what changed —
    // simpler, and cheap enough (a few hidden WinForms objects) not to matter.
    private void ApplyAllSettings()
    {
        _hotkey.SetHotkey(_settings.HotkeyModifiers, _settings.HotkeyKey);
        RebuildOverlays();
        WatchMicDevice();
        if (_settings.ForceAudioOnStartup)
            ApplyDefaultDevicesOnce();
        WatchAudioChanges();
        SetAutostart(_settings.AutoStart);
    }

    private void RebuildOverlays()
    {
        foreach (var old in _overlays)
            old.Dispose();
        foreach (var old in _borders)
            old.Dispose();

        var targets = _settings.OverlayAllMonitors
            ? Screen.AllScreens
            : Screen.AllScreens.Where(s => _settings.OverlayMonitorIds.Contains(s.DeviceName)).ToArray();
        if (targets.Length == 0)
            targets = [Screen.PrimaryScreen!];

        _overlays = _settings.ShowToastOverlay
            ? targets.Select(screen => new OverlayForm
            {
                TargetScreen = screen,
                Position = _settings.OverlayPosition,
                Scale = _settings.OverlayScale,
                PersistWhileMuted = _settings.OverlayPersistWhileMuted,
            }).ToList()
            : [];

        _borders = _settings.ShowBorderGlow
            ? targets.Select(screen =>
            {
                var form = new BorderGlowForm { TargetScreen = screen, Thickness = _settings.BorderThickness };
                form.Bounds = screen.Bounds;
                return form;
            }).ToList()
            : [];

        // Force each native window to exist so BeginInvoke works from the COM callback thread.
        foreach (var overlay in _overlays)
            _ = overlay.Handle;
        foreach (var border in _borders)
            _ = border.Handle;

        // Freshly created overlays start hidden. If the mic is still muted (e.g. the user
        // is tweaking settings while muted), re-sync immediately instead of waiting for a
        // future mute-change event that may never come.
        if (_settings.MicDeviceId is { } id && _controller.GetDevice(id) is { IsMuted: true })
        {
            foreach (var overlay in _overlays)
                overlay.ShowMuteState(true);
            foreach (var border in _borders)
                border.SetMuted(true);
        }
    }

    private void WatchMicDevice()
    {
        _muteSub?.Dispose();
        _muteSub = null;

        if (_settings.MicDeviceId is not { } id)
            return;

        var device = _controller.GetDevice(id);
        if (device is null)
            return;

        // MuteChanged fires on a Core Audio COM callback thread, not the UI thread.
        _muteSub = device.MuteChanged.Subscribe(
            new AnonymousObserver<DeviceMuteChangedArgs>(args =>
            {
                DebugLog.Write($"MuteChanged event received, IsMuted={args.IsMuted}");
                foreach (var overlay in _overlays)
                    overlay.BeginInvoke(() => overlay.ShowMuteState(args.IsMuted));
                foreach (var border in _borders)
                    border.BeginInvoke(() =>
                    {
                        DebugLog.Write("Border.SetMuted starting (dispatched to UI thread)");
                        border.SetMuted(args.IsMuted);
                        DebugLog.Write("Border.SetMuted returned");
                    });
            }));
    }

    // Embedded resource (not a WPF pack:// resource) so this has no dependency on
    // System.Windows.Application ever being constructed.
    internal static Icon LoadIcon(string resourceName)
    {
        using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream(resourceName)!;
        return new Icon(stream);
    }

    private void OnHotkeyPressed()
    {
        DebugLog.Write("Hotkey pressed");

        if (_settings.MicDeviceId is not { } id)
        {
            OpenSettings();
            return;
        }

        var device = _controller.GetDevice(id);
        DebugLog.Write("Calling ToggleMuteAsync");
        // AudioSwitcher's synchronous ToggleMute() blocks the calling thread for up to 1000ms
        // inside the library (CoreAudioDevice.Mute waits on a ManualResetEvent for a hardware
        // confirmation that doesn't always arrive quickly). Calling it on this UI thread was
        // freezing the whole message pump for that same second, which delayed the border/toast
        // BeginInvoke callbacks even though the actual mute (and our MuteChanged subscription)
        // already fired within milliseconds. Fire-and-forget on a background thread instead.
        _ = device?.ToggleMuteAsync();
        DebugLog.Write("ToggleMuteAsync dispatched (not awaited, fire-and-forget)");
    }

    private void ApplyDefaultDevicesOnce()
    {
        if (_settings.DefaultInputId is { } inputId)
        {
            var device = _controller.GetDevice(inputId);
            device?.SetAsDefault();
            device?.SetAsDefaultCommunications();
        }

        if (_settings.DefaultOutputId is { } outputId)
        {
            var device = _controller.GetDevice(outputId);
            device?.SetAsDefault();
            device?.SetAsDefaultCommunications();
        }
    }

    private void WatchAudioChanges()
    {
        _audioWatchSub?.Dispose();
        _audioWatchSub = null;

        if (!_settings.WatchAudioChanges)
            return;

        // AudioDeviceChanged fires on a Core Audio COM callback thread, not the UI thread.
        _audioWatchSub = _controller.AudioDeviceChanged.Subscribe(
            new AnonymousObserver<DeviceChangedArgs>(_ => _deviceToast.BeginInvoke(CheckDefaultDevices)));
    }

    private void CheckDefaultDevices()
    {
        var drifted = new List<string>();

        if (_settings.DefaultInputId is { } inputId && _controller.DefaultCaptureDevice?.Id != inputId)
            drifted.Add("input");
        if (_settings.DefaultOutputId is { } outputId && _controller.DefaultPlaybackDevice?.Id != outputId)
            drifted.Add("output");

        if (drifted.Count > 0)
            _deviceToast.ShowMessage(
                $"Default audio {string.Join(" & ", drifted)} changed from your pick.",
                "Set Default Again",
                ApplyDefaultDevicesOnce);
    }

    private static void SetAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (enabled)
            key?.SetValue("MuteMic", $"\"{Application.ExecutablePath}\"");
        else
            key?.DeleteValue("MuteMic", throwOnMissingValue: false);
    }

    private void ExitApp()
    {
        _tray.Visible = false;
        _muteSub?.Dispose();
        _audioWatchSub?.Dispose();
        _deviceToast.Dispose();
        foreach (var overlay in _overlays)
            overlay.Dispose();
        foreach (var border in _borders)
            border.Dispose();
        _hotkey.Dispose();
        _controller.Dispose();
        Application.Exit();
    }
}
