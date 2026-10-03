param(
    [switch]$Watch,
    [string]$InputId,
    [string]$OutputId,
    [int]$Mode
)

$ErrorActionPreference = 'Stop'
$PidFile = Join-Path $env:TEMP 'KuroForceAudio.pid'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
public enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

[StructLayout(LayoutKind.Explicit)]
public struct PropVariant
{
    [FieldOffset(0)] public short vt;
    [FieldOffset(8)] public IntPtr pointerValue;
}

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
    public Guid fmtid;
    public int pid;
    public PropertyKey(Guid g, int p) { fmtid = g; pid = p; }
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
}

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out int pcDevices);
    [PreserveSig] int Item(int nDevice, out IMMDevice ppDevice);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig] int GetState(out int pdwState);
}

[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    [PreserveSig] int GetCount(out int cProps);
    [PreserveSig] int GetAt(int iProp, out PropertyKey pkey);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant propvar);
    [PreserveSig] int Commit();
}

[Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
    [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
    [PreserveSig] int GetProcessingPeriod(string pszDeviceName, bool bDefault, out long hnsDefaultDevicePeriod, out long hnsMinimumDevicePeriod);
    [PreserveSig] int SetProcessingPeriod(string pszDeviceName, ref long hnsProcessingPeriod);
    [PreserveSig] int GetShareMode(string pszDeviceName, IntPtr pDeviceMode);
    [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
    [PreserveSig] int GetPropertyValue(string pszDeviceName, ref PropertyKey key, IntPtr pv);
    [PreserveSig] int SetPropertyValue(string pszDeviceName, ref PropertyKey key, IntPtr pv);
    [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, ERole role);
    [PreserveSig] int SetEndpointVisibility(string pszDeviceName, bool bVisible);
}

// All COM interop stays inside this compiled class. PowerShell can't reliably call
// custom vtable-only COM interfaces directly (dispatch falls back to IDispatch and
// fails), so the .ps1 only ever calls these plain static methods.
public static class AudioHelper
{
    static readonly Guid EnumeratorClsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    static readonly Guid PolicyConfigClsid = new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
    static PropertyKey FriendlyNameKey = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    static IMMDeviceEnumerator GetEnumerator()
    {
        var type = Type.GetTypeFromCLSID(EnumeratorClsid);
        return (IMMDeviceEnumerator)Activator.CreateInstance(type);
    }

    static IPolicyConfig GetPolicyConfig()
    {
        var type = Type.GetTypeFromCLSID(PolicyConfigClsid);
        return (IPolicyConfig)Activator.CreateInstance(type);
    }

    static string GetName(IMMDevice dev)
    {
        IPropertyStore store;
        dev.OpenPropertyStore(0, out store);
        PropVariant pv;
        store.GetValue(ref FriendlyNameKey, out pv);
        return pv.pointerValue != IntPtr.Zero ? Marshal.PtrToStringUni(pv.pointerValue) : "(unknown)";
    }

    public static string[] GetDeviceNames(int flow)
    {
        var enumerator = GetEnumerator();
        IMMDeviceCollection collection;
        enumerator.EnumAudioEndpoints((EDataFlow)flow, 1, out collection);
        int count;
        collection.GetCount(out count);
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            IMMDevice dev;
            collection.Item(i, out dev);
            result.Add(GetName(dev));
        }
        return result.ToArray();
    }

    public static string[] GetDeviceIds(int flow)
    {
        var enumerator = GetEnumerator();
        IMMDeviceCollection collection;
        enumerator.EnumAudioEndpoints((EDataFlow)flow, 1, out collection);
        int count;
        collection.GetCount(out count);
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            IMMDevice dev;
            collection.Item(i, out dev);
            string id;
            dev.GetId(out id);
            result.Add(id);
        }
        return result.ToArray();
    }

    public static string GetDefaultId(int flow)
    {
        var enumerator = GetEnumerator();
        IMMDevice dev;
        int hr = enumerator.GetDefaultAudioEndpoint((EDataFlow)flow, ERole.eConsole, out dev);
        if (hr != 0 || dev == null) return null;
        string id;
        dev.GetId(out id);
        return id;
    }

    public static void SetDefault(string id)
    {
        var policy = GetPolicyConfig();
        policy.SetDefaultEndpoint(id, ERole.eConsole);
        policy.SetDefaultEndpoint(id, ERole.eMultimedia);
        policy.SetDefaultEndpoint(id, ERole.eCommunications);
    }
}
"@

