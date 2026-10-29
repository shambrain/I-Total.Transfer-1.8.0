namespace ITotalTransfer.Core;

/// <summary>
/// Whether the inventory this scan exposed is comparable with the last complete listing. This is
/// deliberately independent of <see cref="EnumerationStatus"/>: a scan can successfully read every
/// folder it was shown (Complete) while the set of folders itself changed - for example a provider that
/// mounts only some albums after a reconnect. "Complete" never means "the whole library".
/// </summary>
public enum InventoryScopeStatus
{
    /// <summary>No usable previous listing exists, so no comparison can be made.</summary>
    Unknown,
    /// <summary>The exposed folders and file count match the previous complete listing.</summary>
    Stable,
    /// <summary>Folders that held files before are absent now, or the count dropped materially.</summary>
    Changed
}

/// <summary>One folder and what it held, used to explain a scope change without inventing a cause.</summary>
public sealed record FolderInventory(string Folder, int Files, long Bytes)
{
    /// <summary>Case-insensitive comparison key; the display name keeps the device's own separators.</summary>
    public string Key => (Folder ?? string.Empty).Replace('\\', '/');
}

/// <summary>
/// The inventory-scope fact for one scan. It names what was compared and what differs; it does not claim
/// why. A changed scope is a warning to rescan after the phone settles, not a failed enumeration.
/// </summary>
public sealed record InventoryScope(
    InventoryScopeStatus Status,
    int PreviousFiles,
    int CurrentFiles,
    IReadOnlyList<FolderInventory> MissingFolders,
    IReadOnlyList<FolderInventory> AddedFolders,
    string Detail)
{
    public bool IsChanged => Status == InventoryScopeStatus.Changed;

    public static InventoryScope Unknown(string reason) =>
        new(InventoryScopeStatus.Unknown, 0, 0, [], [], reason);
}

/// <summary>
/// Compares a fresh scan with the last complete cached listing at folder granularity. The rules are
/// deliberately conservative and pure, so the report can state a fact instead of guessing:
///
/// <list type="bullet">
/// <item>Any folder that held files before and is absent now makes the scope <see cref="InventoryScopeStatus.Changed"/>.</item>
/// <item>A file-count drop larger than max(100 files, 10 %) is also a change; normal photo additions are not.</item>
/// <item>No previous listing (or an empty one) is <see cref="InventoryScopeStatus.Unknown"/>, never "stable".</item>
/// </list>
/// </summary>
public static class ScanScope
{
    public const int AbsoluteFloor = 100;
    public const double RelativeTolerance = 0.10;

    public static InventoryScope Compare(CachedLibrary? previous, ScanResult current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null || previous.Files.Count == 0)
            return InventoryScope.Unknown(
                "No previous complete listing is available, so the exposed inventory cannot be compared.");

        Dictionary<string, FolderInventory> before = Fold(previous.Files.Select(f => (f.Folder, f.Size)));
        Dictionary<string, FolderInventory> now = Fold(current.Files.Select(f => (f.Folder, f.Size)));

        var missing = before.Values
            .Where(folder => !now.ContainsKey(folder.Key) && folder.Files > 0)
            .OrderByDescending(folder => folder.Files)
            .ToList();
        var added = now.Values
            .Where(folder => !before.ContainsKey(folder.Key) && folder.Files > 0)
            .OrderByDescending(folder => folder.Files)
            .ToList();

        int drop = previous.Files.Count - current.Files.Count;
        int threshold = Math.Max(AbsoluteFloor, (int)(previous.Files.Count * RelativeTolerance));
        bool countDroppedMaterially = drop > threshold;

        if (missing.Count == 0 && !countDroppedMaterially)
        {
            return new InventoryScope(InventoryScopeStatus.Stable,
                previous.Files.Count, current.Files.Count, [], added,
                $"The exposed inventory matches the previous complete listing: {current.Files.Count:N0} files in " +
                $"{now.Count:N0} folders (previous {previous.Files.Count:N0}).");
        }

        var parts = new List<string>
        {
            $"The previous complete listing (captured {previous.CapturedLocal}) exposed {previous.Files.Count:N0} files; " +
            $"this scan exposed {current.Files.Count:N0} in {now.Count:N0} folders."
        };
        if (missing.Count > 0)
        {
            string named = string.Join(", ", missing.Take(5).Select(f => $"{f.Folder} ({f.Files:N0} files)"));
            if (missing.Count > 5) named += $", and {missing.Count - 5} more";
            parts.Add("Folders that held files before are absent now: " + named + ".");
        }
        if (countDroppedMaterially)
            parts.Add($"The file count dropped by {drop:N0}, more than the {threshold:N0}-file comparison threshold.");
        parts.Add("The cause is not known from the listing alone. Rescan after the phone settles before treating " +
                  "this as the whole library; a cloud-only or not-yet-mounted album can look like this.");

        return new InventoryScope(InventoryScopeStatus.Changed,
            previous.Files.Count, current.Files.Count, missing, added, string.Join(" ", parts));
    }

    private static Dictionary<string, FolderInventory> Fold(IEnumerable<(string Folder, long Size)> files)
    {
        var map = new Dictionary<string, FolderInventory>(StringComparer.OrdinalIgnoreCase);
        foreach ((string folder, long size) in files)
        {
            string key = (folder ?? string.Empty).Replace('\\', '/');
            if (map.TryGetValue(key, out FolderInventory? existing))
                map[key] = existing with { Files = existing.Files + 1, Bytes = checked(existing.Bytes + Math.Max(0, size)) };
            else
                map[key] = new FolderInventory(folder ?? string.Empty, 1, Math.Max(0, size));
        }
        return map;
    }
}
/// <summary>
/// Whether a fresh scan may replace the cached "last complete listing". A scan whose inventory scope
/// changed materially is still a real enumeration, but using it as the next comparison baseline would
/// erase the larger listing and silence the warning on the following scan. The baseline is kept until a
/// scan of stable (or larger) scope replaces it.
/// </summary>
public static class ScanCachePolicy
{
    public static bool ShouldReplace(InventoryScope? scope) =>
        scope is null || scope.Status != InventoryScopeStatus.Changed;
}