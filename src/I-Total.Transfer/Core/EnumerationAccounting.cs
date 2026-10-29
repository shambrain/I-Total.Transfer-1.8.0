namespace ITotalTransfer.Core;

/// <summary>
/// Per-scan size accounting. The three backends used to compute "zero-byte" and "unknown size" from
/// the same test (<c>Size == 0</c>), which made every genuine zero-byte file look unmeasurable and
/// every unmeasurable file look empty. They are different facts:
///
/// <list type="bullet">
/// <item>A file whose size the backend read as 0 is a real zero-byte file: it can be copied, hashed
/// and verified like any other file.</item>
/// <item>A file whose size the backend could not read has no size at all: it can still be copied, but
/// nothing may claim it was size-verified, and a listing where this is common is not a healthy scan.</item>
/// </list>
///
/// This helper is the single place that decides which is which, so the report, the completeness rule
/// and the transfer plan cannot disagree.
/// </summary>
public readonly record struct ScanAccounting(int FileCount, int ZeroByteCount, int UnknownSizeCount, long KnownBytes)
{
    public static ScanAccounting Of(IEnumerable<MediaFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        int count = 0, zero = 0, unknown = 0;
        long bytes = 0;
        foreach (MediaFile file in files)
        {
            count++;
            // A negative size is not a measurement either; treat it as unreadable rather than
            // subtracting it from the total.
            if (!file.SizeKnown || file.Size < 0)
            {
                unknown++;
                continue;
            }
            if (file.Size == 0) zero++;
            bytes = checked(bytes + file.Size);
        }
        return new ScanAccounting(count, zero, unknown, bytes);
    }

    /// <summary>
    /// Builds the enumeration report every backend returns. Keeping this in one place means a
    /// backend cannot quietly classify the same listing differently from the others.
    /// </summary>
    public static EnumerationReport BuildReport(string backendName, IReadOnlyList<MediaFile> files,
        IReadOnlyList<FolderScanInfo> folders, int duplicatePathCount, IReadOnlyList<string> warnings)
    {
        ScanAccounting accounting = Of(files);
        int failedFolders = folders.Count(folder => !folder.Succeeded);
        EnumerationStatus status = TransferRules.ClassifyEnumeration(folders.Count, failedFolders,
            accounting.FileCount, duplicatePathCount, accounting.UnknownSizeCount);
        return new EnumerationReport(status, folders.Count, failedFolders, accounting.FileCount,
            accounting.KnownBytes, accounting.ZeroByteCount, accounting.UnknownSizeCount, duplicatePathCount,
            TransferRules.CountLivePhotoPairs(files), backendName, folders, warnings);
    }
}
