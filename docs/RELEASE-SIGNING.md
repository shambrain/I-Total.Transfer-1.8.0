# Release signing

Code signing is a release decision, not a build side effect. This document describes the prepared procedure,
what is verified, and what is currently blocked.

## Current state (2026-10-20)

| Item | State |
| --- | --- |
| `installer/Sign-Artifacts.ps1` | Implemented and statically reviewed; fails closed |
| Windows SDK `signtool.exe` | Not installed on this machine |
| Code-signing certificate (store or PFX) | Not present |
| Signed artifact | None |
| Published hashes | `PROJECT-STATUS.md` lists unsigned SHA-256 values |

Nothing in this repository claims a signature that does not exist. The portable ZIP's `INSTALL.txt` says the
executable is unsigned unless the release notes say otherwise.

## What must be signed

Sign, in this order, then verify each one:

1. `publish\I-Total.Transfer.exe` (before packaging, so the ZIP contains the signed binary).
2. `dist\installer\I-Total.Transfer-<version>-setup.exe` (when Inno Setup produced it).
3. Any MSI or additional executable added to a future package.

Signing after zipping would leave the signed file outside the archive; do not do that.

## Prerequisites

- Windows SDK "Signing Tools" (provides `signtool.exe`) or a standalone copy.
- A code-signing certificate:
  - **OV/EV certificate in the Windows store** on the release machine, or
  - **PFX file** kept outside the repository with its password in `ITOTAL_SIGN_PFX_PASSWORD`.
- Network access to the RFC 3161 timestamp authority (`http://timestamp.digicert.com` by default).

Certificates and passwords must never be committed, copied into `publish`, attached to a release, or pasted
into an issue.

## Procedure

```powershell
# 1. Build and publish exactly as for a normal release (see README).
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# 2. Sign the executable. Either credential form:
$env:ITOTAL_SIGN_THUMBPRINT = 'AB12CD34...'         # certificate already installed
# or
$env:ITOTAL_SIGN_PFX = 'C:\secure\deveg-codesign.pfx'
$env:ITOTAL_SIGN_PFX_PASSWORD = '...'

powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Sign-Artifacts.ps1 `
  -Path .\publish\I-Total.Transfer.exe
```

The script:

1. locates `signtool.exe`;
2. refuses to continue without a credential;
3. with a PFX, imports it into `CurrentUser\My` for the duration of the run so the password never appears on
   a command line, then removes it in `finally`;
4. signs with `/fd sha256 /td sha256 /tr <timestamp>`;
5. runs `signtool verify /pa /v` on every artifact and aborts if verification fails;
6. prints the SHA-256 of each signed file.

Exit codes: `0` success, `2` toolchain or argument missing, `3` credential/certificate missing, `4` signing
failed, `5` verification failed.

## Packaging after signing

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 `
  -Version 1.8.0 -ExpectedSha256 <hash printed by Sign-Artifacts.ps1>
```

`Build-Installer.ps1` refuses to package an executable whose hash does not match, so a stale unsigned binary
cannot slip into a release by accident.

## Verifying a downloaded build (for support)

```powershell
Get-AuthenticodeSignature .\I-Total.Transfer.exe | Format-List Status, StatusMessage, SignerCertificate
Get-FileHash .\I-Total.Transfer.exe -Algorithm SHA256
```

`Status` must be `Valid` and the signer subject must name the publisher. A timestamp means the signature
stays valid after the certificate expires.

## SmartScreen and reputation

A new OV certificate starts with little reputation; SmartScreen may still warn until downloads accumulate.
EV certificates historically gained reputation faster, but this is not a guarantee and is outside the app's
control. Document the expected warning in the release notes rather than telling users to ignore warnings.

## Checklist

- [ ] Version and `CHANGELOG.md` final.
- [ ] Release build: 0 warnings, 0 errors; test suite green.
- [ ] `publish` created from the final source, not an earlier build.
- [ ] Executable signed and `signtool verify /pa` passed.
- [ ] Setup (if produced) signed and verified.
- [ ] Portable ZIP rebuilt **after** signing.
- [ ] `SHA256SUMS.txt` regenerated from the signed artifacts.
- [ ] `PROJECT-STATUS.md` hash table updated.
- [ ] Certificate not exported, password not logged, PFX not committed.

## Why the private signing key is never in the app

The desktop executable contains at most a **public** key, used to verify purchase entitlements. The private
key belongs to the issuing backend. Authenticode signing is a separate key pair, used only by this script on
a release machine. Neither private key is part of the build output or the source tree.
