using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ITotalTransfer.Core;
using ITotalTransfer.Services;

namespace ITotalTransfer.Devices.Afc;

/// <summary>
/// AFC/libimobiledevice backend. It only activates when the native runtime is present and every
/// binary matches the pinned SHA-256 entries in <c>native/dependencies.json</c>; otherwise it reports
/// itself unavailable and the backend factory falls back to WPD or Shell. AFC is rooted at the
/// device's <c>/var/mobile/Media</c> area, which exposes the DCIM tree — it is not unrestricted
/// access to the private Photos database.
/// </summary>
public sealed class AfcDevice : ITransferBackend
{
    private readonly string _nativeDirectory;
    private readonly DependencyReport _dependencyReport;

    public BackendKind Kind => BackendKind.Afc;
    public string Name => "AFC / libimobiledevice";
    public bool CanHashSourceBytes => true;
    public BackendCapabilities Capabilities { get; }

    /// <summary>True when the pinned native runtime verified and AFC can be attempted.</summary>
    public bool RuntimeAvailable { get; }

    public string AvailabilityMessage { get; }

    private IntPtr _device;
    private IntPtr _lockdown;
    private IntPtr _service;
    private IntPtr _afc;
    private string? _udid;
    private bool _disposed;

    public AfcDevice(string? nativeDirectory = null)
    {
        _nativeDirectory = nativeDirectory ?? Path.Combine(AppContext.BaseDirectory, "native");
        _dependencyReport = DependencyVerifier.Verify(_nativeDirectory);
        // Every binary the manifest names has to verify, not only libimobiledevice. The runtime needs
        // libplist, libusbmuxd and libcrypto too; starting with one verified DLL and three missing ones
        // would fail later with an opaque loader error and, worse, could load an unpinned neighbour.
        var unmet = _dependencyReport.Files
            .Where(f => f.State != DependencyFileState.Verified)
            .ToList();
        bool hasCore = _dependencyReport.Files.Any(f =>
            f.Dependency.FileName.StartsWith("libimobiledevice", StringComparison.OrdinalIgnoreCase));
        bool runtimeAvailable = _dependencyReport.Files.Count > 0 && unmet.Count == 0 && hasCore;
        string? loadProblem = hasCore
            ? null
            : "The dependency manifest does not list libimobiledevice-1.0.dll, so there is no AFC runtime to use.";
        if (runtimeAvailable)
        {
            try
            {
                AfcNative.ConfigureVerifiedRuntime(_nativeDirectory,
                    _dependencyReport.Files.Select(f => f.Dependency.FileName).ToList());
            }
            catch (Exception ex)
            {
                // The hashes matched, but Windows could not load what the manifest named (wrong
                // architecture, a corrupt file, a missing private dependency). Stay disabled instead
                // of letting a later P/Invoke discover it mid-transfer.
                runtimeAvailable = false;
                loadProblem = "The pinned AFC runtime verified but could not be loaded: " + ex.Message;
            }
        }
        RuntimeAvailable = runtimeAvailable;
        AvailabilityMessage = _dependencyReport.Files.Count == 0
            ? $"No AFC dependency manifest entries found in '{_nativeDirectory}'."
            : unmet.Count > 0
                ? "AFC runtime is incomplete or untrusted: " +
                  string.Join(" ", unmet.Take(4).Select(f => $"{f.Dependency.FileName}: {f.Detail}")) +
                  (unmet.Count > 4 ? $" (+{unmet.Count - 4} more)" : "")
                : loadProblem ?? $"Pinned AFC native runtime verified and loaded ({_dependencyReport.Files.Count} binaries).";
        Capabilities = runtimeAvailable
            ? new BackendCapabilities(true, true, true, true, true, true,
                "AFC streaming with raw source hashing; read/write within /var/mobile/Media.")
            {
                // AFC-Windows is rooted at the device's whole /var/mobile/Media area, so it can reach
                // every group the banner advertises — subject to what a given iOS build actually
                // exposes under that root, which the scan probes and reports honestly.
                Categories = [.. MediaCategories.All]
            }
            : BackendCapabilities.None(AvailabilityMessage);
    }

    public DependencyReport DependencyStatus => _dependencyReport;

