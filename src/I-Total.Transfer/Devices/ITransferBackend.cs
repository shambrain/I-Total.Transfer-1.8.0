using ITotalTransfer.Core;

namespace ITotalTransfer.Devices;

/// <summary>Identifies which device-access technology a backend wraps.</summary>
/// <remarks>
/// <c>Wpd</c> covers both handset families, because MTP is the transport Android and iOS share; the
/// device family is a property of the attached device, not of the backend. The values stay distinct
/// where the technology really differs: <c>Shell</c> is the Explorer-compatibility fallback and
/// <c>Afc</c> the Apple-only native path.
/// </remarks>
public enum BackendKind { Shell, Wpd, Afc }

/// <summary>What a backend can actually do. The engine and the UI must not advertise anything else.</summary>
public sealed record BackendCapabilities(
    bool CanEnumerate,
    bool CanReadSource,
    bool CanHashSourceBytes,
    bool CanReadBackCommitted,
    bool CanWriteToDevice,
    bool SupportsTimestamps,
    string Notes)
{
    /// <summary>
    /// The banner media groups this backend can actually enumerate. Consumers must present a group
    /// that is absent from this set as unavailable rather than as an empty folder.
    /// </summary>
    public IReadOnlyList<MediaCategory> Categories { get; init; } =
        [MediaCategory.Photos, MediaCategory.Videos];

    /// <summary>True when the backend exposes the given group.</summary>
    public bool Exposes(MediaCategory category) => Categories.Contains(category);

    public static BackendCapabilities None(string notes) => new(false, false, false, false, false, false, notes)
    {
        Categories = []
    };
}

/// <summary>
/// Outcome of staging a single file from the device. <see cref="SourceSha256"/> is populated only
/// when the backend can read and hash the raw source byte stream; backends that cannot (for example
/// Windows Shell <c>CopyHere</c>) leave it null, and the engine must not claim source-to-destination
/// identity for those copies.
/// </summary>
public sealed record CopyResult(bool Success, string? SourceSha256, string Error)
{
    public long BytesWritten { get; init; }
    public bool SourceHashComputed => !string.IsNullOrWhiteSpace(SourceSha256);
}

/// <summary>
/// The single seam through which the transfer engine talks to a device. Every backend (Shell
/// compatibility, native WPD, AFC) implements this, so the engine, manifests, verification, and
/// recovery logic remain backend-agnostic.
/// </summary>
public interface ITransferBackend : IDisposable
{
    BackendKind Kind { get; }
    /// <summary>Human-readable backend description reported in logs and reports.</summary>
    string Name { get; }
    /// <summary>True when this backend can expose and hash raw source bytes (enables source-to-destination verification).</summary>
    bool CanHashSourceBytes { get; }
    /// <summary>The concrete capabilities of this backend instance.</summary>
    BackendCapabilities Capabilities { get; }

    /// <summary>Connectively probe the device without mutating it. Never throws for expected failures.</summary>
    DeviceInfo Probe();

    /// <summary>
    /// Enumerate the full accessible tree. Must fail (throw) rather than return a partial listing as
    /// a complete scan, so a backup is never silently under-scoped.
    /// </summary>
    ScanResult Scan(CancellationToken cancellationToken, Action<string>? progress = null);

    /// <summary>
    /// Stage a single file into <paramref name="stagingDirectory"/>. Implementations stream data (do
    /// not load whole files into memory), honor <paramref name="cancellationToken"/>, and return a
    /// <see cref="CopyResult"/> whose <see cref="CopyResult.SourceSha256"/> is set when source bytes
    /// were hashed during streaming.
    /// </summary>
    CopyResult CopyToStage(MediaFile file, string stagingDirectory, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a forward-only read stream over the raw source bytes, or null when the backend cannot
    /// read source bytes. Used to compute a trustworthy source SHA-256 and to read back committed
    /// device files for verification. The caller disposes the stream.
    /// </summary>
    Stream? OpenSourceRead(MediaFile file) => null;

    /// <summary>
    /// Copies a local file to a device destination (Windows-to-device). Backends that cannot write
    /// throw <see cref="NotSupportedException"/>; the engine must never assume write support.
    /// </summary>
    CopyResult CopyToDevice(string localSourcePath, MediaFile deviceDestination, TimeSpan timeout, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{Name} does not support writing files to the device.");

    /// <summary>Deletes a device file created by this app (used only to roll back a failed write). Optional.</summary>
    bool TryDeleteDeviceFile(MediaFile file) => false;
}
