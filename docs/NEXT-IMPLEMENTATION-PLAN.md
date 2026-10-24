# Native backend and release plan

## Evidence-based design

- WPD discovery starts with `IPortableDeviceManager::GetDevices`; each device is opened with `IPortableDevice::Open` and client information. Object IDs are opaque IDs, not filesystem paths.
- Enumerate recursively using `IPortableDeviceContent::EnumObjects`; fetch object properties in batches where supported, retaining the device object ID, parent ID, original filename, content type, size, and modification metadata.
- Read file data with `IPortableDeviceContent::Transfer` and `IPortableDeviceResources::GetStream` using `WPD_RESOURCE_DEFAULT` and `STGM_READ`. Use the driver-provided optimal buffer size; release every COM interface and task-allocated object ID deterministically.
- The source SHA-256 must be calculated while reading the actual WPD `IStream`; write bytes to a same-volume temporary file while computing SHA-256, flush, then hash/re-read destination or use the same stream digest plus independent destination digest. Commit only after size and digest checks pass. WPD object IDs should be persisted only as scan-session hints, not treated as durable across reconnects.
- A native WPD implementation must preserve a separately selectable Shell fallback because Apple drivers/iOS versions may expose different object sets. It must report which backend and capabilities were actually used.

## AFC dependency policy

Do not ship an unverified DLL copied from an arbitrary download. Pin a specific upstream libimobiledevice release/build, record source commit, compiler/toolchain, target architecture, dependency versions, license notices, SHA-256 for every native binary, and test on clean Windows 10/11 VMs. AFC access to `/var/mobile/Media` is not equivalent to unrestricted access to Apple's private Photos database and must not be advertised as such. If native libraries cannot be pinned and reproducibly packaged, disable AFC as an optional capability rather than silently loading arbitrary DLLs.

## Wireless architecture

A Windows receiver alone cannot initiate an iPhone Photos-library export. The wireless feature needs an explicit iOS-side sender: a companion iOS app or a user-installed Shortcut. Pairing should use a short-lived QR code containing a random, single-use pairing secret; require local-network opt-in, bind the receiver only to the selected private interface, use TLS with certificate pinning or an equivalent authenticated session, rotate tokens, expire pairing, show the receiving device and files, and require user approval. Never expose an unauthenticated upload endpoint or automatically open firewall ports. Do not claim seamless iOS background access without validating Apple's platform restrictions.

**Implemented (1.8.0).** Every clause above now has code and a test behind it, with one deliberate
substitution and one honest gap. *Substitution:* certificate-pinned TLS is **not** implemented, because an iOS
Shortcut cannot pin a self-signed certificate; the "equivalent authenticated session" the clause allows is what
ships - a single-use pairing secret (`Core/PairingState.cs`) redeemed once for a session token that expires,
rotates and is revoked, over a receiver bound to one private interface (`Core/PrivateNetwork.cs`) and behind an
approval gate (`Core/ApprovalInbox.cs`). `WirelessRequireTls` still asks for `https://` and uses it when a
certificate is bound; when it is not, the fallback is stated in the UI, the log and `/health` rather than
claimed. *Gap:* no physical iOS sender has been run against the receiver. The complete sender contract, the
threat model and the certificate procedure are in [`WIRELESS.md`](WIRELESS.md); the acceptance line that stays
open is in [`RELEASE-ACCEPTANCE.md`](RELEASE-ACCEPTANCE.md).

## Android backend

An Android phone reaches Windows through the same transport as an iPhone (MTP, surfaced as Windows
Portable Devices — see [`RESEARCH-SOURCES.md`](RESEARCH-SOURCES.md)). No second COM surface was written:
the family selects a storage *policy*, not a backend.

- **Family as a policy.** `Core/DeviceFamily.cs` classifies an attached device from what it calls itself
  — friendly name, manufacturer, model — and never from `WPD_DEVICE_TYPE`, which Microsoft documents as
  being about visual appearance and states must not be used to decide function. `Core/PortableStorage.cs`
  holds the Android/generic-MTP storage rules; `Core/CameraRoll.cs` keeps the iPhone's;
  `Devices/Wpd/WpdDevice.cs` runs one COM implementation and picks the policy.
