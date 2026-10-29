namespace ITotalTransfer.Core;

/// <summary>
/// Distinguishes the physical and logical states of a device so the UI can tell apart a device that
/// is merely plugged in from one that is trusted, one whose media can be enumerated, and one that is
/// actually ready for transfers.
/// </summary>
public enum DeviceStatus
{
    NotFound,
    /// <summary>USB power/host presence without a usable data session.</summary>
    Connected,
    /// <summary>Visible but locked or not trusted (no media session).</summary>
    LockedOrUntrusted,
    /// <summary>
    /// The iPhone is present and answering on the bus, but no media container is exposed to this
    /// transport (locked screen, Trust not accepted, iCloud-only originals, or MTP still warming up).
    /// This is a different situation from "nothing is plugged in" and the UI must say so.
    /// </summary>
    MediaUnavailable,
    /// <summary>Trusted and media is enumerable, but no backend can stream bytes yet.</summary>
    MediaAvailable,
    /// <summary>Fully ready: trusted, media enumerable, and a streaming backend is available.</summary>
    Ready,
    Error
}

/// <summary>
/// The single place that decides what a <see cref="DeviceStatus"/> means for the rest of the app, so
/// "is there a phone on the bus" never gets confused with "can I read its media".
/// </summary>
public static class DeviceStates
{
    /// <summary>Trusted and enumerable — bytes can actually be read.</summary>
    public static bool IsUsable(this DeviceStatus status) =>
        status is DeviceStatus.Ready or DeviceStatus.MediaAvailable;

    /// <summary>An iPhone answered the transport, even if its media is not readable yet.</summary>
    public static bool IsPresent(this DeviceStatus status) =>
        status is DeviceStatus.Ready or DeviceStatus.MediaAvailable
            or DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted
            or DeviceStatus.Connected;

    /// <summary>Present but needing a physical action on the handset before media can be read.</summary>
    public static bool NeedsAttention(this DeviceStatus status) =>
        status is DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted or DeviceStatus.Connected;
}

public enum TransferState
{
    Discovered, Queued, Copying, Paused, SizeVerified, IntegrityVerified, Skipped,
    CopiedUnverified, Failed, Inaccessible
}

/// <summary>
/// The strongest verification actually achieved for a single transfer entry, ordered from
/// weakest to strongest. A destination size match alone is <see cref="SizeOnly"/>; it never
/// implies byte identity. <see cref="SourceAndDestinationHash"/> is only reachable when the
/// active backend could read and hash the raw source byte stream.
/// </summary>
public enum VerificationLevel { None = 0, SizeOnly = 1, DestinationHash = 2, SourceAndDestinationHash = 3 }

/// <summary>
/// Why an item ended in its current state. The state alone cannot tell "skipped because the filter
/// excluded it" from "skipped because the destination already held a verified copy", and a report that
/// merges the two overstates the backup. This is recorded per item and persisted.
/// </summary>
public enum OutcomeReason
{
    None = 0,
    Planned = 1,
    Copied = 2,
    AlreadyPresent = 3,
    FilterExcluded = 4,
    ConflictSkipped = 5,
    Inaccessible = 6,
    Failed = 7
}

/// <summary>Transfer direction. iPhone source data is read-only by default.</summary>
public enum TransferDirection { DeviceToWindows = 0, WindowsToDevice = 1 }

/// <summary>What to do when a destination file already exists.</summary>
public enum ConflictPolicy { PreserveAndReport = 0, Skip = 1, RenameIncoming = 2, Overwrite = 3 }

/// <summary>How aggressively destination contents are verified.</summary>
public enum VerificationPolicy { SizeOnly = 0, DestinationHash = 1, SourceAndDestinationHash = 2 }

/// <summary>Lifecycle state of an enumeration. Never report <see cref="Complete"/> from a partial scan.</summary>
public enum EnumerationStatus { Complete, Incomplete, Failed, Empty }

public sealed record DeviceInfo(DeviceStatus Status, string Name, string Message)
{
    public string? Model { get; init; }
    public string? OperatingSystem { get; init; }
    public string? SerialNumber { get; init; }
    public string? ConnectionType { get; init; }
    public string? StableId { get; init; }
    public string? BackendName { get; init; }
    public long? AvailableBytes { get; init; }
    public long? TotalBytes { get; init; }
    /// <summary>
    /// The family whose storage rules the attached device is being read with. Decided from the device's
    /// own reported identity and, for a generic MTP volume, from the storage it exposes — never from the
    /// transport's device-type hint, which Microsoft documents as being for display only.
    /// </summary>
    public DeviceFamily Family { get; init; } = DeviceFamily.Unknown;
}

