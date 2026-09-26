; Requires Inno Setup 6 (free): https://jrsoftware.org/isinfo.php
; Build order:
;   1. dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
;   2. Open this file in Inno Setup (or `ISCC installer.iss`) and compile.
; Output: Output\KuroMuteMic-Setup-{#MyAppVersion}.exe

#define MyAppName "Kuro MuteMic"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Kurohiko-id"
#define MyAppURL "https://github.com/Kurohiko-id/Kuro-Mute-Force-Mic-Input"
#define MyAppExeName "Kuro MuteMic.exe"

[Setup]
AppId={{6A9F4C1E-3B7A-4E9E-9A2C-9F1C8B7D2A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; No admin task the app itself needs (autostart/hotkey are HKCU-only), so install
; per-user by default and skip the UAC prompt entirely.
DefaultDirName={autopf}\{#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=KuroMuteMic-Setup-{#MyAppVersion}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "autostart"; Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MuteMic"; \
    ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