- **Whole volumes, not one container.** An Android phone publishes storage volumes ("Internal shared
  storage", an SD card). Each is walked in full and keeps its own name as its destination folder, so an
  internal volume and a card are never merged. Music and Files are advertised for Android because they are
  genuinely there; an unidentified device keeps the conservative iPhone answer.
- **What Android refuses is skipped, not reported as failure.** `Android\data` and `Android\obb` are not
  readable by a file-transfer client, and thumbnail caches are derived data; descending would buy an
  error per app and thousands of redundant round trips. `Android\media` is kept, because that is where
  apps such as WhatsApp keep photos.
- **Its failure modes are named.** A phone set to "Charging only" or "Transfer photos" (PTP) exposes an
  empty tree or camera folders alone, and a locked phone answers with an empty folder; all three look
  identical to the operator and all three are stated — in the device message, as a scan warning, and on
  the diagnostics page.
- **No Apple strings on an Android device.** The dashboard, the device header and the diagnostics page
  had iPhone wording throughout ("IPHONE READY", "tap Trust", "iCloud originals"); each now follows the
  detected family, and an unattached device is asked to identify itself rather than being called an
  iPhone.
- **Two phones are a choice.** Both families share one portable-device bus, so `AppSettings.PreferredHandset`
  (Settings → *When both phones are attached, read*) reorders the two families. It only reorders: the other
  family is still read when it is the only one attached, and the phone that is not read is named. The Apple
  USB diagnosis filters attached devices by family for the same reason — an Android phone answering on that
  bus had appeared under a row titled "Apple handsets seen over USB".
- **A vanished device stops the walk.** An object that cannot be read marks its folder a failed read (the
  scan is `Incomplete`), and twenty consecutive failures abort the enumeration with
  `DeviceStoppedAnsweringException`. This came out of a real mid-scan disconnect, where the walk had reported
  `Complete` over a tree whose tail it could no longer read.

**Implemented (1.8.0); walk and copy are now live-verified on a HUAWEI Y7 Prime 2018 (2026-10-20).** The app
read both volumes (4,106 files / 16,114,057,157 bytes, `Complete`) and copied a filtered selection (412
verified + 108 unverifiable + 0 failed); resume preserved the unverifiable files. The classifier, storage
policy, category mapping, diagnostics wording, two-phone order and disconnect guard are covered by tests.
Cancel -> resume, locked-screen abort, PTP-only mode and an Android throughput benchmark remain open, and
that distinction is carried into [`RELEASE-ACCEPTANCE.md`](RELEASE-ACCEPTANCE.md) rather than blurred.

## Designer scope

A genuine WinForms Designer conversion requires each screen and control to be instantiated in generated `InitializeComponent` code with stable field declarations and event hookups; runtime data binding and event logic can remain in `MainForm.cs`. Moving only a root panel into a `.Designer.cs` file does not make the remaining screen layouts drag-and-drop editable. Migrate one page at a time, open each in Visual Studio Designer, save/reopen, and test at 100%, 125%, 150%, and 200% DPI on narrow and wide windows.

## Release gates

1. Build and test on supported Windows 10 configuration and Windows 11 x64 with a clean restore.
2. Test locked, unlocked/untrusted, trusted, disconnected-during-enumeration, disconnect-during-stream, empty DCIM, inaccessible folder, duplicate names, long paths, Unicode names, low disk space, and iCloud-optimized files.
3. Test the user's 26,400+ item library; reconcile folder/file counts and byte totals against an independent tool and inspect all skipped/inaccessible items.
4. Validate cancellation and resume across cable removal, app crash, PC sleep, destination-full, and corrupted staged/destination bytes.
5. Publish performance results only from recorded hardware, iOS version, cable/port, file count, byte count, elapsed time, and backend.

## Commercial subsystem (implemented in 1.8.0, with external gaps named)

The plan previously left licensing, the trial, the installer and signing as open work. They are now in the
tree and tested:

- **Trial and entitlement service** (`src/I-Total.Transfer/Licensing/`): three meaningful jobs by default,
  signed RSA-PSS entitlements, signed revocations, direct and Steam channels, dual-store ledger, wireless
  metering, and a Settings Licence section. See `MASTER_IMPLEMENTATION_PLAN.md` section G.
- **Per-user installer** (`installer/Install-I-Total.Transfer.ps1`): install, upgrade, uninstall, hash check,
  manifest-scoped removal, data preservation. Tested by executing it.
- **Packaging and signing** (`installer/Build-Installer.ps1`, `Sign-Artifacts.ps1`,
  `I-Total.Transfer.iss`): portable ZIP is produced; setup and signature are blocked on the toolchain and
  certificate and are never claimed.

What remains, in order:

1. **Hardware**: Android walk/copy/PTP, locked phone, mid-scan pull on the fixed exe, two-phone preference,
   cancel/resume, HEIC+MOV, benchmark and buffer sweep (`DEVICE_TEST_MATRIX.md`).
2. **Package and sign**: decide the channel, obtain a certificate and Inno Setup, sign, verify, republish
   hashes (`RELEASE-SIGNING.md`).
3. **Production entitlements**: offline key pair, issuing backend, purchase URL, then live activation, refund
   and revocation drills.
4. **Steamworks**: confirm eligibility, then either integrate the SDK or document the signed-entitlement
   channel as the shipping model.
5. **Presentation**: Windows 11 visual drill, 200 % DPI, a real iOS wireless sender.
