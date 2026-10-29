using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

/// <summary>
/// Crash-safe, throttled persistence for <see cref="SessionManifest"/>. Writes are coalesced (time or
/// change-count based) so a 26,000-file library does not rewrite the whole JSON once per file, while
/// force-flushes still bracket every consequential state transition.
/// </summary>
public sealed class ManifestStore
{
    public const int CurrentSchemaVersion = 2;

    private readonly string _path;
    private readonly LogService _log;
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private DateTime _lastSaveUtc = DateTime.UtcNow;
    private int _dirty;
    private const int MaxDirtyBeforeSave = 250;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);

    public SessionManifest Manifest { get; private set; } = new();
    public string Path => _path;

    public ManifestStore(string destination, LogService log)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(destination, ".i-total"));
        _path = System.IO.Path.Combine(destination, ".i-total", "session.json");
        _log = log;
    }

    public SessionManifest LoadOrCreate(string destination, string deviceName, string? stableId, string backendName)
    {
        // A source-identity refusal is not a damaged manifest. It must escape the read-failure catch
        // below, because falling back to a fresh manifest would quietly accept the other phone.
        string? identityProblem = null;
        try
        {
            if (File.Exists(_path))
            {
                SessionManifest? loaded = JsonSerializer.Deserialize<SessionManifest>(File.ReadAllText(_path));
                if (loaded is not null && loaded.SchemaVersion is 1 or CurrentSchemaVersion &&
                    loaded.Entries is not null &&
                    string.Equals(SafeFullPath(loaded.Destination), SafeFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    identityProblem = SourceIdentityProblem(loaded, deviceName, stableId);
                    if (identityProblem is null)
                    {
                        bool duplicate = loaded.Entries.GroupBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
                            .Any(g => g.Count() > 1);
                        if (!duplicate)
                        {
                            loaded.SchemaVersion = CurrentSchemaVersion;
                            // A manifest written before the stable-identity field existed can be adopted
                            // once, but only after the name check above agreed. From then on the hardware
                            // identity is what decides whether this destination belongs to this phone.
                            if (string.IsNullOrWhiteSpace(loaded.DeviceStableId) && !string.IsNullOrWhiteSpace(stableId))
                            {
                                loaded.DeviceStableId = stableId.Trim();
                                _log.Write($"Recorded the hardware identity for the existing session ({loaded.DeviceName}).");
                            }
                            Manifest = loaded;
                            _log.Write($"Resuming existing manifest with {loaded.Entries.Count:N0} tracked entries.");
                            return loaded;
                        }
                        _log.Write("Existing manifest contains duplicate paths; a fresh manifest will be created. Destination files remain untouched.", "WARN");
                    }
                }
                else
                {
                    _log.Write("Existing manifest did not match the current schema/destination; a new manifest is created and the old file is preserved as session.json.bak.", "WARN");
                    try { File.Copy(_path, _path + ".bak", true); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Write($"Could not read previous manifest; starting a new manifest. {ex.Message}", "WARN");
            try { if (File.Exists(_path)) File.Copy(_path, _path + ".bak", true); } catch { }
        }

        if (identityProblem is not null)
            throw new InvalidOperationException(identityProblem);

        Manifest = new SessionManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            DeviceName = deviceName,
            DeviceStableId = stableId,
            BackendName = backendName,
            Destination = destination
        };
        return Manifest;
    }

    /// <summary>Adds new scan entries and refreshes expected sizes; completed entries are preserved.</summary>
    public void MergeScan(ScanResult scan, string? backendName = null)
    {
        var existing = Manifest.Entries.ToDictionary(e => e.RelativePath, StringComparer.OrdinalIgnoreCase);
        foreach (MediaFile file in scan.Files)
        {
            string path = TransferRules.ValidateRelativePath(file.Folder, file.Name);
            if (existing.TryGetValue(path, out TransferEntry? entry))
            {
                // A change from "size measured" to "size unreadable" (or back) is a source change too:
                // the plan would otherwise keep a verified entry whose size evidence no longer exists.
                bool sourceChanged = entry.ExpectedBytes != file.Size || entry.SizeKnown != file.SizeKnown ||
                    (entry.SourceModifiedUtc is { } recorded && file.ModifiedUtc is { } current && recorded != current);
                entry.ExpectedBytes = file.Size;
                entry.SizeKnown = file.SizeKnown;
                entry.SourceModifiedUtc = file.ModifiedUtc;
                if (sourceChanged)
                {
                    entry.SourceChanged = true;
                    if (entry.State is TransferState.SizeVerified or TransferState.IntegrityVerified
                        or TransferState.CopiedUnverified or TransferState.Skipped)
                    {
                        entry.State = TransferState.Queued;
                        entry.Outcome = OutcomeReason.Planned;
                    }
                }
                if (entry.State is TransferState.Copying or TransferState.Paused) entry.State = TransferState.Queued;
            }
            else
            {
                Manifest.Entries.Add(new TransferEntry
                {
                    RelativePath = path,
                    ExpectedBytes = file.Size,
                    SizeKnown = file.SizeKnown,
                    State = TransferState.Queued,
                    Outcome = OutcomeReason.Planned,
                    SourceModifiedUtc = file.ModifiedUtc
                });
            }
        }
        Manifest.DeviceName = scan.DeviceName;
        if (!string.IsNullOrWhiteSpace(scan.StableId)) Manifest.DeviceStableId = scan.StableId.Trim();
        Manifest.SourceEnumerationStatus = scan.Status.ToString();
        if (backendName is not null) Manifest.BackendName = backendName;
        Manifest.UpdatedUtc = DateTimeOffset.UtcNow;
        MarkDirty(force: true);
    }

    /// <summary>
    /// Refuses to adopt a destination's session record for a different phone. The manifest's entries
    /// carry verified hashes and "already present" decisions; reusing them for another handset with the
    /// same album and filenames would let one phone's copy stand in for another phone's source bytes.
    /// Identity is compared before the manifest is trusted, and nothing is written when it does not match.
    /// </summary>
    private static string? SourceIdentityProblem(SessionManifest existing, string deviceName, string? stableId)
    {
        string existingId = existing.DeviceStableId?.Trim() ?? "";
        string currentId = stableId?.Trim() ?? "";
        string existingName = string.IsNullOrWhiteSpace(existing.DeviceName) ? "a previous phone" : existing.DeviceName.Trim();
        string currentName = string.IsNullOrWhiteSpace(deviceName) ? "the current phone" : deviceName.Trim();
        bool sameName = string.Equals(existingName, currentName, StringComparison.OrdinalIgnoreCase);

        if (existingId.Length > 0 && currentId.Length > 0 &&
            !string.Equals(existingId, currentId, StringComparison.Ordinal))
        {
            return $"This destination belongs to a different phone ({existingName}); the current scan is {currentName}. " +
                   "Choose a new destination folder, or reconnect the original phone. No files were changed.";
        }

        if (existingId.Length > 0 && currentId.Length == 0)
        {
            return $"This destination belongs to {existingName}, but the current scan did not provide a hardware identity. " +
                   "Reconnect the original phone, or choose a new destination folder. No files were changed.";
        }

        if (existingId.Length == 0 && currentId.Length > 0 && !sameName)
        {
            return $"This destination was used by {existingName}, but the current scan is {currentName}. " +
                   "Choose a new destination folder, or reconnect the original phone. No files were changed.";
        }

        if (existingId.Length == 0 && currentId.Length == 0 && !sameName)
        {
            return $"This destination was used by {existingName}, but the current scan is {currentName}. " +
                   "The phones cannot be told apart by hardware identity. Choose a new destination folder. No files were changed.";
        }

        return null;
    }

    public TransferEntry? Find(string relativePath) =>
        Manifest.Entries.FirstOrDefault(e => string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

    public Dictionary<string, TransferEntry> Index() =>
        Manifest.Entries.GroupBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static string SafeFullPath(string path)
    {
        try { return System.IO.Path.GetFullPath(path); } catch { return path; }
    }

    /// <summary>Records that the manifest changed; writes are coalesced unless <paramref name="force"/> is set.</summary>
    public void MarkDirty(bool force = false)
    {
        lock (_sync)
        {
            _dirty++;
            if (!force && _dirty < MaxDirtyBeforeSave && DateTime.UtcNow - _lastSaveUtc < MinInterval) return;
            WriteAtomic();
        }
    }

    public void Flush()
    {
        lock (_sync) { WriteAtomic(); }
    }

    private void WriteAtomic()
    {
        try
        {
            Manifest.UpdatedUtc = DateTimeOffset.UtcNow;
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Manifest, _json), new UTF8Encoding(false));
            if (File.Exists(_path)) ReplaceWithRetry(temporary, _path);
            else File.Move(temporary, _path);
            _dirty = 0;
            _lastSaveUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _log.Write($"Manifest write failed: {ex.Message}", "WARN");
        }
    }

    /// <summary>
    /// Replaces the record in place, retrying briefly. The manifest *is* the recovery plan: a write that
    /// fails here is not fatal (the engine keeps working from the state it already has, and the failure is
    /// logged), but it silently ages the plan that the Overview reads and that a continuation rebuilds from.
    /// Replacing a file is not atomic against everything on Windows — a virus scanner, the search indexer or
    /// a backup tool can hold the destination for a moment and <see cref="File.Replace(string,string,string?,bool)"/>
    /// then fails with "Unable to remove the file to be replaced" (observed once against `settings.json`
    /// while running the suite) — so the same brief retry is applied here.
    /// </summary>
    private static void ReplaceWithRetry(string temporary, string target)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { File.Replace(temporary, target, null, true); return; }
            catch (IOException) when (attempt < 3) { Thread.Sleep(50 * attempt); }
        }
    }
}
