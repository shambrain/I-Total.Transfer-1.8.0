using System.Diagnostics;
using System.Text.Json;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

/// <summary>
/// Persists the last complete listing of a device so a cold scan is not a blank window. One file per
/// (backend, device) pair under the user's application data - deliberately <em>not</em> in the backup
/// destination, because the listing describes the phone, not the folder, and switching destinations must
/// not lose it.
///
/// <para>
/// Rules the store enforces, all of them about not lying to the operator:
/// </para>
/// <list type="bullet">
/// <item>Only a scan whose enumeration was <see cref="EnumerationStatus.Complete"/> is written, so a partial
/// listing can never replace a good one.</item>
/// <item>An empty enumeration is not written either: an empty listing is a legitimate answer about a phone,
/// but it is not a library worth remembering in place of one.</item>
/// <item>A cached listing is refused when it is older than the caller's limit, when it was written by a
/// different schema, or when it describes a different backend or device.</item>
/// <item>A corrupt file is moved aside rather than deleted, and never throws: caching is an optimisation and
/// must never be a reason a scan cannot run.</item>
/// </list>
/// </summary>
public sealed class ScanCacheStore
{
    /// <summary>
    /// A listing larger than this is not written. A 26,400-item camera roll is a few megabytes; the bound is
    /// here so a pathological device cannot fill the user's profile through a feature nobody is watching.
    /// </summary>
    public const long MaxBytes = 64L * 1024 * 1024;

    public string DirectoryPath { get; }

    /// <summary>
    /// Why the most recent <see cref="TryLoad"/> refused to use a listing, phrased for the log. Null when a
    /// listing was returned or when there simply was none.
    /// </summary>
    public string? LastNote { get; private set; }

    public ScanCacheStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "I-Total Transfer", "scan-cache");
    }

    public string PathFor(ScanCacheKey key) => Path.Combine(DirectoryPath, key.FileName);

    public bool Exists(ScanCacheKey key) => File.Exists(PathFor(key));

    /// <summary>
    /// Loads the cached listing for this key, or null when there is none that may honestly be used.
    /// </summary>
    public CachedLibrary? TryLoad(ScanCacheKey key, DateTimeOffset now, TimeSpan maximumAge)
    {
        LastNote = null;
        string path = PathFor(key);
        if (!File.Exists(path)) return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                LastNote = $"the cached listing is {info.Length / 1048576.0:N0} MB, beyond the {MaxBytes / 1048576} MB limit";
                return null;
            }

            CachedLibrary? cached = JsonSerializer.Deserialize<CachedLibrary>(File.ReadAllText(path), AppSettings.JsonOpts);
            if (cached is null)
            {
                LastNote = "the cached listing was empty";
                return null;
            }
            if (cached.SchemaVersion != CachedLibrary.CurrentSchemaVersion)
            {
                LastNote = $"the cached listing was written by schema {cached.SchemaVersion}, not {CachedLibrary.CurrentSchemaVersion}";
                return null;
            }
            if (!string.Equals(cached.BackendName, key.BackendName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(cached.DeviceKey, key.DeviceKey, StringComparison.OrdinalIgnoreCase))
            {
                LastNote = "the cached listing belongs to a different device or backend";
                return null;
            }
            if (!cached.IsUsableAt(now, maximumAge))
            {
                LastNote = cached.Status != EnumerationStatus.Complete
                    ? $"the cached listing came from a {cached.Status} enumeration"
                    : $"the cached listing is {cached.Age(now).TotalDays:N1} day(s) old";
                return null;
            }
            return cached;
        }
        catch (Exception ex)
        {
            LastNote = "the cached listing could not be read (" + ex.Message + ")";
            Quarantine(path);
            return null;
        }
    }

    /// <summary>
    /// Writes the listing for a completed scan. Returns false with a reason for the log when the scan was not
    /// fit to remember; that is a normal outcome, not an error.
    /// </summary>
    public bool Save(ScanCacheKey key, ScanResult scan, string? productVersion, out string? note)
    {
        note = null;
        if (scan.Status != EnumerationStatus.Complete)
        {
            note = $"the scan status was {scan.Status}, so the previous listing was kept rather than replaced by a partial one";
            return false;
        }
        if (scan.Files.Count == 0)
        {
            note = "the scan enumerated no files, so the previous listing was kept";
            return false;
        }

        try
        {
            CachedLibrary cached = CachedLibrary.FromScan(key, scan, productVersion);
            string json = JsonSerializer.Serialize(cached, AppSettings.JsonOpts);
            if (json.Length > MaxBytes)
            {
                note = $"the listing would be {json.Length / 1048576.0:N0} MB, beyond the {MaxBytes / 1048576} MB limit";
                return false;
            }

            Directory.CreateDirectory(DirectoryPath);
            string path = PathFor(key);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            // Replace keeps the previous listing readable at all times: a crash mid-write leaves either the
            // old file or the new one, never half of either.
            if (File.Exists(path)) File.Replace(temporary, path, null, true);
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception ex)
        {
            note = "the listing could not be written (" + ex.Message + ")";
            return false;
        }
    }

    /// <summary>Removes the listing for one device. Returns true when something was removed.</summary>
    public bool Forget(ScanCacheKey key)
    {
        try
        {
            string path = PathFor(key);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scan cache delete failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Removes every cached listing. Returns how many were removed.</summary>
    public int ForgetAll()
    {
        int removed = 0;
        foreach (string path in EnumerateFiles())
        {
            try { File.Delete(path); removed++; }
            catch (Exception ex) { Debug.WriteLine($"Scan cache delete failed for {path}: {ex.Message}"); }
        }
        return removed;
    }

    /// <summary>Everything on disk, for the Diagnostics page and its "what is being kept?" answer.</summary>
    public IReadOnlyList<CachedListing> List()
    {
        var listings = new List<CachedListing>();
        foreach (string path in EnumerateFiles())
        {
            try
            {
                var info = new FileInfo(path);
                CachedLibrary? cached = JsonSerializer.Deserialize<CachedLibrary>(File.ReadAllText(path), AppSettings.JsonOpts);
                listings.Add(new CachedListing(path, info.Length, cached));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scan cache listing failed for {path}: {ex.Message}");
                listings.Add(new CachedListing(path, new FileInfo(path).Length, null));
            }
        }
        return listings;
    }

    private IEnumerable<string> EnumerateFiles()
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        return Directory.EnumerateFiles(DirectoryPath, "library-*.json");
    }

    /// <summary>Moves an unreadable listing aside. Never deletes: the file is evidence about a defect.</summary>
    private void Quarantine(string path)
    {
        try
        {
            File.Move(path, path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scan cache quarantine failed: {ex.Message}");
        }
    }
}

/// <summary>One file on disk as reported to Diagnostics, including an unreadable one.</summary>
public sealed record CachedListing(string Path, long Bytes, CachedLibrary? Library)
{
    public bool IsReadable => Library is not null;
}