public sealed record MediaFile(string Folder, string Name, long Size, string Extension)
{
    public string RelativePath => Path.Combine(Folder, Name);
    /// <summary>
    /// False when the backend could not read this object's size. <see cref="Size"/> is then a 0
    /// placeholder, not a measurement: a genuine zero-byte file has a known size of 0, an unreadable
    /// one has no size at all. The two must never be reported as the same thing, because only the
    /// first can be verified.
    /// </summary>
    public bool SizeKnown { get; init; } = true;
    /// <summary>Last-write timestamp reported by the source backend, when available.</summary>
    public DateTimeOffset? ModifiedUtc { get; init; }
    /// <summary>Opaque backend identity (WPD object ID, AFC path). Not durable across reconnects.</summary>
    public string? SourceObjectId { get; init; }
    public bool IsLivePhotoVideo => Extension is ".mov" or ".mp4";

    /// <summary>The banner group this file belongs to (Photos / Videos / Music / Files).</summary>
    public MediaCategory Category => MediaCategories.Of(Extension);
}

/// <summary>Per-folder enumeration outcome so an incomplete scan can be explained precisely.</summary>
public sealed record FolderScanInfo(string RelativePath, int FileCount, long Bytes, bool Succeeded, string? Error);

/// <summary>
/// A complete, self-describing enumeration report. Distinguishes the number of exposed files from
/// the number of photos/videos the Photos app may display (Live Photos, sidecars and edited assets
/// produce different counts) and records exactly which folders could not be read.
/// </summary>
public sealed record EnumerationReport(
    EnumerationStatus Status,
    int FolderCount,
    int FailedFolderCount,
    int FileCount,
    long TotalBytes,
    int ZeroByteCount,
    int UnknownSizeCount,
    int DuplicatePathCount,
    int LivePhotoPairCount,
    string BackendName,
    IReadOnlyList<FolderScanInfo> Folders,
    IReadOnlyList<string> Warnings)
{
    public static EnumerationReport Empty(string backend, string reason) => new(
        EnumerationStatus.Empty, 0, 0, 0, 0, 0, 0, 0, 0, backend, [], [reason]);
}

public sealed record ScanResult(string DeviceName, DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc,
    IReadOnlyList<MediaFile> Files, IReadOnlyList<string> Warnings)
{
    public long TotalBytes => Files.Sum(f => Math.Max(0, f.Size));
    public EnumerationReport? Report { get; init; }
    /// <summary>The family the scan actually read, when the backend can report it.</summary>
    public DeviceFamily Family { get; init; } = DeviceFamily.Unknown;
    /// <summary>The backend's stable identity for the device the scan actually read.</summary>
    public string? StableId { get; init; }
    public TimeSpan Duration => FinishedUtc - StartedUtc;
    public EnumerationStatus Status => Report?.Status ?? EnumerationStatus.Complete;
    public bool IsComplete => Status == EnumerationStatus.Complete;

    /// <summary>
    /// True when this listing came from the device that was selected for the session. A backend that
    /// re-enumerates the bus between the probe and the scan can otherwise return the other attached
    /// phone's tree, which is how an "iPhone" scan once read the HUAWEI and overwrote the iPhone's
    /// cached listing. When neither side has identity evidence the answer is true: absence of evidence
    /// is not a mismatch, and legacy/test backends must keep working.
    /// </summary>
    public bool BelongsTo(DeviceInfo selected)
    {
        if (!string.IsNullOrWhiteSpace(StableId) && !string.IsNullOrWhiteSpace(selected.StableId))
            return string.Equals(StableId, selected.StableId, StringComparison.Ordinal);
        if (Family != DeviceFamily.Unknown && selected.Family != DeviceFamily.Unknown)
            return Family == selected.Family &&
                   string.Equals(DeviceName, selected.Name, StringComparison.OrdinalIgnoreCase);
        return true;
    }
}

