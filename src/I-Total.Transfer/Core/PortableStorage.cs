namespace ITotalTransfer.Core;

/// <summary>
/// The storage rules for a generic MTP device — an Android phone or tablet, or anything else that
/// exposes MTP shared storage.
///
/// <para>
/// This is deliberately not <see cref="CameraRoll"/>, because the two shapes are not variations of one
/// another. An iPhone presents a single camera-roll container and nothing else worth reading. An
/// Android device presents one storage volume per container — "Internal shared storage", an SD card —
/// and the whole user-visible tree under each of those <em>is</em> the data: <c>DCIM\Camera</c>,
/// <c>Pictures</c>, <c>Movies</c>, <c>Music</c>, <c>Download</c>, <c>Documents</c>, and every folder
/// the operator or an app created. "Find the DCIM container and walk it" is correct for the iPhone and
/// silently wrong for Android.
/// </para>
///
/// <para>
/// One property of the transport is surfaced rather than hidden, because it changes what "complete"
/// may be taken to mean: Android answers MTP through its own <c>MtpDatabase</c>, which performs
/// filesystem operations through <c>MtpStorageManager</c> and supplies media metadata from its
/// <c>MediaProvider</c> (AOSP, <c>frameworks/base/media/java/android/mtp/MtpDatabase.java</c>). What
/// Windows sees is therefore Android's own MTP view of its storage, not a promise that everything the
/// phone itself can see is exposed. The app therefore reports what the device exposed, and never claims
/// to have read anything the device did not offer.
/// </para>
/// </summary>
public static class PortableStorage
{
    /// <summary>
    /// The banner groups a device of this family genuinely exposes.
    ///
    /// <para>
    /// An iPhone presents one camera-roll area, so Photos and Videos are everything this transport can
    /// honestly offer. An Android device presents whole storage volumes, where Photos, Videos, Music and
    /// Files are all really there — advertising only the camera groups, as the iPhone rule does, would
    /// present an Android phone as a device with no music and no documents. An unidentified device gets
    /// the conservative iPhone answer, so nothing is advertised that was not found.
    /// </para>
    /// </summary>
    public static IReadOnlyList<MediaCategory> CategoriesFor(DeviceFamily family) =>
        family == DeviceFamily.Android
            ? [MediaCategory.Photos, MediaCategory.Videos, MediaCategory.Music, MediaCategory.Files]
            : [MediaCategory.Photos, MediaCategory.Videos];

    /// <summary>The label used for a storage container whose own name cannot be used as a folder.</summary>
    public const string FallbackRootLabel = "Storage";

    /// <summary>
    /// The folder that holds per-app private trees. Its <c>data</c> and <c>obb</c> children are not
    /// readable over MTP at all — Android's scoped storage denies them to a file-transfer client — so
    /// descending into them buys an error per app, a great deal of time, and not one file. Its
    /// <c>media</c> child is readable and is where apps such as WhatsApp keep photos and voice notes,
    /// so the folder itself must still be walked.
    /// </summary>
    public const string AppDataFolder = "Android";

