namespace MuteMic;

// Lite edition only: opens the plain-WinForms settings form, no WPF anywhere.
internal sealed partial class TrayApp
{
    private void OpenSettings()
    {
        var form = new SettingsFormLite(_controller, _settings);
        form.SettingsChanged += ApplyAllSettings;
        form.ShowDialog();
        form.SettingsChanged -= ApplyAllSettings;
    }
}
