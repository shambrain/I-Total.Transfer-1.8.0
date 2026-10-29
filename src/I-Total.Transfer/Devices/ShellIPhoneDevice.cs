using System.Runtime.InteropServices;
using ITotalTransfer.Core;

namespace ITotalTransfer.Devices;

/// <summary>
/// Compatibility backend for iOS media exposed through Windows Portable Devices/Shell.
/// Shell CopyHere is asynchronous; the transfer service therefore waits for a stable, unlocked file
/// and verifies its length. This backend cannot hash raw iPhone source bytes, so
/// <see cref="CanHashSourceBytes"/> is false and source-to-destination identity is never claimed.
/// A native WPD resource-stream backend is the primary path; this remains the compatibility fallback.
///
/// <para>
/// Two things about the Shell namespace are easy to get wrong and both were:
/// it only reports portable devices to an <b>STA</b> caller (see <see cref="StaApartment"/>), and the
/// media does not necessarily sit under a folder named "DCIM" (see <see cref="CameraRoll"/>).
/// </para>
/// </summary>
public sealed class ShellIPhoneDevice : ITransferBackend
{
    public BackendKind Kind => BackendKind.Shell;
    public string Name => "Windows Shell (compatibility)";
    public bool CanHashSourceBytes => false;
    public BackendCapabilities Capabilities { get; } = new(
        CanEnumerate: true,
        CanReadSource: false,
        CanHashSourceBytes: false,
        CanReadBackCommitted: false,
        CanWriteToDevice: false,
        SupportsTimestamps: false,
        Notes: "Windows Shell CopyHere is asynchronous and exposes no raw source stream; verification is size-only plus a local destination digest.")
    {
        // The Shell namespace exposes the camera-roll container (a DCIM folder, or the storage
        // container itself when the driver publishes the albums directly — see CameraRoll).
        Categories = [MediaCategory.Photos, MediaCategory.Videos]
    };

    private readonly StaApartment _apartment = new();
    private dynamic? _shell;
    private dynamic? _device;
    private dynamic? _storage;
    private dynamic? _mediaRoot;
    private readonly Dictionary<string, object> _folderCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScanProgressReporter _scanReporter = new();
    private readonly System.Diagnostics.Stopwatch _scanClock = new();
    private long _scanBytes;

    /// <summary>
    /// Every Shell COM call runs on this backend's own STA thread. Creating Shell.Application on a
    /// thread-pool thread yields a "This PC" that lists drive letters and no portable devices at all,
    /// which is how a connected, unlocked, trusted handset came to be reported as not detected.
    /// </summary>
    public DeviceInfo Probe() => _apartment.Invoke(ProbeCore);

