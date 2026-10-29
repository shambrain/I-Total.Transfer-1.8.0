using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using ITotalTransfer.Core;

namespace ITotalTransfer.Devices.Wpd;

/// <summary>
/// Native Windows Portable Devices backend, used for both handset families because the transport is the
/// same. All COM work runs on a dedicated MTA apartment (<see cref="ComApartment"/>). It enumerates the
/// WPD object tree by object ID, reads file content through <c>IPortableDeviceResources::GetStream</c>
/// (streaming, never loading whole files into memory) and hashes the raw source bytes while reading,
/// which enables true source-to-destination verification.
///
/// <para>
/// What differs between an iPhone and an Android phone is not the transport but the storage behind it,
/// so the family selects the storage policy rather than the backend: an iPhone presents one camera-roll
/// container (<see cref="CameraRoll"/>), while an Android phone presents whole storage volumes full of
/// user files (<see cref="PortableStorage"/>). One COM surface, two policies, is deliberately smaller
/// than two nearly identical COM backends that would have to be fixed twice.
/// </para>
///
/// IMPORTANT: WPD object IDs are opaque and not durable across reconnects; they are treated as
/// session hints only. This backend is read-only: neither iOS nor Android exposes its storage for
/// writing to an MTP client in a form this app should use, so
/// <see cref="BackendCapabilities.CanWriteToDevice"/> is false.
/// </summary>
public sealed class WpdDevice : ITransferBackend
{
    public BackendKind Kind => BackendKind.Wpd;
    public string Name => "Windows Portable Devices (native)";
    public bool CanHashSourceBytes => true;
    public BackendCapabilities Capabilities => BuildCapabilities(_family);

    /// <summary>
    /// What the backend can honestly expose, which depends on the family it is reading.
    ///
    /// <para>
    /// An iPhone exposes one camera-roll area, so Photos and Videos are all this transport can offer.
    /// An Android device exposes whole storage volumes, and Photos, Videos, Music and Files are all
    /// genuinely there — advertising only the camera groups, as the iPhone rule does, would present an
    /// Android phone as a device that has no music and no documents.
    /// </para>
    ///
    /// <para>
    /// Before a device has been identified the conservative iPhone answer is used, so an unidentified
    /// transport never advertises groups the app has not actually found.
    /// </para>
    /// </summary>
    private static BackendCapabilities BuildCapabilities(DeviceFamily family)
    {
        bool generic = family == DeviceFamily.Android;
        return new(
            CanEnumerate: true,
            CanReadSource: true,
            CanHashSourceBytes: true,
            CanReadBackCommitted: true,
            CanWriteToDevice: false,
            SupportsTimestamps: true,
            Notes: generic
                ? "Streams objects through IPortableDeviceResources::GetStream and hashes raw source bytes while reading. Read-only: this app never writes to the device."
                : "Streams objects through IPortableDeviceResources::GetStream and hashes raw source bytes while reading. Read-only toward the iPhone.")
        {
            Categories = PortableStorage.CategoriesFor(family)
        };
    }

    private readonly ComApartment _apartment = new();
    private readonly object _gate = new();
    private IPortableDevice? _device;
    private IPortableDeviceContent? _content;
    private IPortableDeviceProperties? _properties;
    private IPortableDeviceResources? _resources;
    private string? _deviceId;
    private string? _mediaRootObjectId;
    /// <summary>The family whose storage rules the attached device is being read with.</summary>
    private DeviceFamily _family = DeviceFamily.Unknown;
    /// <summary>
    /// The phone this backend selected. A probe chooses one phone; every later scan and copy must read
    /// that same phone. Without this, a moment in which the selected iPhone stops answering makes
    /// <see cref="FindDevice"/> fall through to the Android phone on the same bus, and a scan the
    /// operator started for the iPhone silently returns the HUAWEI's tree.
    /// </summary>
    private readonly DeviceSelectionGuard _selected = new();
    /// <summary>A generic MTP device's storage volumes, each with the label used for its destination folder.</summary>
    private readonly List<(string Id, string Label)> _volumes = [];
    private readonly FolderNameIndex _nameIndex;
    private readonly ScanProgressReporter _scanReporter = new();
    private readonly System.Diagnostics.Stopwatch _scanClock = new();
    private long _scanBytes;
    private bool _disposed;
    private readonly int _copyBufferBytes;
    private readonly HandsetPreference _preferredHandset;

    /// <summary>
    /// Creates the backend.
    ///
    /// <para>
    /// <paramref name="copyBufferBytes"/> comes from the Settings "copy buffer size" option, so the
    /// streamed copy uses the granularity the operator chose. It used to be hard-coded at 1 MB while the
    /// setting was silently ignored — a control that could not work.
    /// </para>
    ///
    /// <para>
    /// <paramref name="preferredHandset"/> only decides which family is looked for first when both an
    /// iPhone and an Android phone are attached; the other family is still used when it is the only one
    /// present.
    /// </para>
    /// </summary>
    public WpdDevice(int copyBufferBytes = 1024 * 1024,
        HandsetPreference preferredHandset = HandsetPreference.AppleFirst)
    {
        _copyBufferBytes = Math.Clamp(copyBufferBytes, 64 * 1024, 4 * 1024 * 1024);
        _preferredHandset = preferredHandset;
        // Reading a device folder costs a round-trip per property, so a path lookup must not repeat it:
        // the index remembers each folder's children for the life of the session. This is what keeps a
        // resumed run, which has no object ids to reuse, linear in the number of files rather than quadratic.
        _nameIndex = new FolderNameIndex(parentId => EnumerateChildren(parentId).Select(DescribeChild));
    }

    private DeviceChild DescribeChild(string objectId)
    {
        try
        {
            ObjectProps props = GetProps(objectId);
            return new DeviceChild(objectId, props.Name, props.IsFolder);
        }
        catch
        {
            // A child that cannot be described cannot be the file a path names; the index drops it by name.
            return new DeviceChild(objectId, string.Empty, false);
        }
    }