# Fixed banner text (figlet "ANSI Regular" font) — hardcoded since the phrase never
# changes, no need for a general-purpose font renderer for one static title.
$BannerLines = @(
    '███████  ██████  ██████   ██████ ███████      █████  ██    ██ ██████  ██  ██████  '
    '██      ██    ██ ██   ██ ██      ██          ██   ██ ██    ██ ██   ██ ██ ██    ██ '
    '█████   ██    ██ ██████  ██      █████       ███████ ██    ██ ██   ██ ██ ██    ██ '
    '██      ██    ██ ██   ██ ██      ██          ██   ██ ██    ██ ██   ██ ██ ██    ██ '
    '██       ██████  ██   ██  ██████ ███████     ██   ██  ██████  ██████  ██  ██████  '
)

function Write-Banner {
    Write-Host ""
    foreach ($line in $BannerLines) { Write-Host $line -ForegroundColor Cyan }
    Write-Host ""
    Write-Host "                                      by Kurohiko" -ForegroundColor DarkGray
    Write-Host ""
}

function Select-FromList([string]$title, [string[]]$items, [switch]$ShowBanner) {
    $index = 0
    $typed = ""
    while ($true) {
        Clear-Host
        if ($ShowBanner) {
            Write-Banner
        } else {
            Write-Host "=== Kuro Force Audio (Super Lite) ===" -ForegroundColor Cyan
            Write-Host ""
        }
        Write-Host $title
        Write-Host ""
        for ($i = 0; $i -lt $items.Count; $i++) {
            $label = "$($i + 1). $($items[$i])"
            if ($i -eq $index) { Write-Host "> $label" -ForegroundColor Yellow } else { Write-Host "  $label" }
        }
        Write-Host ""
        Write-Host "Panah atas/bawah + Enter, atau ketik nomor + Enter." -ForegroundColor DarkGray
        if ($typed) { Write-Host "Ketik: $typed" -ForegroundColor DarkGray }

        $key = [Console]::ReadKey($true)
        switch ($key.Key) {
            'UpArrow'   { $index = ($index - 1 + $items.Count) % $items.Count; $typed = "" }
            'DownArrow' { $index = ($index + 1) % $items.Count; $typed = "" }
            'Enter' {
                if ($typed) {
                    $n = [int]$typed - 1
                    if ($n -ge 0 -and $n -lt $items.Count) { return $n }
                    $typed = ""
                } else {
                    return $index
                }
            }
            'Backspace' { if ($typed.Length -gt 0) { $typed = $typed.Substring(0, $typed.Length - 1) } }
            default {
                if ($key.KeyChar -match '[0-9]') { $typed += $key.KeyChar }
            }
        }
    }
}

function Stop-ExistingWatcher {
    if (Test-Path $PidFile) {
        $oldPid = Get-Content $PidFile -ErrorAction SilentlyContinue
        if ($oldPid) {
            try { Stop-Process -Id $oldPid -Force -ErrorAction Stop } catch { }
        }
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
    }
}

