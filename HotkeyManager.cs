using System.Runtime.InteropServices;

namespace MuteMic;

// Hidden message-only window whose sole job is receiving WM_HOTKEY.
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;

    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xB00C;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event Action? HotkeyPressed;

    public HotkeyManager() => CreateHandle(new CreateParams());

    public void SetHotkey(uint modifiers, uint key)
    {
        UnregisterHotKey(Handle, HotkeyId);
        RegisterHotKey(Handle, HotkeyId, modifiers, key);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            HotkeyPressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, HotkeyId);
        DestroyHandle();
    }
}
