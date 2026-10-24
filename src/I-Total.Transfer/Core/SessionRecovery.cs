using System.Text.Json;

namespace ITotalTransfer.Core;

/// <summary>
/// What the session manifest left behind in a destination folder says about the last run.
///
/// <para>
/// A transfer writes <c>.i-total\session.json</c> beside the copied files and flushes it at every
/// consequential boundary, so the folder itself is the durable record: it survives a crash, a
/// disconnect, a cancelled run and a reboot. This type turns that record into the numbers an
/// operator needs - how much of the job is done, what is still outstanding, and under what
/// circumstances the run can be continued - without trusting any of it: the copy loop revalidates
/// each outstanding file against the source and destination again.
/// </para>
///
/// <para>
/// Nothing here is inferred from a scan result or a cache. It reports only what the manifest on
/// disk states, and it says "unknown" when the record cannot answer.
/// </para>
/// </summary>
public sealed record RecoveryState
{
    /// <summary>True when a readable session manifest was found for the destination.</summary>
    public bool HasSession { get; init; }

    /// <summary>The destination folder the manifest belongs to.</summary>
    public string Destination { get; init; } = "";

    /// <summary>The device name recorded when the session was planned.</summary>
    public string DeviceName { get; init; } = "";

    /// <summary>The backend recorded when the session was planned, when the manifest carries one.</summary>
    public string? BackendName { get; init; }

    /// <summary>When the manifest was last written (the engine stamps this on every flush).</summary>
    public DateTimeOffset? UpdatedUtc { get; init; }

    /// <summary>The raw <see cref="SessionManifest.LastSessionStatus"/>, or <c>UNKNOWN</c> when none was recorded.</summary>
    public string Status { get; init; } = StatusUnknown;

    public int Total { get; init; }

    /// <summary>Entries whose bytes were compared against the source (full or size-only verification).</summary>
    public int Verified { get; init; }

    /// <summary>Entries skipped by policy or by the active filter; deliberately not copied.</summary>
    public int Skipped { get; init; }

    /// <summary>Entries that were copied but could not be verified (no trustworthy source size).</summary>
    public int CopiedUnverified { get; init; }

    /// <summary>Entries whose copy failed; the destination file, if any, was preserved.</summary>
    public int Failed { get; init; }

    /// <summary>Entries the active backend could not read at all.</summary>
    public int Inaccessible { get; init; }

    /// <summary>Entries the session planned but never reached a terminal state (interrupted mid-flight).</summary>
    public int Pending { get; init; }

    public long VerifiedBytes { get; init; }

    /// <summary>Bytes still to move: pending, failed, unreadable and unverified entries.</summary>
    public long OutstandingBytes { get; init; }

    /// <summary>Set when a manifest exists but could not be used, with the reason.</summary>
    public string? Problem { get; init; }

    public const string StatusUnknown = "UNKNOWN";

    /// <summary>Entries the destination is missing or whose last attempt did not succeed.</summary>
    public int Outstanding => Pending + Failed + Inaccessible + CopiedUnverified;

