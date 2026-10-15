# Building MonWin

MonWin has a Windows app (WPF) and a Linux app (Avalonia) over a shared core — see
[ARCHITECTURE.md](ARCHITECTURE.md#projects). The first half of this file covers Windows,
the second half ([Linux](#linux)) covers Linux. The Windows app can only be built on
Windows; the Linux app and the shared core build on Windows, Linux or macOS.

## Prerequisites (Windows)

- Windows 10 1803+ or Windows 11 (GPU performance counters require 1803+)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`winget install Microsoft.DotNet.SDK.8`)
- No other tools required for the Windows app — no Node or Java.

Verify the SDK is installed:

```powershell
dotnet --version   # should print 8.0.x
```

## Build

```powershell
.\scripts\build.ps1
```

Equivalent to `dotnet restore` + `dotnet build -c Release` of `SystemMonitor.sln`, which
contains every project (Windows app, Linux app, shared core and all test projects).

## Test

```powershell
dotnet test SystemMonitor.sln -c Release
```

Runs three test projects (counts as of this writing: 22 Windows-only, 87 shared-core,
92 Linux — the last two run on any OS):

- `SystemMonitor.Tests` (Windows only): Win32/registry-backed services, taskbar service,
  GPU aggregation, startup registry round-trip, the Windows service graph, and a smoke test
  that loads the real WPF windows without showing them.
- `MonWin.Core.Tests`: CPU delta math, rolling-history buffer, settings load/save/corruption
  recovery and field validation, thresholds, rate formatting, hotkey parsing, network/disk
  math, overlay positioning math.
- `MonWin.Linux.Tests`: Linux `/proc` and `/sys` parsing, CPU/RAM/disk/GPU providers against
  fixture files, XDG paths, autostart file, session detection, single-instance lock, sparkline
  geometry and the Linux service graph.

## Publish

```powershell
.\scripts\publish.ps1
```

Produces a self-contained, single-file, ReadyToRun win-x64 executable at
`publish\SystemMonitor.exe`. Self-contained means the target machine needs **no .NET
runtime installed** — the trade-off is a larger executable (~170MB, since the whole
.NET + WPF + WinForms runtime and ahead-of-time-compiled native code are bundled).

If you'd rather have a small (~1MB) executable and don't mind requiring the .NET 8
Desktop Runtime on the target machine, publish framework-dependent instead:

```powershell
dotnet publish src\SystemMonitor\SystemMonitor.csproj -c Release -r win-x64 --self-contained false -o publish-fd
```

## Package

```powershell
.\scripts\package.ps1
```

Zips `publish\` into `dist\MonWin-<version>-win-x64.zip` — a portable distributable.
Run `publish.ps1` first.

## Installation (portable)

1. Unzip the package anywhere (no installer needed, no admin rights needed).
2. Run `SystemMonitor.exe`.
3. Turn on "Start with Windows" from Settings or the tray menu if desired.

MSIX packaging was considered but intentionally skipped: it adds identity/signing
requirements that aren't worth the complexity for a portable utility like this one. A
future release could add an MSIX manifest and packaging project without changing
anything else described here.

## Linux

### Prerequisites

- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Linux, Windows or macOS
  (cross-publishing for Linux works from any of them; no Linux machine is needed to *build*).
- Python 3.8+ (standard library only) for `scripts/package-linux.py`.
- To *run* the result: a graphical session with fontconfig and the X11 client libraries
  (libX11, libICE, libSM) — present on every mainstream desktop. No .NET install is needed
  because the published build is self-contained.

### Build and test

```bash
scripts/build-linux.sh          # builds MonWin.Linux, runs MonWin.Core.Tests and MonWin.Linux.Tests
```

This builds only the shared core and the Linux app (never the Windows-only WPF project), so it
works on a plain Linux box. `MonWin.Linux.slnf` is the same set as a Visual Studio / `dotnet`
solution filter.

### Publish and package

```bash
scripts/publish-linux.sh        # self-contained linux-x64 build -> publish-linux/linux-x64/
python3 scripts/package-linux.py   # -> dist/MonWin-<version>-linux-x64.tar.gz (+ .sha256)
```

The package contains the `monwin` binary with the bundled .NET runtime (~90 MB unpacked,
~38 MB compressed), `install.sh`, a `.desktop` template, the icon, `README.txt` and `LICENSE`.
`package-linux.py` writes the archive itself with explicit file modes, so the executable bits
are correct even when it runs on Windows.

Only **linux-x64** is packaged. `publish-linux.sh linux-arm64` compiles, but arm64 is
neither tested nor released. AppImage/DEB/RPM are possible later additions; the tar.gz plus
per-user `install.sh` was chosen first because it needs no root, no packaging toolchain and no
distribution-specific metadata.

### Install

```bash
tar -xzf MonWin-<version>-linux-x64.tar.gz
cd MonWin-<version>-linux-x64
./install.sh          # per user: ~/.local/share/monwin, ~/.local/bin/monwin, menu entry, icon
./install.sh --uninstall
```

`install.sh` never uses sudo and never touches your settings or autostart entry.

### Build-time telemetry (disabled)

Avalonia ships a package, `Avalonia.BuildServices`, that sends anonymous *build-time*
telemetry (machine id, hashed machine/project name, OS version) whenever an Avalonia project
compiles. MonWin promises no telemetry, so `MonWin.Linux.csproj` and `MonWin.Linux.Tests.csproj`
reference that package with `ExcludeAssets="all"`, which stops its MSBuild task from being
imported at all. The scripts and CI additionally set `AVALONIA_TELEMETRY_OPTOUT=1`. It never
ran inside the shipped app. If you build with a different setup, set that environment variable
yourself.

## Icon

`src\SystemMonitor\Assets\app.ico` is generated by `scripts\generate-icon.ps1` (uses
`System.Drawing` to draw a small rounded-square glyph and save it via
`Icon.Save`). Re-run that script if you want to change the icon design; it overwrites
the committed `.ico` in place. The Linux app uses a 128 px PNG export of the same icon
(`src\MonWin.Linux\Assets\monwin.png`, copied to `packaging\linux\monwin.png`); regenerate
both from the `.ico` if the design changes.
