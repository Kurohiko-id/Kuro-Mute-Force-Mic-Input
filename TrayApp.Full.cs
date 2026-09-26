namespace MuteMic;

// Full edition only: opens the WPF/WPF-UI Fluent settings window.
internal sealed partial class TrayApp
{
    private static App? _wpfApp;

    private void OpenSettings()
    {
        // System.Windows.Application can only ever be constructed once per process;
        // deferred here so the WPF stack only loads once Settings is actually opened.
        _wpfApp ??= new App();

        var window = new SettingsWindow(_controller, _settings);
        window.SettingsChanged += ApplyAllSettings;
        window.ShowDialog();
        window.SettingsChanged -= ApplyAllSettings;
    }
}