    public DeviceInfo Probe()
    {
        if (!RuntimeAvailable)
            return new DeviceInfo(DeviceStatus.NotFound, "", AvailabilityMessage)
            {
                BackendName = Name,
                Family = DeviceFamily.Apple
            };
        try
        {
            string? udid = FindDeviceUdid();
            if (udid is null)
                return new DeviceInfo(DeviceStatus.NotFound, "", "No AFC-compatible device was found.")
                {
                    BackendName = Name,
                    Family = DeviceFamily.Apple
                };
            EnsureConnected(udid);
            AfcStatus status = (AfcStatus)AfcNative.afc_read_directory(_afc, "/DCIM", out IntPtr listing);
            if (status != AfcStatus.Success)
            {
                AfcNative.ReadCStringList(listing);
                return new DeviceInfo(DeviceStatus.LockedOrUntrusted, "Apple iPhone", $"Connected through AFC, but /DCIM is not readable ({status}). Unlock the iPhone and confirm Trust.")
                {
                    BackendName = Name,
                    Family = DeviceFamily.Apple,
                    StableId = udid
                };
            }
            AfcNative.ReadCStringList(listing);
            return new DeviceInfo(DeviceStatus.Ready, "Apple iPhone", "iPhone media is accessible through AFC (hash-verified transfers available).")
            {
                BackendName = Name,
                ConnectionType = "USB (AFC)",
                Family = DeviceFamily.Apple,
                StableId = udid
            };
        }
        catch (Exception ex)
        {
            Reset();
            return new DeviceInfo(DeviceStatus.Error, "", "AFC probe failed: " + ex.Message)
            {
                BackendName = Name,
                Family = DeviceFamily.Apple
            };
        }
    }

