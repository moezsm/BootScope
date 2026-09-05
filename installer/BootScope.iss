; Inno Setup script for BootScope.
;
; Produces BootScope-Setup-{version}.exe, a per-user Windows installer for the self-contained
; win-x64 publish output. Built by .github/workflows/windows-executable.yml using the Inno
; Setup Compiler (ISCC.exe) that is preinstalled on GitHub's windows-latest runners.
;
; Command-line build example (run from the repository root, after publishing):
;   ISCC.exe installer\BootScope.iss /DMyAppVersion=1.0.0 /DMyFileVersion=1.0.0.0 ^
;     /DPublishDir=BootScope\bin\Release\net8.0-windows\win-x64\publish /DOutputDir=dist
;
; All /D parameters have sensible defaults below so the script can also be compiled locally
; without passing them, provided the app has been published to the default path first.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyFileVersion
  #define MyFileVersion "1.0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\BootScope\bin\Release\net8.0-windows\win-x64\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define MyAppName "BootScope"
#define MyAppPublisher "BootScope"
#define MyAppExeName "BootScope.exe"
#define MyAppURL "https://github.com/moezsm/BootScope"

[Setup]
; Fixed AppId so upgrades are recognized as the same product (no duplicate Start Menu/
; Add-or-Remove-Programs entries across versions). This GUID was generated once for BootScope
; and must NEVER change across releases, or existing installs will no longer be recognized as
; upgradable (Inno Setup would treat the new version as a different product). If this project
; is ever forked/renamed into an unrelated product, generate a fresh GUID for it (e.g. via
; Tools > Generate GUID in the Inno Setup IDE, or any standard GUID generator) instead of
; reusing this one.
AppId={{6C6E8C9F-6E9A-4C8B-9C4C-6F6E8F5C3B21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyFileVersion}
; Per-user install: no administrator privileges are ever requested or required. This mirrors
; the app's own safety principle of running with normal user permissions, and lets the
; "launch at sign in" task write to HKCU without elevation.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#OutputDir}
OutputBaseFilename=BootScope-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Windows 10 and Windows 11 x64 only - matches the self-contained win-x64 publish output.
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "launchatlogin"; Description: "Launch BootScope when I sign in to Windows"; GroupDescription: "Startup options:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Registers (or, on uninstall, removes) only BootScope's own per-user Run value - the exact
; same HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BootScope value that
; StartupRegistrationService manages from within the app, so the installer option and the
; in-app "Launch BootScope when I log into Windows" Settings toggle stay in sync. The path is
; quoted so it keeps working if the install directory ever contains spaces.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BootScope"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: launchatlogin; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch BootScope"; Flags: nowait postinstall skipifsilent

; Intentionally no [UninstallDelete] entries for %LocalAppData%\BootScope: user settings
; (settings.json) live outside the install directory and are never touched by the installer or
; uninstaller, so they are preserved across upgrades and reinstalls, and are only removed if the
; user manually deletes that folder.