    /// <summary>The unreadable children of <see cref="AppDataFolder"/>.</summary>
    private static readonly HashSet<string> UnreadableAppTrees = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "obb"
    };

    /// <summary>
    /// Folder names that hold derived data rather than the operator's files. Android keeps
    /// thumbnail caches inside the camera folders; they are small copies of images already being
    /// copied, and enumerating thousands of them costs a device round trip each.
    /// </summary>
    private static readonly HashSet<string> DerivedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".thumbnails", ".thumbnail"
    };

    /// <summary>
    /// Folders a phone exposes when it is in file-transfer mode and hides in photo-transfer mode.
    /// Their total absence, alongside the presence of camera folders, is the signature of the wrong
    /// USB mode rather than of an empty phone.
    /// </summary>
    private static readonly string[] FileTransferOnlyFolders =
        ["Download", "Music", "Movies", "Documents", "Android", "Podcasts", "Ringtones", "Alarms"];

    /// <summary>The camera folders, whose presence alone is not evidence of anything wrong.</summary>
    private static readonly string[] CameraFolders = ["DCIM", "Pictures"];

    /// <summary>
    /// True when a folder inside a storage volume must not be walked.
    /// </summary>
    /// <param name="relativeFolder">
    /// The folder's path from the volume root, whose first segment is the volume's own label —
    /// e.g. <c>Internal shared storage\Android\data</c>.
    /// </param>
    /// <param name="folderName">The folder's own name, used for the derived-data checks.</param>
    public static bool ShouldSkipFolder(string? relativeFolder, string? folderName)
    {
        string name = (folderName ?? string.Empty).Trim();
        if (name.Length > 0 && DerivedFolders.Contains(name)) return true;

        // The unreadable app trees sit directly under the "Android" folder, so only that exact
        // position is refused: a user folder called "Android\data" is Android's, not theirs, but a
        // user folder called "Holiday\data" is just a folder and is walked normally.
        string[] segments = Segments(relativeFolder);
        for (int i = 0; i + 1 < segments.Length; i++)
        {
            if (!string.Equals(segments[i], AppDataFolder, StringComparison.OrdinalIgnoreCase)) continue;
            if (UnreadableAppTrees.Contains(segments[i + 1])) return true;
        }
        return false;
    }

    /// <summary>
    /// A storage volume's own name, made safe to use as a destination folder. The name is kept as the
    /// device reports it — "Internal shared storage", "SD card" — because that is what lets an
    /// internal volume and a card be told apart in the destination, rather than being merged into one
    /// pile by a label the app invented.
    /// </summary>
    public static string RootLabel(string? containerName)
    {
        string raw = (containerName ?? string.Empty).Trim();
        if (raw.Length == 0) return FallbackRootLabel;

        var cleaned = new System.Text.StringBuilder(raw.Length);
        bool pendingSpace = false;
        foreach (char c in raw)
        {
            if (c is '\\' or '/' || char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c))
            {
                pendingSpace = cleaned.Length > 0;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = cleaned.Length > 0;
                continue;
            }
            if (pendingSpace) { cleaned.Append(' '); pendingSpace = false; }
            cleaned.Append(c);
        }

        // Windows will not accept a folder name that ends in a dot or a space.
        string result = cleaned.ToString().TrimEnd('.', ' ');
        return result.Length == 0 ? FallbackRootLabel : result;
    }

    /// <summary>
    /// True when a volume's own listing looks like a phone in "Transfer photos" (PTP) mode: camera
    /// folders are present and not one folder that only file-transfer mode exposes is.
    ///
    /// <para>
    /// This is a warning, not a diagnosis, and the wording says so. A phone in PTP mode really does
    /// expose only DCIM and Pictures, and a backup taken in that state is genuinely incomplete while
    /// looking perfectly healthy — but a brand-new phone can also legitimately hold nothing else yet,
    /// so the app must raise the possibility without asserting it.
    /// </para>
    /// </summary>
    public static bool LooksLikePhotoTransferOnly(IEnumerable<string?> topLevelNames)
    {
        ArgumentNullException.ThrowIfNull(topLevelNames);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? raw in topLevelNames)
        {
            string name = (raw ?? string.Empty).Trim();
            if (name.Length > 0) seen.Add(name);
        }

        if (seen.Count == 0) return false;
        if (!CameraFolders.Any(seen.Contains)) return false;
        return !FileTransferOnlyFolders.Any(seen.Contains);
    }

    /// <summary>The message shown when <see cref="LooksLikePhotoTransferOnly"/> is true.</summary>
    public const string PhotoTransferOnlyWarning =
        "The device exposed only its camera folders (DCIM, Pictures). If its own Files app shows more than that, " +
        "the phone is in \"Transfer photos\" (PTP) mode and everything else is hidden from this PC; switch the USB " +
        "notification to \"File transfer\" and scan again.";

    /// <summary>The path segments of a relative folder, ignoring separators and empty parts.</summary>
    private static string[] Segments(string? relativeFolder) =>
        (relativeFolder ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
}