    private DeviceInfo ProbeCore()
    {
        ClearFolderCache();
        ReleaseRoots();
        Release(_device);
        _device = null;
        dynamic? computer = null;
        dynamic? devices = null;
        dynamic? deviceFolder = null;
        dynamic? children = null;
        dynamic? mediaItems = null;
        try
        {
            EnsureShell();
            computer = _shell!.NameSpace(17);
            devices = computer.Items();
            int count = Convert.ToInt32(devices.Count);
            for (int i = 0; i < count; i++)
            {
                dynamic? item = null;
                try
                {
                    item = devices.Item(i);
                    string name = Convert.ToString(item.Name) ?? "Unknown device";
                    if (!IsAppleDeviceName(name)) continue;

                    _device = item;
                    item = null; // ownership transferred to _device
                    deviceFolder = _device.GetFolder;
                    children = deviceFolder.Items();
                    int childCount = Convert.ToInt32(children.Count);
                    for (int j = 0; j < childCount; j++)
                    {
                        dynamic? child = null;
                        try
                        {
                            child = children.Item(j);
                            string childName = Convert.ToString(child.Name) ?? "";
                            if (childName.Contains("Internal Storage", StringComparison.OrdinalIgnoreCase))
                            {
                                _storage = child.GetFolder;
                                child = null;
                                break;
                            }
                            if (j == 0 && childCount == 1) { _storage = child.GetFolder; child = null; }
                        }
                        finally { Release(child); }
                    }
                    if (_storage is null)
                        return new(DeviceStatus.Connected, name,
                            "The iPhone is connected and Windows can see it, but its storage container is not exposed yet. Unlock the screen, tap Trust, then check the connection again.")
                        { Family = DeviceFamily.Apple };

                    // A just-connected handset regularly publishes its storage container before any
                    // content, so the lookup is retried before the media is declared missing.
                    CameraRollShape shape = CameraRollShape.Unknown;
                    for (int attempt = 0; attempt < 5 && shape == CameraRollShape.Unknown; attempt++)
                    {
                        if (attempt > 0) Thread.Sleep(800);
                        shape = ResolveMediaRoot(_storage, out _mediaRoot);
                    }
                    if (_mediaRoot is null || shape == CameraRollShape.Unknown)
                        return new(DeviceStatus.MediaUnavailable, name,
                            "Windows can see the iPhone's storage, but it exposed neither a DCIM folder nor the camera-roll albums. Unlock the screen, tap Trust, then check the connection again — and if the photos live only in iCloud, turn off \"Optimize iPhone Storage\" for this transfer.")
                        { Family = DeviceFamily.Apple };

                    mediaItems = _mediaRoot.Items();
                    int mediaCount = Convert.ToInt32(mediaItems.Count);
                    if (mediaCount == 0)
                        return new(DeviceStatus.MediaUnavailable, name,
                            "The iPhone's camera-roll container is exposed but reports no files. If your photos are stored only in iCloud this is expected — otherwise unlock the screen and check the connection again.")
                        { Family = DeviceFamily.Apple };
                    return new(DeviceStatus.MediaAvailable, name,
                        shape == CameraRollShape.StorageContainer
                            ? "The iPhone's camera roll is exposed through its storage container (Shell compatibility backend; source hashing unavailable)."
                            : "iPhone DCIM is accessible (Shell compatibility backend; source hashing unavailable).")
                    {
                        ConnectionType = "USB (WPD/Shell)",
                        BackendName = Name,
                        Family = DeviceFamily.Apple
                    };
                }
                finally { Release(item); }
            }
            return new(DeviceStatus.NotFound, "", "No accessible phone was found through Windows Shell.");
        }
        catch (Exception ex)
        {
            return new(DeviceStatus.Error, "", $"Device probe failed: {ex.Message}");
        }
        finally
        {
            Release(mediaItems); Release(children); Release(deviceFolder);
            Release(devices); Release(computer);
        }
    }

    public ScanResult Scan(CancellationToken cancellationToken, Action<string>? progress = null) =>
        _apartment.Invoke(() => ScanCore(cancellationToken, progress));

    private ScanResult ScanCore(CancellationToken cancellationToken, Action<string>? progress)
    {
        // ProbeCore, not Probe: this already runs inside the apartment, and re-entering it would deadlock.
        DeviceInfo state = ProbeCore();
        if (!state.Status.IsUsable())
            throw new InvalidOperationException(state.Message);

        DateTimeOffset started = DateTimeOffset.UtcNow;
        _scanBytes = 0;
        _scanClock.Restart();
        var files = new List<MediaFile>();
        var warnings = new List<string>();
        var folders = new List<FolderScanInfo>();
        ScanFolder(_mediaRoot!, "", files, cancellationToken, progress, warnings, folders);

        var duplicatePaths = files.GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).Take(20).ToArray();
        int duplicateCount = duplicatePaths.Length;
        if (duplicateCount > 0)
            warnings.Add("The device exposed duplicate destination paths: " + string.Join(", ", duplicatePaths));

        foreach (FolderScanInfo failed in folders.Where(f => !f.Succeeded))
            warnings.Add($"Folder '{failed.RelativePath}' could not be fully enumerated: {failed.Error}");

