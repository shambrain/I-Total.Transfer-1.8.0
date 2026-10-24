using System.Security.Cryptography;
using System.Text;

namespace ITotalTransfer.Core;

/// <summary>
/// Identity of the library a cached listing describes: the backend that produced it plus the handset it
/// came from. A cached listing is only ever reused for the same pair, so one iPhone's library can never be
/// shown as another's.
///
/// <para>
/// The device key prefers the platform's stable identity (a WPD/UDID-style value), then the serial number,
/// and only then the model name - which is deliberately the weakest option, because two different iPhones
/// of the same model must not share a listing unless nothing better exists.
/// </para>
/// </summary>
public sealed record ScanCacheKey(string BackendName, string DeviceKey)
{
    public static ScanCacheKey From(string? backendName, string? stableId, string? serialNumber, string? model = null)
    {
        string backend = Clean(backendName) ?? "unknown-backend";
        string device = Clean(stableId) ?? Clean(serialNumber) ?? Clean(model) ?? "unknown-device";
        return new ScanCacheKey(backend, device);
    }

    /// <summary>
    /// File name for this key. The device string is hashed rather than written verbatim, so a serial number
    /// never ends up in a file name handed around by backup tools, archivers or a screenshot of the folder.
    /// The readable part is only the backend, and the file describes itself from the inside.
    /// </summary>
    public string FileName
    {
        get
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(BackendName + "\u001f" + DeviceKey));
            return $"library-{Token(BackendName)}-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}.json";
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Keeps a short, lowercase, file-system-safe token for readability in the file name.</summary>
    private static string Token(string value)
    {
        var builder = new StringBuilder(Math.Min(24, value.Length));
        foreach (char c in value)
        {
            if (builder.Length == 24) break;
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        string token = builder.ToString().Trim('-');
        return token.Length == 0 ? "cache" : token;
    }
}

/// <summary>
/// One file in a cached listing. <see cref="MediaFile.SourceObjectId"/> is deliberately <em>not</em> stored:
/// the backend documents it as an opaque id that is not durable across reconnects, so resurrecting it from
/// disk could point at a different object. The engine resolves by path instead, exactly as it does when the
/// id is absent.
/// </summary>
public sealed record CachedFile(string Folder, string Name, long Size, string Extension, DateTimeOffset? ModifiedUtc)
{
    public static CachedFile From(MediaFile file) =>
        new(file.Folder, file.Name, file.Size, file.Extension, file.ModifiedUtc);