    /// <summary>A session that finished and reported its outcome. A finished session is not offered for resume.</summary>
    public bool Finished => Status.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase) &&
                            !Status.Contains("NOT A VERIFIED", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the last run did not finish and something is left to do. This is the only condition
    /// under which the app offers to continue: an interrupted run with outstanding entries.
    /// </summary>
    public bool CanResume => HasSession && !Finished && Outstanding > 0;

    /// <summary>A short, operator-facing label for how the last session ended.</summary>
    public string Headline => Status switch
    {
        "RUNNING" => "A transfer to this folder was interrupted",
        "CANCELLED" => "A transfer to this folder was cancelled",
        "ABORTED" => "A transfer to this folder was aborted",
        _ when Finished => "The last transfer to this folder finished",
        _ => "A transfer to this folder did not finish"
    };

    /// <summary>The circumstances under which the last session ended, in plain language.</summary>
    public string WhyStopped => Status switch
    {
        "RUNNING" => "the app stopped while files were still moving (no completion was recorded)",
        "CANCELLED" => "you cancelled it",
        "ABORTED" => "an error aborted it",
        "HASH-VERIFIED COMPLETE (source-to-destination)" => "it completed with source-to-destination hash verification",
        "DESTINATION-HASH-VERIFIED COMPLETE" => "it completed with destination-side hash verification",
        "SIZE-VERIFIED COMPLETE" => "it completed with size verification only",
        _ when Status.StartsWith("INCOMPLETE", StringComparison.OrdinalIgnoreCase) => "it finished a pass that could not be declared complete",
        _ when Status.StartsWith("COPIED WITH UNVERIFIABLE", StringComparison.OrdinalIgnoreCase) => "it finished a pass that left unverifiable files behind",
        _ => "the outcome was never recorded"
    };

    /// <summary>
    /// A short summary for a fixed-height bar: the three numbers that decide what to do next. It is
    /// deliberately compact rather than clever - a bar that slices its own sentence in half tells the
    /// operator less than one that never mentions the detail.
    /// </summary>
    public string Compact()
    {
        // A filtered run records every scanned file in the session: the ones the filter rejected are
        // Skipped, not planned. Counting them as planned puts "4,106 planned" next to "412 copied" and
        // makes the finished run look as if 3,500 files went missing. Name the skipped set instead.
        int planned = Math.Max(0, Total - Skipped);
        string text = $"{planned:N0} planned · {Verified:N0} copied and verified · {Outstanding:N0} outstanding";
        if (Skipped > 0) text += $" · {Skipped:N0} skipped by filter";
        if (Failed > 0 || Inaccessible > 0 || CopiedUnverified > 0)
            text += $" ({Failed:N0} failed, {Inaccessible:N0} unreadable, {CopiedUnverified:N0} unverified)";
        return text;
    }

    /// <summary>One sentence describing what the folder holds and what is outstanding.</summary>
    public string Describe()
    {
        if (!HasSession) return "No session record was found in this destination folder.";
        if (Problem is not null) return Problem;

        var parts = new List<string>();
        if (Verified > 0) parts.Add($"{Verified:N0} copied and verified");
        if (Skipped > 0) parts.Add($"{Skipped:N0} skipped by policy or filter");
        if (Pending > 0) parts.Add($"{Pending:N0} never attempted");
        if (CopiedUnverified > 0) parts.Add($"{CopiedUnverified:N0} copied but unverified");
        if (Failed > 0) parts.Add($"{Failed:N0} failed");
        if (Inaccessible > 0) parts.Add($"{Inaccessible:N0} unreadable");
        string body = parts.Count == 0 ? "it recorded no usable file states" : string.Join(", ", parts);

        string when = UpdatedUtc is DateTimeOffset updated
            ? $" Last updated {updated.ToLocalTime():yyyy-MM-dd HH:mm}."
            : "";
        return $"The session planned {Total:N0} file(s): {body}.{when}";
    }

    /// <summary>
    /// A stable identity for this particular unfinished session, used to remember that the operator
    /// dismissed it. Any change to the session (a later attempt, more entries, a different folder)
    /// produces a different value, so a dismissal never hides a new problem.
    /// </summary>
    public string Fingerprint =>
        $"{Destination}|{UpdatedUtc?.UtcTicks ?? 0}|{Total}|{Outstanding}";
}

/// <summary>
/// Reads the durable session record out of a destination folder and rebuilds the outstanding work
/// from it, so an interrupted transfer can be continued without paying for a fresh enumeration.
/// </summary>
public static class SessionRecovery
{
    /// <summary>The manifest path inside a destination folder.</summary>
    public static string ManifestPath(string destination) =>
        Path.Combine(destination, ".i-total", "session.json");

    /// <summary>
    /// Loads the manifest for a destination, or returns null with a reason. Never throws: a
    /// destination folder that does not exist, holds no manifest, or holds a damaged one is reported,
    /// not fatal.
    /// </summary>
    public static SessionManifest? Load(string destination, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(destination)) { problem = "No destination folder is selected."; return null; }

        string path;
        try { path = ManifestPath(Path.GetFullPath(destination)); }
        catch (Exception ex) { problem = $"The destination path could not be resolved: {ex.Message}"; return null; }

        if (!File.Exists(path)) return null;

        try
        {
            SessionManifest? manifest = JsonSerializer.Deserialize<SessionManifest>(File.ReadAllText(path));
            if (manifest is null) { problem = "The session record is empty and could not be read."; return null; }
            if (manifest.SchemaVersion is not (1 or 2))
            {
                problem = $"The session record uses schema {manifest.SchemaVersion}, which this build does not understand.";
                return null;
            }
            if (manifest.Entries is null || manifest.Entries.Count == 0)
            {
                problem = "The session record lists no files, so there is nothing to continue.";
                return null;
            }
            if (!string.Equals(SafeFullPath(manifest.Destination), SafeFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                problem = "The session record belongs to a different destination folder and was ignored.";
                return null;
            }
            return manifest;
        }
        catch (Exception ex)
        {
            problem = $"The session record could not be read ({ex.GetType().Name}); it was left untouched.";
            return null;
        }
    }

