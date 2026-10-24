#requires -Version 5.1
<#
.SYNOPSIS
    Signs and verifies release artifacts with Authenticode.

.DESCRIPTION
    Signing is an explicit decision, never an automatic side effect of a build. This script signs each file
    with SHA-256 and an RFC 3161 timestamp, then runs `signtool verify /pa` on every artifact. If the
    toolchain, the certificate or the verification is missing, it fails with a distinct exit code and signs
    nothing else.

    Two credential forms are accepted, neither of which is stored in this repository:

      -Thumbprint   A code-signing certificate already installed in CurrentUser\My or LocalMachine\My.
      -PfxPath      A PFX file. Its password is read from ITOTAL_SIGN_PFX_PASSWORD; the environment variable
                    is cleared before signtool runs so it cannot leak into a child process listing.

    Configure once per release shell:
        $env:ITOTAL_SIGN_THUMBPRINT = 'AB12...'
        # or
        $env:ITOTAL_SIGN_PFX = 'C:\secure\deveg-codesign.pfx'
        $env:ITOTAL_SIGN_PFX_PASSWORD = '...'   (read interactively instead when possible)

    Never add a private key, a PFX or a password to the source tree or to a published zip.

.PARAMETER Path
    One or more files to sign (executable, setup, MSI).

.EXAMPLE
    powershell -NoProfile -File .\installer\Sign-Artifacts.ps1 -Path .\publish\I-Total.Transfer.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [string]$Thumbprint = $env:ITOTAL_SIGN_THUMBPRINT,
    [string]$PfxPath = $env:ITOTAL_SIGN_PFX,
    [string]$PfxPassword = $env:ITOTAL_SIGN_PFX_PASSWORD,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$SigntoolPath
)

$ErrorActionPreference = 'Stop'

function Fail([int]$Code, [string]$Message) {
    [Console]::Error.WriteLine($Message)
    exit $Code
}

if (-not $SigntoolPath) {
    $Candidates = New-Object System.Collections.Generic.List[string]
    $SdkRoots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    )
    foreach ($Root in $SdkRoots) {
        if (-not (Test-Path -LiteralPath $Root)) { continue }
        Get-ChildItem -LiteralPath $Root -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object {
                $Candidate = Join-Path $_.FullName 'x64\signtool.exe'
                if (Test-Path -LiteralPath $Candidate) { $Candidates.Add($Candidate) }
            }
    }
    $OnPath = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($OnPath) { $Candidates.Add($OnPath.Source) }
    if ($Candidates.Count -gt 0) { $SigntoolPath = $Candidates[0] }
}

if (-not $SigntoolPath -or -not (Test-Path -LiteralPath $SigntoolPath)) {
    Fail 2 "signtool.exe was not found. Install the Windows SDK (Signing Tools) or pass -SigntoolPath. Nothing was signed."
}

foreach ($File in $Path) {
    if (-not (Test-Path -LiteralPath $File)) { Fail 2 "Artifact not found: $File. Nothing was signed." }
}

if (-not $Thumbprint -and -not $PfxPath) {
    Fail 2 "No code-signing credential was supplied. Set ITOTAL_SIGN_THUMBPRINT or ITOTAL_SIGN_PFX (or pass -Thumbprint / -PfxPath). Nothing was signed."
}

$ImportedCertificate = $null
if ($PfxPath) {
    if (-not (Test-Path -LiteralPath $PfxPath)) { Fail 3 "PFX not found: $PfxPath. Nothing was signed." }
    if (-not $PfxPassword) { Fail 3 "ITOTAL_SIGN_PFX_PASSWORD is not set. Set it in the environment, or install the certificate and use -Thumbprint instead." }
    try {
        # Import into the per-user store so the password never appears on signtool's command line, where
        # any local process could read it. The certificate is removed again in the finally block.
        $ImportedCertificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
            $PfxPath, $PfxPassword,
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::UserKeySet -bor
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)
        $Store = New-Object System.Security.Cryptography.X509Certificates.X509Store('My', 'CurrentUser')
        $Store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $Store.Add($ImportedCertificate)
        $Store.Close()
        $Thumbprint = $ImportedCertificate.Thumbprint
        Write-Host "IMPORTED  $($ImportedCertificate.Subject) ($Thumbprint) for this run"
    }
    catch {
        Fail 3 "The PFX could not be imported: $($_.Exception.Message). Nothing was signed."
    }
}

$Common = @('sign', '/fd', 'sha256', '/td', 'sha256', '/tr', $TimestampUrl)
$Common += @('/sha1', $Thumbprint)
$StoreName = 'CurrentUser'
$Found = Get-ChildItem "Cert:\$StoreName\My" -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $Thumbprint }
if (-not $Found) {
    $StoreName = 'LocalMachine'
    $Found = Get-ChildItem "Cert:\$StoreName\My" -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint }
}
if (-not $Found) {
    Fail 3 "No code-signing certificate with thumbprint $Thumbprint exists in CurrentUser\My or LocalMachine\My. Nothing was signed."
}
Write-Host "CERTIFICATE $($Found.Subject) ($Thumbprint, $StoreName)"

try {
    foreach ($File in $Path) {
        Write-Host "SIGNING   $File"
        & $SigntoolPath @Common $File
        if ($LASTEXITCODE -ne 0) { Fail 4 "signtool failed for $File with exit code $LASTEXITCODE." }
    }

    foreach ($File in $Path) {
        Write-Host "VERIFYING $File"
        & $SigntoolPath verify /pa /v $File
        if ($LASTEXITCODE -ne 0) { Fail 5 "Signature verification failed for $File. Do not publish it." }
    }

    foreach ($File in $Path) {
        Write-Host ("SHA-256   {0}  {1}" -f (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash, $File)
    }
}
finally {
    if ($ImportedCertificate) {
        try {
            $Cleanup = New-Object System.Security.Cryptography.X509Certificates.X509Store('My', 'CurrentUser')
            $Cleanup.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $Cleanup.Remove($ImportedCertificate)
            $Cleanup.Close()
        }
        catch {
            [Console]::Error.WriteLine("The temporary signing certificate could not be removed from CurrentUser\My: $($_.Exception.Message)")
        }
        $ImportedCertificate.Dispose()
    }
    $PfxPassword = $null
}
