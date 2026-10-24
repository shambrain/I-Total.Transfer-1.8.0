namespace ITotalTransfer.Core;

/// <summary>Where the camera roll was found once a storage container has been inspected.</summary>
public enum CameraRollShape
{
    /// <summary>A container named <c>DCIM</c> is present: <c>Storage\DCIM\album\file</c>.</summary>
    DcimFolder,

    /// <summary>
    /// The DCIM albums — or the media itself — sit directly in the storage container, so the
    /// container *is* the camera roll: <c>Storage\album\file</c>.
    /// </summary>
    StorageContainer,

    /// <summary>Nothing that looks like a camera roll is exposed.</summary>
    Unknown
}

/// <summary>
/// Works out where an iPhone's camera roll lives inside the storage Windows exposes over MTP/WPD.
///
/// The documented shape is <c>Storage\DCIM\100APPLE\image</c>, and for a long time that was the only
/// shape. It is not the only shape in the wild: the Windows driver does not present the tree
/// identically across iOS versions and driver builds, and an iPhone 13 Pro on current iOS exposes the
/// DCIM album folders ("202608_a", "202003__", "201901_a") <em>directly</em> at the storage root with
/// no <c>DCIM</c> level at all.
///
/// Requiring a literal <c>DCIM</c> folder therefore declares a fully unlocked, trusted, perfectly
/// readable handset to be "connected but no DCIM container" — and never finds a single file. Both
/// shapes are accepted here, and both resolve to the same media root, which keeps the canonical
/// <c>DCIM</c> label (and the "preserve device folder structure (DCIM\…)" destination layout) accurate
/// in both cases: the albums really are the camera roll.
/// </summary>
public static class CameraRoll
{
    /// <summary>The label used for the media root in relative paths and destination folders.</summary>
    public const string RootLabel = "DCIM";

    /// <summary>True when the name is the DCIM container itself.</summary>
    public static bool IsDcimContainer(string? name) =>
        string.Equals((name ?? string.Empty).Trim(), RootLabel, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for the album folders iOS keeps inside DCIM: "100APPLE", "101APPLE", … on older handsets,
    /// and "201901_a" / "202003__" / "202608_a" on current ones.
    /// </summary>
    public static bool IsAlbumFolder(string? name)
    {
        string value = (name ?? string.Empty).Trim();
        if (value.Length < 6) return false;

        // 100APPLE / 101APPLE — three digits and the word APPLE.
        if (value.Length == 8 &&
            value.EndsWith("APPLE", StringComparison.OrdinalIgnoreCase) &&
            AllDigits(value, 3))
        {
            return true;
        }

        // YYYYMM, optionally followed by a two-character "slot": 201901_a, 202003__, 202608_b.
        if (!AllDigits(value, 6)) return false;
        string suffix = value[6..];
        if (suffix.Length > 2) return false;
        return suffix.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    /// <summary>
    /// True when the file looks like camera-roll content (a photo or a video). Sidecars such as
    /// ".AAE" deliberately do not count: they are metadata that rides along with an image, not
    /// evidence that a container is the camera roll.
    /// </summary>
    public static bool LooksLikeMedia(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        string extension;
        try { extension = Path.GetExtension(fileName); }
        catch (ArgumentException) { return false; }
        if (string.IsNullOrEmpty(extension)) return false;
        return MediaCategories.Of(extension) is MediaCategory.Photos or MediaCategory.Videos;
    }

    /// <summary>
    /// Decides the camera roll's location from the direct children of a storage container. A DCIM
    /// container wins wherever it appears in the listing; otherwise any album folder, or any media
    /// file, is taken as proof that the container itself is the camera roll.
    /// </summary>
    public static CameraRollShape Decide(IEnumerable<string?> childNames)
    {
        ArgumentNullException.ThrowIfNull(childNames);
        bool looksLikeCameraRoll = false;
        foreach (string? raw in childNames)
        {
            string name = (raw ?? string.Empty).Trim();
            if (name.Length == 0) continue;
            if (IsDcimContainer(name)) return CameraRollShape.DcimFolder;
            if (IsAlbumFolder(name) || LooksLikeMedia(name)) looksLikeCameraRoll = true;
        }
        return looksLikeCameraRoll ? CameraRollShape.StorageContainer : CameraRollShape.Unknown;
    }

    private static bool AllDigits(string value, int count)
    {
        if (value.Length < count) return false;
        for (int i = 0; i < count; i++)
        {
            if (!char.IsAsciiDigit(value[i])) return false;
        }
        return true;
    }
}