    // ------------------------------------------------------------------ Probe
    public DeviceInfo Probe() => Run(() =>
    {
        try
        {
            string? id = FindDevice(out string friendly, out DeviceFamily family, out _,
                out IReadOnlyList<string> unrecognised, out string? otherAttached);
            if (id is null)
            {
                return new DeviceInfo(DeviceStatus.NotFound, "",
                    _selected.IsBound ? _selected.GoneMessage(null) : NoDeviceMessage(unrecognised))
                {
                    BackendName = Name,
                    Family = _selected.Family
                };
            }
            if (_selected.IsDifferent(id))
            {
                return new DeviceInfo(DeviceStatus.NotFound, "", _selected.GoneMessage(friendly))
                {
                    BackendName = Name,
                    Family = _selected.Family
                };
            }

            _selected.Bind(id, friendly, family);
            _family = family;
            (string model, string serial, string firmware) = ReadDeviceProperties(id);

            // The device is on the bus: from here on every failure mode means "connected but not
            // readable", never "not connected", so the shell can show an honest indicator.
            DeviceInfo info = new(DeviceStatus.MediaUnavailable, friendly, ConnectedMessage(family))
            {
                Model = string.IsNullOrWhiteSpace(model) ? null : model,
                SerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial,
                OperatingSystem = string.IsNullOrWhiteSpace(firmware) ? null : firmware,
                ConnectionType = family == DeviceFamily.Android ? "USB (MTP)" : "USB (WPD)",
                StableId = id,
                BackendName = Name,
                Family = family
            };

            if (otherAttached is not null)
                info = info with
                {
                    Message = info.Message + " Another device is attached as well (" + otherAttached +
                              "); this app reads one device at a time."
                };

            // A locked handset refuses a content session; that is a trust problem, not a missing device.
            try
            {
                EnsureOpen(id);
            }
            catch (Exception ex)
            {
                ResetSession();
                return info with
                {
                    Status = family == DeviceFamily.Android ? DeviceStatus.MediaUnavailable : DeviceStatus.LockedOrUntrusted,
                    Message = family == DeviceFamily.Android
                        ? "The Android device is connected but its content session was refused (" + ex.Message +
                          "). Unlock the phone, set the USB notification to \"File transfer\", then check the connection again."
                        : "The iPhone is connected but its content session was refused (" + ex.Message +
                          "). Unlock the iPhone, tap Trust, then check the connection again."
                };
            }

            if (family == DeviceFamily.Android)
            {
                // Android publishes one storage volume per volume of the phone (its internal storage,
                // and one more for each card). A just-unlocked phone answers with an empty tree for a
                // moment, so the lookup retries in the same way the iPhone's does.
                List<(string Id, string Label)> volumes = ResolveVolumesWithRetry(attempts: 5, delayMs: 800);
                if (volumes.Count == 0)
                {
                    int roots = CountChildren(WpdKeys.DeviceObjectId);
                    return info with
                    {
                        Status = DeviceStatus.MediaUnavailable,
                        Message = roots <= 0
                            ? "The device is connected, but Windows Portable Devices returned an empty device tree. Unlock the phone and set its USB notification to \"File transfer\", then check again."
                            : "Windows Portable Devices can see the device's storage, but nothing in it can be read. Unlock the phone and set the USB notification to \"File transfer\" rather than \"Charging only\" or \"Transfer photos\", then check again."
                    };
                }
                return info with
                {
                    Status = DeviceStatus.Ready,
                    Message = "Device storage is accessible through native WPD (hash-verified transfers available)."
                };
            }

            // An MTP object tree is frequently empty for the first seconds after a connect or an
            // unlock, so the lookup retries before it is allowed to conclude the media is missing.
            string? mediaRoot = ResolveMediaRootWithRetry(attempts: 5, delayMs: 800);
            if (mediaRoot is null)
            {
                int roots = CountChildren(WpdKeys.DeviceObjectId);
                return info with
                {
                    Status = DeviceStatus.MediaUnavailable,
                    Message = roots <= 0
                        ? "The iPhone is connected, but Windows Portable Devices returned an empty device tree. Unlock the screen, tap Trust, then check again."
                        : "Windows Portable Devices can see the iPhone's storage, but it exposed neither a DCIM container nor the camera-roll albums. Unlock the screen, tap Trust, and if photos are stored only in iCloud, turn off \"Optimize iPhone Storage\" for this transfer."
                };
            }
            return info with { Status = DeviceStatus.Ready, Message = "iPhone media is accessible through native WPD (hash-verified transfers available)." };
        }
        catch (Exception ex)
        {
            return new DeviceInfo(DeviceStatus.Error, "", "WPD probe failed: " + ex.Message) { BackendName = Name };
        }
    });

