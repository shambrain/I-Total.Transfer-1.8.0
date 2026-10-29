using System.Security.Cryptography;
using ITotalTransfer.Core;
using ITotalTransfer.Devices;

namespace ITotalTransfer.Tests;

/// <summary>
/// Deterministic in-memory backend for exercising the transfer engine without a physical device.
/// When <see cref="_canHashSourceBytes"/> is true it reports the real SHA-256 of the bytes it staged,
/// emulating a native WPD resource-stream backend; otherwise it returns null, emulating Shell CopyHere.
/// </summary>
public sealed class MockBackend : ITransferBackend
{
    private readonly bool _canHashSourceBytes;
    private readonly IReadOnlyList<MediaFile> _files;
    private readonly Dictionary<string, byte[]> _contentByRelativePath;

    /// <param name="sizesKnown">
    /// False emulates a backend that exposes bytes but cannot report their size (the case the HUAWEI
    /// produced for some placeholder files). The size is then unknown, not zero, and the engine must
    /// not treat it as a measurement.
    /// </param>
    public MockBackend(bool canHashSourceBytes, int fileCount, int bytesPerFile = 1024, bool sizesKnown = true,
        int unknownSizeFiles = -1)
    {
        _canHashSourceBytes = canHashSourceBytes;
        var files = new List<MediaFile>();
        var content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fileCount; i++)
        {
            string name = $"IMG_{i:D4}.bin";
            bool known = unknownSizeFiles < 0 ? sizesKnown : i >= unknownSizeFiles;
            var file = new MediaFile("DCIM", name, known ? bytesPerFile : 0, ".bin")
            {
                SizeKnown = known
            };
            files.Add(file);
            byte[] bytes = new byte[bytesPerFile];
            for (int j = 0; j < bytes.Length; j++) bytes[j] = (byte)(i * 7 + j % 251);
            content[file.RelativePath] = bytes;
        }
        _files = files;
        _contentByRelativePath = content;
    }

    public BackendKind Kind => _canHashSourceBytes ? BackendKind.Wpd : BackendKind.Shell;
    public string Name => "Mock backend";
    public bool CanHashSourceBytes => _canHashSourceBytes;

    /// <summary>
    /// Relative paths this backend must refuse to stage, so a partially completed session can be produced
    /// deterministically. The engine treats a refusal as a failed file, exactly as it would a real one.
    /// </summary>
    public HashSet<string> FailPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Relative paths that fail with a permanent error (denied or missing). The engine must not spend
    /// its retry budget on these.
    /// </summary>
    public HashSet<string> PermanentFailurePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Relative paths whose fetch throws instead of reporting a failure, emulating a backend fault (a
    /// dropped device, a COM failure) as opposed to a file that simply could not be read.
    /// </summary>
    public HashSet<string> ThrowPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Relative paths whose staged bytes are corrupted after the source digest is taken, so the engine
    /// sees a source hash that does not match the committed destination. Used to prove that a failed
    /// overwrite restores the previous destination instead of losing it.
    /// </summary>
    public HashSet<string> CorruptPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Relative paths whose staged file is written short of the reported size, emulating a provider
    /// that ends the stream early. The engine must notice the size mismatch and refuse to commit.
    /// </summary>
    public HashSet<string> TruncatePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every relative path the engine actually asked the device to fetch, in order.</summary>
    public List<string> FetchedPaths { get; } = new();

    /// <summary>Invoked at the start of every staging attempt, so a test can observe or interrupt mid-flight.</summary>
    public Action<MediaFile>? OnCopy { get; set; }

    public BackendCapabilities Capabilities => _canHashSourceBytes
        ? new BackendCapabilities(true, true, true, false, false, false, "Mock backend emulating a source-hashing device backend.")
        : new BackendCapabilities(true, true, false, false, false, false, "Mock backend emulating Shell CopyHere (no source hash).");

    public DeviceInfo Probe() => new(DeviceStatus.Ready, "Mock iPhone", "mock ready");

    public ScanResult Scan(CancellationToken cancellationToken, Action<string>? progress = null)
    {
        // Emulate a device backend closely enough to exercise the engine's contract: it checks for
        // cancellation as it goes, and it reports progress while it walks the folder. A backend that
        // ignores both is how "the scan seems to hang with no way to stop it" becomes possible.
        for (int i = 0; i < _files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (progress is not null && (i == 0 || i % 1000 == 0))
                progress($"Scanning DCIM · {i:N0} files · {i * 1024L / (1024 * 1024)} MB");
        }
        // Build the same report a real backend builds, so the engine's completeness decision is
        // exercised against real accounting rather than a hard-coded Complete.
        EnumerationReport report = ScanAccounting.BuildReport(Name, _files, [], 0, []);
        return new ScanResult("Mock iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, _files, Array.Empty<string>())
        {
            Report = report,
            Family = DeviceFamily.Apple
        };
    }

    public CopyResult CopyToStage(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FetchedPaths.Add(file.RelativePath);
        OnCopy?.Invoke(file);
        if (ThrowPaths.Contains(file.RelativePath))
            throw new IOException("mock backend fault while fetching " + file.RelativePath);
        if (PermanentFailurePaths.Contains(file.RelativePath))
            return new CopyResult(false, null, "mock access denied") { FailureKind = CopyFailureKind.Permanent };
        if (FailPaths.Contains(file.RelativePath))
            return new CopyResult(false, null, "mock staging refusal");
        if (!_contentByRelativePath.TryGetValue(file.RelativePath, out byte[]? bytes))
            return new CopyResult(false, null, "source disappeared");
        Directory.CreateDirectory(stagingDirectory);
        if (TruncatePaths.Contains(file.RelativePath) && bytes.Length > 0)
            bytes = bytes[..(bytes.Length / 2)];
        File.WriteAllBytes(Path.Combine(stagingDirectory, file.Name), bytes);
        if (CorruptPaths.Contains(file.RelativePath))
        {
            byte[] corrupted = (byte[])bytes.Clone();
            if (corrupted.Length > 0) corrupted[0] ^= 0xFF;
            File.WriteAllBytes(Path.Combine(stagingDirectory, file.Name), corrupted);
        }
        string? sourceHash = _canHashSourceBytes ? Convert.ToHexString(SHA256.HashData(bytes)) : null;
        return new CopyResult(true, sourceHash, "");
    }

    public void Dispose() { }
}