    public ScanResult Scan(CancellationToken cancellationToken, Action<string>? progress = null)
    {
        if (!RuntimeAvailable) throw new InvalidOperationException(AvailabilityMessage);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var files = new List<MediaFile>();
        var folders = new List<FolderScanInfo>();
        var warnings = new List<string>();
        string udid = FindDeviceUdid() ?? throw new InvalidOperationException("No AFC-compatible device was found.");
        EnsureConnected(udid);

        // The AFC media area is split across fixed roots. Camera media lives in /DCIM; the rest of the
        // banner's groups (Music, Files) live in the other roots. A root that the device does not expose
        // is skipped and named, so "Music: 0 files" never hides an unreadable root.
        var skipped = new List<string>();
        foreach ((string afcPath, string relative) in MediaRoots)
        {
            if (!RootExists(afcPath))
            {
                skipped.Add(relative);
                continue;
            }
            EnumerateFolder(afcPath, relative, files, folders, warnings, cancellationToken, progress);
        }
        if (skipped.Count > 0)
            warnings.Add("Not exposed by this device: " + string.Join(", ", skipped) + ".");
        progress?.Invoke(skipped.Count == 0
            ? "AFC media roots: all exposed."
            : $"AFC media roots: {MediaRoots.Length - skipped.Count} of {MediaRoots.Length} exposed (absent: {string.Join(", ", skipped)}).");

        var duplicates = files.GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).Take(20).ToArray();
        if (duplicates.Length > 0) warnings.Add("Duplicate destination paths: " + string.Join(", ", duplicates));
        int unknownSize = files.Count(f => f.Size <= 0);
        int failed = folders.Count(f => !f.Succeeded);
        var status = TransferRules.ClassifyEnumeration(folders.Count, failed, files.Count, duplicates.Length);
        var report = new EnumerationReport(status, folders.Count, failed, files.Count,
            files.Sum(f => Math.Max(0, f.Size)), files.Count(f => f.Size == 0), unknownSize, duplicates.Length,
            TransferRules.CountLivePhotoPairs(files), Name, folders, warnings);
        progress?.Invoke($"AFC scan finished: {files.Count:N0} files in {folders.Count:N0} folders ({status}).");
        return new ScanResult("Apple iPhone", started, DateTimeOffset.UtcNow, files, warnings)
        {
            Report = report,
            Family = DeviceFamily.Apple,
            StableId = udid
        };
    }

    private static readonly (string AfcPath, string Relative)[] MediaRoots =
    [
        ("/DCIM", "DCIM"),
        ("/iTunes_Control/Music", "Music"),
        ("/Recordings", "Recordings"),
        ("/Books", "Books"),
        ("/Downloads", "Downloads"),
        ("/Purchases", "Purchases")
    ];

    /// <summary>The first path segment of every relative folder we enumerate from a media root.</summary>
    private static readonly string[] MediaRootNames =
        ["DCIM", "Music", "Recordings", "Books", "Downloads", "Purchases"];

    /// <summary>
    /// Whether the device exposes a media root. A root that does not exist is not a scan failure, so this
    /// must not throw; it reads and frees the directory listing to keep libimobiledevice's allocator happy.
    /// </summary>
    private bool RootExists(string afcPath)
    {
        try
        {
            AfcStatus status = (AfcStatus)AfcNative.afc_read_directory(_afc, afcPath, out IntPtr listing);
            if (status != AfcStatus.Success) return false;
            AfcNative.ReadCStringList(listing);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EnumerateFolder(string afcPath, string relativeFolder, List<MediaFile> files,
        List<FolderScanInfo> folders, List<string> warnings, CancellationToken token, Action<string>? progress)
    {
        token.ThrowIfCancellationRequested();
        progress?.Invoke($"Scanning {relativeFolder}");
        int fileCount = 0;
        long bytes = 0;
        try
        {
            AfcStatus status = (AfcStatus)AfcNative.afc_read_directory(_afc, afcPath, out IntPtr listing);
            if (status != AfcStatus.Success)
                throw new IOException($"afc_read_directory('{afcPath}') failed: {status}");
            List<string> flat = AfcNative.ReadCStringList(listing);

            // The listing is a flat list of key/value pairs; entries restart at each "st_name".
            string? name = null, ifmt = null, sizeText = null;
            void Flush()
            {
                if (name is null) return;
                string childPath = afcPath.TrimEnd('/') + "/" + name;
                string childRelative = Path.Combine(relativeFolder, name);
                if (ifmt == "S_IFDIR")
                {
                    EnumerateFolder(childPath, childRelative, files, folders, warnings, token, progress);
                }
                else if (name is not "." and not "..")
                {
                    long size = long.TryParse(sizeText, out long s) ? s : 0;
                    files.Add(new MediaFile(relativeFolder, name, size, Path.GetExtension(name).ToLowerInvariant()) { SourceObjectId = childPath });
                    fileCount++; bytes += Math.Max(0, size);
                }
                name = ifmt = sizeText = null;
            }

            for (int i = 0; i + 1 < flat.Count; i += 2)
            {
                string key = flat[i], value = flat[i + 1];
                switch (key)
                {
                    case "st_name": Flush(); name = value; break;
                    case "st_ifmt": ifmt = value; break;
                    case "st_size": sizeText = value; break;
                }
            }
            Flush();
            folders.Add(new FolderScanInfo(relativeFolder, fileCount, bytes, true, null));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            folders.Add(new FolderScanInfo(relativeFolder, fileCount, bytes, false, ex.Message));
            warnings.Add($"Could not fully enumerate '{relativeFolder}': {ex.Message}");
        }
    }

    public CopyResult CopyToStage(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken token)
    {
        if (!RuntimeAvailable) return new CopyResult(false, null, AvailabilityMessage);
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            string stagePath = Path.Combine(stagingDirectory, file.Name + ".partial");
            string finalPath = Path.Combine(stagingDirectory, file.Name);
            if (File.Exists(finalPath)) File.Delete(finalPath);
            string afcPath = ToAfcPath(file.Folder, file.Name);
            if (!_afcConnected) Probe();

            AfcStatus open = (AfcStatus)AfcNative.afc_open_file(_afc, afcPath, AfcNative.AfcFopenRdOnly, out IntPtr handle);
            if (open != AfcStatus.Success) return new CopyResult(false, null, $"afc_open_file('{afcPath}') failed: {open}");
            try
            {
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long written = 0;
                byte[] buffer = new byte[1024 * 1024];
                using (var output = new FileStream(stagePath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        AfcStatus read = (AfcStatus)AfcNative.afc_read_file(_afc, handle, buffer, buffer.Length, out int n);
                        if (read != AfcStatus.Success && read != AfcStatus.EndOfData)
                        {
                            TryDelete(stagePath);
                            return new CopyResult(false, null, $"afc_read_file failed: {read}") { BytesWritten = written };
                        }
                        if (n <= 0) break;
                        sha.AppendData(buffer, 0, n);
                        output.Write(buffer, 0, n);
                        written += n;
                    }
                    output.Flush(true);
                }
                if (file.Size > 0 && written != file.Size)
                {
                    TryDelete(stagePath);
                    return new CopyResult(false, null, $"AFC size mismatch: expected {file.Size}, read {written}.") { BytesWritten = written };
                }
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(stagePath, finalPath);
                return new CopyResult(true, Convert.ToHexString(sha.GetHashAndReset()), "") { BytesWritten = written };
            }
            finally { AfcNative.afc_close_file(_afc, handle); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Reset(); return new CopyResult(false, null, "AFC copy failed: " + ex.Message); }
    }

    public CopyResult CopyToDevice(string localSourcePath, MediaFile deviceDestination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!RuntimeAvailable) return new CopyResult(false, null, AvailabilityMessage);
        string? remoteTemp = null;
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (!_afcConnected) Probe();
            string afcPath = ToAfcPath(deviceDestination.Folder, deviceDestination.Name);
            EnsureAfcDirectory(Path.GetDirectoryName(afcPath)!.Replace('\\', '/'));

            // This direction must never destroy something already on the phone. A read probe tells us
            // whether the path is occupied before any write handle is opened; an unusual status is
            // treated as a refusal too, because guessing here could overwrite the operator's media.
            AfcStatus exists = (AfcStatus)AfcNative.afc_open_file(_afc, afcPath, AfcNative.AfcFopenRdOnly, out IntPtr existing);
            if (exists == AfcStatus.Success)
            {
                AfcNative.afc_close_file(_afc, existing);
                return new CopyResult(false, null,
                    $"A file already exists on the device at '{afcPath}'. It was left untouched.");
            }
            if (exists != AfcStatus.ObjectNotFound)
                return new CopyResult(false, null,
                    $"Could not check '{afcPath}' before writing ({exists}). Nothing was written.");

            // Write a sibling temporary name and publish it only after the byte count and hash are
            // complete. A cancel, unplug or no-space failure leaves the final name untouched.
            remoteTemp = afcPath + ".i-total-" + Guid.NewGuid().ToString("N") + ".partial";
            AfcStatus open = (AfcStatus)AfcNative.afc_open_file(_afc, remoteTemp, AfcNative.AfcFopenRw, out handle);
            if (open != AfcStatus.Success)
            {
                TryDeleteRemote(remoteTemp);
                remoteTemp = null;
                return new CopyResult(false, null, $"afc_open_file(write '{afcPath}') failed: {open}");
            }

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long written = 0;
            byte[] buffer = new byte[1024 * 1024];
            using (var input = new FileStream(localSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            {
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha.AppendData(buffer, 0, n);
                    int offset = 0;
                    while (offset < n)
                    {
                        AfcStatus wr = (AfcStatus)AfcNative.afc_write_file(_afc, handle, buffer[offset..(offset + (n - offset))], n - offset, out int wrote);
                        if (wr != AfcStatus.Success || wrote <= 0)
                        {
                            CloseWriteHandle(ref handle);
                            TryDeleteRemote(remoteTemp);
                            remoteTemp = null;
                            return new CopyResult(false, null, $"afc_write_file failed: {wr}") { BytesWritten = written };
                        }
                        offset += wrote; written += wrote;
                    }
                }
            }

            if (deviceDestination.Size > 0 && written != deviceDestination.Size)
            {
                CloseWriteHandle(ref handle);
                TryDeleteRemote(remoteTemp);
                remoteTemp = null;
                return new CopyResult(false, null,
                    $"The device received {written:N0} of {deviceDestination.Size:N0} bytes. The partial file was removed.")
                    { BytesWritten = written };
            }

            string sourceHash = Convert.ToHexString(sha.GetHashAndReset());
            CloseWriteHandle(ref handle);
            AfcStatus moved = (AfcStatus)AfcNative.afc_rename_path(_afc, remoteTemp, afcPath);
            if (moved != AfcStatus.Success)
            {
                TryDeleteRemote(remoteTemp);
                remoteTemp = null;
                return new CopyResult(false, null,
                    $"afc_rename_path('{afcPath}') failed: {moved}. The temporary file was removed and no destination was published.")
                    { BytesWritten = written };
            }
            remoteTemp = null;
            return new CopyResult(true, sourceHash, "") { BytesWritten = written };
        }
        catch (OperationCanceledException)
        {
            CloseWriteHandle(ref handle);
            if (remoteTemp is not null) TryDeleteRemote(remoteTemp);
            throw;
        }
        catch (Exception ex)
        {
            CloseWriteHandle(ref handle);
            if (remoteTemp is not null) TryDeleteRemote(remoteTemp);
            Reset();
            return new CopyResult(false, null, "AFC write failed: " + ex.Message);
        }
        finally { CloseWriteHandle(ref handle); }
    }

    private void CloseWriteHandle(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        try { AfcNative.afc_close_file(_afc, handle); } catch { }
        handle = IntPtr.Zero;
    }

    private void TryDeleteRemote(string path)
    {
        try { AfcNative.afc_remove_path(_afc, path); } catch { }
    }

    public bool TryDeleteDeviceFile(MediaFile file)
    {
        if (!RuntimeAvailable) return false;
        try { return (AfcStatus)AfcNative.afc_remove_path(_afc, ToAfcPath(file.Folder, file.Name)) == AfcStatus.Success; }
        catch { return false; }
    }

    public Stream? OpenSourceRead(MediaFile file)
    {
        if (!RuntimeAvailable) return null;
        try
        {
            if (!_afcConnected) Probe();
            string afcPath = ToAfcPath(file.Folder, file.Name);
            AfcStatus open = (AfcStatus)AfcNative.afc_open_file(_afc, afcPath, AfcNative.AfcFopenRdOnly, out IntPtr handle);
            if (open != AfcStatus.Success) return null;
            return new AfcReadStream(_afc, handle);
        }
        catch { return null; }
    }

    private sealed class AfcReadStream : Stream
    {
        private readonly IntPtr _client;
        private readonly IntPtr _handle;
        private bool _eof;
        public AfcReadStream(IntPtr client, IntPtr handle) { _client = client; _handle = handle; }
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
            byte[] temp = new byte[count];
            AfcStatus status = (AfcStatus)AfcNative.afc_read_file(_client, _handle, temp, count, out int n);
            if (status != AfcStatus.Success || n <= 0) { _eof = true; return 0; }
            Array.Copy(temp, 0, buffer, offset, n);
            return n;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { AfcNative.afc_close_file(_client, _handle); } catch { } }
            base.Dispose(disposing);
        }
    }

    private void EnsureAfcDirectory(string dir)
    {
        string[] segments = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        foreach (string segment in segments)
        {
            current += "/" + segment;
            AfcStatus status = (AfcStatus)AfcNative.afc_make_directory(_afc, current);
            if (status is not (AfcStatus.Success or AfcStatus.ObjectExists))
            {
                // The directory may already exist; ignore OBJECT_EXISTS and continue.
                if (status != AfcStatus.ObjectExists) throw new IOException($"afc_make_directory('{current}') failed: {status}");
            }
        }
    }

    private bool _afcConnected;

    private void EnsureConnected(string udid)
    {
        if (_afcConnected && string.Equals(_udid, udid, StringComparison.OrdinalIgnoreCase)) return;
        Reset();
        Check(AfcNative.idevice_new(out _device, udid), "idevice_new");
        Check(AfcNative.lockdownd_client_new_with_handshake(_device, out _lockdown, "I-Total Transfer"), "lockdownd_client_new_with_handshake");
        Check(AfcNative.lockdownd_start_service(_lockdown, "com.apple.afc", out _service), "lockdownd_start_service");
        Check(AfcNative.afc_client_new(_device, _service, out _afc), "afc_client_new");
        _udid = udid;
        _afcConnected = true;
    }

    private string? FindDeviceUdid()
    {
        Check(AfcNative.idevice_get_device_list(out IntPtr list, out int count), "idevice_get_device_list");
        List<string> devices = AfcNative.ReadCStringList(list);
        return devices.FirstOrDefault();
    }

    private static void Check(int status, string operation)
    {
        if (status != 0) throw new IOException($"{operation} failed with libimobiledevice error {status}.");
    }

    private static string ToAfcPath(string folder, string name)
    {
        string relative = TransferRules.ValidateRelativePath(folder, name).Replace('\\', '/');
        // Older manifests stored a bare "100APPLE\IMG_0001.JPG"; those still belong under /DCIM.
        // Folders that already start with a media root (DCIM, Music, Books, …) are used verbatim.
        string first = relative.Split('/')[0];
        if (!MediaRootNames.Any(root => string.Equals(root, first, StringComparison.OrdinalIgnoreCase)))
            relative = "DCIM/" + relative;
        return "/" + relative.TrimStart('/');
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private void Reset()
    {
        try { if (_afc != IntPtr.Zero) AfcNative.afc_client_free(_afc); } catch { }
        try { if (_service != IntPtr.Zero) AfcNative.lockdownd_service_descriptor_free(_service); } catch { }
        try { if (_lockdown != IntPtr.Zero) AfcNative.lockdownd_client_free(_lockdown); } catch { }
        try { if (_device != IntPtr.Zero) AfcNative.idevice_free(_device); } catch { }
        _afc = _lockdown = _service = _device = IntPtr.Zero;
        _udid = null;
        _afcConnected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
    }
}
