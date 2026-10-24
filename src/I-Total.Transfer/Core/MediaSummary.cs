namespace ITotalTransfer.Core;

/// <summary>How many files, and how many bytes, one media group holds.</summary>
public readonly record struct CategoryStat(int Files, long Bytes)
{
    public static CategoryStat Empty => new(0, 0);
}

/// <summary>
/// Aggregates an enumerated file list into the banner's four groups. Kept in Core so the dashboard,
/// the library and the tests all describe the same grouping.
/// </summary>
public static class MediaSummary
{
    /// <summary>Counts files and bytes per group. Every group is present, even at zero.</summary>
    public static Dictionary<MediaCategory, CategoryStat> From(IEnumerable<MediaFile> files)
    {
        var totals = new Dictionary<MediaCategory, CategoryStat>();
        foreach (MediaCategory category in MediaCategories.All) totals[category] = CategoryStat.Empty;

        foreach (MediaFile file in files)
        {
            MediaCategory category = file.Category;
            CategoryStat current = totals[category];
            totals[category] = new CategoryStat(current.Files + 1, current.Bytes + Math.Max(0, file.Size));
        }
        return totals;
    }

    /// <summary>A compact, human-readable byte count.</summary>
    public static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):N2} GB"
        : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):N1} MB"
        : bytes >= 1024 ? $"{bytes / 1024d:N0} KB"
        : $"{bytes:N0} B";
}