    /// <summary>
    /// Summarises the destination's session record. Never throws and never touches the device: this
    /// answers "is there unfinished work here?" even when no iPhone is attached.
    /// </summary>
    public static RecoveryState Inspect(string destination)
    {
        string full;
        try { full = Path.GetFullPath(destination); }
        catch { return new RecoveryState { Destination = destination }; }

        SessionManifest? manifest = Load(full, out string? problem);
        if (manifest is null)
        {
            bool exists = File.Exists(ManifestPath(full));
            return new RecoveryState { Destination = full, Problem = exists ? problem : null };
        }

        int verified = 0, skipped = 0, unverified = 0, failed = 0, inaccessible = 0, pending = 0;
        long verifiedBytes = 0, outstandingBytes = 0;

        foreach (TransferEntry entry in manifest.Entries)
        {
            long bytes = Math.Max(0, entry.ExpectedBytes);
            switch (entry.State)
            {
                case TransferState.SizeVerified:
                case TransferState.IntegrityVerified:
                    verified++; verifiedBytes += bytes; break;
                case TransferState.Skipped:
                    skipped++; break;
                case TransferState.CopiedUnverified:
                    unverified++; outstandingBytes += bytes; break;
                case TransferState.Failed:
                    failed++; outstandingBytes += bytes; break;
                case TransferState.Inaccessible:
                    inaccessible++; outstandingBytes += bytes; break;
                default: // Discovered / Queued / Copying / Paused: planned, never finished.
                    pending++; outstandingBytes += bytes; break;
            }
        }

        return new RecoveryState
        {
            HasSession = true,
            Destination = full,
            DeviceName = manifest.DeviceName ?? "",
            BackendName = manifest.BackendName,
            UpdatedUtc = manifest.UpdatedUtc,
            Status = string.IsNullOrWhiteSpace(manifest.LastSessionStatus) ? RecoveryState.StatusUnknown : manifest.LastSessionStatus!.Trim(),
            Total = manifest.Entries.Count,
            Verified = verified,
            Skipped = skipped,
            CopiedUnverified = unverified,
            Failed = failed,
            Inaccessible = inaccessible,
            Pending = pending,
            VerifiedBytes = verifiedBytes,
            OutstandingBytes = outstandingBytes
        };
    }

    /// <summary>
    /// Rebuilds a scan plan for the work the last session did not finish: the entries that are
    /// pending, failed, unreadable or unverified, and nothing else.
    ///
    /// <para>
    /// Entries already verified are deliberately left out rather than re-checked, because
    /// re-validating them means hashing every destination file again - 106 GB on a full camera roll.
    /// Their recorded state is not thrown away either: it stays in the same manifest, so the
    /// finished session's summary still counts them. An operator who wants those files proven again
    /// has an explicit verification action for exactly that.
    /// </para>
    ///
    /// <para>
    /// The plan carries no <see cref="MediaFile.SourceObjectId"/>: object identifiers do not survive
    /// a reconnect (and there may be no connect at all), so the backend resolves each file by path
    /// when the copy runs. Paths the engine would refuse are dropped here and counted, never passed
    /// on to look like work.
    /// </para>
    /// </summary>
    public static ScanResult BuildResumePlan(SessionManifest manifest, out int unsafeSkipped)
    {
        var files = new List<MediaFile>();
        unsafeSkipped = 0;

        foreach (TransferEntry entry in manifest.Entries)
        {
            if (entry.State is TransferState.SizeVerified or TransferState.IntegrityVerified or TransferState.Skipped)
                continue;

            if (!TrySplit(entry.RelativePath, out string folder, out string name)) { unsafeSkipped++; continue; }
            try { TransferRules.ValidateRelativePath(folder, name); }
            catch (InvalidDataException) { unsafeSkipped++; continue; }

            files.Add(new MediaFile(folder, name, Math.Max(0, entry.ExpectedBytes),
                Path.GetExtension(name).ToLowerInvariant())
            {
                ModifiedUtc = entry.SourceModifiedUtc
            });
        }

        var now = DateTimeOffset.UtcNow;
        EnumerationStatus sourceStatus = EnumerationStatus.Complete;
        if (!string.IsNullOrWhiteSpace(manifest.SourceEnumerationStatus) &&
            !Enum.TryParse(manifest.SourceEnumerationStatus, ignoreCase: true, out sourceStatus))
        {
            // An unknown value is evidence of damage, not evidence of a complete walk. Report it as
            // partial so a resumed run cannot turn an unreadable record into a complete backup.
            sourceStatus = EnumerationStatus.Incomplete;
        }
        var warnings = new List<string>
        {
            $"Recovered {files.Count:N0} outstanding file(s) from the session record in this folder."
        };
        EnumerationReport? report = null;
        if (sourceStatus != EnumerationStatus.Complete)
        {
            warnings.Add($"The original enumeration was {sourceStatus}; this resumed plan remains partial.");
            report = new EnumerationReport(sourceStatus, 0, 0, files.Count, files.Sum(f => Math.Max(0, f.Size)),
                0, 0, 0, 0, manifest.BackendName ?? "", [], warnings);
        }
        return new ScanResult(manifest.DeviceName ?? "", now, now, files, warnings)
        {
            // The plan is derived from the record, so it carries the record's identity. The engine
            // still needs the live device check below before it may act on these paths.
            StableId = string.IsNullOrWhiteSpace(manifest.DeviceStableId) ? null : manifest.DeviceStableId.Trim(),
            // A cancel must not erase the fact that the listing itself was partial. Without this, a
            // resumed run could copy every outstanding entry and call the result a complete backup.
            Report = report
        };
    }

