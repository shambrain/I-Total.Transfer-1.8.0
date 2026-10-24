namespace ITotalTransfer.Core;

/// <summary>
/// Compares two enumerations of the same device by relative path and size. This is the check behind
/// "Confirm each scan with a second full enumeration": a camera roll can change while it is being walked
/// (a photo is taken, iCloud swaps an optimised asset for the original, an album is edited), and a plan
/// built from the first pass would then name files that no longer exist in that shape.
///
/// <para>
/// The comparison is a dictionary lookup per path, deliberately. The first implementation asked
/// <c>List.FirstOrDefault</c> once per path, which is quadratic: on a 26,000-item camera roll that is
/// hundreds of millions of string comparisons on the UI thread immediately after a cold scan, at exactly
/// the moment the window has to stay responsive. It also built a <c>ToDictionary</c> from the first pass,
/// which throws on a duplicate path — and a partial scan is *allowed* to report duplicates (that is why it
/// is classified incomplete), so the old code could turn a warning into an unhandled scan failure.
/// </para>
/// </summary>
public static class ScanStability
{
    /// <summary>
    /// The paths that were added, removed or changed size between the two passes, in the order a person
    /// wants to read them: what the second pass holds first (added and resized), then what it no longer
    /// holds (removed). The list is capped at <paramref name="take"/> so a wholesale change cannot flood a
    /// log line or the failure dialog it is quoted in.
    /// </summary>
    public static IReadOnlyList<string> ChangedPaths(ScanResult first, ScanResult second, int take = 20)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (take <= 0) return [];

        Dictionary<string, long> before = Index(first);
        Dictionary<string, long> after = Index(second);
        var changed = new List<string>();

        foreach (KeyValuePair<string, long> entry in after)
        {
            if (!before.TryGetValue(entry.Key, out long previous) || previous != entry.Value)
            {
                changed.Add(entry.Key);
                if (changed.Count >= take) return changed;
            }
        }

        foreach (string path in before.Keys)
        {
            if (!after.ContainsKey(path))
            {
                changed.Add(path);
                if (changed.Count >= take) return changed;
            }
        }

        return changed;
    }

    /// <summary>True when both passes describe the same set of paths at the same sizes.</summary>
    public static bool Agree(ScanResult first, ScanResult second) => ChangedPaths(first, second, take: 1).Count == 0;

    /// <summary>
    /// Indexes a pass by relative path the way Windows addresses files: case-insensitively. A duplicate
    /// path keeps the last entry rather than throwing, matching how the manifest treats one (the scan has
    /// already been classified incomplete and warned about it).
    /// </summary>
    private static Dictionary<string, long> Index(ScanResult scan)
    {
        var map = new Dictionary<string, long>(scan.Files.Count, StringComparer.OrdinalIgnoreCase);
        foreach (MediaFile file in scan.Files) map[file.RelativePath] = file.Size;
        return map;
    }
}