    public MediaFile ToMediaFile() => new(Folder, Name, Size, Extension) { ModifiedUtc = ModifiedUtc };
}

/// <summary>
/// A listing persisted from a completed scan, so the window is not empty for the many minutes a cold camera
/// roll takes to enumerate.
///
/// <para>
/// A cached listing is a history record, never a measurement: it carries the moment it was captured, and
/// every consumer is expected to say so. It is only written from a scan whose enumeration status was
/// <see cref="EnumerationStatus.Complete"/>, so a partial listing can never overwrite a good one.
/// </para>
/// </summary>
public sealed class CachedLibrary
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Product { get; set; } = "I-Total Transfer";
    public string? ProductVersion { get; set; }
    public string BackendName { get; set; } = "";
    public string DeviceKey { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTimeOffset CapturedUtc { get; set; }
    public string EnumerationStatus { get; set; } = nameof(ITotalTransfer.Core.EnumerationStatus.Complete);
    public long TotalBytes { get; set; }
    public int ZeroByteCount { get; set; }
    public int UnknownSizeCount { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<CachedFile> Files { get; set; } = [];

    public EnumerationStatus Status =>
        Enum.TryParse(EnumerationStatus, ignoreCase: true, out EnumerationStatus parsed)
            ? parsed
            : ITotalTransfer.Core.EnumerationStatus.Failed;

    public TimeSpan Age(DateTimeOffset now) => now - CapturedUtc;

    public bool HasFiles => Files.Count > 0;

    public string CapturedLocal => CapturedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>
    /// Whether this listing may be presented as "the last known library" right now. A listing from a
    /// different schema, from an incomplete enumeration, that enumerated nothing, or that is older than
    /// <paramref name="maximumAge"/> is refused rather than shown with a caveat nobody reads.
    /// </summary>
    public bool IsUsableAt(DateTimeOffset now, TimeSpan maximumAge) =>
        SchemaVersion == CurrentSchemaVersion &&
        HasFiles &&
        Status == ITotalTransfer.Core.EnumerationStatus.Complete &&
        Age(now) <= maximumAge;

    public IReadOnlyList<MediaFile> ToMediaFiles()
    {
        var files = new List<MediaFile>(Files.Count);
        foreach (CachedFile file in Files) files.Add(file.ToMediaFile());
        return files;
    }

    public static CachedLibrary FromScan(ScanCacheKey key, ScanResult scan, string? productVersion)
    {
        var cached = new CachedLibrary
        {
            ProductVersion = productVersion,
            BackendName = key.BackendName,
            DeviceKey = key.DeviceKey,
            DeviceName = scan.DeviceName,
            CapturedUtc = scan.FinishedUtc == default ? DateTimeOffset.UtcNow : scan.FinishedUtc,
            EnumerationStatus = scan.Status.ToString(),
            TotalBytes = scan.TotalBytes,
            ZeroByteCount = scan.Report?.ZeroByteCount ?? 0,
            UnknownSizeCount = scan.Report?.UnknownSizeCount ?? 0,
            Warnings = [.. scan.Warnings]
        };
        foreach (MediaFile file in scan.Files) cached.Files.Add(CachedFile.From(file));
        return cached;
    }
}

/// <summary>
/// What changed between a cached listing and the live one. Used to tell the operator what the fresh scan
/// actually saw, instead of silently swapping one number for another.
/// </summary>
public sealed record ScanCacheDiff(int Added, int Removed, int SizeChanged, int Unchanged, IReadOnlyList<string> Examples)
{
    public bool IsIdentical => Added == 0 && Removed == 0 && SizeChanged == 0;

    public int ChangedCount => Added + Removed + SizeChanged;

    public static ScanCacheDiff Between(CachedLibrary? cached, ScanResult fresh)
    {
        var previous = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (cached is not null)
            foreach (CachedFile file in cached.Files)
                previous[Path.Combine(file.Folder, file.Name)] = file.Size;

        var current = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (MediaFile file in fresh.Files)
            current[file.RelativePath] = file.Size;

        int added = 0, removed = 0, sizeChanged = 0, unchanged = 0;
        var examples = new List<string>();

        foreach ((string path, long size) in current)
        {
            if (!previous.TryGetValue(path, out long before)) { added++; Example(examples, "+ " + path); }
            else if (before != size) { sizeChanged++; Example(examples, "~ " + path + $" ({MediaSummary.FormatBytes(before)} -> {MediaSummary.FormatBytes(size)})"); }
            else unchanged++;
        }
        foreach (string path in previous.Keys)
        {
            if (current.ContainsKey(path)) continue;
            removed++;
            Example(examples, "- " + path);
        }

        return new ScanCacheDiff(added, removed, sizeChanged, unchanged, examples);

        static void Example(List<string> list, string entry)
        {
            if (list.Count < 5) list.Add(entry);
        }
    }

    /// <summary>A one-line, plain-language account of the change, or that there was none.</summary>
    public string Describe()
    {
        if (IsIdentical) return "the live scan matched the cached listing exactly";
        var parts = new List<string>(3);
        if (Added > 0) parts.Add($"{Added:N0} added");
        if (Removed > 0) parts.Add($"{Removed:N0} removed");
        if (SizeChanged > 0) parts.Add($"{SizeChanged:N0} changed size");
        string detail = Examples.Count == 0 ? "" : " (" + string.Join("; ", Examples) + ")";
        return $"{ChangedCount:N0} change(s) since the cached listing: {string.Join(", ", parts)}{detail}";
    }
}
