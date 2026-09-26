using System.IO;
using System.Text.Json;

namespace MuteMic;

internal enum OverlayPosition { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

internal sealed class AppSettings
{
    public Guid? MicDeviceId { get; set; }
    public Guid? DefaultInputId { get; set; }
    public Guid? DefaultOutputId { get; set; }

    // Win32 MOD_* flags (RegisterHotKey), default Ctrl+Alt+M.
    public uint HotkeyModifiers { get; set; } = HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT;
    public uint HotkeyKey { get; set; } = (uint)Keys.M;

    public bool ShowToastOverlay { get; set; } = true;
    public bool ShowBorderGlow { get; set; } = false;
    public bool OverlayPersistWhileMuted { get; set; } = false;
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.BottomCenter;
    public double OverlayScale { get; set; } = 1.0; // 0.5 - 2.0
    public bool AutoStart { get; set; } = true;
    public bool ForceAudioOnStartup { get; set; } = true;
    public bool WatchAudioChanges { get; set; } = false;
    public int BorderThickness { get; set; } = 6;
    public bool OverlayAllMonitors { get; set; } = true;
    public List<string> OverlayMonitorIds { get; set; } = []; // Screen.DeviceName, used when OverlayAllMonitors is false

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuteMic", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