# ============================== WATCH MODE (hidden background) ==============================
if ($Watch) {
    if ($Mode -eq 0) {
        # Auto-revert: no UI needed at all, plain polling loop is enough since there's
        # nothing to click. Self-heals every check, so no "already notified" bookkeeping needed.
        while ($true) {
            Start-Sleep -Seconds 2
            $curIn = [AudioHelper]::GetDefaultId(1)
            $curOut = [AudioHelper]::GetDefaultId(0)
            if (($curIn -ne $InputId) -or ($curOut -ne $OutputId)) {
                [AudioHelper]::SetDefault($InputId)
                [AudioHelper]::SetDefault($OutputId)
            }
        }
        exit 0
    }

    # Notify-only mode: needs a real message loop (Application.Run) so the balloon's
    # click event actually fires — a plain Start-Sleep loop can't dispatch that event.
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $notify = New-Object System.Windows.Forms.NotifyIcon
    $notify.Icon = [System.Drawing.SystemIcons]::Warning
    $notify.Text = "Kuro Force Audio - memantau"
    $notify.Visible = $true

    $script:alreadyNotified = $false

    $notify.add_BalloonTipClicked({
        [AudioHelper]::SetDefault($InputId)
        [AudioHelper]::SetDefault($OutputId)
        $script:alreadyNotified = $false
    })

    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 2000
    $timer.add_Tick({
        $curIn = [AudioHelper]::GetDefaultId(1)
        $curOut = [AudioHelper]::GetDefaultId(0)
        $changed = ($curIn -ne $InputId) -or ($curOut -ne $OutputId)
        if ($changed -and -not $script:alreadyNotified) {
            $notify.BalloonTipTitle = "Kuro Force Audio"
            $notify.BalloonTipText = "Default audio device berubah. Klik notifikasi ini buat kembaliin."
            $notify.ShowBalloonTip(8000)
            $script:alreadyNotified = $true
        } elseif (-not $changed) {
            $script:alreadyNotified = $false
        }
    })
    $timer.Start()
    [System.Windows.Forms.Application]::Run()
    exit 0
}

# ============================== INTERACTIVE MODE ==============================
$langChoice = Select-FromList "Choose language / Pilih bahasa:" @("Bahasa Indonesia", "English") -ShowBanner

$L = if ($langChoice -eq 0) {
    @{
        MenuTitle      = "Mau ngapain?"
        MenuForce      = "Pilih & paksa default audio device"
        MenuStop       = "Stop pemantauan yang lagi jalan"
        MenuExit       = "Keluar"
        PickMic        = "Pilih INPUT (microphone) yang mau dipaksa jadi default:"
        PickSpeaker    = "Pilih OUTPUT (speaker) yang mau dipaksa jadi default:"
        NoMic          = "Nggak ada microphone aktif ketemu."
        NoSpeaker      = "Nggak ada speaker aktif ketemu."
        AskReaction    = "Kalau Windows ganti default ini lagi, mau diapain?"
        ReactAuto      = "Ya, auto ganti balik ke pilihan di atas"
        ReactNotify    = "Tampilkan notifikasi aja (nggak auto-ganti)"
        ReactNone      = "Tidak usah, sekali ini aja"
        Done           = "Selesai. Jendela ini boleh ditutup."
        WatchingBg     = "Mantau di background, nggak ada jendela yang kebuka."
        WatchStopHint  = "Buat berhenti, jalanin ForceAudio.bat lagi dan pilih 'Stop pemantauan'."
        StopOk         = "Berhenti memantau."
        StopNotRunning = "Nggak ada yang lagi mantau."
        PressKeyExit   = "Tekan tombol apa aja buat keluar..."
        ModeAuto       = "auto ganti balik"
        ModeNotify     = "notifikasi aja"
    }
} else {
    @{
        MenuTitle      = "What do you want to do?"
        MenuForce      = "Pick & force default audio device"
        MenuStop       = "Stop the running watcher"
        MenuExit       = "Exit"
        PickMic        = "Pick the INPUT (microphone) to force as default:"
        PickSpeaker    = "Pick the OUTPUT (speaker) to force as default:"
        NoMic          = "No active microphone found."
        NoSpeaker      = "No active speaker found."
        AskReaction    = "If Windows changes this default again, what should happen?"
        ReactAuto      = "Yes, auto switch back to the picks above"
        ReactNotify    = "Just show a notification (don't auto-switch)"
        ReactNone      = "No, just this once"
        Done           = "Done. This window can be closed."
        WatchingBg     = "Watching in the background, no window stays open."
        WatchStopHint  = "To stop, run ForceAudio.bat again and pick 'Stop the running watcher'."
        StopOk         = "Stopped watching."
        StopNotRunning = "Nothing is currently being watched."
        PressKeyExit   = "Press any key to exit..."
        ModeAuto       = "auto switch back"
        ModeNotify     = "notify only"
    }
}

