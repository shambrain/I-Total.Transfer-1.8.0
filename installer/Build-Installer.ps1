#requires -Version 5.1
<#
.SYNOPSIS
    Builds the distributable installers for I-Total Transfer from an existing publish folder.

.DESCRIPTION
    Two artifacts are possible:

      1. A portable ZIP (always produced). It contains the single-file executable, the per-user installer
         script, and the operator documentation.
      2. An Inno Setup executable (produced only when ISCC.exe is available). Inno Setup is an external
         toolchain, so this script reports a skipped setup honestly instead of pretending one exists.

    No certificate or password is accepted here. Signing is a separate, explicit step
    (installer\Sign-Artifacts.ps1) so a release can never be published unsigned by accident after a build
    that merely had no certificate available.

.PARAMETER Version
    Product version written into names and metadata. Defaults to 1.8.0.

.PARAMETER PublishDir
    Folder produced by `dotnet publish` (default: ..\publish).

.PARAMETER OutputDir
    Where artifacts are written (default: ..\dist\installer).

.PARAMETER ExpectedSha256
    Optional expected hash of publish\I-Total.Transfer.exe. The build fails rather than packaging an
    executable that changed unexpectedly.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 -Version 1.8.0
#>
[CmdletBinding()]
param(
    [string]$Version = '1.8.0',
    [string]$PublishDir,
    [string]$OutputDir,
    [string]$IsccPath,
    [string]$ExpectedSha256,
    [switch]$PortableOnly
)

$ErrorActionPreference = 'Stop'
$Product = 'I-Total Transfer'
$ExeName = 'I-Total.Transfer.exe'
$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if (-not $PublishDir) { $PublishDir = Join-Path $RepoRoot 'publish' }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'dist\installer' }
$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

function Fail([int]$Code, [string]$Message) {
    [Console]::Error.WriteLine($Message)
    exit $Code
}

$Exe = Join-Path $PublishDir $ExeName
if (-not (Test-Path -LiteralPath $Exe)) {
    Fail 2 "The published executable was not found at '$Exe'. Run 'dotnet publish' first."
}

$Hash = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash
if ($ExpectedSha256 -and $ExpectedSha256.ToUpperInvariant() -ne $Hash) {
    Fail 4 "Hash mismatch: the published executable is $Hash, not $($ExpectedSha256.ToUpperInvariant()). Refusing to package it."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$Staging = Join-Path ([System.IO.Path]::GetTempPath()) ('itotal-portable-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Staging -Force | Out-Null

try {
    Copy-Item -LiteralPath $Exe -Destination (Join-Path $Staging $ExeName)
    $Pdb = Join-Path $PublishDir 'I-Total.Transfer.pdb'
    if (Test-Path -LiteralPath $Pdb) { Copy-Item -LiteralPath $Pdb -Destination (Join-Path $Staging 'I-Total.Transfer.pdb') }
    foreach ($Optional in @('steam_appid.txt', 'steam_entitlement.lic')) {
        $From = Join-Path $PublishDir $Optional
        if (Test-Path -LiteralPath $From) { Copy-Item -LiteralPath $From -Destination (Join-Path $Staging $Optional) }
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-I-Total.Transfer.ps1') -Destination (Join-Path $Staging 'Install-I-Total.Transfer.ps1')
    foreach ($Doc in @('README.md', 'CHANGELOG.md', 'LICENSE')) {
        $From = Join-Path $RepoRoot $Doc
        if (Test-Path -LiteralPath $From) { Copy-Item -LiteralPath $From -Destination (Join-Path $Staging $Doc) }
    }

    $InstallText = @"
I-Total Transfer $Version - portable package

Run the application:
    I-Total.Transfer.exe

Install for this user only (no administrator prompt, no firewall change):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-I-Total.Transfer.ps1

Uninstall (keeps settings, trial and licence under %APPDATA%\I-Total Transfer):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-I-Total.Transfer.ps1 -Uninstall

The executable is not code-signed in this package unless the release notes say otherwise. Check the
SHA-256 in SHA256SUMS.txt before running it.

Executable SHA-256:
    $Hash
"@
    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $Staging 'INSTALL.txt'), $InstallText, $Utf8)

    $ZipName = "$Product-$Version-portable-win-x64.zip".Replace(' ', '.')
    $ZipPath = Join-Path $OutputDir $ZipName
    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Compress-Archive -Path (Join-Path $Staging '*') -DestinationPath $ZipPath -CompressionLevel Optimal
    $ZipHash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash
    Write-Host "PORTABLE  $ZipPath"
    Write-Host "SHA-256   $ZipHash"

    $SetupPath = ''
    if (-not $PortableOnly) {
        if (-not $IsccPath) {
            $Candidates = @(
                (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
                (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
                (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
            )
            foreach ($Candidate in $Candidates) {
                if ($Candidate -and (Test-Path -LiteralPath $Candidate)) { $IsccPath = $Candidate; break }
            }
            if (-not $IsccPath) {
                $OnPath = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
                if ($OnPath) { $IsccPath = $OnPath.Source }
            }
        }

        if ($IsccPath -and (Test-Path -LiteralPath $IsccPath)) {
            & $IsccPath "/DMyAppVersion=$Version" "/O$OutputDir" (Join-Path $PSScriptRoot 'I-Total.Transfer.iss')
            if ($LASTEXITCODE -ne 0) { Fail 5 "Inno Setup failed with exit code $LASTEXITCODE. The portable ZIP is still valid." }
            $SetupPath = Join-Path $OutputDir ("$Product-$Version-setup.exe".Replace(' ', '.'))
            if (Test-Path -LiteralPath $SetupPath) {
                Write-Host "SETUP     $SetupPath"
                Write-Host "SHA-256   $((Get-FileHash -LiteralPath $SetupPath -Algorithm SHA256).Hash)"
            } else {
                Write-Host "SETUP     reported success but no setup executable was found in $OutputDir"
                $SetupPath = ''
            }
        } else {
            Write-Host "SETUP     SKIPPED - Inno Setup 6 (ISCC.exe) is not installed on this machine."
            Write-Host "          The portable ZIP is complete and installable with Install-I-Total.Transfer.ps1."
            Write-Host "          Install Inno Setup 6, or pass -IsccPath, to produce the setup executable."
        }
    }

    $Sums = New-Object System.Collections.Generic.List[string]
    $Sums.Add("$ZipHash  $ZipName")
    if ($SetupPath) { $Sums.Add("$((Get-FileHash -LiteralPath $SetupPath -Algorithm SHA256).Hash)  $([System.IO.Path]::GetFileName($SetupPath))") }
    [System.IO.File]::WriteAllLines((Join-Path $OutputDir 'SHA256SUMS.txt'), $Sums, $Utf8)

    Write-Host "EXE       $Exe"
    Write-Host "SHA-256   $Hash"
}
finally {
    Remove-Item -LiteralPath $Staging -Recurse -Force -ErrorAction SilentlyContinue
}