    // ------------------------------------------------------------------ Scan
    public ScanResult Scan(CancellationToken cancellationToken, Action<string>? progress = null) => Run(() =>
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        _scanBytes = 0;
        _scanClock.Restart();
        // The tree is about to be read afresh; anything the last lookup remembered may already be stale.
        _nameIndex.Clear();
        var files = new List<MediaFile>();
        var folders = new List<FolderScanInfo>();
        var warnings = new List<string>();

        string? id = FindDevice(out string friendly, out DeviceFamily family, out _,
            out IReadOnlyList<string> unrecognised, out _);
        if (id is null)
        {
            throw new InvalidOperationException(_selected.IsBound
                ? _selected.GoneMessage(null)
                : NoDeviceMessage(unrecognised));
        }
        if (_selected.IsDifferent(id))
            throw new InvalidOperationException(_selected.GoneMessage(friendly));

        _selected.Bind(id, friendly, family);
        _family = family;
        EnsureOpen(id);

        if (family == DeviceFamily.Android)
        {
            List<(string Id, string Label)> volumes = ResolveVolumes(refresh: true);
            if (volumes.Count == 0)
                throw new InvalidOperationException(
                    "The device exposed no storage through Windows Portable Devices. Unlock the phone, set its USB " +
                    "notification to \"File transfer\", then scan again.");

            // Every volume is walked, not just the one holding the camera roll: on Android the camera
            // folders are a small part of what the operator asked to back up, and stopping at DCIM
            // would report a complete scan of a phone whose Music, Movies, Downloads and Documents had
            // never been looked at.
            foreach ((string volumeId, string label) in volumes)
                EnumerateRecursive(volumeId, label, files, folders, warnings, cancellationToken, progress,
                    skipUnreadableTrees: true);

            // A camera-only listing is the signature of the phone being in "Transfer photos" (PTP)
            // mode. Saying so turns a scan that looks complete but is not into one that explains itself.
            if (volumes.Count > 0 && PortableStorage.LooksLikePhotoTransferOnly(ChildNames(volumes[0].Id)))
                warnings.Add(PortableStorage.PhotoTransferOnlyWarning);
        }
        else
        {
            string? mediaRootId = ResolveMediaRootObjectId();
            if (mediaRootId is null)
                throw new InvalidOperationException("The iPhone did not expose a DCIM container or camera-roll albums through WPD.");

            EnumerateRecursive(mediaRootId, CameraRoll.RootLabel, files, folders, warnings, cancellationToken, progress,
                skipUnreadableTrees: false);
        }

        var duplicatePaths = files.GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).Take(20).ToArray();
        if (duplicatePaths.Length > 0)
            warnings.Add("The device exposed duplicate destination paths: " + string.Join(", ", duplicatePaths));
        int unknownSize = files.Count(f => !f.SizeKnown);
        if (unknownSize > 0)
            warnings.Add($"{unknownSize:N0} file(s) reported no readable size; they can be copied but not size-verified.");

        EnumerationReport report = ScanAccounting.BuildReport(Name, files, folders, duplicatePaths.Length, warnings);
        var status = report.Status;

