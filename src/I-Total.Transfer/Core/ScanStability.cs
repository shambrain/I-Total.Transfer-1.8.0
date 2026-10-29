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

        Dictionary<string, Stamp> before = Index(first);
        Dictionary<string, Stamp> after = Index(second);
        var changed = new List<string>();

        foreach (KeyValuePair<string, Stamp> entry in after)
        {
            if (!before.TryGetValue(entry.Key, out Stamp previous) || Differs(previous, entry.Value))
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
    private static Dictionary<string, Stamp> Index(ScanResult scan)
    {
        var map = new Dictionary<string, Stamp>(scan.Files.Count, StringComparer.OrdinalIgnoreCase);
        foreach (MediaFile file in scan.Files)
            map[file.RelativePath] = new Stamp(file.Size, file.SizeKnown, file.ModifiedUtc);
        return map;
    }

    /// <summary>
    /// A path changed when its measured size changed, when a size became readable or unreadable, or when
    /// both passes reported a modification time and those times differ. Two unknown sizes are not
    /// evidence of equality, but neither are they evidence of a change, so they are not reported.
    /// A timestamp present on one side only is also not treated as a change: providers report it
    /// intermittently and inventing instability would block a healthy listing.
    /// </summary>
    private static bool Differs(Stamp first, Stamp second)
    {
        if (first.SizeKnown != second.SizeKnown) return true;
        if (first.SizeKnown && first.Size != second.Size) return true;
        if (first.ModifiedUtc is { } a && second.ModifiedUtc is { } b && a != b) return true;
        return false;
    }

    private readonly record struct Stamp(long Size, bool SizeKnown, DateTimeOffset? ModifiedUtc);
}
