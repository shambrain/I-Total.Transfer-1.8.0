namespace ITotalTransfer.Core;

/// <summary>One child of a device folder, as much as a backend needs to describe it.</summary>
public readonly record struct DeviceChild(string Id, string Name, bool IsFolder);

/// <summary>Why a relative path could not be located on the device.</summary>
public enum ObjectLookup
{
    Found,
    FileMissing,
    FolderMissing,
}

/// <summary>The outcome of locating one file inside the device's tree by its relative path.</summary>
public readonly record struct ObjectLocation(ObjectLookup Outcome, string Id, string MissingSegment)
{
    public static ObjectLocation Found(string id) => new(ObjectLookup.Found, id, string.Empty);

    public static ObjectLocation FileMissing() => new(ObjectLookup.FileMissing, string.Empty, string.Empty);

    public static ObjectLocation FolderMissing(string segment) => new(ObjectLookup.FolderMissing, string.Empty, segment);
}

/// <summary>
/// Locates device objects by relative path while reading each device folder at most once per session.
///
/// <para>
/// A portable-device folder is expensive to read: <c>EnumObjects</c> hands back opaque object IDs, and the
/// name of each one is a further property round-trip to the phone. Resolving a file by walking its folder
/// and reading every candidate's name therefore costs one enumeration plus one property read per sibling —
/// and doing that <em>per file</em> is quadratic in the size of the album. That is precisely what a resumed
/// transfer used to do, because object IDs are deliberately not persisted (they do not survive a reconnect),
/// so every file in a recovered run was located by path: for a 10,000-photo album it meant tens of millions
/// of property round-trips before the first byte moved.
/// </para>
///
/// <para>
/// This type reads a folder once, keeps the child by name, and answers every later lookup from memory. Its
/// matching is case-insensitive, like the filesystem and the device. It holds no state that outlives a
/// session: a reconnect can hand out entirely new object IDs, so the caller clears it whenever it drops the
/// session.
/// </para>
/// </summary>
public sealed class FolderNameIndex
{
    private readonly Func<string, IEnumerable<DeviceChild>> _children;
    private readonly Dictionary<string, Dictionary<string, DeviceChild>> _folders = new(StringComparer.Ordinal);
    private int _enumerations;

    /// <summary>
    /// Creates an index over one device session. <paramref name="children"/> is the backend's own folder
    /// read; it is called at most once per folder.
    /// </summary>
    public FolderNameIndex(Func<string, IEnumerable<DeviceChild>> children) =>
        _children = children ?? throw new ArgumentNullException(nameof(children));

    /// <summary>
    /// How many times a device folder has actually been read. This is the property that keeps the lookup
    /// linear: it must stay bounded by the number of folders, never by the number of files.
    /// </summary>
    public int FolderEnumerations => _enumerations;

    /// <summary>Forgets every folder read so far. The caller does this when the device session is dropped.</summary>
    public void Clear()
    {
        _folders.Clear();
        _enumerations = 0;
    }

    /// <summary>
    /// Finds <paramref name="fileName"/> inside <paramref name="folder"/> below the device's media root.
    /// The root label names the root itself and is skipped where it appears as a segment, which is what
    /// keeps a folder of the same name from being searched for underneath the root.
    /// </summary>
    /// <param name="rootLabel">
    /// The label the caller walks with. It defaults to the iPhone's canonical <c>DCIM</c>; a generic MTP
    /// volume passes its own name ("Internal shared storage", an SD card's label), because that is the
    /// segment its relative paths begin with and it is the root the caller is passing in.
    /// </param>
    public ObjectLocation Locate(string rootId, string folder, string fileName, string rootLabel = CameraRoll.RootLabel)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        string parentId = rootId;

        foreach (string segment in Segments(folder, rootLabel))
        {
            if (!Read(parentId).TryGetValue(segment, out DeviceChild child) || !child.IsFolder)
                return ObjectLocation.FolderMissing(segment);
            parentId = child.Id;
        }

        return Read(parentId).TryGetValue(fileName, out DeviceChild leaf) && !leaf.IsFolder
            ? ObjectLocation.Found(leaf.Id)
            : ObjectLocation.FileMissing();
    }

    private static IEnumerable<string> Segments(string folder, string rootLabel) =>
        (folder ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.Equals(segment, rootLabel, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The children of one folder, read once and kept. A nameless object is not put in the map: no path can
    /// name it, and keeping it would only hide a genuinely missing file behind an unusable id.
    /// </summary>
    private Dictionary<string, DeviceChild> Read(string parentId)
    {
        if (_folders.TryGetValue(parentId, out Dictionary<string, DeviceChild>? known)) return known;

        var map = new Dictionary<string, DeviceChild>(StringComparer.OrdinalIgnoreCase);
        _enumerations++;
        foreach (DeviceChild child in _children(parentId))
            if (!string.IsNullOrWhiteSpace(child.Name)) map[child.Name] = child;

        _folders[parentId] = map;
        return map;
    }
}
