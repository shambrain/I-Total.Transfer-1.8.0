# Research sources checked — 2026-10-16

This is a compact source register for the architecture decisions. Forum posts are anecdotal evidence of failure modes, not proof of protocol behavior or universal causes.

## Primary sources

1. **Microsoft Learn — WPD `IPortableDeviceContent::EnumObjects`**  
   https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-enumobjects  
   Documents that the method enumerates immediate child objects and returns an enumerator of object IDs. Design implication: recurse object IDs, query properties, do not assume a folder path from an ID.

2. **Microsoft Learn — Windows App SDK**  
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/  
   Official Windows desktop application platform guidance. WinUI 3 is the recommended native UI framework for new Windows desktop applications. The current source GUI remains a WinForms compatibility implementation until the backend contract stabilizes.

3. **Microsoft Learn — SQLite data access in Windows apps**  
   https://learn.microsoft.com/en-us/windows/apps/develop/data-access/sqlite-data-access  
   Documents SQLite usage and Microsoft.Data.Sqlite in Windows applications. SQLite is appropriate for durable per-file state, transactional queue updates and resumable sessions.

4. **libimobiledevice — AFC public header**  
   https://github.com/libimobiledevice/libimobiledevice/blob/master/include/libimobiledevice/afc.h  
   Source of truth for AFC C function signatures, error codes, file modes, service name and directory/file-info API semantics. Use the header from the exact pinned native build for P/Invoke declarations.

5. **libimobiledevice — ifuse README**  
   https://github.com/libimobiledevice/ifuse  
   Documents AFC-backed access to the `/var/mobile/Media` chroot by default, and distinguishes that from unrestricted/root filesystem access. This is why AFC is a capability-probed optional backend, not a promise of complete private Photos-library access.

6. **Archived imobiledevice-net binding**  
   https://github.com/libimobiledevice-win32/imobiledevice-net  
   GitHub identifies this repository as archived (Jan 2024). Avoid assuming it is a maintained, current .NET dependency.

7. **Current imobiledevice-net fork status**  
   https://github.com/libimobiledevice-NET/imobiledevice-net  
   The project describes itself as in maintenance mode and warns that the latest commit may not compile. Pinning it without a reproducible build and integration tests is not production-safe.

8. **Apple Support — If your computer doesn't recognize your iPhone or iPad**  
   https://support.apple.com/en-us/108643  
   Official troubleshooting for USB connection, device trust and Apple software.

9. **Apple Support — Transfer photos and videos from your iPhone to your computer**  
   https://support.apple.com/en-us/HT201302  
   Official photo-transfer guidance. When iCloud Photos is enabled, originals must be downloaded to the iPhone before USB import if they are not stored locally; users may instead use iCloud for Windows.

## Community reports sampled

10. **Reddit r/iphone — “issues transferring photos to windows after ios26 update”**  
    https://www.reddit.com/r/iphone/comments/1nkd4mr/issues_transferring_photos_to_windows_after_ios26/  
    Users report folders appearing inconsistently after reconnects and report “Keep Originals” as a workaround. Treat this as a symptom report, not a guarantee that the setting fixes every device.

11. **Reddit r/iphone — “Why is transferring photos from phone to pc so hard?” (Apr 2026)**  
    https://www.reddit.com/r/iphone/comments/1sur7qx/why_is_transferring_photos_from_phone_to_pc_so/  
    Reports large-library transfers that stall or fail and Live Photo pairs being split. This motivates resumability, per-file outcomes and paired-asset reporting.

12. **Reddit r/iphone — 35k photos/videos from iPhone to PC (Oct 2026)**  
    https://www.reddit.com/r/iphone/comments/1x31r3s/transferring_35k_photosvideos_75_gb_from_iphone/  
    Recent report of long enumeration, slow transfer and malformed-looking small files. Community evidence only; use it to design tests, not infer a single root cause.

## Research conclusions

- The original concept's general goals are sound: persistent queue, explicit per-file state, destination preflight, resumability, robust logs and strict completion reporting.
- Claims that AFC is automatically faster, reveals all DCIM assets, or provides full private Photos-library access are not established by these sources. Treat performance and coverage as testable capabilities, not assumptions.
- A second scan can detect changes, but cannot establish that the device exposed every library asset.
- Source-side SHA-256 is unavailable unless the chosen backend can read source bytes and hash them or provides a trusted hash. Destination-only hashing does not prove source/destination equality.

