#requires -Version 5.1
<#
.SYNOPSIS
    Per-user installer, upgrader and uninstaller for I-Total Transfer.

.DESCRIPTION
    A safe installer has to do five things honestly: place the files where a normal user may write, never
    ask for administrator rights, never open a firewall port, leave the operator's settings/licence/trial
    state alone during an upgrade, and be reversible. This script does exactly that, without an installer
    toolchain.

    It installs to %LOCALAPPDATA%\Programs\I-Total Transfer by default (no UAC prompt), writes a Start Menu
    shortcut, registers a per-user uninstall entry under HKCU, and records every file it put on disk in
    install-manifest.json. Uninstall removes only the files named by that manifest, so an unrelated file in
    the same folder is never touched.

    User data lives in %APPDATA%\I-Total Transfer (settings, trial ledger, licence, scan cache, logs). It is
    kept on uninstall unless -RemoveUserData is passed, because that folder is the operator's data and the
    trial/licence state must survive a reinstall.

.PARAMETER SourceDir
    Folder produced by `dotnet publish` (default: ..\publish relative to this script).

.PARAMETER ExpectedSha256
    When supplied, installation refuses to proceed unless the published executable has this hash.

.PARAMETER Uninstall
    Removes the installed files, shortcuts and registry entry. Keeps user data unless -RemoveUserData.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-I-Total.Transfer.ps1 -Json

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-I-Total.Transfer.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$SourceDir,
    [string]$InstallDir,
    [string]$RegistryKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\I-Total Transfer',
    [string]$UserDataDir,
    [string]$StartMenuDir,
    [string]$ExpectedSha256,
    [switch]$Uninstall,
    [switch]$RemoveUserData,
    [switch]$NoShortcut,
    [switch]$DesktopShortcut,
    [switch]$Force,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$Product = 'I-Total Transfer'
$ExeName = 'I-Total.Transfer.exe'
$PdbName = 'I-Total.Transfer.pdb'
$ManifestName = 'install-manifest.json'
$ReadmeName = 'README-INSTALL.txt'
$UninstallScriptName = 'Uninstall-I-Total.Transfer.ps1'