        int unknownSize = files.Count(f => !f.SizeKnown);
        if (unknownSize > 0)
            warnings.Add($"{unknownSize:N0} file(s) reported no readable size. They can be copied but cannot be size-verified.");

        EnumerationReport report = ScanAccounting.BuildReport(Name, files, folders, duplicateCount, warnings);
        var status = report.Status;

        string deviceName = "iPhone";
        try { deviceName = Convert.ToString(_device!.Name) ?? deviceName; } catch { }
        progress?.Invoke($"Scan finished: {files.Count:N0} files in {folders.Count:N0} folders ({status}).");
        return new ScanResult(deviceName, started, DateTimeOffset.UtcNow, files, warnings)
        {
            Report = report,
            Family = DeviceFamily.Apple
        };
    }

    private void ScanFolder(dynamic folder, string relativeFolder, List<MediaFile> files,
        CancellationToken cancellationToken, Action<string>? progress, List<string> warnings, List<FolderScanInfo> folders)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The shell also pays the device's per-object cost, and its cached listing only helps after a
        // first pass, so the same throttled progress reporting applies here.
        if (_scanReporter.ShouldReport(relativeFolder, _scanClock.Elapsed))
            progress?.Invoke(ScanProgressReporter.Describe(relativeFolder, files.Count, _scanBytes, _scanClock.Elapsed));
        int folderFiles = 0;
        long folderBytes = 0;
        dynamic? items = null;
        try
        {
            items = folder.Items();
            int count = Convert.ToInt32(items.Count);
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic? item = null;
                try
                {
                    item = items.Item(i);
                    string name = Convert.ToString(item.Name) ?? "";
                    bool isFolder = Convert.ToBoolean(item.IsFolder);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        warnings.Add($"An item in '{(string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder)}' has no readable name and was skipped.");
                        continue;
                    }

                    if (isFolder)
                    {
                        dynamic? childFolder = null;
                        try
                        {
                            childFolder = item.GetFolder;
                            string childRelative = CombineRelative(relativeFolder, name);
                            ScanFolder(childFolder, childRelative, files, cancellationToken, progress, warnings, folders);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            folders.Add(new FolderScanInfo(CombineRelative(relativeFolder, name), 0, 0, false, ex.Message));
                            warnings.Add($"Could not open subfolder '{CombineRelative(relativeFolder, name)}': {ex.Message}");
                        }
                        finally { Release(childFolder); }
                    }
                    else
                    {
                        string fileName = GetFileName(item);
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            warnings.Add($"A file in '{(string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder)}' has no readable filename and was skipped.");
                            continue;
                        }
                        string folderPath = string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder;
                        long size = GetFileSize(item, out bool sizeKnown);
                        files.Add(new MediaFile(folderPath, fileName, size, Path.GetExtension(fileName).ToLowerInvariant())
                        {
                            SizeKnown = sizeKnown
                        });
                        folderFiles++;
                        folderBytes += Math.Max(0, size);
                        _scanBytes += Math.Max(0, size);

                        if (_scanReporter.ShouldReport(relativeFolder, _scanClock.Elapsed))
                            progress?.Invoke(ScanProgressReporter.Describe(relativeFolder, files.Count, _scanBytes, _scanClock.Elapsed));
                    }
                }
                finally { Release(item); }
            }
            folders.Add(new FolderScanInfo(string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder,
                folderFiles, folderBytes, true, null));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            folders.Add(new FolderScanInfo(string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder,
                folderFiles, folderBytes, false, ex.Message));
            warnings.Add($"Could not enumerate folder '{(string.IsNullOrEmpty(relativeFolder) ? "DCIM" : relativeFolder)}': {ex.Message}");
        }
        finally { Release(items); }
    }

    private static string CombineRelative(string parent, string child) =>
        string.IsNullOrEmpty(parent) ? child : Path.Combine(parent, child);

    /// <summary>
    /// Finds the camera roll inside a storage container and reports which shape it has.
    /// Two layouts occur in the field and both mean the same thing: a DCIM child folder, or — when the
    /// driver publishes the DCIM albums at the storage root — the storage container itself.
    /// </summary>
    private static CameraRollShape ResolveMediaRoot(dynamic storage, out dynamic? mediaRoot)
    {
        mediaRoot = null;
        var names = new List<string?>();
        dynamic? dcim = null;
        dynamic? items = null;
        try
        {
            items = storage.Items();
            int count = Convert.ToInt32(items.Count);
            for (int i = 0; i < count; i++)
            {
                dynamic? child = null;
                try
                {
                    child = items.Item(i);
                    string childName = (Convert.ToString(child.Name) ?? "").Trim();
                    names.Add(childName);
                    if (dcim is null && CameraRoll.IsDcimContainer(childName))
                    {
                        dcim = child.GetFolder;
                        child = null; // ownership transferred to dcim
                    }
                }
                finally { Release(child); }
            }
        }
        catch { Release(dcim); return CameraRollShape.Unknown; }
        finally { Release(items); }

        CameraRollShape shape = CameraRoll.Decide(names);
        if (shape == CameraRollShape.DcimFolder && dcim is not null)
        {
            mediaRoot = dcim;
            return shape;
        }
        Release(dcim);
        if (shape == CameraRollShape.StorageContainer)
        {
            mediaRoot = storage;
            return shape;
        }
        return CameraRollShape.Unknown;
    }

    public CopyResult CopyToStage(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken token) =>
        _apartment.Invoke(() => CopyToStageCore(file, stagingDirectory, timeout, token));

    private CopyResult CopyToStageCore(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken token)
    {
        dynamic? source = null;
        dynamic? destination = null;
        try
        {
            token.ThrowIfCancellationRequested();
            string relativeFolder = string.Equals(file.Folder, "DCIM", StringComparison.OrdinalIgnoreCase)
                ? string.Empty : file.Folder;
            dynamic? folder = ResolveFolder(relativeFolder);
            if (folder is null) throw new IOException($"Source folder '{file.Folder}' is no longer accessible.");

            // ParseName avoids re-enumerating every item in a large folder for every file.
            // The fallback is retained for virtual-folder providers that do not support ParseName.
            try { source = folder.ParseName(file.Name); } catch { source = null; }
            if (source is null)
            {
                dynamic? items = null;
                try
                {
                    items = folder.Items();
                    int count = Convert.ToInt32(items.Count);
                    for (int i = 0; i < count; i++)
                    {
                        dynamic? item = null;
                        try
                        {
                            item = items.Item(i);
                            if (!Convert.ToBoolean(item.IsFolder) && string.Equals(GetFileName(item), file.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                source = item;
                                item = null;
                                break;
                            }
                        }
                        finally { Release(item); }
                    }
                }
                finally { Release(items); }
            }
            if (source is null) throw new FileNotFoundException("The source item disappeared from the iPhone.", file.RelativePath);
            Directory.CreateDirectory(stagingDirectory);
            string stagePath = Path.Combine(stagingDirectory, file.Name);
            if (File.Exists(stagePath)) File.Delete(stagePath);
            destination = _shell!.NameSpace(stagingDirectory);
            if (destination is null) throw new IOException("Windows Shell could not open the staging destination.");
            destination.CopyHere(source, 4 + 16 + 512 + 1024);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            long lastSize = -1;
            DateTime lastChange = DateTime.UtcNow;
            int stableSamples = 0;
            while (timer.Elapsed < timeout)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(300);
                if (!File.Exists(stagePath)) continue;
                long size = new FileInfo(stagePath).Length;
                if (size != lastSize) { lastSize = size; lastChange = DateTime.UtcNow; stableSamples = 0; }
                else stableSamples++;
                bool unlocked = CanOpenExclusively(stagePath);
                bool sizeMatches = file.Size > 0 && size == file.Size;
                bool unknownSizeStable = file.Size <= 0 && stableSamples >= 10;
                if (unlocked && (sizeMatches || unknownSizeStable)) return new CopyResult(true, null, "") { BytesWritten = size };
                if (DateTime.UtcNow - lastChange > TimeSpan.FromSeconds(45))
                    throw new TimeoutException($"Staging file stopped changing before it matched the expected size ({size}/{file.Size} bytes).");
            }
            throw new TimeoutException($"Timed out waiting for Shell copy of '{file.RelativePath}'.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new CopyResult(false, null, ex.Message); }
        finally { Release(destination); Release(source); }
    }

    private dynamic? ResolveFolder(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return _mediaRoot;
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".." || s.Contains(':'))) return null;

        dynamic parent = _mediaRoot!;
        string current = "";
        foreach (string segment in segments)
        {
            current = current.Length == 0 ? segment : current + "/" + segment;
            if (_folderCache.TryGetValue(current, out object? cached))
            {
                parent = cached;
                continue;
            }

            dynamic? items = null;
            dynamic? childFolder = null;
            try
            {
                items = parent.Items();
                int count = Convert.ToInt32(items.Count);
                for (int i = 0; i < count; i++)
                {
                    dynamic? item = null;
                    bool match = false;
                    try
                    {
                        item = items.Item(i);
                        match = Convert.ToBoolean(item.IsFolder) &&
                            string.Equals(Convert.ToString(item.Name), segment, StringComparison.OrdinalIgnoreCase);
                        if (match) { childFolder = item.GetFolder; item = null; }
                    }
                    finally { Release(item); }
                    if (match) break;
                }
            }
            catch { Release(childFolder); return null; }
            finally { Release(items); }

            if (childFolder is null) return null;
            _folderCache[current] = childFolder;
            parent = childFolder;
        }
        return parent;
    }

    private static bool IsAppleDeviceName(string name) =>
        name.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Apple", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("iPad", StringComparison.OrdinalIgnoreCase);

    private void EnsureShell() => _shell ??= Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true)!)!;

    private static string GetFileName(dynamic item)
    {
        try { string? name = Convert.ToString(item.ExtendedProperty("System.FileName")); if (!string.IsNullOrWhiteSpace(name)) return name; } catch { }
        return Convert.ToString(item.Name) ?? "";
    }

    /// <summary>
    /// Reads the Shell item's size, reporting whether a size was actually available. The Shell can
    /// return an item whose properties are not populated yet; that is an unknown size, not 0 bytes.
    /// </summary>
    private static long GetFileSize(dynamic item, out bool known)
    {
        try
        {
            object? size = item.ExtendedProperty("System.Size");
            if (size is not null) { known = true; return Convert.ToInt64(size); }
        }
        catch { }
        try { long value = Convert.ToInt64(item.Size); known = true; return value; }
        catch { known = false; return 0; }
    }

    private static bool CanOpenExclusively(string path)
    {
        try { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return true; }
        catch { return false; }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } }
    }

    private void ClearFolderCache()
    {
        foreach (object folder in _folderCache.Values) Release(folder);
        _folderCache.Clear();
    }

    /// <summary>
    /// Releases the storage and media roots. When the driver publishes the albums at the storage root
    /// the two fields reference the same object, so releasing both would final-release it twice.
    /// </summary>
    private void ReleaseRoots()
    {
        if (!ReferenceEquals(_mediaRoot, _storage)) Release(_mediaRoot);
        Release(_storage);
        _mediaRoot = null;
        _storage = null;
    }

    public void Dispose()
    {
        try { _apartment.Invoke(DisposeCore); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shell dispose error: {ex}"); }
        _apartment.Dispose();
    }

    private void DisposeCore()
    {
        ClearFolderCache();
        ReleaseRoots();
        Release(_device);
        _device = null;
        Release(_shell);
        _shell = null;
    }
}
