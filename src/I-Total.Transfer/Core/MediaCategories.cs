namespace ITotalTransfer.Core;

/// <summary>
/// The four media groups the app presents, mirroring the product banner
/// ("Photos · Videos · Music · Files"). Membership is decided by file extension so a single
/// enumerated file always lands in exactly one group.
/// </summary>
public enum MediaCategory
{
    Photos = 0,
    Videos = 1,
    Music = 2,
    Files = 3
}

public static class MediaCategories
{
    /// <summary>Banner order: Photos, Videos, Music, Files.</summary>
    public static readonly MediaCategory[] All =
        [MediaCategory.Photos, MediaCategory.Videos, MediaCategory.Music, MediaCategory.Files];

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".heif", ".png", ".dng", ".gif", ".tiff", ".tif",
        ".bmp", ".webp", ".raw", ".arw", ".cr2", ".nef"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".avi", ".hevc", ".mkv", ".3gp", ".mpg", ".mpeg", ".wmv"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".aiff", ".aif", ".wav", ".flac", ".alac", ".ogg",
        ".m4b", ".m4p", ".m4r", ".opus", ".wma"
    };

    /// <summary>Which group an extension belongs to. Anything unrecognised counts as a plain file.</summary>
    public static MediaCategory Of(string extension)
    {
        string ext = Normalize(extension);
        if (PhotoExtensions.Contains(ext)) return MediaCategory.Photos;
        if (VideoExtensions.Contains(ext)) return MediaCategory.Videos;
        if (AudioExtensions.Contains(ext)) return MediaCategory.Music;
        return MediaCategory.Files;
    }

    private static string Normalize(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return "";
        string ext = extension.Trim().ToLowerInvariant();
        return ext[0] == '.' ? ext : "." + ext;
    }

    /// <summary>The short product name shown on navigation, filters and tiles.</summary>
    public static string Title(this MediaCategory category) => category switch
    {
        MediaCategory.Photos => "Photos",
        MediaCategory.Videos => "Videos",
        MediaCategory.Music => "Music",
        _ => "Files"
    };

    /// <summary>The banner's own tagline for the group, reused verbatim as the tile subtitle.</summary>
    public static string Tagline(this MediaCategory category) => category switch
    {
        MediaCategory.Photos => "Keep your memories",
        MediaCategory.Videos => "Big moments, no limits",
        MediaCategory.Music => "Your sound, your way",
        _ => "Everything in one place"
    };

    /// <summary>
    /// A short explanation of where the group is read from.
    ///
    /// <para>
    /// The answer depends on the device, because the two families store media differently: an iPhone
    /// keeps everything the app can read inside its camera-roll container, while an Android phone keeps
    /// photos, video, audio and documents in their own folders across each storage volume. Saying
    /// "exposed through DCIM" about an Android music file would be wrong about where it came from.
    /// </para>
    /// </summary>
    public static string Source(this MediaCategory category, DeviceFamily family = DeviceFamily.Apple) =>
        family == DeviceFamily.Android ? AndroidSource(category) : AppleSource(category);

    private static string AppleSource(MediaCategory category) => category switch
    {
        MediaCategory.Photos => "Camera Roll and albums exposed through DCIM",
        MediaCategory.Videos => "Recorded and imported video exposed through DCIM",
        MediaCategory.Music => "On-device audio exposed through the iPhone media area",
        _ => "App documents and downloads exposed through the iPhone media area"
    };

    private static string AndroidSource(MediaCategory category) => category switch
    {
        MediaCategory.Photos => "Camera and gallery folders exposed by the phone (DCIM, Pictures)",
        MediaCategory.Videos => "Recorded and downloaded video exposed by the phone (DCIM, Movies)",
        MediaCategory.Music => "Audio files exposed by the phone (Music, Download)",
        _ => "Documents and downloads exposed by the phone"
    };
}
