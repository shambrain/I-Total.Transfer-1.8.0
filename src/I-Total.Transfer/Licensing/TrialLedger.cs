using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace ITotalTransfer.Licensing;

/// <summary>
/// The persisted trial counter. It records how many meaningful jobs were used and the fingerprint of each
/// one, so reporting the same finished job twice (a retried report, a window closed and reopened) can never
/// consume a second use.
///
/// <para>
/// Nothing here identifies the operator or the phone: a fingerprint is a truncated SHA-256 of the job id the
/// engine generated for that run.
/// </para>
/// </summary>
public sealed class TrialLedgerState
{
    public int SchemaVersion { get; set; } = 1;
    public int JobsUsed { get; set; }
    public List<string> ConsumedJobs { get; set; } = [];
    public DateTimeOffset FirstSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastJobUtc { get; set; }

    /// <summary>Upper bound on stored fingerprints; a trial can never need more than its own limit.</summary>
    public const int MaxConsumedJobs = 1024;

    /// <summary>Fingerprint of one job, so a replayed report is recognised and costs nothing.</summary>
    public static string Fingerprint(string jobId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(jobId ?? ""));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    public bool HasConsumed(string fingerprint) =>
        ConsumedJobs.Contains(fingerprint, StringComparer.Ordinal);

    /// <summary>Adds one job, clamped so the list cannot grow without bound.</summary>
    public void Consume(string fingerprint, DateTimeOffset now)
    {
        if (!HasConsumed(fingerprint))
        {
            ConsumedJobs.Add(fingerprint);
            if (ConsumedJobs.Count > MaxConsumedJobs)
                ConsumedJobs.RemoveRange(0, ConsumedJobs.Count - MaxConsumedJobs);
        }
        JobsUsed++;
        LastJobUtc = now;
    }

    /// <summary>
    /// Combines two copies of the ledger by taking the most pessimistic view of each field: the higher use
    /// count, the union of consumed jobs, the earliest first run and the latest activity. Deleting one of
    /// the two stores therefore cannot reduce the counter while the other still exists.
    /// </summary>
    public void MergeFrom(TrialLedgerState other)
    {
        JobsUsed = Math.Max(JobsUsed, other.JobsUsed);
        foreach (string job in other.ConsumedJobs)
            if (!HasConsumed(job)) ConsumedJobs.Add(job);
        if (ConsumedJobs.Count > MaxConsumedJobs)
            ConsumedJobs = ConsumedJobs.Take(MaxConsumedJobs).ToList();
        if (other.FirstSeenUtc < FirstSeenUtc) FirstSeenUtc = other.FirstSeenUtc;
        if (other.LastSeenUtc > LastSeenUtc) LastSeenUtc = other.LastSeenUtc;
        if (other.LastJobUtc is { } otherJob && (LastJobUtc is null || otherJob > LastJobUtc))
            LastJobUtc = otherJob;
    }

    /// <summary>Returns a validated copy: negative or absurd values are repaired rather than thrown.</summary>
    public TrialLedgerState Normalized()
    {
        var copy = new TrialLedgerState
        {
            SchemaVersion = Math.Max(1, SchemaVersion),
            JobsUsed = Math.Clamp(JobsUsed, 0, MaxConsumedJobs),
            FirstSeenUtc = FirstSeenUtc,
            LastSeenUtc = LastSeenUtc,
            LastJobUtc = LastJobUtc
        };
        copy.ConsumedJobs = (ConsumedJobs ?? [])
            .Where(job => !string.IsNullOrWhiteSpace(job))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxConsumedJobs)
            .ToList();
        // A use count below the number of recorded jobs would let the display disagree with the ledger.
        copy.JobsUsed = Math.Max(copy.JobsUsed, copy.ConsumedJobs.Count);
        return copy;
    }
}

/// <summary>
/// One place the trial counter can live. Two implementations ship (a JSON file and the current user's
/// registry) and the application writes both, so removing one is not a reset.
/// </summary>
public interface ITrialStore
{
    string Description { get; }
    TrialLedgerState? Load();
    void Save(TrialLedgerState state);
}