    /// <summary>
    /// Checks a live probe against the phone identity stored in a destination's session record.
    ///
    /// <para>
    /// A plan rebuilt from the record has no live list of its own; the copy loop resolves each path
    /// through whichever backend is attached. If that is a different phone with the same album and
    /// filenames, an unchecked resume could read the wrong source bytes. This check is the gate: a
    /// different hardware identity, or a missing identity when the record has one, stops the resume
    /// before anything is fetched. A legacy record without an identity is accepted only when the device
    /// name agrees, and the caller may then record the live identity.
    /// </para>
    /// </summary>
    public static bool MatchesCurrentDevice(SessionManifest manifest, DeviceInfo current, out string? problem)
    {
        string recordedId = manifest.DeviceStableId?.Trim() ?? "";
        string currentId = current.StableId?.Trim() ?? "";
        string recordedName = string.IsNullOrWhiteSpace(manifest.DeviceName) ? "the original phone" : manifest.DeviceName.Trim();
        string currentName = string.IsNullOrWhiteSpace(current.Name) ? "the attached phone" : current.Name.Trim();
        bool sameName = string.Equals(recordedName, currentName, StringComparison.OrdinalIgnoreCase);

        if (recordedId.Length > 0 && currentId.Length > 0 &&
            !string.Equals(recordedId, currentId, StringComparison.Ordinal))
        {
            problem = $"This session was recorded from {recordedName}, but the attached phone is {currentName} " +
                      "with a different hardware identity. Reconnect the original phone, or choose a new " +
                      "destination folder. Nothing has been copied.";
            return false;
        }

        if (recordedId.Length > 0 && currentId.Length == 0)
        {
            problem = $"This session was recorded from {recordedName}, but the attached phone did not provide a " +
                      "hardware identity, so it cannot be proven to be the same phone. Reconnect the original " +
                      "phone, or choose a new destination folder. Nothing has been copied.";
            return false;
        }

        if (recordedId.Length == 0 && currentId.Length > 0 && !sameName)
        {
            problem = $"This session was recorded from {recordedName}, but the attached phone is {currentName}. " +
                      "Choose a new destination folder. Nothing has been copied.";
            return false;
        }

        if (recordedId.Length == 0 && currentId.Length == 0 && !sameName)
        {
            problem = $"This session was recorded from {recordedName}, but the attached phone is {currentName}. " +
                      "These phones cannot be told apart by hardware identity. Choose a new destination " +
                      "folder. Nothing has been copied.";
            return false;
        }

        problem = null;
        return true;
    }

    private static bool TrySplit(string relativePath, out string folder, out string name)
    {
        folder = ""; name = "";
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        string normalized = relativePath.Replace('/', '\\').TrimStart('\\');
        int slash = normalized.LastIndexOf('\\');
        if (slash <= 0 || slash == normalized.Length - 1) return false;
        folder = normalized[..slash];
        name = normalized[(slash + 1)..];
        return true;
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); } catch { return path; }
    }
}