## Additional research used for v1.2.0 (2026-10-16)
- Microsoft Learn, `IPortableDeviceResources::GetStream`: https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream — native WPD resource streams provide an `IStream` for object content, and callers must release the stream; a folder's stream is not recursive. This supports a future direct stream backend but does not mean this release implements it.
- Microsoft Learn, WinForms Designer DPI troubleshooting: https://learn.microsoft.com/en-us/visualstudio/designers/disable-dpi-awareness?view=visualstudio — documents designer scaling differences and `ForceDesignerDPIUnaware` support in VS 2022 17.8+ for .NET 6+ WinForms.
- Reddit, iOS 26 folder visibility / “Keep Originals” reports: https://www.reddit.com/r/iphone/comments/1nkd4mr/issues_transferring_photos_to_windows_after_ios26/ — anecdotal reports of missing DCIM folders and mixed results with “Keep Originals”; not an authoritative platform guarantee.
- Reddit, large-library Windows transfer failures: https://www.reddit.com/r/iphone/comments/1sur7qx/why_is_transferring_photos_from_iphone_to_pc_so/ — community reports of long transfers, failed imports, and Live Photo photo/video pairs; anecdotal rather than benchmark evidence.


## Additional native-backend references (2026-10-17)

- Microsoft `IPortableDeviceResources::GetStream`: https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream — stream content using the object ID and WPD resource key; caller releases returned stream; use optimal buffer size.
- Microsoft WPD COM content transfer sample: https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/PortableDeviceCOM/cpp/ContentTransfer.cpp — demonstrates `IPortableDeviceContent::Transfer`, resource acquisition and `GetStream`.
- Microsoft WPD enumeration sample: https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/multimedia/wpd/wpdapisample/cpp/ContentEnumeration.cpp — enumerates child object IDs and explicitly frees returned IDs.
- Upstream libimobiledevice releases: https://github.com/libimobiledevice/libimobiledevice/releases — native dependency versions must be pinned and validated before packaging.
- Windows binary distribution project (community-maintained, not Microsoft/Apple): https://github.com/jrjr/libimobiledevice-windows — useful candidate for build provenance review, not accepted as a trusted dependency without independent verification.

## UI framework decision for v1.7.0 (2026-10-18)

The brief asked for the latest Fluent Design language, "built around WinUI 3", and explicitly kept
Windows 10 support. The decision was to modernise the existing WinForms app **to Fluent, on .NET 10**,
rather than rewrite the shell in WinUI 3. The evidence:

- **Windows App SDK (WinUI 3) still supports Windows 10 1809+.** Microsoft: "Windows App SDK APIs run on
  Windows 11 and earlier versions starting from Windows 10, version 1809."
  https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/ — so Win10 alone is not the reason to
  avoid it.