/// <summary>Atomic JSON file under <c>%APPDATA%\I-Total Transfer\trial.json</c>.</summary>
public sealed class FileTrialStore : ITrialStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Path { get; }
    public string Description => "file " + Path;

    public FileTrialStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "I-Total Transfer", "trial.json");
    }

    public TrialLedgerState? Load()
    {
        try
        {
            if (!File.Exists(Path)) return null;
            TrialLedgerState? state = JsonSerializer.Deserialize<TrialLedgerState>(File.ReadAllText(Path), Json);
            return state?.Normalized();
        }
        catch (Exception)
        {
            // A corrupt counter is preserved for diagnosis, never silently treated as a fresh trial.
            TryQuarantine();
            return null;
        }
    }

    public void Save(TrialLedgerState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state.Normalized(), Json));
        if (!File.Exists(Path)) { File.Move(temporary, Path); return; }
        for (int attempt = 1; ; attempt++)
        {
            try { File.Replace(temporary, Path, null, true); return; }
            catch (IOException) when (attempt < 3) { Thread.Sleep(50 * attempt); }
        }
    }

    private void TryQuarantine()
    {
        try
        {
            if (File.Exists(Path))
                File.Move(Path, Path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
        }
        catch (Exception) { /* A locked file is left where it is. */ }
    }
}

/// <summary>
/// The current user's registry copy of the same counter. An uninstaller that removes the application
/// folder therefore cannot reset a trial by accident, and an update cannot lose it either.
/// </summary>
public sealed class RegistryTrialStore : ITrialStore
{
    public const string DefaultSubKey = @"Software\I-Total Transfer\Trial";

    public string SubKey { get; }
    public string Description => @"registry HKCU\" + SubKey;

    public RegistryTrialStore(string? subKey = null) => SubKey = subKey ?? DefaultSubKey;

    public TrialLedgerState? Load()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SubKey);
            if (key is null) return null;
            object? rawUsed = key.GetValue("JobsUsed");
            if (rawUsed is null) return null;

            var state = new TrialLedgerState
            {
                JobsUsed = Convert.ToInt32(rawUsed),
                ConsumedJobs = (key.GetValue("ConsumedJobs") as string ?? "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList(),
                FirstSeenUtc = ParseDate(key.GetValue("FirstSeenUtc")) ?? DateTimeOffset.UtcNow,
                LastSeenUtc = ParseDate(key.GetValue("LastSeenUtc")) ?? DateTimeOffset.UtcNow,
                LastJobUtc = ParseDate(key.GetValue("LastJobUtc"))
            };
            return state.Normalized();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(TrialLedgerState state)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(SubKey, writable: true);
            TrialLedgerState normalized = state.Normalized();
            key.SetValue("JobsUsed", normalized.JobsUsed, RegistryValueKind.DWord);
            key.SetValue("ConsumedJobs", string.Join('\n', normalized.ConsumedJobs), RegistryValueKind.String);
            key.SetValue("FirstSeenUtc", normalized.FirstSeenUtc.ToString("O"), RegistryValueKind.String);
            key.SetValue("LastSeenUtc", normalized.LastSeenUtc.ToString("O"), RegistryValueKind.String);
            if (normalized.LastJobUtc is { } job)
                key.SetValue("LastJobUtc", job.ToString("O"), RegistryValueKind.String);
        }
        catch (Exception)
        {
            // A locked or policy-restricted registry is not a reason to fail a job. The file copy remains.
        }
    }

    private static DateTimeOffset? ParseDate(object? value) =>
        value is string text && DateTimeOffset.TryParse(text, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset parsed)
            ? parsed
            : null;
}

/// <summary>
/// Writes the ledger to every store and reads it back as the most pessimistic merge. This is what makes
/// "delete one file to reset the trial" ineffective without shipping any secret.
/// </summary>
public sealed class CompositeTrialStore : ITrialStore
{
    private readonly IReadOnlyList<ITrialStore> _stores;

    public CompositeTrialStore(params ITrialStore[] stores) => _stores = stores;

    public string Description => "composite of " + string.Join(" and ", _stores.Select(s => s.Description));

    public TrialLedgerState? Load()
    {
        TrialLedgerState? merged = null;
        foreach (ITrialStore store in _stores)
        {
            TrialLedgerState? state = store.Load();
            if (state is null) continue;
            if (merged is null) merged = state;
            else merged.MergeFrom(state);
        }
        return merged?.Normalized();
    }

    public void Save(TrialLedgerState state)
    {
        foreach (ITrialStore store in _stores)
        {
            try { store.Save(state); }
            catch (Exception) { /* One unavailable store must not stop the other. */ }
        }
    }
}

/// <summary>In-memory store used by tests and as an explicit fallback.</summary>
public sealed class MemoryTrialStore : ITrialStore
{
    private TrialLedgerState? _state;
    public string Description => "memory";
    public int Saves { get; private set; }
    public bool FailOnSave { get; set; }

    public MemoryTrialStore(TrialLedgerState? initial = null) => _state = initial?.Normalized();

    public TrialLedgerState? Load() => _state?.Normalized();

    public void Save(TrialLedgerState state)
    {
        if (FailOnSave) throw new IOException("The trial ledger is read-only for this test.");
        _state = state.Normalized();
        Saves++;
    }
}