public sealed class TransferEntry
{
    public string RelativePath { get; set; } = "";
    public long ExpectedBytes { get; set; }
    /// <summary>
    /// Whether <see cref="ExpectedBytes"/> was measured by the backend. False means the source size is
    /// unknown (stored as 0 for schema compatibility), so the copy cannot be size-verified and a
    /// resume cannot treat a same-size destination as evidence of anything.
    /// </summary>
    public bool SizeKnown { get; set; } = true;
    /// <summary>
    /// Bytes actually committed to the destination. When the source size is unknown this is the only
    /// honest byte figure for the item, so reports and byte totals use it instead of the 0 placeholder.
    /// </summary>
    public long BytesWritten { get; set; }
    public TransferState State { get; set; } = TransferState.Discovered;
    /// <summary>Why the item reached its state; see <see cref="OutcomeReason"/>.</summary>
    public OutcomeReason Outcome { get; set; } = OutcomeReason.None;
    public int Attempts { get; set; }
    /// <summary>SHA-256 of the raw source byte stream, populated only when the backend can read and hash the source bytes. Null for backends that cannot expose source bytes.</summary>
    public string? SourceSha256 { get; set; }
    /// <summary>SHA-256 of the payload staged from the device through the active backend.</summary>
    public string? StagedPayloadSha256 { get; set; }
    /// <summary>SHA-256 of the destination bytes at the last successful local integrity check.</summary>
    public string? DestinationSha256 { get; set; }
    /// <summary>The strongest verification level actually achieved for this entry.</summary>
    public VerificationLevel Verification { get; set; } = VerificationLevel.None;
    public string? Error { get; set; }
    public DateTimeOffset? SourceModifiedUtc { get; set; }
    /// <summary>
    /// True when a later scan reported a different size or modification time than the copy recorded.
    /// A same-size content change is only detectable through the timestamp; without one the engine
    /// cannot tell, and this stays false.
    /// </summary>
    public bool SourceChanged { get; set; }
    public bool PreserveTimestamp { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SessionManifest
{
    public int SchemaVersion { get; set; } = 2;
    public string Product { get; set; } = "I-Total Transfer";
    public string DeviceName { get; set; } = "";
    public string? DeviceStableId { get; set; }
    public string Destination { get; set; } = "";
    public string? BackendName { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? LastSessionStatus { get; set; }
    /// <summary>
    /// The enumeration status that produced this plan, as text so a resumed session can still say its
    /// source listing was partial after a cancel or restart. Missing on older records means Complete.
    /// </summary>
    public string? SourceEnumerationStatus { get; set; }
    public List<TransferEntry> Entries { get; set; } = [];
}

public sealed record TransferProgress(string Phase, int Current, int Total, long BytesDone, long TotalBytes,
    double BytesPerSecond, string CurrentFile, string Message)
{
    public double FilesPerSecond { get; init; }
    public int Copied { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int Unverifiable { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Eta { get; init; }
    public bool IsPaused { get; init; }
}

public sealed record TransferSummary(int Total, int Copied, int Skipped, int Failed, int Inaccessible, int Unverifiable,
    long ExpectedBytes, TimeSpan Elapsed, bool Completed, string Status, string ManifestPath)
{
    public int AlreadyPresent { get; init; }
    public long BytesCopied { get; init; }
    public VerificationLevel WeakestLevel { get; init; }
    public string? ReportPath { get; init; }
    /// <summary>Per-object reconciliation of the plan this run belongs to; null for non-copy operations.</summary>
    public ReconciliationReport? Reconciliation { get; init; }

    /// <summary>
    /// Identity of this run, generated by the engine. The licensing layer fingerprints it so a repeated
    /// report of the same finished job cannot consume a second trial use. Empty when a caller (or a test)
    /// constructed the record without one.
    /// </summary>
    public string JobId { get; init; } = "";
}

public sealed record VerificationResult(int Total, int Matching, int Missing, int WrongSize, int Unverifiable, int Collisions,
    bool Completed, IReadOnlyList<string> Issues)
{
    /// <summary>Files whose size matched but whose SHA-256 did not.</summary>
    public int WrongHash { get; init; }
    public int SourceHashVerified { get; init; }
    public int DestinationHashVerified { get; init; }
    public int Repaired { get; init; }
    public string? ReportPath { get; init; }
}