if (-not $InstallDir) { $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\I-Total Transfer' }
if (-not $UserDataDir) { $UserDataDir = Join-Path $env:APPDATA 'I-Total Transfer' }
if (-not $StartMenuDir) { $StartMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs' }
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
$UserDataDir = [System.IO.Path]::GetFullPath($UserDataDir)

function Write-Result($Value) {
    if ($Json) {
        Write-Output ($Value | ConvertTo-Json -Compress)
    } else {
        foreach ($Key in $Value.Keys) { Write-Host ("{0}: {1}" -f $Key, $Value[$Key]) }
    }
}

function Stop-Install([int]$Code, [string]$Message) {
    [Console]::Error.WriteLine($Message)
    if ($Json) { Write-Output (@{ ok = $false; error = $Message } | ConvertTo-Json -Compress) }
    exit $Code
}

function Test-SafeInstallRoot([string]$Path) {
    # Installing into a drive root, the Windows folder or a shared program root would mix our files with
    # other software and make uninstall ambiguous. Refuse those and name a safe alternative instead.
    $Full = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $Roots = @(
        [System.IO.Path]::GetPathRoot($Full).TrimEnd('\'),
        $env:SystemRoot.TrimEnd('\'),
        $env:WINDIR.TrimEnd('\'),
        $env:ProgramFiles.TrimEnd('\'),
        ${env:ProgramFiles(x86)}.TrimEnd('\')
    )
    foreach ($Root in $Roots) {
        if ($Root -and $Full -ieq $Root) { return $false }
    }
    return $true
}

function Get-StartMenuShortcutPath([string]$Directory) {
    return (Join-Path $Directory ($Product + '.lnk'))
}

function Remove-Shortcuts() {
    $Candidates = @(
        (Get-StartMenuShortcutPath $StartMenuDir),
        (Join-Path ([Environment]::GetFolderPath('Desktop')) ($Product + '.lnk'))
    )
    foreach ($Shortcut in $Candidates) {
        if (Test-Path -LiteralPath $Shortcut) {
            try { Remove-Item -LiteralPath $Shortcut -Force }
            catch { [Console]::Error.WriteLine("Could not remove shortcut ${Shortcut}: $($_.Exception.Message)") }
        }
    }
}

function Invoke-Uninstall {
    $ManifestPath = Join-Path $InstallDir $ManifestName
    $ExePath = Join-Path $InstallDir $ExeName

    $Known = @($ExeName, $PdbName, $ManifestName, $UninstallScriptName, $ReadmeName,
        'steam_appid.txt', 'steam_entitlement.lic')
    $Files = $Known

    if (Test-Path -LiteralPath $ManifestPath) {
        try {
            $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
            if ($Manifest.files) {
                # Union the recorded files with the names this version knows about, so an older manifest
                # that predates a file (or an interrupted upgrade) still leaves nothing behind.
                $Files = @($Known + @($Manifest.files) | Select-Object -Unique)
            }
        } catch {
            [Console]::Error.WriteLine("The install manifest could not be read; only the known file names will be removed.")
        }
    }

    if (-not (Test-Path -LiteralPath $ExePath) -and -not (Test-Path -LiteralPath $ManifestPath) -and -not $Force) {
        Stop-Install 3 "Refusing to uninstall: '$InstallDir' does not look like an I-Total Transfer installation (no executable and no manifest). Use -Force only if you are certain."
    }

    foreach ($Name in $Files) {
        if ([string]::IsNullOrWhiteSpace($Name)) { continue }
        # The manifest contains file names only. Reject separators so a tampered manifest cannot point the
        # uninstaller outside its own folder.
        if ($Name -match '[\\/]' -or $Name -eq '..' -or $Name -eq '.') {
            [Console]::Error.WriteLine("Ignoring unsafe manifest entry: $Name")
            continue
        }
        $Target = Join-Path $InstallDir $Name
        if (Test-Path -LiteralPath $Target) {
            try { Remove-Item -LiteralPath $Target -Force }
            catch { [Console]::Error.WriteLine("Could not remove ${Target}: $($_.Exception.Message)") }
        }
    }

    Remove-Shortcuts

    if (Test-Path -LiteralPath $RegistryKey) {
        try { Remove-Item -LiteralPath $RegistryKey -Recurse -Force }
        catch { [Console]::Error.WriteLine("Could not remove the uninstall registry entry: $($_.Exception.Message)") }
    }

    $UserDataKept = $true
    if ($RemoveUserData -and (Test-Path -LiteralPath $UserDataDir)) {
        # Only delete a folder that carries our own marker files. Pointing -UserDataDir at, say, Documents
        # must never wipe it.
        $LooksOurs = (Test-Path -LiteralPath (Join-Path $UserDataDir 'settings.json')) -or
                     (Test-Path -LiteralPath (Join-Path $UserDataDir 'trial.json')) -or
                     (Test-Path -LiteralPath (Join-Path $UserDataDir 'licence.lic'))
        if ($LooksOurs) {
            try { Remove-Item -LiteralPath $UserDataDir -Recurse -Force; $UserDataKept = $false }
            catch { [Console]::Error.WriteLine("Could not remove user data: $($_.Exception.Message)") }
        } else {
            [Console]::Error.WriteLine("Refusing to delete '$UserDataDir': it has none of this product's data files.")
        }
    }

    if ((Test-Path -LiteralPath $InstallDir) -and -not (Get-ChildItem -LiteralPath $InstallDir -Force)) {
        try { Remove-Item -LiteralPath $InstallDir -Force } catch { }
    }

    Write-Result ([ordered]@{
        ok = $true
        action = 'uninstall'
        installDir = $InstallDir
        userDataKept = $UserDataKept
        userDataDir = $UserDataDir
        registryKeyRemoved = -not (Test-Path -LiteralPath $RegistryKey)
    })
}

function Invoke-Install {
    if (-not $SourceDir) { $SourceDir = Join-Path $PSScriptRoot '..\publish' }
    $SourceDir = [System.IO.Path]::GetFullPath($SourceDir)

    $SourceExe = Join-Path $SourceDir $ExeName
    if (-not (Test-Path -LiteralPath $SourceExe)) {
        Stop-Install 2 "The published executable was not found at '$SourceExe'. Run 'dotnet publish' first, or pass -SourceDir."
    }

    if (-not (Test-SafeInstallRoot $InstallDir)) {
        Stop-Install 3 "Refusing to install directly into '$InstallDir'. Choose a private folder such as '$env:LOCALAPPDATA\Programs\I-Total Transfer'."
    }

    $Hash = (Get-FileHash -LiteralPath $SourceExe -Algorithm SHA256).Hash
    if ($ExpectedSha256 -and $ExpectedSha256.ToUpperInvariant() -ne $Hash) {
        Stop-Install 4 "Hash mismatch: expected $($ExpectedSha256.ToUpperInvariant()) but the published executable is $Hash. Nothing was installed."
    }

    $Running = Get-Process -Name 'I-Total.Transfer' -ErrorAction SilentlyContinue
    if ($Running) {
        Stop-Install 1 "I-Total Transfer is running. Close it before installing or upgrading so the executable is not locked."
    }

    $ManifestPath = Join-Path $InstallDir $ManifestName
    $PreviousVersion = $null
    if (Test-Path -LiteralPath $ManifestPath) {
        try {
            $Previous = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
            $PreviousVersion = $Previous.version
        } catch { }
    }

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    $Files = New-Object System.Collections.Generic.List[string]
    Copy-Item -LiteralPath $SourceExe -Destination (Join-Path $InstallDir $ExeName) -Force
    $Files.Add($ExeName)

    foreach ($Optional in @($PdbName, 'steam_appid.txt', 'steam_entitlement.lic')) {
        $From = Join-Path $SourceDir $Optional
        if (Test-Path -LiteralPath $From) {
            Copy-Item -LiteralPath $From -Destination (Join-Path $InstallDir $Optional) -Force
            $Files.Add($Optional)
        }
    }

    $SelfPath = $PSCommandPath
    if (-not $SelfPath) { $SelfPath = $MyInvocation.MyCommand.Path }
    if ($SelfPath -and (Test-Path -LiteralPath $SelfPath)) {
        Copy-Item -LiteralPath $SelfPath -Destination (Join-Path $InstallDir $UninstallScriptName) -Force
        $Files.Add($UninstallScriptName)
    } else {
        [Console]::Error.WriteLine("The installer could not copy itself as the uninstaller; use Settings > Apps to remove the files instead.")
    }

    $Readme = @"
I-Total Transfer

Installed by:      per-user installer (no administrator rights)
Executable:        $ExeName
Uninstall:         Settings > Apps > I-Total Transfer, or run:
                   powershell -NoProfile -ExecutionPolicy Bypass -File "$UninstallScriptName" -Uninstall
User data:         $UserDataDir
                   Settings, the trial ledger and an accepted licence live there.
                   Uninstalling keeps this folder unless -RemoveUserData is passed.

The application never opens a firewall port on its own. Wireless receiving is
started explicitly from the Wireless page and binds to a private address only.
"@
    [System.IO.File]::WriteAllText((Join-Path $InstallDir $ReadmeName), $Readme, (New-Object System.Text.UTF8Encoding($false)))
    $Files.Add($ReadmeName)

    $Version = (Get-Item -LiteralPath $SourceExe).VersionInfo.FileVersion
    if (-not $Version) { $Version = (Get-Item -LiteralPath $SourceExe).VersionInfo.ProductVersion }
    if (-not $Version) { $Version = '0.0.0.0' }
    $Manifest = [ordered]@{
        schema = 1
        product = $Product
        version = $Version
        installedUtc = (Get-Date).ToUniversalTime().ToString('o')
        sourceSha256 = $Hash
        installDir = $InstallDir
        userDataDir = $UserDataDir
        files = @($Files)
    }
    [System.IO.File]::WriteAllText($ManifestPath, ($Manifest | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))

    if (-not $NoShortcut) {
        New-Item -ItemType Directory -Path $StartMenuDir -Force | Out-Null
        $Shell = New-Object -ComObject WScript.Shell
        $ShortcutPath = Get-StartMenuShortcutPath $StartMenuDir
        $Shortcut = $Shell.CreateShortcut($ShortcutPath)
        $Shortcut.TargetPath = Join-Path $InstallDir $ExeName
        $Shortcut.WorkingDirectory = $InstallDir
        $Shortcut.IconLocation = (Join-Path $InstallDir $ExeName) + ',0'
        $Shortcut.Description = 'Phone to Windows media transfer, verified'
        $Shortcut.Save()

        if ($DesktopShortcut) {
            $Desktop = [Environment]::GetFolderPath('Desktop')
            $DesktopShortcutPath = Get-StartMenuShortcutPath $Desktop
            $DesktopLink = $Shell.CreateShortcut($DesktopShortcutPath)
            $DesktopLink.TargetPath = Join-Path $InstallDir $ExeName
            $DesktopLink.WorkingDirectory = $InstallDir
            $DesktopLink.IconLocation = (Join-Path $InstallDir $ExeName) + ',0'
            $DesktopLink.Save()
        }
    }

    New-Item -Path $RegistryKey -Force | Out-Null
    $InstallBytes = 0
    foreach ($Name in $Files) {
        $Item = Get-Item -LiteralPath (Join-Path $InstallDir $Name) -ErrorAction SilentlyContinue
        if ($Item) { $InstallBytes += $Item.Length }
    }
    $UninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' +
        (Join-Path $InstallDir $UninstallScriptName) + '" -Uninstall -InstallDir "' + $InstallDir +
        '" -RegistryKey "' + $RegistryKey + '" -UserDataDir "' + $UserDataDir + '"'

    Set-ItemProperty -Path $RegistryKey -Name DisplayName -Value $Product
    Set-ItemProperty -Path $RegistryKey -Name DisplayVersion -Value $Version
    Set-ItemProperty -Path $RegistryKey -Name Publisher -Value 'Deveg'
    Set-ItemProperty -Path $RegistryKey -Name DisplayIcon -Value ((Join-Path $InstallDir $ExeName) + ',0')
    Set-ItemProperty -Path $RegistryKey -Name InstallLocation -Value $InstallDir
    Set-ItemProperty -Path $RegistryKey -Name UninstallString -Value $UninstallCommand
    Set-ItemProperty -Path $RegistryKey -Name QuietUninstallString -Value $UninstallCommand
    Set-ItemProperty -Path $RegistryKey -Name NoModify -Value 1 -Type DWord
    Set-ItemProperty -Path $RegistryKey -Name NoRepair -Value 1 -Type DWord
    Set-ItemProperty -Path $RegistryKey -Name EstimatedSize -Value ([int]($InstallBytes / 1024)) -Type DWord
    Set-ItemProperty -Path $RegistryKey -Name InstallDate -Value (Get-Date -Format 'yyyyMMdd')

    $Action = 'install'
    if ($PreviousVersion) { $Action = 'upgrade' }
    $ShortcutReported = ''
    if (-not $NoShortcut) { $ShortcutReported = Get-StartMenuShortcutPath $StartMenuDir }

    Write-Result ([ordered]@{
        ok = $true
        action = $Action
        version = $Version
        previousVersion = $PreviousVersion
        installDir = $InstallDir
        userDataDir = $UserDataDir
        sha256 = $Hash
        files = @($Files)
        shortcut = $ShortcutReported
    })
}

if ($Uninstall) { Invoke-Uninstall } else { Invoke-Install }
