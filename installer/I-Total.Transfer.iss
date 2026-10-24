; I-Total Transfer — per-user Inno Setup script.
;
; This is the packaged-setup path. It installs into %LOCALAPPDATA%\Programs\I-Total Transfer with
; PrivilegesRequired=lowest, so no UAC prompt appears and no administrator account is needed. It never
; creates a firewall rule, a service or a scheduled task. User data (%APPDATA%\I-Total Transfer) is not
; installed and is not removed by the uninstaller.
;
; Build with installer\Build-Installer.ps1. Inno Setup 6 is an external toolchain: when ISCC.exe is absent
; the build script produces the portable ZIP and states plainly that no setup executable was made.
;
; Signing: do not embed a certificate here. Pass a configured SignTool to ISCC, e.g.
;   ISCC.exe /DMyAppVersion=1.8.0 /Ssigntool="signtool.exe sign /fd sha256 /tr http://timestamp.digicert.com $f"
; or sign the produced setup with installer\Sign-Artifacts.ps1 afterwards. See docs/RELEASE-SIGNING.md.

#ifndef MyAppVersion
  #define MyAppVersion "1.8.0"
#endif

#define MyAppName "I-Total Transfer"
#define MyAppPublisher "Deveg"
#define MyAppExeName "I-Total.Transfer.exe"
#define MyAppId "{{6F0E5B64-3C1B-4B8E-9C5B-7B1B0C9E4A21}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\I-Total Transfer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=..\dist\installer
OutputBaseFilename=I-Total.Transfer-{#MyAppVersion}-setup
SetupIconFile=..\src\I-Total.Transfer\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
DisableWelcomePage=no
LicenseFile=..\LICENSE
InfoBeforeFile=..\README.md

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\I-Total.Transfer.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\steam_appid.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\steam_entitlement.lic"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\installer\Install-I-Total.Transfer.ps1"; DestDir: "{app}"; DestName: "Uninstall-I-Total.Transfer.ps1"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

; Deliberately no [UninstallDelete] entry under {userappdata}: settings, the trial ledger, an accepted
; licence and the scan cache are the operator's data and must survive an uninstall or a reinstall.
