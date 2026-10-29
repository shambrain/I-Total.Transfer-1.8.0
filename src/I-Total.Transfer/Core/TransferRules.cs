namespace ITotalTransfer.Core;

/// <summary>How an existing destination file is classified when rebuilding a resume plan.</summary>
public enum ResumeDecision
{
    /// <summary>No existing destination (or it must be re-copied); proceed with the copy.</summary>
    Copy,
    /// <summary>Existing destination size and saved SHA-256 both match; safe to skip.</summary>
    SkipVerified,
    /// <summary>Existing destination size matches but no saved SHA-256 exists; record a local baseline and do not overwrite.</summary>
    RecordBaselineSkip,
    /// <summary>A saved SHA-256 exists but the current destination bytes differ; preserve it for manual review.</summary>
    PreserveHashMismatch,
    /// <summary>Source size is unknown or differs from the existing destination; preserve the existing file.</summary>
    PreserveCollision
}

/// <summary>An immutable, pure filter specification derived from user settings.</summary>
public sealed record FilterSpec(
    IReadOnlySet<string> IncludeExtensions,
    IReadOnlyList<string> ExcludeFolders,
    IReadOnlyList<string> ExcludeNameContains,
    bool ExcludeSidecars,
    long MinSize,
    long MaxSize)
{
    public static FilterSpec None { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase), [], [], false, 0, 0);
}

/// <summary>Pure transfer safety rules shared by production code and automated tests.</summary>
public static class TransferRules
{
    // Windows reserved device names are invalid as file/folder stems on any drive, with or without an extension.
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    private static readonly string[] SidecarExtensions = [".aae", ".aa", ".thm"];

    public static bool DestinationSizeMatches(long expectedBytes, long actualBytes) =>
        expectedBytes >= 0 && actualBytes >= 0 && expectedBytes == actualBytes;

    public static bool CanDeclareComplete(int discovered, int processed, int failed, int inaccessible) =>
        discovered > 0 && processed == discovered && failed == 0 && inaccessible == 0;

    /// <summary>Parses a user-supplied extension list ("jpg, .png;.MOV") into a normalised set.</summary>
    public static HashSet<string> ParseExtensionList(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return set;
        foreach (string token in raw.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = token.Trim();
            if (trimmed.Length == 0) continue;
            set.Add(trimmed.StartsWith('.') ? trimmed : "." + trimmed);
        }
        return set;
    }

    public static List<string> ParseTokenList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    public static FilterSpec BuildFilter(AppSettings s) => new(
        ParseExtensionList(s.IncludeExtensions),
        ParseTokenList(s.ExcludeFolders),
        ParseTokenList(s.ExcludeNameContains),
        s.ExcludeSidecars,
        Math.Max(0, s.MinFileSizeBytes),
        Math.Max(0, s.MaxFileSizeBytes));

    /// <summary>
    /// Pure inclusion test. Applied to enumeration output so the queue only ever contains files the
    /// user asked for; excluded files are counted as skipped, never silently dropped mid-transfer.
    /// </summary>
    public static bool IsIncluded(MediaFile file, FilterSpec filter)
    {
        if (filter.IncludeExtensions.Count > 0 &&
            !filter.IncludeExtensions.Contains(NormalizeExtension(file.Extension, file.Name)))
            return false;

        if (filter.ExcludeSidecars && SidecarExtensions.Contains(NormalizeExtension(file.Extension, file.Name)))
            return false;

        foreach (string fragment in filter.ExcludeNameContains)
            if (file.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)) return false;

        if (filter.ExcludeFolders.Count > 0)
        {
            string[] segments = file.Folder.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
                if (filter.ExcludeFolders.Contains(segment, StringComparer.OrdinalIgnoreCase)) return false;
        }

