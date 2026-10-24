# Installation, upgrade and removal

I-Total Transfer ships in two forms. Both are per-user: they never require administrator rights, never create
a firewall rule, and never install a service or scheduled task.

| Form | Produced by | Needs an external tool | State here |
| --- | --- | --- | --- |
| Portable ZIP + PowerShell installer | `installer\Build-Installer.ps1` | No | Complete and tested |
| Inno Setup `setup.exe` | same script, when `ISCC.exe` is found | Inno Setup 6 | Script written; toolchain absent |

The portable form is the supported one until a signed setup executable exists.

## What gets installed where

| Item | Default location |
| --- | --- |
| Executable, PDB, uninstaller, docs | `%LOCALAPPDATA%\Programs\I-Total Transfer` |
| Start Menu shortcut | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\I-Total Transfer.lnk` |
| Uninstall entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\I-Total Transfer` |
| Settings | `%APPDATA%\I-Total Transfer\settings.json` |
| Trial ledger | `%APPDATA%\I-Total Transfer\trial.json` and the same HKCU area |
| Accepted licence | `%APPDATA%\I-Total Transfer\licence.lic` |
| Scan cache | `%APPDATA%\I-Total Transfer\scan-cache\` |
| Logs | `%APPDATA%\I-Total Transfer\logs\` |
| Installation manifest | `%LOCALAPPDATA%\Programs\I-Total Transfer\install-manifest.json` |

User data is deliberately outside the installation folder. An upgrade or uninstall therefore cannot take it
away by accident. It survives reinstall, which is also what keeps an accepted licence and the trial counter
intact.

## Installing from the portable ZIP

```powershell
# Extract, then from the extracted folder:
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-I-Total.Transfer.ps1
```

Useful options:

```powershell
-SourceDir <folder>        # folder containing I-Total.Transfer.exe (default: ..\publish)
-InstallDir <folder>       # defaults to %LOCALAPPDATA%\Programs\I-Total Transfer
-ExpectedSha256 <hex>      # refuse to install if the executable does not match
-StartMenuDir <folder>     # test/portable layouts
-NoShortcut                # no Start Menu entry
-DesktopShortcut           # also create a desktop shortcut (off by default)
-Json                      # machine-readable summary (used by the automated tests)
```

Exit codes: `0` success, `1` application running or bad usage, `2` publish output missing, `3` unsafe
location or refused uninstall, `4` hash mismatch.

A hash mismatch, a running application, a drive root or the Windows folder are all refused **before** any
file is written.

## Upgrading

Run the same command with the new package. The installer:

1. verifies the new executable (and hash, when supplied);
2. replaces the executable and support files;
3. rewrites `install-manifest.json` and the uninstall registry values;
4. leaves settings, trial ledger, licence, cache and logs untouched.

Downgrades use the same path. There is no database schema to migrate in the install folder; session
manifests live beside the destination and are already versioned by the application.

## Uninstalling

From Settings > Apps, or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-I-Total.Transfer.ps1 -Uninstall
```

The uninstaller:

- reads `install-manifest.json` and removes only the recorded file names (plus the known support files);
- removes the Start Menu/desktop shortcuts and the HKCU uninstall entry;
- removes the installation folder only when it is empty afterwards;
- **keeps** `%APPDATA%\I-Total Transfer` by default;
- refuses to run against a folder that has neither the executable nor a manifest, so a mistyped path cannot
  wipe unrelated files.

To remove user data as well:

```powershell
... -Uninstall -RemoveUserData
```

`-RemoveUserData` only deletes a folder that contains at least one of this product's known data files
(`settings.json`, `trial.json`, `licence.lic`). Pointing it at Documents does nothing.

## Inno Setup path

`installer\I-Total.Transfer.iss` mirrors the same layout with `PrivilegesRequired=lowest` and
`DefaultDirName={localappdata}\Programs\I-Total Transfer`. It adds the usual wizard, Start Menu group and an
optional desktop icon. It contains no firewall, service or scheduled-task entries, and no uninstall rule for
`%APPDATA%`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 -Version 1.8.0
```

If `ISCC.exe` is not found the script prints:

```
SETUP     SKIPPED - Inno Setup 6 (ISCC.exe) is not installed on this machine.
```

and still produces the portable ZIP and `SHA256SUMS.txt`. That is the current state of this workspace. See
`RELEASE-SIGNING.md` for signing the setup once it exists.

## Security properties

- No elevation: everything is per-user; there is no `runas` prompt and no machine-wide state.
- No silent network exposure: the installer never opens a port. Wireless receiving is opt-in from the
  application and binds only to a private interface (see `WIRELESS.md`).
- Hash-checked install when `-ExpectedSha256` is supplied; the packaging script always supplies it.
- Manifest-scoped removal: uninstall deletes a fixed, recorded list, never a wildcard tree.
- No secrets: the installer contains no certificate, key or password; signing is separate.

## Automated evidence

`tests/I-Total.Transfer.Tests/InstallerScriptTests.cs` runs the real script against temporary folders and a
temporary HKCU key and asserts:

1. install writes the executable, manifest and uninstaller, and the registry entry names the uninstaller;
2. upgrade replaces the executable while `settings.json` and `trial.json` survive;
3. uninstall (run through the installed copy) removes the folder and registry key but keeps user data;
4. a wrong hash exits `4` and installs nothing;
5. uninstalling an unrelated folder exits `3` and deletes nothing.

These are automated tests, not a substitute for the Windows 11 install drill listed in
`DEVICE_TEST_MATRIX.md`.