        progress?.Invoke($"WPD scan finished: {files.Count:N0} files in {folders.Count:N0} folders ({status}).");
        return new ScanResult(string.IsNullOrWhiteSpace(friendly) ? DeviceFamilies.Describe(family) : friendly,
            started, DateTimeOffset.UtcNow, files, warnings)
        {
            Report = report,
            Family = family,
            StableId = id
        };
    });

    private void EnumerateRecursive(string objectId, string relativeFolder, List<MediaFile> files,
        List<FolderScanInfo> folders, List<string> warnings, CancellationToken token, Action<string>? progress,
        bool skipUnreadableTrees)
    {
        token.ThrowIfCancellationRequested();
        int fileCount = 0;
        long bytes = 0;
        int unreadable = 0;
        int consecutiveFailures = 0;
        try
        {
            // Walking into a new folder always reports, so the operator can see where the walk is even
            // though each object costs the device ~27 ms and a single album can hold 26,000 of them.
            if (_scanReporter.ShouldReport(relativeFolder, _scanClock.Elapsed))
                progress?.Invoke(ScanProgressReporter.Describe(relativeFolder, files.Count, _scanBytes, _scanClock.Elapsed));

            foreach (string childId in EnumerateChildren(objectId))
            {
                token.ThrowIfCancellationRequested();
                ObjectProps props;
                try
                {
                    props = GetProps(childId);
                    consecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    unreadable++;
                    consecutiveFailures++;
                    warnings.Add($"Could not read properties of an object in '{relativeFolder}': {ex.Message}");

                    // One damaged object is data loss on the device; a long unbroken run of them is the
                    // device itself going away. Letting the walk continue through a removed cable is how
                    // a scan ends up claiming Complete over a tree it could no longer read: every object
                    // fails, every failure is only a warning, and the folder is still recorded as read.
                    if (TransferRules.DeviceStoppedAnswering(consecutiveFailures))
                        throw new DeviceStoppedAnsweringException(
                            $"The phone stopped answering during the scan: {consecutiveFailures:N0} objects in a row " +
                            $"could not be read in '{relativeFolder}' ({ex.Message}). Nothing from this walk was " +
                            "accepted as a complete listing - reconnect the phone, unlock it, and scan again.");
                    continue;
                }

                if (props.IsFolder)
                {
                    string childRelative = Path.Combine(relativeFolder, props.Name);
                    // On a generic MTP volume some trees are not merely uninteresting, they are
                    // unreadable: Android denies a file-transfer client its per-app private data, so
                    // descending buys an error per app and no files at all.
                    if (skipUnreadableTrees && PortableStorage.ShouldSkipFolder(childRelative, props.Name)) continue;
                    EnumerateRecursive(childId, childRelative, files, folders, warnings, token, progress, skipUnreadableTrees);
                }
                else
                {
                    long size = props.Size;
                    var file = new MediaFile(relativeFolder, props.Name, size,
                        Path.GetExtension(props.Name).ToLowerInvariant())
                    {
                        SizeKnown = props.SizeKnown,
                        SourceObjectId = childId,
                        ModifiedUtc = props.ModifiedUtc
                    };
                    files.Add(file);
                    fileCount++;
                    bytes += Math.Max(0, size);

                    if (_scanReporter.ShouldReport(relativeFolder, _scanClock.Elapsed))
                        progress?.Invoke(ScanProgressReporter.Describe(relativeFolder, files.Count, _scanBytes, _scanClock.Elapsed));
                }
            }

            // An object that could not be read is an object missing from the backup, so the folder is
            // not recorded as a clean read. This is what turns "9,500 warnings and a complete scan" into
            // "incomplete scan, N objects missing", which is the only one of the two that is true.
            folders.Add(new FolderScanInfo(relativeFolder, fileCount, bytes, unreadable == 0,
                unreadable == 0 ? null : $"{unreadable:N0} object(s) could not be read"));
            if (unreadable > 0)
                warnings.Add($"'{relativeFolder}': {unreadable:N0} object(s) could not be read and are absent from this scan.");
        }
        catch (OperationCanceledException) { throw; }
        catch (DeviceStoppedAnsweringException) { throw; }
        catch (Exception ex)
        {
            folders.Add(new FolderScanInfo(relativeFolder, fileCount, bytes, false, ex.Message));
            warnings.Add($"Could not fully enumerate '{relativeFolder}': {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ Copy
    public CopyResult CopyToStage(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken token) => Run(() =>
    {
        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(stagingDirectory);
            string stagePath = Path.Combine(stagingDirectory, file.Name + ".partial");
            string finalPath = Path.Combine(stagingDirectory, file.Name);
            if (File.Exists(finalPath)) File.Delete(finalPath);

            EnsureOpen(_deviceId ?? throw new IOException("WPD session is not open."));
            IStream stream = OpenSourceStream(file);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long written = 0;
            byte[] buffer = new byte[_copyBufferBytes];
            IntPtr readPtr = Marshal.AllocCoTaskMem(4);
            try
            {
                using var output = new FileStream(stagePath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    stream.Read(buffer, buffer.Length, readPtr);
                    int read = Marshal.ReadInt32(readPtr);
                    if (read <= 0) break;
                    sha.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                    written += read;
                }
                output.Flush(true);
            }
            finally
            {
                Marshal.FreeCoTaskMem(readPtr);
                ReleaseCom(stream);
                // A failed or cancelled attempt must not leave a large .partial behind. On success the
                // partial has already been moved to its final staging name, so this is a no-op.
                TryDelete(stagePath);
            }

            if (file.Size > 0 && written != file.Size)
            {
                TryDelete(stagePath);
                return new CopyResult(false, null, $"WPD stream size mismatch: expected {file.Size} bytes, read {written}.") { BytesWritten = written };
            }
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(stagePath, finalPath);
            return new CopyResult(true, Convert.ToHexString(sha.GetHashAndReset()), "") { BytesWritten = written };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            ResetSessionOnFailure(ex);
            return new CopyResult(false, null, "WPD copy failed: " + ex.Message);
        }
    });

    public Stream? OpenSourceRead(MediaFile file) => Run<Stream?>(() =>
    {
        try
        {
            EnsureOpen(_deviceId ?? throw new IOException("WPD session is not open."));
            string objectId = ResolveObjectId(file);
            return new WpdStream(this, objectId);
        }
        catch { return null; }
    });

    // ------------------------------------------------------------------ Helpers
    private void EnsureOpen(string deviceId)
    {
        lock (_gate)
        {
            if (_device is not null && string.Equals(_deviceId, deviceId, StringComparison.OrdinalIgnoreCase)) return;
            ResetSession();
            IPortableDeviceValues client = WpdFactory.CreateValues();
            PropertyKey nameKey = WpdKeys.ClientName;
            client.SetStringValue(ref nameKey, "I-Total Transfer");
            PropertyKey major = WpdKeys.ClientMajorVersion;
            client.SetUnsignedIntegerValue(ref major, 1);
            PropertyKey minor = WpdKeys.ClientMinorVersion;
            client.SetUnsignedIntegerValue(ref minor, 6);
            PropertyKey rev = WpdKeys.ClientRevision;
            client.SetUnsignedIntegerValue(ref rev, 0);

            IPortableDevice device = WpdFactory.CreateDevice();
            device.Open(deviceId, client);
            device.Content(out IPortableDeviceContent content);
            content.Properties(out IPortableDeviceProperties properties);
            content.Transfer(out IPortableDeviceResources resources);
            _device = device; _content = content; _properties = properties; _resources = resources; _deviceId = deviceId;
        }
    }

    /// <summary>One portable device the transport reports, with the family it was classified as.</summary>
    private sealed record PortableDevice(string Id, string Friendly, DeviceFamilyVerdict Verdict);

    /// <summary>
    /// Reads the transport's device list and classifies every entry by what it calls itself, without
    /// opening any of them. Devices that identify as neither family are returned separately so the
    /// caller can name them rather than pretend nothing is attached: a phone sitting in Explorer
    /// beside a "no device found" message is the confusion this app exists to remove.
    /// </summary>
    private static List<PortableDevice> ListDevices(out List<string> unrecognised)
    {
        var devices = new List<PortableDevice>();
        unrecognised = [];
        IPortableDeviceManager manager = WpdFactory.CreateManager();
        uint count = 0;
        manager.GetDevices(IntPtr.Zero, ref count);
        if (count == 0) return devices;

        IntPtr buffer = Marshal.AllocCoTaskMem(IntPtr.Size * (int)count);
        try
        {
            manager.GetDevices(buffer, ref count);
            for (int i = 0; i < count; i++)
            {
                IntPtr p = Marshal.ReadIntPtr(buffer, i * IntPtr.Size);
                if (p == IntPtr.Zero) continue;
                string? id = Marshal.PtrToStringUni(p);
                if (string.IsNullOrEmpty(id)) continue;

                string friendly = GetFriendlyName(manager, id);
                string manufacturer = GetManufacturer(manager, id);
                DeviceFamilyVerdict verdict =
                    DeviceFamilies.Classify(new PortableDeviceIdentity(friendly, manufacturer, null));

                if (verdict.Family == DeviceFamily.Unknown)
                {
                    string described = string.IsNullOrWhiteSpace(friendly) ? manufacturer : friendly;
                    unrecognised.Add(string.IsNullOrWhiteSpace(described) ? "(unnamed device)" : described.Trim());
                    continue;
                }

                devices.Add(new PortableDevice(id, string.IsNullOrWhiteSpace(friendly) ? id : friendly.Trim(), verdict));
            }
        }
        finally { Marshal.FreeCoTaskMem(buffer); }
        return devices;
    }

    /// <summary>
    /// Finds the portable device this app should read, and the family whose storage rules apply.
    ///
    /// <para>
    /// The family order comes from the operator's "when both are attached" preference, which defaults to
    /// Apple first so a machine with an iPhone attached behaves exactly as it did before Android support
    /// existed. The preference only reorders the two families — when the preferred one is not attached the
    /// other is read — and whichever device is not read is named in the message, so the operator is never
    /// left guessing which phone the app is looking at.
    /// </para>
    /// </summary>
    private string? FindDevice(out string friendlyName, out DeviceFamily family, out DeviceFamilyVerdict verdict,
        out IReadOnlyList<string> unrecognised, out string? otherAttached)
    {
        friendlyName = "";
        family = DeviceFamily.Unknown;
        verdict = new DeviceFamilyVerdict(DeviceFamily.Unknown, "no portable device identified itself");

        List<PortableDevice> devices = ListDevices(out List<string> unknown);
        unrecognised = unknown;
        otherAttached = null;

        PortableDevice? chosen = null;
        foreach (DeviceFamily candidate in DeviceFamilies.SelectionOrder(_preferredHandset))
        {
            chosen = devices.FirstOrDefault(d => d.Verdict.Family == candidate);
            if (chosen is not null) break;
        }
        if (chosen is null) return null;

        family = chosen.Verdict.Family;
        verdict = chosen.Verdict;
        friendlyName = chosen.Friendly;
        PortableDevice? other = devices.FirstOrDefault(d => !ReferenceEquals(d, chosen));
        otherAttached = other?.Friendly;
        return chosen.Id;
    }

    /// <summary>
    /// What to say when no supported device was found. Devices Windows lists but this app does not
    /// recognise are named, because "no device found" means something very different from "a device
    /// is attached and did not identify itself".
    /// </summary>
    private static string NoDeviceMessage(IReadOnlyList<string> unrecognised)
    {
        if (unrecognised.Count == 0)
            return "No Apple or Android device was found through Windows Portable Devices.";

        return "No Apple or Android device was found through Windows Portable Devices, but Windows lists " +
               unrecognised.Count + " portable device" + (unrecognised.Count == 1 ? "" : "s") +
               " that identified as neither: " + string.Join(", ", unrecognised) +
               ". If one of those is your phone, set its USB connection to \"File transfer\" (not \"Charging only\") and check again.";
    }

    private static string ConnectedMessage(DeviceFamily family) => family == DeviceFamily.Android
        ? "The Android device is connected through Windows Portable Devices."
        : "The iPhone is connected through Windows Portable Devices.";

    /// <summary>
    /// The storage volumes a generic MTP device exposes, each with the label its destination folder
    /// will carry.
    ///
    /// <para>
    /// A phone reports one volume for its internal storage and one more for each card. Keeping them
    /// apart under the device's own names — "Internal shared storage", the card's label — is what lets
    /// an internal volume and a card be told apart in the destination, instead of being merged into one
    /// pile under a label this app invented.
    /// </para>
    /// </summary>
    private List<(string Id, string Label)> ResolveVolumes(bool refresh)
    {
        if (!refresh && _volumes.Count > 0) return _volumes;
        _volumes.Clear();

        foreach (string containerId in EnumerateChildren(WpdKeys.DeviceObjectId))
        {
            try
            {
                // A storage volume reports itself as a functional object of the storage category, which
                // GetProps already treats as a container.
                ObjectProps props = GetProps(containerId);
                if (!props.IsFolder) continue;
                _volumes.Add((containerId, PortableStorage.RootLabel(props.Name)));
            }
            catch { /* a volume whose name cannot be read could not be named in a destination either */ }
        }
        return _volumes;
    }

    private List<(string Id, string Label)> ResolveVolumesWithRetry(int attempts, int delayMs)
    {
        for (int i = 0; i < attempts; i++)
        {
            List<(string Id, string Label)> found;
            try { found = ResolveVolumes(refresh: true); }
            catch { found = []; }
            if (found.Count > 0) return found;
            if (i < attempts - 1) Thread.Sleep(delayMs);
        }
        return [];
    }

    private static string ReadWpdString(IPortableDeviceManager manager, string id, bool friendly)
    {
        // Probe the required length, then read into an unmanaged buffer.
        uint length = 0;
        try
        {
            if (friendly) manager.GetDeviceFriendlyName(id, IntPtr.Zero, ref length);
            else manager.GetDeviceManufacturer(id, IntPtr.Zero, ref length);
        }
        catch { length = 0; }
        if (length == 0) length = 260;
        IntPtr buf = Marshal.AllocCoTaskMem((int)length * 2 + 2);
        try
        {
            uint actual = length;
            try
            {
                if (friendly) manager.GetDeviceFriendlyName(id, buf, ref actual);
                else manager.GetDeviceManufacturer(id, buf, ref actual);
            }
            catch { return ""; }
            return Marshal.PtrToStringUni(buf) ?? "";
        }
        finally { Marshal.FreeCoTaskMem(buf); }
    }

    private static string GetFriendlyName(IPortableDeviceManager manager, string id) => ReadWpdString(manager, id, true);
    private static string GetManufacturer(IPortableDeviceManager manager, string id) => ReadWpdString(manager, id, false);

    private (string Model, string Serial, string Firmware) ReadDeviceProperties(string deviceId)
    {
        try
        {
            EnsureOpen(deviceId);
            IPortableDeviceKeyCollection keys = WpdFactory.CreateKeyCollection();
            PropertyKey kModel = WpdKeys.DeviceModel, kSerial = WpdKeys.DeviceSerialNumber, kFw = WpdKeys.DeviceFirmwareVersion;
            keys.Add(ref kModel); keys.Add(ref kSerial); keys.Add(ref kFw);
            _properties!.GetValues(WpdKeys.DeviceObjectId, keys, out IPortableDeviceValues values);
            return (GetString(values, WpdKeys.DeviceModel), GetString(values, WpdKeys.DeviceSerialNumber), GetString(values, WpdKeys.DeviceFirmwareVersion));
        }
        catch { return ("", "", ""); }
    }

    private static string GetString(IPortableDeviceValues values, PropertyKey key)
    {
        try
        {
            PropertyKey k = key;
            values.GetStringValue(ref k, out IntPtr p, out _);
            try { return p == IntPtr.Zero ? "" : Marshal.PtrToStringUni(p) ?? ""; }
            finally { if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); }
        }
        catch { return ""; }
    }

    private string? ResolveMediaRootObjectId()
    {
        if (_mediaRootObjectId is not null) return _mediaRootObjectId;

        // The documented shape: a DCIM container under the storage object.
        string? dcim = FindContainerByName(WpdKeys.DeviceObjectId, CameraRoll.RootLabel, maxDepth: 3);
        if (dcim is not null) return _mediaRootObjectId = dcim;

        // The other shape in the field: the driver publishes the DCIM albums directly at the storage
        // root, so the storage container itself is the camera roll. Only accepted when its children
        // actually look like one, so an empty or unrelated volume is never mistaken for media.
        IReadOnlyList<string> roots = EnumerateChildren(WpdKeys.DeviceObjectId);
        foreach (string storageId in roots)
        {
            try
            {
                // A storage object normally reports itself as a container; when the device publishes a
                // single root, accept it either way — the Shell backend picks its only container too.
                if (!GetProps(storageId).IsFolder && roots.Count != 1) continue;
                if (CameraRoll.Decide(ChildNames(storageId)) == CameraRollShape.StorageContainer)
                    return _mediaRootObjectId = storageId;
            }
            catch { /* an unreadable container simply is not the camera roll */ }
        }
        return null;
    }

    /// <summary>
    /// Breadth-first search for a container with the given name.
    ///
    /// <para>
    /// The walk deliberately refuses to descend into a camera-roll album folder. An album's children
    /// are the media files themselves, so stepping into one turns a "is the phone ready?" probe into a
    /// full device scan — tens of thousands of property reads — which is what made every probe crawl.
    /// Reading a container's child <em>names</em> is what the walk actually needs.
    /// </para>
    /// </summary>
    private string? FindContainerByName(string parentId, string name, int maxDepth)
    {
        var queue = new Queue<(string Id, int Depth)>();
        queue.Enqueue((parentId, 0));
        while (queue.Count > 0)
        {
            (string id, int depth) = queue.Dequeue();
            if (depth > maxDepth) continue;
            foreach (string child in EnumerateChildren(id))
            {
                try
                {
                    ObjectProps props = GetProps(child);
                    if (!props.IsFolder) continue;
                    if (string.Equals(props.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                        return child;
                    if (CameraRoll.IsAlbumFolder(props.Name)) continue;
                    queue.Enqueue((child, depth + 1));
                }
                catch { /* skip unreadable object */ }
            }
        }
        return null;
    }

    /// <summary>The names of a container's direct children, read without touching their contents.</summary>
    private List<string?> ChildNames(string parentId)
    {
        var names = new List<string?>();
        foreach (string child in EnumerateChildren(parentId))
        {
            try { names.Add(GetProps(child).Name); }
            catch { names.Add(null); }
        }
        return names;
    }

    /// <summary>
    /// Resolves the media root with retries. A freshly connected or just-unlocked handset regularly
    /// answers the first MTP enumeration with an empty tree, so a single failed walk is not evidence
    /// that the media is missing.
    /// </summary>
    private string? ResolveMediaRootWithRetry(int attempts, int delayMs)
    {
        for (int i = 0; i < attempts; i++)
        {
            string? found;
            try { found = ResolveMediaRootObjectId(); }
            catch { found = null; }
            if (found is not null) return found;
            _mediaRootObjectId = null;
            if (i < attempts - 1) Thread.Sleep(delayMs);
        }
        return null;
    }

    /// <summary>Counts the device root's children, or -1 when the tree cannot be read at all.</summary>
    private int CountChildren(string parentId)
    {
        try { return EnumerateChildren(parentId).Count; }
        catch { return -1; }
    }

    /// <summary>
    /// Opens the source stream for a file the current scan enumerated.
    ///
    /// <para>
    /// The scan already paid the device a full enumeration to produce object ids, so the copy path uses
    /// the recorded id directly. The previous shape asked the device to confirm that id with an extra
    /// property read before opening it — one avoidable round trip per file, which for a 26,335-file
    /// camera roll is tens of thousands of needless device calls queued ahead of the actual transfer. If
    /// the id has gone stale (a re-plugged handset recycles them), the open fails and the path walk
    /// resolves it exactly as the confirmation step would have.
    /// </para>
    /// </summary>
    private IStream OpenSourceStream(MediaFile file)
    {
        if (!string.IsNullOrEmpty(file.SourceObjectId))
        {
            try { return OpenStream(file.SourceObjectId!, out uint _); }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException or ObjectDisposedException)
            {
                // Stale id from an earlier session: fall through to the path walk below.
            }
        }
        return OpenStream(ResolveObjectId(file), out uint _);
    }

    private string ResolveObjectId(MediaFile file)
    {
        if (!string.IsNullOrEmpty(file.SourceObjectId) && ObjectExists(file.SourceObjectId!))
            return file.SourceObjectId!;

        // Fall back to the relative folder path, read from the device once per folder and remembered
        // (see FolderNameIndex). Re-walking the album for every file is what a resumed run used to do,
        // and on a real album that is a property round-trip per sibling per file.
        string rootId;
        string rootLabel;
        if (_family == DeviceFamily.Android)
        {
            // A generic MTP device has one root per storage volume, and the file's own path names the
            // volume it came from in its first segment.
            string wanted = FirstSegment(file.Folder);
            string? volumeId = null;
            rootLabel = wanted;
            foreach ((string Id, string Label) volume in ResolveVolumes(refresh: false))
            {
                if (!string.Equals(volume.Label, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                volumeId = volume.Id;
                rootLabel = volume.Label;
                break;
            }
            if (volumeId is null)
                throw new FileNotFoundException(
                    $"The device's storage '{wanted}' is no longer present.", file.RelativePath);
            rootId = volumeId;
        }
        else
        {
            rootId = ResolveMediaRootObjectId() ?? throw new IOException("The iPhone's camera-roll container was not found.");
            rootLabel = CameraRoll.RootLabel;
        }

        ObjectLocation location = _nameIndex.Locate(rootId, file.Folder, file.Name, rootLabel);
        return location.Outcome switch
        {
            ObjectLookup.Found => location.Id,
            ObjectLookup.FolderMissing => throw new FileNotFoundException(
                $"Folder '{location.MissingSegment}' is no longer present on the device.", file.RelativePath),
            _ => throw new FileNotFoundException("The source object is no longer present on the device.", file.RelativePath),
        };
    }

    private static string FirstSegment(string folder) =>
        (folder ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

    private bool ObjectExists(string objectId)
    {
        try { GetProps(objectId); return true; } catch { return false; }
    }

    private List<string> EnumerateChildren(string parentId)
    {
        var result = new List<string>();
        _content!.EnumObjects(WpdKeys.WpdFlagNone, parentId, IntPtr.Zero, out IEnumPortableDeviceObjectIDs enumerator);
        const uint batch = 16;
        IntPtr array = Marshal.AllocCoTaskMem(IntPtr.Size * (int)batch);
        try
        {
            while (true)
            {
                enumerator.Next(batch, array, out uint fetched);
                if (fetched == 0) break;
                for (int i = 0; i < fetched; i++)
                {
                    IntPtr p = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                    if (p == IntPtr.Zero) continue;
                    string? id = Marshal.PtrToStringUni(p);
                    Marshal.FreeCoTaskMem(p);
                    if (!string.IsNullOrEmpty(id)) result.Add(id);
                }
                if (fetched < batch) break;
            }
        }
        finally { Marshal.FreeCoTaskMem(array); }
        return result;
    }

    private ObjectProps GetProps(string objectId)
    {
        PropertyKey kN = WpdKeys.ObjectName, kO = WpdKeys.ObjectOriginalFileName, kS = WpdKeys.ObjectSize,
            kC = WpdKeys.ObjectContentType, kM = WpdKeys.ObjectDateModified, kF = WpdKeys.FunctionalObjectCategory;
        try
        {
            IPortableDeviceKeyCollection keys = WpdFactory.CreateKeyCollection();
            keys.Add(ref kN); keys.Add(ref kO); keys.Add(ref kS); keys.Add(ref kC); keys.Add(ref kM); keys.Add(ref kF);
            _properties!.GetValues(objectId, keys, out IPortableDeviceValues values);
            return ReadProps(values, objectId);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            // GetValues is all-or-nothing: a driver that does not implement one of the optional keys can
            // reject the whole request. The HUAWEI Y7 does exactly that for its storage roots, which made
            // every volume look like a file, and the Android phone read as "connected but nothing in it
            // can be read" while Explorer could open it. Ask for each key on its own instead, so one
            // missing optional property cannot hide an object - or a whole phone - from the plan.
            string name = ReadString(objectId, WpdKeys.ObjectName);
            if (string.IsNullOrWhiteSpace(name)) name = ReadString(objectId, WpdKeys.ObjectOriginalFileName);
            string original = ReadString(objectId, WpdKeys.ObjectOriginalFileName);
            if (!string.IsNullOrWhiteSpace(original) && LooksLikeFileName(original)) name = original;

            Guid contentType = ReadGuid(objectId, WpdKeys.ObjectContentType);
            Guid functional = ReadGuid(objectId, WpdKeys.FunctionalObjectCategory);
            bool isFolder = contentType == WpdKeys.ContentTypeFolder || functional == WpdKeys.FunctionalCategoryStorage;
            long size = ReadULong(objectId, WpdKeys.ObjectSize, out bool sizeKnown);
            DateTimeOffset? modified = ReadDate(objectId, WpdKeys.ObjectDateModified);
            return new ObjectProps(string.IsNullOrWhiteSpace(name) ? objectId : name, size, isFolder, modified, sizeKnown);
        }
    }

    private ObjectProps ReadProps(IPortableDeviceValues values, string objectId)
    {
        string name = GetString(values, WpdKeys.ObjectName);
        if (string.IsNullOrWhiteSpace(name)) name = GetString(values, WpdKeys.ObjectOriginalFileName);
        string original = GetString(values, WpdKeys.ObjectOriginalFileName);
        if (!string.IsNullOrWhiteSpace(original) && LooksLikeFileName(original)) name = original;

        Guid contentType = GetGuid(values, WpdKeys.ObjectContentType);
        Guid functional = GetGuid(values, WpdKeys.FunctionalObjectCategory);
        bool isFolder = contentType == WpdKeys.ContentTypeFolder || functional == WpdKeys.FunctionalCategoryStorage;
        long size = (long)GetULong(values, WpdKeys.ObjectSize, out bool sizeKnown);
        DateTimeOffset? modified = GetDate(values, WpdKeys.ObjectDateModified);
        return new ObjectProps(string.IsNullOrWhiteSpace(name) ? objectId : name, size, isFolder, modified, sizeKnown);
    }

    /// <summary>
    /// Reads one property in its own request. Used only when a batched read was rejected, so the extra
    /// round trips never touch a device that answers the batch.
    /// </summary>
    private bool TryGetValue(string objectId, PropertyKey key, out IPortableDeviceValues values)
    {
        values = null!;
        try
        {
            IPortableDeviceKeyCollection keys = WpdFactory.CreateKeyCollection();
            keys.Add(ref key);
            _properties!.GetValues(objectId, keys, out values);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            return false;
        }
    }

    private string ReadString(string objectId, PropertyKey key) =>
        TryGetValue(objectId, key, out IPortableDeviceValues values) ? GetString(values, key) : "";

    private Guid ReadGuid(string objectId, PropertyKey key) =>
        TryGetValue(objectId, key, out IPortableDeviceValues values) ? GetGuid(values, key) : Guid.Empty;

    private long ReadULong(string objectId, PropertyKey key, out bool known)
    {
        if (TryGetValue(objectId, key, out IPortableDeviceValues values))
            return (long)GetULong(values, key, out known);
        known = false;
        return 0;
    }

    private DateTimeOffset? ReadDate(string objectId, PropertyKey key) =>
        TryGetValue(objectId, key, out IPortableDeviceValues values) ? GetDate(values, key) : null;

    private static bool LooksLikeFileName(string value) => value.Contains('.');

    private static Guid GetGuid(IPortableDeviceValues values, PropertyKey key)
    {
        try { PropertyKey k = key; values.GetGuidValue(ref k, out Guid g); return g; }
        catch { return Guid.Empty; }
    }

    /// <summary>
    /// Reads an unsigned size and reports whether the provider actually supplied one. A missing
    /// property is not a zero-byte object: the size is unknown, and the caller must keep the two
    /// apart so a zero-byte file can still be verified while an unmeasurable one is never called
    /// size-verified.
    /// </summary>
    private static ulong GetULong(IPortableDeviceValues values, PropertyKey key, out bool known)
    {
        try { PropertyKey k = key; values.GetUnsignedLargeIntegerValue(ref k, out ulong v); known = true; return v; }
        catch
        {
            try { PropertyKey k = key; values.GetUnsignedIntegerValue(ref k, out uint v2); known = true; return v2; }
            catch { known = false; return 0; }
        }
    }

    private static DateTimeOffset? GetDate(IPortableDeviceValues values, PropertyKey key)
    {
        try
        {
            PropertyKey k = key;
            values.GetStringValue(ref k, out IntPtr p, out _);
            try
            {
                if (p == IntPtr.Zero) return null;
                string s = Marshal.PtrToStringUni(p) ?? "";
                return DateTimeOffset.TryParse(s, out DateTimeOffset dto) ? dto : null;
            }
            finally { if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); }
        }
        catch { return null; }
    }

    private IStream OpenStream(string objectId, out uint optimalBufferSize)
    {
        PropertyKey key = WpdKeys.ResourceDefault;
        uint buffer = 0;
        _resources!.GetStream(objectId, ref key, WpdKeys.StgmRead, ref buffer, out IStream stream);
        optimalBufferSize = buffer;
        return stream;
    }

    private void ResetSession()
    {
        try { _device?.Close(); } catch { }
        ReleaseCom(_resources); ReleaseCom(_properties); ReleaseCom(_content); ReleaseCom(_device);
        _device = null; _content = null; _properties = null; _resources = null; _deviceId = null; _mediaRootObjectId = null;
        // A volume list and a family belong to one attached device, exactly as its object ids do.
        _volumes.Clear();
        _family = DeviceFamily.Unknown;
        // Object ids belong to the session that handed them out; a reconnect can renumber every one.
        _nameIndex.Clear();
    }

    private void ResetSessionOnFailure(Exception ex)
    {
        if (ex is COMException or IOException or InvalidOperationException or ObjectDisposedException) ResetSession();
    }

    private static void ReleaseCom(object? o)
    {
        if (o is not null && Marshal.IsComObject(o)) { try { Marshal.FinalReleaseComObject(o); } catch { } }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private T Run<T>(Func<T> work)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _apartment.Invoke(work);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _apartment.Invoke(ResetSession); } catch { }
        _apartment.Dispose();
    }

    private readonly record struct ObjectProps(string Name, long Size, bool IsFolder, DateTimeOffset? ModifiedUtc, bool SizeKnown);

    /// <summary>Buffered read-only stream over a WPD resource stream, used for source hashing and read-back.</summary>
    private sealed class WpdStream : Stream
    {
        private readonly WpdDevice _owner;
        private readonly string _objectId;
        private IStream? _stream;
        private readonly byte[] _buffer = new byte[1024 * 1024];
        private int _count;
        private bool _eof;

        public WpdStream(WpdDevice owner, string objectId) { _owner = owner; _objectId = objectId; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_eof) return 0;
            if (_count == 0) Fill();
            if (_count == 0) return 0;
            int take = Math.Min(count, _count);
            Array.Copy(_buffer, 0, buffer, offset, take);
            // Shift the remainder forward so subsequent reads stay contiguous.
            Array.Copy(_buffer, take, _buffer, 0, _count - take);
            _count -= take;
            return take;
        }

        private void Fill()
        {
            _owner._apartment.Invoke(() =>
            {
                _stream ??= _owner.OpenStream(_objectId, out _);
                IntPtr readPtr = Marshal.AllocCoTaskMem(4);
                try
                {
                    _stream.Read(_buffer, _buffer.Length, readPtr);
                    _count = Marshal.ReadInt32(readPtr);
                    if (_count <= 0) { _count = 0; _eof = true; }
                }
                finally { Marshal.FreeCoTaskMem(readPtr); }
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _stream is not null)
            {
                IStream? s = _stream; _stream = null;
                try { _owner._apartment.Invoke(() => ReleaseCom(s)); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// The device went away in the middle of a walk: every object in a row failed to be read. Kept as its
/// own type because it is the one failure that must abort the enumeration instead of being recorded as
/// another unreadable object - a scan that keeps going through a removed cable would otherwise finish
/// with a "complete" listing of everything before the cable moved and nothing after it.
/// </summary>
public sealed class DeviceStoppedAnsweringException : Exception
{
    public DeviceStoppedAnsweringException(string message) : base(message) { }
}