$mainChoice = Select-FromList $L.MenuTitle @($L.MenuForce, $L.MenuStop, $L.MenuExit)

if ($mainChoice -eq 2) { exit 0 }

if ($mainChoice -eq 1) {
    Clear-Host
    Write-Banner
    if (Test-Path $PidFile) {
        $oldPid = Get-Content $PidFile -ErrorAction SilentlyContinue
        $stillRunning = $oldPid -and (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)
        Stop-ExistingWatcher
        Write-Host $(if ($stillRunning) { $L.StopOk } else { $L.StopNotRunning }) -ForegroundColor $(if ($stillRunning) { 'Green' } else { 'Yellow' })
    } else {
        Write-Host $L.StopNotRunning -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host $L.PressKeyExit -ForegroundColor DarkGray
    [Console]::ReadKey($true) | Out-Null
    exit 0
}

# mainChoice -eq 0: pick & force devices
Stop-ExistingWatcher

$captureNames = [AudioHelper]::GetDeviceNames(1)
$captureIds = [AudioHelper]::GetDeviceIds(1)
if ($captureNames.Count -eq 0) { Write-Host $L.NoMic -ForegroundColor Red; exit 1 }
$inChoice = Select-FromList $L.PickMic $captureNames
$chosenInputId = $captureIds[$inChoice]
$chosenInputName = $captureNames[$inChoice]

$playbackNames = [AudioHelper]::GetDeviceNames(0)
$playbackIds = [AudioHelper]::GetDeviceIds(0)
if ($playbackNames.Count -eq 0) { Write-Host $L.NoSpeaker -ForegroundColor Red; exit 1 }
$outChoice = Select-FromList $L.PickSpeaker $playbackNames
$chosenOutputId = $playbackIds[$outChoice]
$chosenOutputName = $playbackNames[$outChoice]

[AudioHelper]::SetDefault($chosenInputId)
[AudioHelper]::SetDefault($chosenOutputId)

Clear-Host
Write-Host "=== Kuro Force Audio (Super Lite) ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Default input  -> $chosenInputName" -ForegroundColor Green
Write-Host "Default output -> $chosenOutputName" -ForegroundColor Green
Write-Host ""

$modeChoice = Select-FromList $L.AskReaction @($L.ReactAuto, $L.ReactNotify, $L.ReactNone)

Clear-Host
Write-Host "=== Kuro Force Audio (Super Lite) ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Default input  -> $chosenInputName" -ForegroundColor Green
Write-Host "Default output -> $chosenOutputName" -ForegroundColor Green
Write-Host ""

if ($modeChoice -eq 2) {
    Write-Host $L.Done -ForegroundColor Green
    exit 0
}

$proc = Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", "`"$PSCommandPath`"",
    "-Watch",
    "-InputId", "`"$chosenInputId`"",
    "-OutputId", "`"$chosenOutputId`"",
    "-Mode", "$modeChoice"
)
Set-Content -Path $PidFile -Value $proc.Id -Force

$modeText = if ($modeChoice -eq 0) { $L.ModeAuto } else { $L.ModeNotify }
Write-Host "$($L.WatchingBg) ($modeText)" -ForegroundColor Yellow
Write-Host $L.WatchStopHint -ForegroundColor DarkGray
