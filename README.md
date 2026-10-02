<img width="720" alt="Social" src="https://github.com/user-attachments/assets/c1af9e84-5e0b-465e-9579-04d4ee6410a6" />

# Kuro Mute & Force Mic

Mute your microphone with a fully customizable hotkey and overlay. Force Windows to keep the input and output device you actually picked, so your apps stop losing their audio settings every time Windows switches them behind your back.

I love Mute Microphone VCM Feature, but they removed it. I've been stuck in powertoys v0.87.1. and now I Made my own app that has better feature. Because it can force my audio Default Input.

Language: [English](#english) | [Bahasa Indonesia](#bahasa-indonesia)

---
<img width="1919" height="1079" alt="image (4)" src="https://github.com/user-attachments/assets/c41d8e27-2c2c-41cd-a121-37101e0c9437" />


## English

### What is this

Kuro Mute & Force Mic is a lightweight Windows system tray app for two problems:

1. Muting your microphone instantly with a global hotkey, with clear visual feedback so you always know your current mute state.
2. Windows quietly switching your default microphone or speaker to a different device (a new USB device plugged in, a Bluetooth headset reconnecting, and so on). The app watches for this and offers to force your chosen devices back.

### Features

- **Global hotkey mute toggle** - Mute or unmute your chosen microphone with a fully customizable hotkey (default `Ctrl+Alt+M`), from anywhere, in any app.
- **Toast overlay (on-screen display)** - A small, PowerToys-style notification that briefly appears in a corner of the screen showing "Microphone Muted" or "Microphone Unmuted". Position (6 corners), scale, and whether it persists while muted are all configurable, per monitor.
- **Screen border glow** - An optional full-screen colored border drawn around the edge of your monitor(s) while the microphone is muted, so you can tell at a glance even without looking at the tray icon. Border thickness is adjustable.
- **Multi-monitor support** - Overlay and border glow can each target all monitors or only specific ones you choose.
- **Force default audio device** - Pick your preferred microphone and speaker, and the app can set them as the Windows default automatically on startup, undoing any silent changes Windows made.
- **Watch for audio device changes** - Optionally get notified the moment Windows changes your default input or output device away from your pick, with a one-click button to set it back.
- **Autostart with Windows** - Launch automatically when you log in, toggleable in settings.
- **Automatic update check** - Checks GitHub Releases for a newer version and notifies you with a direct download link. Never installs anything automatically and never phones home beyond that check.
- **Report a bug** - One click from the tray menu opens a pre-filled GitHub issue with your app version and OS details attached.

### Two editions

- **Full** - Fluent-styled settings window (built with WPF-UI). Ships as a self-contained installer, no separate .NET install needed. Larger download (~150MB), higher RAM usage since it loads the WPF stack.
- **Lite** - Same hotkey/overlay/force-audio-device behavior, with a plain WinForms settings window instead. Portable single .exe, well under 1MB, noticeably lower RAM usage. Requires the .NET 8 Desktop Runtime to already be installed.

### Installation

Download the latest release from the [Releases](../../releases) page:
- **Full**: run `KuroMuteMic-Setup-x.x.x.exe`. Lets you choose the install location, optionally creates a desktop shortcut and a Windows startup entry, and creates its own uninstaller in the install folder.
- **Lite**: download `Kuro MuteMic Lite.exe` and run it directly, no installer, no admin needed. Requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if not already installed.

### Building from source

Requires the .NET 8 SDK on Windows. This repo has two project files sharing the same source:

```
# Full edition (self-contained, WPF settings window)
dotnet publish MuteMic.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# Lite edition (framework-dependent, WinForms settings window)
dotnet publish MuteMic.Lite.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish-lite
```

To build the Full installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and compile `installer.iss`.

---

## Bahasa Indonesia

### Apa ini

Kuro Mute & Force Mic adalah aplikasi system tray Windows yang ringan, dibuat untuk menyelesaikan dua masalah:

1. Mematikan mikrofon secara instan lewat hotkey yang bisa diatur bebas, dengan indikator visual yang jelas supaya kamu selalu tahu status mute saat ini.
2. Windows yang diam-diam mengganti mikrofon atau speaker default ke perangkat lain (misalnya ada perangkat USB baru yang dicolok, headset Bluetooth yang reconnect, dan sejenisnya). Aplikasi ini memantau perubahan tersebut dan menawarkan untuk memaksa balik ke perangkat pilihanmu.

### Fitur-fitur

- **Toggle mute lewat hotkey global** - Mute atau unmute mikrofon pilihanmu dengan hotkey yang bisa dikustomisasi sepenuhnya (default `Ctrl+Alt+M`), dari aplikasi apa pun, kapan saja.
- **Overlay toast (tampilan di layar)** - Notifikasi kecil bergaya PowerToys yang muncul sebentar di salah satu sudut layar, menampilkan tulisan "Microphone Muted" atau "Microphone Unmuted". Posisi (6 pilihan sudut), ukuran, dan apakah overlay tetap tampil selama mute, semuanya bisa diatur per monitor.
- **Border layar menyala** - Border berwarna full-screen opsional di tepi monitor yang muncul saat mikrofon sedang mute, jadi kamu bisa tahu statusnya sekilas tanpa perlu melihat ikon tray. Ketebalan border bisa diatur.
- **Dukungan multi-monitor** - Overlay dan border bisa masing-masing diarahkan ke semua monitor atau hanya monitor tertentu yang kamu pilih.
- **Paksa perangkat audio default** - Pilih mikrofon dan speaker favoritmu, dan aplikasi bisa otomatis menjadikannya default Windows setiap kali start, membatalkan perubahan diam-diam yang dilakukan Windows.
- **Pantau perubahan perangkat audio** - Opsional mendapat notifikasi begitu Windows mengganti perangkat input/output default dari pilihanmu, lengkap dengan tombol satu klik untuk mengembalikannya.
- **Autostart dengan Windows** - Otomatis jalan saat kamu login, bisa dinyalakan/dimatikan di pengaturan.
- **Pengecekan update otomatis** - Mengecek GitHub Releases untuk versi terbaru dan memberi notifikasi dengan tautan download langsung. Tidak pernah menginstal apa pun secara otomatis dan tidak mengirim data apa pun selain pengecekan tersebut.
- **Lapor bug** - Satu klik dari menu tray membuka GitHub issue yang sudah terisi otomatis dengan versi aplikasi dan detail OS kamu.

### Dua edisi

- **Full** - Jendela pengaturan bergaya Fluent (dibangun dengan WPF-UI). Dirilis sebagai installer self-contained, nggak perlu install .NET terpisah. Download lebih besar (~150MB), RAM lebih tinggi karena WPF ke-load.
- **Lite** - Fitur hotkey/overlay/force-audio sama persis, tapi jendela pengaturannya WinForms biasa. Portable, satu .exe, jauh di bawah 1MB, RAM jauh lebih rendah. Butuh .NET 8 Desktop Runtime sudah ke-install duluan.

### Instalasi

Unduh rilis terbaru dari halaman [Releases](../../releases):
- **Full**: jalankan `KuroMuteMic-Setup-x.x.x.exe`. Installer memungkinkan kamu memilih lokasi instalasi, opsional membuat shortcut desktop dan entri autostart Windows, serta membuat uninstaller-nya sendiri di dalam folder instalasi.
- **Lite**: unduh `Kuro MuteMic Lite.exe` dan langsung jalankan, tanpa installer, tanpa admin. Butuh [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) kalau belum ke-install.

### Build dari source

Membutuhkan .NET 8 SDK di Windows. Repo ini punya dua file project yang berbagi source code yang sama:

```
# Edisi Full (self-contained, Settings window WPF)
dotnet publish MuteMic.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# Edisi Lite (framework-dependent, Settings window WinForms)
dotnet publish MuteMic.Lite.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish-lite
```

Untuk membuat installer edisi Full, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) lalu compile `installer.iss`.
