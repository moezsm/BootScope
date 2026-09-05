# BootScope
 A lightweight Windows startup diagnostics app that monitors CPU, RAM, disk, and network usage, identifies resource-heavy processes, and provides safe recommendations for managing non-essential applications.

## What it does

BootScope is a Windows desktop application (C# / .NET 8 / WPF) designed to launch automatically
after you log into Windows and help you understand what is causing high CPU, RAM, disk, or
network usage right after startup. It shows a live, sortable, filterable table of running
processes with CPU/RAM/disk usage, publisher information, and a safety classification, plus
plain-language recommendations for each process.

## Safety principles

BootScope is diagnostic-only by default:

- It **never** automatically terminates a process, disables a Windows service, or changes
  system configuration.
- It **always requires explicit user confirmation** before stopping a process, and shows a
  strong warning before stopping anything classified as a critical Windows process (e.g.
  `System`, `Registry`, `smss.exe`, `csrss.exe`, `wininit.exe`, `services.exe`, `lsass.exe`,
  `svchost.exe`, `dwm.exe`, `explorer.exe`).
- It runs with **normal user permissions** by default and does not request administrator
  elevation at launch.
- Every process is labelled as a **Critical Windows process**, **Windows background process**,
  **Third-party application**, **Startup application**, or **Unknown process**, together with a
  recommendation (*Safe to close*, *Close only if unused*, *Do not close*, *Needs investigation*).

## Project structure

```
BootScope/
  Models/        Plain data models (ProcessInfo, AppSettings, SystemUsageSnapshot, ...)
  ViewModels/     MVVM view models (MainViewModel, SettingsViewModel)
  Views/          WPF windows (MainWindow, SettingsWindow)
  Services/       Process monitoring, system usage, startup detection, safety classification,
                  settings persistence, session logging, and process actions
  Helpers/        MVVM base classes (ObservableObject, RelayCommand) and Win32 P/Invoke helpers
  Resources/      Shared WPF styles for the dashboard UI
  App.xaml(.cs)   Application entry point / composition root
```

## Building
Requires the .NET 8 SDK (Windows Desktop workload) and Windows to run:

```
dotnet build BootScope.slnx
```

## Download

> **Platform note:** the current release only targets **Windows 10/11 x64**.

The latest packaged build is always available from the
[GitHub Releases page](https://github.com/moezsm/BootScope/releases/latest). Each release
contains:

- `BootScope-Setup-<version>.exe` — a real Windows installer (recommended).
- `BootScope-win-x64-<version>.zip` — a portable, no-install ZIP.
- `BootScope-<version>-SHA256SUMS.txt` — SHA-256 checksums for both files above, so you can
  verify the download (`Get-FileHash <file> -Algorithm SHA256` on Windows, or
  `sha256sum <file>` on Linux/macOS, and compare against the matching line in the checksums
  file).

BootScope is self-contained: no separate .NET installation is required on the target computer
either way.

### Installer (recommended)

1. Download `BootScope-Setup-<version>.exe` from the latest release.
2. Run it. It installs **for the current user only** (into your local app data folder), so
   **no administrator privileges are required**.
3. During setup you can optionally:
   - Create a desktop shortcut.
   - Enable **"Launch BootScope when I sign in to Windows"** (this only ever writes BootScope's
     own value to your personal `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key — no
     Windows service is installed, and no other startup entries are touched).
4. A Start Menu shortcut and a normal Windows "Add or remove programs" entry are created.

For scripted/CI installs, the installer supports Inno Setup's standard silent switches, e.g.:

```
BootScope-Setup-1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES
```

Add `/TASKS="desktopicon,launchatlogin"` (or `/TASKS="!desktopicon"` etc.) to control the
optional tasks during a silent install.

Re-running the installer with a newer version upgrades BootScope in place and **preserves your
saved settings** (`%LocalAppData%\BootScope\settings.json` is never touched by the
installer/uninstaller). Uninstalling BootScope from "Add or remove programs" removes the
installed application files and Start Menu/desktop shortcuts, but likewise **never deletes your
settings file** — remove `%LocalAppData%\BootScope` yourself if you want a full cleanup.

### Portable ZIP

1. Download `BootScope-win-x64-<version>.zip` from the latest release.
2. Extract it anywhere.
3. Double-click `BootScope.exe`. Nothing is installed, and no shortcuts or startup entries are
   created automatically — use the in-app **Settings** window if you want BootScope to launch
   at sign-in.

### Automatic startup

Whether you used the installer task or the in-app **Settings → Launch BootScope when I log
into Windows** toggle, BootScope only ever registers itself under your own user account (HKCU),
never as a Windows service, and never requires administrator privileges. The two are kept in
sync: on first launch, BootScope reads back whatever startup state is already registered
(e.g. from the installer task) rather than silently overriding it, so no duplicate startup
entry is ever created.

### Uninstalling

Use Windows **Settings → Apps → Installed apps** (or the classic **Add or remove programs**)
and remove **BootScope**. This deletes the installed application and shortcuts, disables
automatic startup if it was enabled, and leaves your settings file untouched unless you delete
it yourself.

### A note on SmartScreen

The installer and executable are **not code-signed**. Windows SmartScreen may show a
"Windows protected your PC" warning the first time you run an unsigned download. You can
proceed by choosing **More info → Run anyway**. This is expected for unsigned builds and does
not indicate a corrupted download — you can verify the file against the published SHA-256
checksum if you want extra assurance.

## Publishing a new release

Releases are built and published automatically by the **Build Windows executable** GitHub
Actions workflow (`.github/workflows/windows-executable.yml`). To publish version `1.0.0`:

```
git tag v1.0.0
git push origin v1.0.0
```

Pushing a tag matching `v*.*.*` triggers the workflow, which builds the self-contained app,
packages `BootScope-Setup-<version>.exe` and `BootScope-win-x64-<version>.zip`, verifies both
are non-empty, generates SHA-256 checksums, and publishes a GitHub Release for the tag (with
auto-generated release notes) containing all three files. You can watch progress from the
repository's **Actions** tab, and once it finishes, the new release appears at
`https://github.com/moezsm/BootScope/releases`.

The workflow can also be run manually via `workflow_dispatch` (from the **Actions** tab) to
test the build/packaging/verification steps without publishing a release.

## Building and publishing locally

Requires the .NET 8 SDK (Windows Desktop workload) and Windows to run:

```
dotnet build BootScope.slnx
```

To publish the same self-contained package the release workflow builds:

```
dotnet publish BootScope/BootScope.csproj --configuration Release --property:PublishProfile=Windows-x64
```

The executable is written to
`BootScope/bin/Release/net8.0-windows/win-x64/publish/BootScope.exe`.

To build the installer locally on Windows (requires
[Inno Setup 6](https://jrsoftware.org/isinfo.php)), after publishing as above:

```
ISCC.exe installer\BootScope.iss /DMyAppVersion=1.0.0
```

The installer is written to `dist/BootScope-Setup-1.0.0.exe`.