- **The same page documents that the Windows App SDK can be added to an existing WinForms app**
  ("It can be added to existing apps built with WPF, Windows Forms, Win32, or other application
  frameworks"), including XAML Islands. So WinUI is a real future option for individual surfaces; a full
  shell rewrite is not required to adopt it.
- **`Application.SetColorMode` is the supported way to give Win32 common controls the app colour mode,
  and its dark mode is Windows 11 only.** Microsoft: "the dark color mode is only available from Windows 11
  on or later versions", and if the system changes its setting "the application will not automatically
  adapt". https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode —
  this is why the app re-themes itself on `UserPreferenceChanged` and why combo boxes fall back to a
  themed flat style on Windows 10.
- **Decision consequence:** rewriting in WinUI 3 would have discarded the working device layer (WPD COM
  interop over `IPortableDeviceContent`/`IPortableDeviceResources`, the AFC P/Invoke surface, the SQLite
  session store and the wireless receiver), the Designer-authored page layouts, and every layout and DPI
  fix already verified by capture — with no way to build or test WinUI 3 packaging in this environment.
  The Fluent work therefore reuses all of that and replaces the presentation layer instead: a control
  library (`UI/Controls`), a banner-derived palette (`UI/Theme.cs`) and a new shell.
- **Windows 10 is past consumer end of support (14 Oct 2025).** The app still targets it because the
  requirement asked for it, and every Win11-only capability (rounded corners, caption tint, common-control
  dark chrome) is feature-detected at runtime rather than assumed.

## QR code decision for v1.8.0 (2026-10-19)

The wireless pairing ticket is shown as a URL **and** a QR code, so the app needs an encoder. Considered:

- **QRCoder 1.6.0 / ZXing.Net.** Both are capable and both were available (QRCoder was already in the local
  NuGet cache). Rejected: they add a third-party dependency to a security-relevant display path, and the
  supported subset here is small and fully specified - pairing URLs are ASCII, well under 200 bytes, so
  **byte mode, error-correction level M, versions 1-10** is the whole requirement.
- **A system encoder.** None is exposed on Windows without a dependency.

Decision: implement the encoder in `Core/QrCode.cs` against the published standard, **ISO/IEC 18004** (QR Code
bar-code symbology - mode indicators, character count indicators, Reed-Solomon blocks, the interleaving rule
for multi-block symbols, mask evaluation and format information), and prove it with a **test-only decoder** in
`tests/I-Total.Transfer.Tests/QrDecoder.cs` that shares no code with the encoder. The proof is therefore not
"the picture looks right" but "an independent implementation recovers the exact payload", across every version
the encoder claims including the multi-block versions 4 and up. Both the encoder and the decoder were written
here; the decoder is not shipped.

## Android and generic-MTP research for v1.8.0 (2026-10-20)

The Android work was designed from sources, not from a device: **no Android phone had been attached when the
rules below were written**, so every rule states the evidence it rests on, and the claims that could only be
sourced to community reports are labelled as such rather than presented as specification. (A HUAWEI Y7 Prime
2018 was attached later, on 2026-10-20; the application then walked both volumes end to end — 4,106 files /
16,114,057,157 bytes, `Complete` — and copied a filtered selection, which confirms the volume-per-storage
rule. The rules below were still designed from sources; the live run is evidence, not a redesign.)

- **MTP is the transport Android and iOS share.** Microsoft's `Windows.Devices.Portable` reference lists
  what a WPD storage device is: "USB mass storage flash drives and cameras; Media Transfer Protocol (MTP)
  music players, **phones**, and cameras; Picture Transfer Protocol (PTP) cameras."
  https://learn.microsoft.com/en-us/uwp/api/windows.devices.portable — this is why Android needed no
  second COM surface: `IPortableDevice` already reaches it, so the family selects a storage *policy*
  rather than a second backend.
- **The device type must not decide behaviour.** Microsoft's `WPD_DEVICE_TYPES` reference says
  applications "may use these values to determine the generic visual appearance of the device", then
  states: "WPD applications must use the capabilities of the portable device to determine functionally,
  not the **WPD_DEVICE_TYPE** value."
  https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/portabledevice/ne-portabledevice-tagwpd_device_types
  — so `Core/DeviceFamily.cs` classifies from what the device calls itself and from what it exposes, and
  never reads the type.
- **What "complete" may mean on Android.** AOSP's `MtpDatabase` javadoc (main branch) states: "MtpDatabase
  provides an interface for MTP operations that MtpServer can use. To do this, it uses MtpStorageManager
  for filesystem operations and MediaProvider to get media metadata. File operations are also reflected
  in MediaProvider if possible."
  https://android.googlesource.com/platform/frameworks/base/+/refs/heads/main/media/java/android/mtp/MtpDatabase.java
  — the Windows-visible tree is Android's own MTP view of its storage, assembled from filesystem
  operations plus media metadata, so a scan is a scan of *what the device exposed* and is described that
  way rather than as a filesystem walk.
- **Two failure modes, community-documented rather than specified.** A Microsoft Q&A thread on a Samsung
  S24 ("the S24 is shown in the explorer but ... this folder is empty") collects both: a locked phone
  "can show the device but returns an empty folder", and the recommended path begins with selecting
  "File Transfer" (MTP) as the USB mode. This is a community answer, not vendor documentation, so the app
  raises the *possibility* ("may be in PTP mode", "unlock the phone") and never asserts the cause.
  https://learn.microsoft.com/en-au/answers/questions/5688285/when-i-connect-my-samsung-s24-to-the-pc-with-a-usb
- **The shape of Android shared storage, community-sampled.** A published listing of a phone's MTP root
  shows `Alarms`, `Android`, `DCIM`, `Download` and app folders directly under a container named
  "Internal storage" — which is why `Core/PortableStorage.cs` walks every volume in full and keeps the
  volume's own name, instead of looking for a `DCIM` container the way the iPhone policy does.
  https://askubuntu.com/questions/702447/folders-on-android-internal-storage-show-as-files-via-mtp
- **App-private trees are not readable over MTP.** Community reports and Android's scoped-storage rules
  agree that a file-transfer client cannot read `Android/data` or `Android/obb`, while `Android/media`
  remains readable — which is where apps such as WhatsApp keep photos. The policy skips the first two and
  keeps the third; the skip is what stops the walk spending a device round trip per app collecting
  errors.
- **No published performance claim.** The live walk (~6 s for 4,106 files) and copy (1:15 for 520 planned)
  are single observations, not benchmarks. This release states no Android throughput, buffer sweep or
  device-write result; the Android cancel/lock/PTP legs of [`RELEASE-ACCEPTANCE.md`](RELEASE-ACCEPTANCE.md)
  remain open.

## Licensing, signing and installer references (2026-10-20)

- **RSA-PSS is the signature scheme, not PKCS#1 v1.5.** RFC 8017 (PKCS #1 v2.2) defines RSASSA-PSS with
  MGF1 and a salt; .NET exposes it as `RSA.SignData(..., RSASignaturePadding.Pss)` with SHA-256. PSS is
  used because the verifier only ever holds a public key and the scheme has a security proof with a
  randomised salt; the format also gives a clean place to bind product, channel and app id inside the signed
  bytes.
  https://www.rfc-editor.org/rfc/rfc8017
  https://learn.microsoft.com/dotnet/api/system.security.cryptography.rsa.signdata
- **Entitlement envelope**: the app signs the exact Base64 payload text and verifies it before deserialising,
  which avoids the "parse first, verify later" class of bug; malformed documents become verdicts
  (`EntitlementCodecTests`), never exceptions.
- **Timestamps**: Authenticode signatures are timestamped with RFC 3161 so they remain valid after the
  certificate expires. `signtool` is instructed with `/tr <url> /td sha256`.
  https://www.rfc-editor.org/rfc/rfc3161
  https://learn.microsoft.com/windows/win32/seccrypto/signtool
- **Authenticode and SmartScreen**: `signtool verify /pa` is the check the script enforces; SmartScreen
  reputation is accumulated by the certificate/download history and cannot be guaranteed by the application.
  New OV certificates commonly warn until reputation builds, so the release notes say so instead of telling
  users to ignore warnings.
  https://learn.microsoft.com/windows/win32/seccrypto/authenticode
  https://learn.microsoft.com/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/
- **Per-user installation**: Inno Setup's `PrivilegesRequired=lowest` and `{localappdata}` support the
  no-elevation model; the script mirrors the same rules as the PowerShell installer rather than inventing a
  second layout.
  https://jrsoftware.org/ishelp/
- **Windows PowerShell 5.1 encoding**: a BOM-less `.ps1` is read as ANSI, so a UTF-8 em dash becomes a smart
  quote and changes parsing. The installer scripts are therefore ASCII-only; this was found by running
  `Build-Installer.ps1`, not by reading about it.
  https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_character_encoding
- **Steam**: entitlement binding follows the app-id model in the Steamworks documentation; a live ownership
  check needs the SDK and a partner account, neither of which exists here, so the signed-file channel is the
  supported one and the limitation is stated in `KNOWN_ISSUES.md`.
  https://partner.steamgames.com/doc/features

## 2026-10-29 — WPD enumerator ownership (checked for the COM-cleanup fix)

| Item | Value |
| --- | --- |
| Source | Microsoft Learn, `IPortableDeviceContent::EnumObjects` (portabledeviceapi.h) |
| URL | https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-enumobjects |
| Accessed | 2026-10-29 |
| Finding | The `ppEnum` out-parameter receives an `IEnumPortableDeviceObjectIDs`, and the documentation states: "The caller must release this interface when it is done with it." |
| Relevance | `WpdDevice.EnumerateChildren` created one enumerator per folder and never released it. The code now releases it in a `finally` block (`ReleaseCom`), which is what the documentation requires. |
| Evidence class | Primary vendor documentation, fetched this session. |
| Caveat | The same page does not discuss `IPortableDeviceValues`; releasing that object follows the general COM ownership rule (every interface obtained from a COM call is released by the caller), and is an inference from that rule rather than a claim taken from this page. |