        if (filter.MinSize > 0 && file.Size > 0 && file.Size < filter.MinSize) return false;
        if (filter.MaxSize > 0 && file.Size > filter.MaxSize) return false;
        return true;
    }

    public static IReadOnlyList<MediaFile> ApplyFilter(IEnumerable<MediaFile> files, FilterSpec filter) =>
        files.Where(f => IsIncluded(f, filter)).ToList();

    /// <summary>
    /// Classifies an existing destination file purely from sizes and hashes so the resume decision
    /// is unit-testable and independent of I/O. Mirrors the production collision/resume policy:
    /// same-size with a matching saved digest is skipped; same-size without a digest records a
    /// baseline; a changed digest is preserved for review; any size mismatch (or unknown source
    /// size) is preserved as a collision. Nothing here ever authorizes overwriting user data.
    /// </summary>
    public static ResumeDecision ClassifyExistingDestination(long sourceSize, long existingSize,
        string? savedDestinationSha256, string? existingSha256, bool sourceSizeKnown = true)
    {
        // A recorded destination digest that still matches the bytes on disk proves the destination is
        // exactly what this session committed, which is stronger evidence than any size comparison.
        // This is also the only way an item whose source size is unknown can be resumed without
        // calling an already verified file a collision.
        bool sizesAgree = !sourceSizeKnown || (sourceSize >= 0 && sourceSize == existingSize);
        if (sizesAgree && !string.IsNullOrWhiteSpace(savedDestinationSha256) && !string.IsNullOrWhiteSpace(existingSha256) &&
            string.Equals(savedDestinationSha256, existingSha256, StringComparison.OrdinalIgnoreCase))
            return ResumeDecision.SkipVerified;

        // A known size of 0 is a measurement: a genuine zero-byte file can be compared and verified.
        // An unknown size is not 0, so nothing may be concluded from it.
        if (sourceSizeKnown && sourceSize >= 0 && sourceSize == existingSize)
        {
            if (string.IsNullOrWhiteSpace(savedDestinationSha256))
                return ResumeDecision.RecordBaselineSkip;
            return ResumeDecision.PreserveHashMismatch;
        }
        return ResumeDecision.PreserveCollision;
    }

    /// <summary>
    /// Derives the strongest verification level supported by the observed hashes. Source-to-destination
    /// identity is only declared when a non-empty source hash exactly matches the destination hash;
    /// staged-payload-to-destination equality is at most <see cref="VerificationLevel.DestinationHash"/>;
    /// a size match with no usable digest is <see cref="VerificationLevel.SizeOnly"/>.
    /// </summary>
    public static VerificationLevel ClassifyVerification(string? sourceSha256, string? stagedSha256,
        string? destinationSha256, bool sizeVerified)
    {
        if (!string.IsNullOrWhiteSpace(sourceSha256) && !string.IsNullOrWhiteSpace(destinationSha256) &&
            string.Equals(sourceSha256, destinationSha256, StringComparison.OrdinalIgnoreCase))
            return VerificationLevel.SourceAndDestinationHash;
        if (!string.IsNullOrWhiteSpace(stagedSha256) && !string.IsNullOrWhiteSpace(destinationSha256) &&
            string.Equals(stagedSha256, destinationSha256, StringComparison.OrdinalIgnoreCase))
            return VerificationLevel.DestinationHash;
        return sizeVerified ? VerificationLevel.SizeOnly : VerificationLevel.None;
    }

    /// <summary>
    /// Produces the destination-relative path for an incoming file, applying conflict resolution when
    /// the candidate path is already taken. Never returns a path outside the destination root.
    /// </summary>
    public static string ResolveTargetPath(string destinationRoot, string relativePath, ConflictPolicy policy,
        Func<string, bool> exists)
    {
        string candidate = Path.Combine(destinationRoot, relativePath);
        if (!exists(candidate) || policy is ConflictPolicy.PreserveAndReport) return candidate;
        if (policy is ConflictPolicy.Skip or ConflictPolicy.Overwrite) return candidate;

        // RenameIncoming: append " (n)" before the extension until a free name is found.
        string? dir = Path.GetDirectoryName(relativePath);
        string stem = Path.GetFileNameWithoutExtension(relativePath);
        string ext = Path.GetExtension(relativePath);
        for (int n = 1; n < 10_000; n++)
        {
            string renamed = Path.Combine(dir ?? "", $"{stem} ({n}){ext}");
            string full = Path.Combine(destinationRoot, renamed);
            if (!exists(full)) return full;
        }
        throw new IOException($"Could not find a free collision-safe name for '{relativePath}'.");
    }

    /// <summary>
    /// How many objects in a row may fail to be read before the walk concludes that the device has
    /// stopped answering rather than that a few objects are damaged. Twenty is deliberately far above
    /// any plausible run of individually unreadable items: a real camera roll with a handful of corrupt
    /// entries has readable ones between them, while a removed cable fails every single object.
    /// </summary>
    public const int ConsecutiveObjectFailureLimit = 20;

    /// <summary>True when an unbroken run of read failures means the device has gone, not the objects.</summary>
    public static bool DeviceStoppedAnswering(int consecutiveFailures) =>
        consecutiveFailures >= ConsecutiveObjectFailureLimit;

    /// <summary>Classifies an enumeration outcome. A scan is only Complete when every folder read and no duplicates exist.</summary>
    public static EnumerationStatus ClassifyEnumeration(int folderCount, int failedFolders, int fileCount,
        int duplicatePaths, int unknownSizeCount = 0)
    {
        if (fileCount == 0 && failedFolders == 0) return EnumerationStatus.Empty;
        if (failedFolders > 0) return EnumerationStatus.Incomplete;
        if (duplicatePaths > 0) return EnumerationStatus.Incomplete;
        // A handset that disappears during enumeration can still return names while every later
        // property read yields zero. A small number of genuine zero-byte files is normal; a large
        // fraction is not a complete listing. Ten percent is deliberately conservative.
        if (unknownSizeCount > 0 && unknownSizeCount * 10 >= fileCount) return EnumerationStatus.Incomplete;
        return EnumerationStatus.Complete;
    }

    /// <summary>Counts Live Photo candidates (a HEIC/JPEG with a sibling MOV of the same stem).</summary>
    public static int CountLivePhotoPairs(IEnumerable<MediaFile> files)
    {
        var photos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var videos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MediaFile file in files)
        {
            string stem = Path.Combine(file.Folder, Path.GetFileNameWithoutExtension(file.Name));
            if (file.IsLivePhotoVideo) videos.Add(stem); else photos.Add(stem);
        }
        return photos.Intersect(videos, StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>True when the bare name (ignoring any extension) collides with a reserved Windows device name.</summary>
    public static bool IsReservedWindowsName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        int dot = name.IndexOf('.');
        string stem = dot > 0 ? name[..dot] : name;
        return ReservedDeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    public static string ValidateRelativePath(string folder, string filename)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidDataException("Source folder path is empty.");
        if (string.IsNullOrWhiteSpace(filename))
            throw new InvalidDataException("Source filename is empty.");

        string normalizedFolder = folder.Replace('\\', '/');
        if (normalizedFolder.StartsWith('/') || normalizedFolder.Contains(':'))
            throw new InvalidDataException($"Unsafe absolute source folder path: '{folder}'.");

        string[] folderSegments = normalizedFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (folderSegments.Length == 0 || folderSegments.Any(IsUnsafeSegment))
            throw new InvalidDataException($"Unsafe source folder path: '{folder}'.");
        if (IsUnsafeSegment(filename) || filename.Contains('/') || filename.Contains('\\') || filename.Contains(':'))
            throw new InvalidDataException($"Unsafe source filename: '{filename}'.");

        return Path.Combine(folderSegments.Append(filename).ToArray());
    }

    /// <summary>Sanitises a single path segment so a source name can be made destination-safe without traversal.</summary>
    public static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "_";
        var chars = value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        string cleaned = new string(chars).TrimEnd(' ', '.');
        if (cleaned.Length == 0) cleaned = "_";
        if (IsReservedWindowsName(cleaned)) cleaned = "_" + cleaned;
        return cleaned;
    }

    private static string NormalizeExtension(string extension, string fileName)
    {
        string ext = string.IsNullOrWhiteSpace(extension) ? Path.GetExtension(fileName) : extension;
        return ext.ToLowerInvariant();
    }

    private static bool IsUnsafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains('/') || value.Contains('\\') || value.Contains(':'))
            return true;

        // Reject reserved device names (CON, PRN, NUL, COM1..9, LPT1..9) with or without extensions.
        if (IsReservedWindowsName(value)) return true;

        // Windows trims trailing spaces and dots from file and directory names; a segment that ends
        // with them would collide with another name and cannot be created reliably.
        if (value.EndsWith(' ') || value.EndsWith('.')) return true;

        // Reject control characters, which are invalid in Win32 long paths.
        return value.Any(ch => ch < 0x20);
    }
}
