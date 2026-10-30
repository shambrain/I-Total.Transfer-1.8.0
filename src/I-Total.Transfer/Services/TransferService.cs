using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Devices;

namespace ITotalTransfer.Services;

/// <summary>
/// Backend-agnostic transfer engine. It streams from the device, stages payloads, commits atomically,
/// verifies with the strongest hash the backend supports, and persists a crash-safe manifest so an
/// interrupted run can be resumed without silently skipping incomplete files.
/// </summary>
public sealed class TransferService
{
    private readonly ITransferBackend _device;
    private readonly LogService _log;
    private readonly TransferOptions _options;
    private readonly FilterSpec _filter;
    private ManifestStore? _store;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public PauseController Pause { get; } = new();

    public TransferService(ITransferBackend device, LogService log) : this(device, log, TransferOptions.FromSettings(AppSettings.Defaults()), FilterSpec.None) { }

    public TransferService(ITransferBackend device, LogService log, TransferOptions options, FilterSpec? filter = null)
    {
        _device = device;
        _log = log;
        _options = options;
        _filter = filter ?? FilterSpec.None;
    }

    public BackendCapabilities Capabilities => _device.Capabilities;

    // ------------------------------------------------------------------ Scan
    public Task<ScanResult> ScanAsync(CancellationToken token, IProgress<string>? progress = null) =>
        ContextFree.Run(() => ScanCoreAsync(token, progress), token);

    private async Task<ScanResult> ScanCoreAsync(CancellationToken token, IProgress<string>? progress)
    {
        _log.Write($"Starting read-only device scan with backend '{_device.Name}'.");
        ScanResult result = await Task.Run(() => _device.Scan(token, m => progress?.Report(m)), token);
        foreach (string warning in result.Warnings) _log.Write(warning, "WARN");
        _log.Write($"Scan finished: {result.Files.Count:N0} files, {FormatBytes(result.TotalBytes)}, " +
                   $"status={result.Status}, duration={result.Duration.TotalMinutes:N1} min, backend={_device.Name}.");
        return result;
    }

    public static void SaveScanReport(ScanResult scan, string destination, string backendName)
    {
        Directory.CreateDirectory(destination);
        string path = Path.Combine(destination, "I-total-enumeration-report.json");
        LogService.ExportJson(path, new
        {
            Product = "I-Total Transfer by Deveg",
            scan.DeviceName,
            Backend = backendName,
            scan.StartedUtc,
            scan.FinishedUtc,
            DurationSeconds = scan.Duration.TotalSeconds,
            Status = scan.Status.ToString(),
            FileCount = scan.Files.Count,
            TotalBytes = scan.TotalBytes,
            InventoryScope = scan.Scope,
            Report = scan.Report
        });
    }

    // ------------------------------------------------------------------ Copy (device -> Windows)
    public Task<TransferSummary> CopyAsync(ScanResult scan, string destination, IProgress<TransferProgress> progress, CancellationToken token)
        => CopyAsync(scan, destination, _options.MaxRetries, progress, token);

    /// <summary>
    /// The count and bytes the active filter will actually plan. The scan itself is always the whole
    /// device listing; this is the same inclusion rule the engine applies before it writes anything, so
    /// the operator's confirmation cannot promise 26,000 files when the filter selects eight.
    /// </summary>
    public (int Count, long Bytes) PlanSummary(ScanResult scan)
    {
        IReadOnlyList<MediaFile> planned = TransferRules.ApplyFilter(scan.Files, _filter);
        return (planned.Count, planned.Sum(file => Math.Max(0, file.Size)));
    }

    public Task<TransferSummary> CopyAsync(ScanResult scan, string destination, int maxRetries,
        IProgress<TransferProgress> progress, CancellationToken token) =>
        ContextFree.Run(() => CopyCoreAsync(scan, destination, maxRetries, progress, token), token);

    private async Task<TransferSummary> CopyCoreAsync(ScanResult scan, string destination, int maxRetries,
        IProgress<TransferProgress> progress, CancellationToken token)
    {
        bool toWindows = _options.Direction == TransferDirection.DeviceToWindows;
        if (!toWindows) throw new InvalidOperationException("Use CopyWindowsToDeviceAsync for the Windows-to-device direction.");
        if (scan.Files.Count == 0) throw new InvalidOperationException("Cannot start a transfer with an empty scan.");
        if (scan.Status is EnumerationStatus.Failed or EnumerationStatus.Empty)
            throw new InvalidOperationException("The scan is not complete; refusing to start a partial backup.");
        EnsureUniqueSourcePaths(scan);
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination is required.", nameof(destination));
        destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(destination);

        _store = new ManifestStore(destination, _log);
        SessionManifest manifest = _store.LoadOrCreate(destination, scan.DeviceName, scan.StableId, _device.Name);
        manifest.BackendName = _device.Name;
        _store.MergeScan(scan);
        Dictionary<string, TransferEntry> entries = _store.Index();

        // Apply the include/exclude filter. Excluded files are recorded as skipped, never silently dropped.
        var planned = new List<MediaFile>();
        int filteredOut = 0;
        foreach (MediaFile file in scan.Files)
        {
            if (TransferRules.IsIncluded(file, _filter)) planned.Add(file);
            else
            {
                filteredOut++;
                if (entries.TryGetValue(TransferRules.ValidateRelativePath(file.Folder, file.Name), out TransferEntry? ex))
                {
                    ex.State = TransferState.Skipped;
                    ex.Outcome = OutcomeReason.FilterExcluded;
                    ex.Error = "Excluded by filter.";
                }
            }
        }
        if (filteredOut > 0)
            _log.Write($"{filteredOut:N0} file(s) excluded by the active filter and marked skipped.", "WARN");
        if (planned.Count == 0)
            throw new InvalidOperationException("Every scanned file was excluded by the current filter; nothing to transfer.");

        // The plan, not the device, owns these numbers. A filter that selects one folder must not
        // reserve the whole phone's free space, and the progress display and summary must promise the
        // bytes this run will actually copy. Reporting the unfiltered scan total beside the filtered
        // file count produced a screen that said "520 files / 15.01 GB" for a 2.63 GB plan.
        long required = planned.Sum(f => Math.Max(0, f.Size));
        long reserve = Math.Max(512L * 1024 * 1024, (long)(required * 0.03));
        long available = -1;
        try
        {
            string? root = Path.GetPathRoot(destination);
            if (!string.IsNullOrEmpty(root)) available = new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // A UNC/network destination has no DriveInfo. Refusing the run would remove a destination
            // the user chose; the copy loop still reports a disk-full failure per file.
            _log.Write($"Free space could not be determined for '{destination}'; continuing without the space preflight. {ex.Message}", "WARN");
        }
        if (available >= 0 && available < required + reserve)
            throw new IOException($"Insufficient destination space. Need approximately {FormatBytes(required + reserve)} including safety reserve; available {FormatBytes(available)}.");

        // The destination folder is the durable record of this session, so stamp it as in-flight before
        // a single byte moves. Without this, a run that stops without writing a summary (power loss, a
        // disconnect, a kill, a crash) is indistinguishable on disk from one that finished - and a
        // 106 GB job would have to be guessed at instead of continued.
        manifest.LastSessionStatus = "RUNNING";
        _store.Flush();

        string sessionId = Guid.NewGuid().ToString("N");
        using SessionDatabase? database = TryOpenDatabase(destination, sessionId, scan, entries);

        string stagingRoot = Path.Combine(destination, ".i-total", "staging");
        Directory.CreateDirectory(stagingRoot);

        var timer = Stopwatch.StartNew();
        long bytesDone = 0, bytesCopied = 0;
        int copied = 0, skipped = 0, failed = 0, inaccessible = 0, unverifiable = 0, processed = 0;
        int total = planned.Count;
        var fileRates = new Queue<(long Bytes, double Seconds)>();

        // File states are buffered and written to SQLite in batches. A committed transaction per update
        // cost far more than copying a small photo (measured: ~1.9 ms per file, and twice per file), and
        // UpsertFiles already exists to take a batch in one transaction. Draining is explicit at every
        // consequential boundary, so a crash loses at most the last few entries - and those are
        // revalidated against the destination on resume, so nothing is silently trusted.
        var pendingStates = new List<TransferEntry>();
        void DrainStates()
        {
            if (database is null || pendingStates.Count == 0) return;
            SessionDatabase sessionDatabase = database;
            TransferEntry[] batch = pendingStates.ToArray();
            pendingStates.Clear();
            TryDatabase(() => sessionDatabase.UpsertFiles(destination, batch), "file states");
        }
        void RecordState(TransferEntry tracked)
        {
            if (database is null) return;
            if (!pendingStates.Contains(tracked)) pendingStates.Add(tracked);
            if (pendingStates.Count >= 64) DrainStates();
        }

        if (scan.Scope is { Status: InventoryScopeStatus.Changed })
            _log.Write("SCOPE CHANGED: " + scan.Scope.Detail, "WARN");
        _log.Write($"Transfer started. Device={scan.DeviceName}; Backend={_device.Name}; Files={total:N0}; Expected={FormatBytes(required)}; Destination={destination}; Direction=device->windows");

        try
        {
            foreach (MediaFile file in planned)
            {
                token.ThrowIfCancellationRequested();
                await Pause.WaitWhilePausedAsync(token);

                string relative;
                try { relative = TransferRules.ValidateRelativePath(file.Folder, file.Name); }
                catch (InvalidDataException ex)
                {
                    inaccessible++; processed++;
                    _log.Write($"INACCESSIBLE {file.Folder}/{file.Name}: {ex.Message}", "ERROR");
                    progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, file.Name, "Unsafe source name; item skipped and reported.", copied, skipped, failed, unverifiable));
                    continue;
                }

                string target = Path.Combine(destination, relative);
                string stageDirectory = Path.Combine(stagingRoot, file.Folder);
                string stagePath = Path.Combine(stageDirectory, file.Name);
                if (!entries.TryGetValue(relative, out TransferEntry? entry))
                    throw new InvalidDataException($"Transfer manifest entry is missing for '{relative}'. Aborting instead of silently skipping it.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                if (File.Exists(target))
                {
                    ResumeOutcome outcome = await HandleExistingAsync(file, target, relative, entry, destination, database, manifest, progress,
                        processed, total, bytesDone, required, bytesCopied, fileRates, timer, token);
                    if (outcome.Handled)
                    {
                        processed++;
                        bytesDone += Math.Max(0, file.Size);
                        switch (outcome.Kind)
                        {
                            case ResumeKind.Skipped: skipped++; break;
                            case ResumeKind.Failed: failed++; break;
                        }
                        continue;
                    }
                    target = outcome.Target; // RenameIncoming: a fresh, collision-free path.
                }

                bool success = false;
                bool committed = false;
                string lastError = "No copy attempt completed.";
                int attempts = Math.Clamp(maxRetries, 1, 10);
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    await Pause.WaitWhilePausedAsync(token);
                    committed = false;
                    entry.State = TransferState.Copying; entry.Attempts++; entry.Error = null; entry.UpdatedUtc = DateTimeOffset.UtcNow;
                    // Throttled, not forced. ManifestStore already has a policy (save after 250 dirty
                    // entries or 2 s), and a forced write serialises the *entire* manifest once per file -
                    // on a 26,000-item library that is measured at ~11 ms per small file, far more than the
                    // file itself costs. The consequential boundaries (plan, pause, cancel, abort, summary)
                    // still force a flush, and recovery revalidates the destination, so at most the last
                    // few entries are re-derived after a crash.
                    _store.MarkDirty();
                    RecordState(entry);
                    var attemptTimer = Stopwatch.StartNew();
                    TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(90, _options.CopyTimeoutSeconds + Math.Ceiling(Math.Max(0, file.Size) / (1024d * 1024d))));
                    CopyResult stageResult = await Task.Run(() => _device.CopyToStage(file, stageDirectory, timeout, token), token);
                    lastError = stageResult.Error;
                    if (stageResult.Success)
                    {
                        string? replacedBackup = null;
                        try
                        {
                            long stageSize = new FileInfo(stagePath).Length;
                            // Only a measured source size can be compared. An unknown size is not 0, so
                            // it must not fail a staged file that has real bytes.
                            if (file.SizeKnown && stageSize != file.Size)
                                throw new InvalidDataException($"Size mismatch in staging: expected {file.Size}, got {stageSize}.");

                            // A backend that hashes the raw source bytes while streaming (WPD does, and returns
                            // that digest on the CopyResult) has already produced the source truth. Re-reading
                            // the staged payload here would burn a second full pass over the file — 106 GB of
                            // pointless reads on a full camera roll — and would only compare the copy with
                            // itself. Compare the streamed source digest with the committed destination
                            // instead: that still catches a bad write, and it additionally catches a bad read.
                            // Backends that cannot hash source bytes (the Shell compatibility backend) keep the
                            // staged read-back, because for them it is the only independent comparison there is.
                            string? stagedPayloadHash = string.IsNullOrWhiteSpace(stageResult.SourceSha256)
                                ? await ComputeSha256Async(stagePath, token)
                                : null;
                            // Overwrite is explicit, but a corrupt new copy must not destroy the old one.
                            // Move the previous file aside first, put it back if the commit fails, and
                            // delete it only once the new bytes are verified.
                            if (File.Exists(target))
                            {
                                string backupDirectory = Path.Combine(destination, ".i-total", "backup");
                                Directory.CreateDirectory(backupDirectory);
                                replacedBackup = Path.Combine(backupDirectory,
                                    Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(target));
                                File.Move(target, replacedBackup);
                            }
                            try { File.Move(stagePath, target); }
                            catch
                            {
                                if (replacedBackup is not null)
                                {
                                    try { File.Move(replacedBackup, target); } catch { }
                                }
                                throw;
                            }
                            committed = true;
                            if (_options.PreserveTimestamps && file.ModifiedUtc is DateTimeOffset modified)
                                TrySetTimestamps(target, modified);

                            string destinationHash = await ComputeSha256Async(target, token);
                            string? expectedHash = stagedPayloadHash ?? stageResult.SourceSha256;
                            if (!string.IsNullOrWhiteSpace(expectedHash) &&
                                !string.Equals(expectedHash, destinationHash, StringComparison.OrdinalIgnoreCase))
                            {
                                RestoreReplacedTarget(target, replacedBackup);
                                committed = false;
                                throw new InvalidDataException("SHA-256 mismatch: the committed destination does not match the bytes read from the source. The destination copy was rejected.");
                            }

                            entry.SourceSha256 = stageResult.SourceSha256;
                            entry.StagedPayloadSha256 = stagedPayloadHash;
                            entry.DestinationSha256 = destinationHash;
                            string planFull = Path.GetFullPath(Path.Combine(destination, relative));
                            string committedFull = Path.GetFullPath(target);
                            entry.CommittedPath = string.Equals(planFull, committedFull, StringComparison.OrdinalIgnoreCase)
                                ? null
                                : Path.GetRelativePath(destination, committedFull);
                            entry.Verification = TransferRules.ClassifyVerification(entry.SourceSha256, stagedPayloadHash, destinationHash, file.SizeKnown);
                            entry.PreserveTimestamp = _options.PreserveTimestamps;
                            entry.SourceChanged = false;
                            success = true;
                            if (replacedBackup is not null) { try { File.Delete(replacedBackup); } catch { } }
                            entry.BytesWritten = new FileInfo(target).Length;
                            long countedBytes = file.SizeKnown ? Math.Max(0, file.Size) : entry.BytesWritten;
                            fileRates.Enqueue((countedBytes, Math.Max(0.001, attemptTimer.Elapsed.TotalSeconds)));
                            while (fileRates.Count > 30) fileRates.Dequeue();
                            bytesCopied += countedBytes;
                            break;
                        }
                        catch (Exception ex)
                        {
                            // The bytes reached the destination but could not be verified - a hash interrupted
                            // by Stop, a read error on the destination. Leaving that file in place would put a
                            // file in the backup that no record vouches for, and the next attempt would
                            // classify it as a legacy baseline at size-only strength: a quiet downgrade of a
                            // claim the operator never agreed to. Discard it instead, so what is present is
                            // either verified or absent, and the next run fetches it again.
                            if (committed)
                            {
                                RestoreReplacedTarget(target, replacedBackup);
                                committed = false;
                                lastError = $"{ex.Message} (the committed copy was discarded because it could not be verified)";
                            }
                            else lastError = ex.Message;
                        }
                    }
                    if (File.Exists(stagePath)) { try { File.Delete(stagePath); } catch { } }
                    _log.Write($"Attempt {attempt}/{attempts} failed for {relative}: {lastError}", "WARN");
                    // A denial, a missing object or an unsupported resource will fail identically on the
                    // next attempt; retrying only delays the report and inflates the log.
                    if (!stageResult.Success && stageResult.FailureKind == CopyFailureKind.Permanent)
                    {
                        _log.Write($"Not retrying {relative}: the failure is permanent for this run ({lastError}).", "WARN");
                        break;
                    }
                    if (attempt < attempts) await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * attempt, 10)), token);
                }

                processed++;
                bytesDone += Math.Max(0, file.Size);
                if (success)
                {
                    copied++;
                    // A measured size (including a genuine zero) can be verified. An unmeasured size
                    // can only be accepted when the source byte stream itself was hashed and matched
                    // the committed destination, because that proves the bytes without a size.
                    bool verifiedWithoutSize = !file.SizeKnown &&
                        entry.Verification == VerificationLevel.SourceAndDestinationHash;
                    if (file.SizeKnown || verifiedWithoutSize)
                    {
                        entry.State = TransferState.IntegrityVerified; entry.Outcome = OutcomeReason.Copied; entry.Error = null;
                        string note = entry.Verification == VerificationLevel.SourceAndDestinationHash
                            ? "Source and destination SHA-256 match (raw source bytes were read and hashed)."
                            : "Staged payload and destination SHA-256 match, but the backend cannot hash raw source bytes, so byte identity with the original is not asserted.";
                        string sizeNote = file.SizeKnown ? "" : " The source size was unavailable; identity rests on the source-to-destination hash, not on a size.";
                        _log.Write($"COPY COMPLETE {relative} ({(file.SizeKnown ? FormatBytes(file.Size) : "size unknown")}): verification={entry.Verification}; source={entry.SourceSha256}; staged={entry.StagedPayloadSha256 ?? "(not read: the streamed source digest was compared with the destination directly)"}; destination={entry.DestinationSha256}. {note}{sizeNote}", "OK");
                    }
                    else
                    {
                        unverifiable++; entry.State = TransferState.CopiedUnverified; entry.Outcome = OutcomeReason.Copied;
                        entry.Error = "The source reported no readable size and no source-byte hash was available; the copy completed but cannot be called verified.";
                        _log.Write($"COPIED-UNVERIFIABLE {relative}: source size unavailable and no source-byte digest; cannot claim verified completion.", "WARN");
                    }
                }
                else
                {
                    failed++; entry.State = TransferState.Failed; entry.Outcome = OutcomeReason.Failed; entry.Error = lastError;
                    _log.Write($"FAILED {relative}: {lastError}", "ERROR");
                }
                entry.UpdatedUtc = DateTimeOffset.UtcNow;
                _store.MarkDirty();
                RecordState(entry);
                progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, relative,
                    success ? "File copied; staged payload and destination SHA-256 match." : "File failed; details saved in manifest.",
                    copied, skipped, failed, unverifiable));
            }
        }
        catch (OperationCanceledException)
        {
            // Record the interruption in the folder itself, not only in the in-session database, so the
            // operator is told on the next launch that this destination holds an unfinished job.
            manifest.LastSessionStatus = "CANCELLED";
            _store?.Flush();
            DrainStates();
            TryDatabase(() => database?.EndSession(sessionId, "CANCELLED"), "cancelled session");
            _log.Write("Transfer cancelled by user. Completed files and queue state are saved; resume is available on the next run.", "WARN");
            throw;
        }
        catch (Exception ex)
        {
            manifest.LastSessionStatus = "ABORTED";
            _store?.Flush();
            DrainStates();
            TryDatabase(() => database?.EndSession(sessionId, "ABORTED"), "aborted session");
            _log.Write($"Transfer aborted: {ex}", "ERROR");
            throw;
        }
        finally { timer.Stop(); _store?.Flush(); DrainStates(); }

        // The manifest, not this run's counters, is the authority on whether the session is done.
        //
        // A continued session plans only the outstanding entries, so a run that copies all of them
        // would otherwise be able to announce a verified complete backup while an older entry in the
        // same folder is still failed, unreadable or unverified. Nothing may be left in any
        // non-successful state anywhere in the session. This run's own counters stay as they are: they
        // describe the work this run actually did, which is what the progress display promised.
        bool manifestOutstanding = manifest.Entries.Any(e => e.State is TransferState.Failed
            or TransferState.Inaccessible or TransferState.CopiedUnverified
            or TransferState.Discovered or TransferState.Queued
            or TransferState.Copying or TransferState.Paused);
        int unverifiableInManifest = manifest.Entries.Count(e => e.State == TransferState.CopiedUnverified);
        unverifiable = Math.Max(unverifiable, unverifiableInManifest);

        int accepted = manifest.Entries.Count(e => e.State is TransferState.SizeVerified or TransferState.IntegrityVerified);
        // A run can copy every file it was given and still be a partial backup, because the enumeration
        // that produced the plan was itself incomplete (an unreadable folder, a duplicate path, a phone
        // that stopped answering). The per-run counters cannot see that, so the scan's own status is
        // part of the completion decision. An incomplete plan never becomes a "complete backup".
        bool enumerationComplete = scan.Status == EnumerationStatus.Complete;
        bool complete = enumerationComplete &&
                        TransferRules.CanDeclareComplete(total, processed, failed, inaccessible) &&
                        unverifiable == 0 && !manifestOutstanding;
        VerificationLevel weakest = manifest.Entries
            .Where(e => e.State is TransferState.SizeVerified or TransferState.IntegrityVerified or TransferState.Skipped)
            .Select(e => e.Verification)
            .DefaultIfEmpty(VerificationLevel.None)
            .Min();
        // A run whose every planned file was already present under an accepted policy is a no-op, not a
        // copy: saying "verified complete" would imply this run wrote something. The reconciliation is
        // computed here so the verdict and the report use the same numbers.
        ReconciliationReport reconciliation = ReconciliationReport.Build(scan, manifest.Entries);
        bool nothingToCopy = complete && reconciliation.CopiedFiles == 0 && reconciliation.AlreadyPresentFiles == total;
        string status = nothingToCopy
            ? "ALREADY PRESENT \u2014 NO FILES NEEDED COPYING (verified against the session record)"
            : complete
            ? weakest switch
            {
                VerificationLevel.SourceAndDestinationHash => "HASH-VERIFIED COMPLETE (source-to-destination)",
                VerificationLevel.DestinationHash => "DESTINATION-HASH-VERIFIED COMPLETE",
                _ => "SIZE-VERIFIED COMPLETE"
            }
            : unverifiable > 0 && failed == 0
                ? "COPIED WITH UNVERIFIABLE FILES — NOT A VERIFIED COMPLETE BACKUP"
                : "INCOMPLETE — NOT A VERIFIED COMPLETE BACKUP";

        if (!enumerationComplete)
        {
            status = "INCOMPLETE ENUMERATION \u2014 NOT A VERIFIED COMPLETE BACKUP";
            _log.Write("The scan was incomplete; the run is reported as an incomplete enumeration even though every planned file may have been copied.", "WARN");
        }

        manifest.LastSessionStatus = status;
        _store.Flush();
        // Copied-but-unverifiable is a warning, not a crash: every planned file was processed and none
        // failed, but the phone refused a size the verification needed. Logging it as ERROR makes an
        // honest limitation look like a failed run in every log reader.
        string summaryLevel = complete ? "OK" : failed == 0 && inaccessible == 0 ? "WARN" : "ERROR";
        _log.Write($"SUMMARY {status}; processed={processed}/{total}; copied={copied}; skipped={skipped}; failed={failed}; inaccessible={inaccessible}; unverifiable={unverifiable}; elapsed={timer.Elapsed:hh\\:mm\\:ss}", summaryLevel);
        TryDatabase(() => database?.EndSession(sessionId, status, total, copied, failed, bytesCopied, scan.Warnings.Count), "session summary");
        ExportReports(destination, scan, manifest, status, weakest, reconciliation);
        return new TransferSummary(total, copied, skipped, failed, inaccessible, unverifiable, required, timer.Elapsed,
            complete, status, _store.Path)
        {
            BytesCopied = bytesCopied,
            WeakestLevel = weakest,
            AlreadyPresent = reconciliation.AlreadyPresentFiles,
            Reconciliation = reconciliation,
            ReportPath = Path.Combine(destination, "I-total-session-report.json"),
            // One id per run. It is what makes the trial ledger idempotent: a resumed session and a fresh
            // session are different runs, but reporting the same finished run twice is recognised.
            JobId = Guid.NewGuid().ToString("N")
        };
    }

    private static int filteredOutCount(SessionManifest manifest) =>
        manifest.Entries.Count(e => e.State == TransferState.Skipped && e.Error == "Excluded by filter.");
    /// <summary>
    /// A source listing that names one destination path twice cannot be copied unambiguously: the
    /// engine resolves work by relative path, so both entries would share one record and one target.
    /// The scan is already marked incomplete by the duplicate-path warning, but this refuses the run
    /// before any file is written and names the paths, rather than letting a later collision stand in
    /// for the wrong source bytes.
    /// </summary>
    private static void EnsureUniqueSourcePaths(ScanResult scan)
    {
        List<string> duplicates = scan.Files
            .GroupBy(f => TransferRules.ValidateRelativePath(f.Folder, f.Name), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(5)
            .ToList();
        if (duplicates.Count == 0) return;
        throw new InvalidOperationException(
            "The scan lists the same destination path more than once (" + string.Join(", ", duplicates) +
            "). A transfer cannot tell which source object belongs to that path, so nothing was copied. " +
            "Rescan the phone and check the duplicate-path warning before trying again.");
    }

    private enum ResumeKind { Handled, Skipped, Failed }

    private readonly record struct ResumeOutcome(bool Handled, ResumeKind Kind, string Target);

    private async Task<ResumeOutcome> HandleExistingAsync(MediaFile file, string target, string relative, TransferEntry entry,
        string destination, SessionDatabase? database, SessionManifest manifest, IProgress<TransferProgress> progress,
        int processed, int total, long bytesDone, long required, long bytesCopied,
        Queue<(long Bytes, double Seconds)> fileRates, Stopwatch timer, CancellationToken token)
    {
        // A source that changed after its copy was recorded must not be treated as unchanged. With
        // PreserveAndReport the existing destination is kept and the file is reported; with Overwrite the
        // stale destination is replaced; Skip and Rename still follow their own policy.
        bool sourceChanged = entry.SourceChanged;
        switch (_options.ConflictPolicy)
        {
            case ConflictPolicy.Skip:
                entry.State = TransferState.Skipped; entry.Outcome = OutcomeReason.ConflictSkipped; entry.Error = "Destination exists; skipped by policy.";
                entry.UpdatedUtc = DateTimeOffset.UtcNow;
                _store!.MarkDirty(); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, relative, "Destination exists; skipped by conflict policy."));
                return new ResumeOutcome(true, ResumeKind.Skipped, target);

            case ConflictPolicy.RenameIncoming:
                // If an earlier run already stored this source under a renamed path, do not add another
                // copy on every resume: accept the recorded copy when its bytes still match.
                if (!string.IsNullOrWhiteSpace(entry.CommittedPath) && !entry.SourceChanged)
                {
                    string committedFull = Path.Combine(destination, entry.CommittedPath);
                    if (File.Exists(committedFull) && !string.IsNullOrWhiteSpace(entry.DestinationSha256))
                    {
                        string? committedHash = await ComputeSha256Async(committedFull, token);
                        if (string.Equals(committedHash, entry.DestinationSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            entry.State = TransferState.IntegrityVerified;
                            entry.Outcome = OutcomeReason.AlreadyPresent;
                            entry.Verification = TransferRules.ClassifyVerification(entry.SourceSha256, null, entry.DestinationSha256, file.SizeKnown);
                            entry.Error = null;
                            entry.UpdatedUtc = DateTimeOffset.UtcNow;
                            _store!.MarkDirty(); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                            return new ResumeOutcome(true, ResumeKind.Skipped, committedFull);
                        }
                    }
                }
                target = TransferRules.ResolveTargetPath(destination, relative, ConflictPolicy.RenameIncoming, File.Exists);
                return new ResumeOutcome(false, ResumeKind.Handled, target);

            case ConflictPolicy.Overwrite:
            {
                if (_options.SkipUnchanged && !sourceChanged)
                {
                    long existingSize = new FileInfo(target).Length;
                    bool sizeComparable = file.SizeKnown && existingSize == file.Size;
                    string? existingHash = sizeComparable || (!file.SizeKnown && !string.IsNullOrWhiteSpace(entry.DestinationSha256))
                        ? await ComputeSha256Async(target, token)
                        : null;
                    ResumeDecision d = TransferRules.ClassifyExistingDestination(file.Size, existingSize, entry.DestinationSha256, existingHash, file.SizeKnown);
                    if (d == ResumeDecision.SkipVerified)
                    {
                        entry.State = TransferState.IntegrityVerified;
                        entry.Outcome = OutcomeReason.AlreadyPresent;
                        entry.Verification = TransferRules.ClassifyVerification(entry.SourceSha256, null, entry.DestinationSha256, file.SizeKnown);
                        entry.UpdatedUtc = DateTimeOffset.UtcNow;
                        _store!.MarkDirty(); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                        return new ResumeOutcome(true, ResumeKind.Skipped, target);
                    }
                }
                return new ResumeOutcome(false, ResumeKind.Handled, target); // Overwrite: the copy loop deletes then writes.
            }

            default: // PreserveAndReport
            {
                if (sourceChanged)
                {
                    entry.State = TransferState.Failed;
                    entry.Outcome = OutcomeReason.Failed;
                    entry.Verification = VerificationLevel.None;
                    entry.Error = "The source file changed since it was copied (size or modification time). " +
                        "The existing destination was preserved; choose Overwrite or Rename to replace it.";
                    entry.UpdatedUtc = DateTimeOffset.UtcNow;
                    _store!.MarkDirty(force: true); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                    _log.Write($"SOURCE CHANGED {relative}: {entry.Error}", "WARN");
                    progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, relative,
                        "Source changed since the recorded copy; existing destination preserved."));
                    return new ResumeOutcome(true, ResumeKind.Failed, target);
                }
                long existingSize = new FileInfo(target).Length;
                // A file that a previous pass already copied but could not verify (the phone reported
                // no source size) is not a collision. Hash the destination anyway so the recorded
                // digest can prove it is the same file, and keep the honest unverified state instead
                // of degrading it to Failed - otherwise every resume of a zero-byte placeholder makes
                // the session look worse while changing nothing on disk.
                bool wasCopiedUnverified = entry.State == TransferState.CopiedUnverified;
                string? existingHash = (file.SizeKnown && existingSize == file.Size) || wasCopiedUnverified ||
                    (!file.SizeKnown && !string.IsNullOrWhiteSpace(entry.DestinationSha256))
                    ? await ComputeSha256Async(target, token)
                    : null;
                if (wasCopiedUnverified && existingHash is not null &&
                    !string.IsNullOrWhiteSpace(entry.DestinationSha256) &&
                    string.Equals(existingHash, entry.DestinationSha256, StringComparison.OrdinalIgnoreCase))
                {
                    entry.State = TransferState.CopiedUnverified;
                    entry.Outcome = OutcomeReason.AlreadyPresent;
                    entry.Verification = VerificationLevel.None;
                    entry.Error = "Already copied but unverified: the source size is unavailable. The existing destination matches the recorded SHA-256 and was preserved.";
                    entry.UpdatedUtc = DateTimeOffset.UtcNow;
                    _store!.MarkDirty(force: true); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                    _log.Write($"PRESERVED UNVERIFIED {relative}: destination matches the recorded SHA-256; no second copy was attempted.", "WARN");
                    progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, relative,
                        "Already copied but unverified; destination preserved."));
                    return new ResumeOutcome(true, ResumeKind.Skipped, target);
                }
                ResumeDecision decision = TransferRules.ClassifyExistingDestination(file.Size, existingSize, entry.DestinationSha256, existingHash, file.SizeKnown);
                switch (decision)
                {
                    case ResumeDecision.SkipVerified:
                        entry.State = TransferState.IntegrityVerified;
                        entry.Outcome = OutcomeReason.AlreadyPresent;
                        entry.Verification = TransferRules.ClassifyVerification(entry.SourceSha256, null, entry.DestinationSha256, file.SizeKnown);
                        entry.Error = null; entry.UpdatedUtc = DateTimeOffset.UtcNow;
                        _store!.MarkDirty(); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                        progress.Report(BuildProgress("Copy", processed, total, bytesDone, required, bytesCopied, fileRates, timer, relative, "Existing destination matches its saved SHA-256."));
                        return new ResumeOutcome(true, ResumeKind.Skipped, target);

                    case ResumeDecision.RecordBaselineSkip:
                        entry.DestinationSha256 = existingHash;
                        entry.State = TransferState.SizeVerified;
                        entry.Outcome = OutcomeReason.AlreadyPresent;
                        entry.Verification = VerificationLevel.SizeOnly;
                        entry.Error = "Legacy file baseline SHA-256 recorded from destination; source identity remains unverified.";
                        entry.UpdatedUtc = DateTimeOffset.UtcNow;
                        _store!.MarkDirty(force: true); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                        _log.Write($"LEGACY DESTINATION BASELINE {relative}: recorded local SHA-256; source-byte identity is not asserted.", "WARN");
                        return new ResumeOutcome(true, ResumeKind.Skipped, target);

                    case ResumeDecision.PreserveHashMismatch:
                        entry.State = TransferState.Failed; entry.Outcome = OutcomeReason.Failed; entry.Verification = VerificationLevel.None;
                        entry.Error = "Existing destination file's SHA-256 differs from the saved manifest. File preserved; manual review required.";
                        entry.UpdatedUtc = DateTimeOffset.UtcNow;
                        _store!.MarkDirty(force: true); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                        _log.Write($"HASH MISMATCH {relative}: existing destination preserved.", "ERROR");
                        return new ResumeOutcome(true, ResumeKind.Failed, target);

                    default: // PreserveCollision
                        entry.State = TransferState.Failed; entry.Outcome = OutcomeReason.Failed; entry.Verification = VerificationLevel.None;
                        entry.Error = !file.SizeKnown
                            ? $"Source size is unavailable; existing destination file ({existingSize} bytes) was preserved because it cannot be safely classified."
                            : $"Destination collision: existing file size {existingSize} differs from source-reported size {file.Size}. Existing file was preserved.";
                        entry.UpdatedUtc = DateTimeOffset.UtcNow;
                        _store!.MarkDirty(force: true); TryDatabase(() => database?.UpsertFile(destination, entry), relative);
                        _log.Write($"COLLISION {relative}: {entry.Error}", "ERROR");
                        return new ResumeOutcome(true, ResumeKind.Failed, target);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Windows -> device
    public Task<TransferSummary> CopyWindowsToDeviceAsync(IReadOnlyList<string> localFiles, string deviceFolder,
        IProgress<TransferProgress> progress, CancellationToken token) =>
        ContextFree.Run(() => CopyWindowsToDeviceCoreAsync(localFiles, deviceFolder, progress, token), token);

    private async Task<TransferSummary> CopyWindowsToDeviceCoreAsync(IReadOnlyList<string> localFiles, string deviceFolder,
        IProgress<TransferProgress> progress, CancellationToken token)
    {
        if (!_device.Capabilities.CanWriteToDevice)
            throw new NotSupportedException($"Backend '{_device.Name}' cannot write files to the device. Writing to a phone needs a backend that supports it; in this release only AFC, with its verified native runtime, can write, and only to an iPhone.");
        if (localFiles.Count == 0) throw new InvalidOperationException("No local files were selected for transfer.");

        var timer = Stopwatch.StartNew();
        long totalBytes = 0;
        foreach (string f in localFiles) { try { totalBytes += new FileInfo(f).Length; } catch { } }
        int processed = 0, copied = 0, failed = 0, unverifiable = 0;
        long bytesDone = 0, bytesCopied = 0;
        var rates = new Queue<(long Bytes, double Seconds)>();

        foreach (string localPath in localFiles)
        {
            token.ThrowIfCancellationRequested();
            await Pause.WaitWhilePausedAsync(token);
            processed++;
            string name = Path.GetFileName(localPath);
            long size = 0; try { size = new FileInfo(localPath).Length; } catch { }
            var destination = new MediaFile(deviceFolder, name, size, Path.GetExtension(name).ToLowerInvariant());
            var attemptTimer = Stopwatch.StartNew();
            CopyResult result = await Task.Run(() => _device.CopyToDevice(localPath, destination,
                TimeSpan.FromSeconds(_options.CopyTimeoutSeconds), token), token);
            bytesDone += size;
            if (result.Success)
            {
                copied++; bytesCopied += size;
                // Read the committed device file back and hash it when the backend supports it.
                if (_device.Capabilities.CanReadBackCommitted)
                {
                    string localHash = await ComputeSha256Async(localPath, token);
                    string? deviceHash = await Task.Run(() => HashSourceAsync(destination, token), token);
                    if (deviceHash is null || !string.Equals(localHash, deviceHash, StringComparison.OrdinalIgnoreCase))
                    {
                        failed++; copied--;
                        _log.Write($"WINDOWS->DEVICE VERIFY FAILED {name}: local and device SHA-256 differ. Removing device copy.", "ERROR");
                        _device.TryDeleteDeviceFile(destination);
                    }
                }
                rates.Enqueue((size, Math.Max(0.001, attemptTimer.Elapsed.TotalSeconds)));
                while (rates.Count > 30) rates.Dequeue();
            }
            else
            {
                failed++;
                _log.Write($"WINDOWS->DEVICE FAILED {name}: {result.Error}", "ERROR");
            }
            progress.Report(BuildProgress("Copy", processed, localFiles.Count, bytesDone, totalBytes, bytesCopied, rates, timer, name,
                result.Success ? "Sent to device." : "Failed: " + result.Error, copied, 0, failed, unverifiable));
        }
        timer.Stop();
        bool complete = processed == localFiles.Count && failed == 0;
        string status = complete ? "SENT TO DEVICE (hash-verified where supported)" : "PARTIALLY SENT / FAILED";
        _log.Write($"SUMMARY {status}; sent={copied}; failed={failed}; elapsed={timer.Elapsed:hh\\:mm\\:ss}", complete ? "OK" : "ERROR");
        return new TransferSummary(localFiles.Count, copied, 0, failed, 0, unverifiable, totalBytes, timer.Elapsed, complete, status, "")
        {
            BytesCopied = bytesCopied,
            JobId = Guid.NewGuid().ToString("N")
        };
    }

    private string? HashSourceAsync(MediaFile file, CancellationToken token)
    {
        try
        {
            using Stream? stream = _device.OpenSourceRead(file);
            if (stream is null) return null;
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------ Verify / repair
    public Task<VerificationResult> VerifyAsync(ScanResult scan, string destination, IProgress<TransferProgress> progress, CancellationToken token)
        => VerifyAsync(scan, destination, progress, token, repair: false);

    public Task<VerificationResult> VerifyAsync(ScanResult scan, string destination, IProgress<TransferProgress> progress,
        CancellationToken token, bool repair) =>
        ContextFree.Run(() => VerifyCoreAsync(scan, destination, progress, token, repair), token);

    private async Task<VerificationResult> VerifyCoreAsync(ScanResult scan, string destination,
        IProgress<TransferProgress> progress, CancellationToken token, bool repair)
    {
        if (scan.Files.Count == 0) throw new InvalidOperationException("Cannot verify an empty scan.");
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination is required.", nameof(destination));
        EnsureUniqueSourcePaths(scan);
        _store ??= new ManifestStore(destination, _log);
        SessionManifest manifest = _store.LoadOrCreate(destination, scan.DeviceName, scan.StableId, _device.Name);
        Dictionary<string, TransferEntry> manifestByPath = _store.Index();

        var issues = new List<string>();
        int matching = 0, missing = 0, wrongSize = 0, wrongHash = 0, wrong = 0, unverifiable = 0, collisions = 0, sourceVerified = 0, destVerified = 0, repaired = 0;
        int total = scan.Files.Count, current = 0;
        bool enumerationComplete = scan.Status == EnumerationStatus.Complete;
        if (!enumerationComplete)
            issues.Add("ENUMERATION INCOMPLETE: the scan that defines this verification missed at least one folder or file. A match here is not a complete backup.");

        foreach (MediaFile file in scan.Files)
        {
            token.ThrowIfCancellationRequested();
            current++;
            string relative;
            try { relative = TransferRules.ValidateRelativePath(file.Folder, file.Name); }
            catch (InvalidDataException ex) { unverifiable++; issues.Add($"INVALID PATH: {file.Folder}/{file.Name}; {ex.Message}"); continue; }

            manifestByPath.TryGetValue(relative, out TransferEntry? entry);
            string path = Path.Combine(destination, relative);
            if (!string.IsNullOrWhiteSpace(entry?.CommittedPath) &&
                !string.Equals(entry.CommittedPath, relative, StringComparison.OrdinalIgnoreCase))
            {
                // A renamed copy lives under a different name; verify those bytes, not the untouched
                // file that caused the collision.
                path = Path.Combine(destination, entry.CommittedPath);
                issues.Add($"STORED AS: {relative} was copied as {entry.CommittedPath} by the conflict policy.");
            }

            if (!File.Exists(path))
            {
                missing++;
                issues.Add($"MISSING: {relative}");
                if (repair && entry is not null)
                {
                    string staged = Path.Combine(destination, ".i-total", "staging", file.Folder, file.Name);
                    try
                    {
                        CopyResult copy = await Task.Run(() => _device.CopyToStage(file, Path.GetDirectoryName(staged)!,
                            TimeSpan.FromSeconds(_options.CopyTimeoutSeconds), token), token);
                        if (!copy.Success)
                        {
                            entry.State = TransferState.Failed;
                            entry.Error = "Repair failed: " + copy.Error;
                            _store.MarkDirty(force: true);
                            issues.Add($"REPAIR FAILED: {relative}; {copy.Error}");
                        }
                        else
                        {
                            long stagedSize = new FileInfo(staged).Length;
                            if (file.Size > 0 && stagedSize != file.Size)
                                throw new InvalidDataException($"Repaired staging size mismatch: expected {file.Size}, got {stagedSize}.");
                            string stagedHash = await ComputeSha256Async(staged, token);
                            if (File.Exists(path)) File.Delete(path);
                            File.Move(staged, path);
                            string destinationHash = await ComputeSha256Async(path, token);
                            string? expected = copy.SourceSha256 ?? stagedHash;
                            if (!string.Equals(expected, destinationHash, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(path); } catch { }
                                throw new InvalidDataException("Repaired copy failed its SHA-256 comparison; the file was discarded.");
                            }
                            entry.SourceSha256 = copy.SourceSha256 ?? entry.SourceSha256;
                            entry.StagedPayloadSha256 = string.IsNullOrWhiteSpace(copy.SourceSha256) ? stagedHash : null;
                            entry.DestinationSha256 = destinationHash;
                            entry.State = TransferState.IntegrityVerified;
                            entry.Error = null;
                            entry.SourceChanged = false;
                            entry.Verification = TransferRules.ClassifyVerification(entry.SourceSha256, entry.StagedPayloadSha256, destinationHash, file.Size > 0);
                            _store.MarkDirty(force: true);
                            repaired++; matching++; missing--;
                            issues.Add($"REPAIRED: {relative}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (File.Exists(staged)) { try { File.Delete(staged); } catch { } }
                        entry.State = TransferState.Failed;
                        entry.Error = "Repair failed: " + ex.Message;
                        _store.MarkDirty(force: true);
                        issues.Add($"REPAIR FAILED: {relative}; {ex.Message}");
                    }
                }
            }
            else if (!file.SizeKnown)
            {
                // The size cannot be compared, but a recorded destination digest still proves the bytes
                // are unchanged, and the source-hash policy can additionally re-read the source. Calling
                // every unmeasured file "unverifiable" would ignore evidence that does exist.
                string? saved = entry?.DestinationSha256;
                if (string.IsNullOrWhiteSpace(saved))
                {
                    unverifiable++;
                    issues.Add($"UNVERIFIABLE: {relative}; source size was unavailable and no destination digest was recorded.");
                }
                else
                {
                    string actualHash = await ComputeSha256Async(path, token);
                    if (!string.Equals(actualHash, saved, StringComparison.OrdinalIgnoreCase))
                    {
                        wrongHash++; wrong++;
                        if (entry is not null)
                        {
                            entry.State = TransferState.Failed;
                            entry.Error = "Destination SHA-256 differs from the saved digest; the file was changed after it was verified.";
                            entry.Verification = VerificationLevel.None;
                            _store.MarkDirty(force: true);
                        }
                        issues.Add($"SHA-256 MISMATCH: {relative}; destination bytes differ from the saved destination digest.");
                    }
                    else
                    {
                        destVerified++;
                        bool contradicted = false;
                        if (_device.Capabilities.CanReadBackCommitted && _options.VerificationPolicy == VerificationPolicy.SourceAndDestinationHash)
                        {
                            string? liveSource = await Task.Run(() => HashSourceAsync(file, token), token);
                            if (!string.IsNullOrWhiteSpace(liveSource))
                            {
                                if (string.Equals(liveSource, actualHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    sourceVerified++;
                                    if (entry is not null)
                                    {
                                        entry.SourceSha256 = liveSource;
                                        entry.Verification = VerificationLevel.SourceAndDestinationHash;
                                    }
                                }
                                else
                                {
                                    contradicted = true;
                                    wrongHash++; wrong++;
                                    if (entry is not null)
                                    {
                                        entry.State = TransferState.Failed;
                                        entry.Error = "Live source SHA-256 differs from the destination during verification.";
                                        entry.Verification = VerificationLevel.None;
                                        _store.MarkDirty(force: true);
                                    }
                                    issues.Add($"SOURCE/DESTINATION MISMATCH: {relative}; live source SHA-256 differs from destination.");
                                }
                            }
                        }
                        if (!contradicted) matching++;
                    }
                }
            }
            else
            {
                long actual = new FileInfo(path).Length;
                if (actual != file.Size)
                {
                    wrongSize++;
                    wrong++;
                    if (entry is not null)
                    {
                        entry.State = TransferState.Failed;
                        entry.Error = $"Destination size mismatch during verification: expected {file.Size}, found {actual}.";
                        entry.Verification = VerificationLevel.None;
                        _store.MarkDirty(force: true);
                    }
                    issues.Add($"SIZE MISMATCH: {relative}; expected={file.Size}; actual={actual}");
                }
                else if (string.IsNullOrWhiteSpace(entry?.DestinationSha256))
                {
                    unverifiable++; issues.Add($"NO SAVED HASH: {relative}; size matches but no prior destination SHA-256 is recorded.");
                }
                else
                {
                    string actualHash = await ComputeSha256Async(path, token);
                    if (!string.Equals(actualHash, entry.DestinationSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        wrongHash++;
                        wrong++;
                        entry.State = TransferState.Failed;
                        entry.Error = "Destination SHA-256 differs from the saved digest; the file was changed after it was verified.";
                        entry.Verification = VerificationLevel.None;
                        _store.MarkDirty(force: true);
                        issues.Add($"SHA-256 MISMATCH: {relative}; destination bytes differ from the saved destination digest.");
                    }
                    else
                    {
                        destVerified++;
                        // When the backend can stream raw source bytes, independently re-hash the source for
                        // a real source-to-destination comparison rather than trusting the stored digest alone.
                        if (_device.Capabilities.CanReadBackCommitted && _options.VerificationPolicy == VerificationPolicy.SourceAndDestinationHash)
                        {
                            string? liveSource = await Task.Run(() => HashSourceAsync(file, token), token);
                            if (!string.IsNullOrWhiteSpace(liveSource))
                            {
                                if (string.Equals(liveSource, actualHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    sourceVerified++; matching++;
                                    entry.SourceSha256 = liveSource;
                                    entry.Verification = VerificationLevel.SourceAndDestinationHash;
                                    _store.MarkDirty();
                                }
                                else
                                {
                                    wrongHash++;
                                    wrong++;
                                    entry.State = TransferState.Failed;
                                    entry.Error = "Live source SHA-256 differs from the destination during verification.";
                                    entry.Verification = VerificationLevel.None;
                                    _store.MarkDirty(force: true);
                                    issues.Add($"SOURCE/DESTINATION MISMATCH: {relative}; live source SHA-256 differs from destination.");
                                }
                            }
                            else matching++; // Source could not be re-read; destination digest still matches.
                        }
                        else matching++;
                    }
                }
            }

            if (current % 50 == 0 || current == total)
                progress.Report(new TransferProgress("Verify", current, total, 0, 0, 0, relative, "Checking destination files.")
                { Copied = matching, Failed = missing + wrong, Unverifiable = unverifiable });
            if (current % 200 == 0) await Task.Yield();
        }

        bool complete = enumerationComplete && total > 0 && matching == total && missing == 0 &&
                        wrong == 0 && unverifiable == 0 && collisions == 0;
        _store.Flush();
        PersistVerification(destination, scan, manifest, complete);
        _log.Write($"Verification: matching={matching}/{total}; source-hash-verified={sourceVerified}; destination-hash-verified={destVerified}; missing={missing}; size-mismatches={wrongSize}; hash-mismatches={wrongHash}; unverifiable={unverifiable}; repaired={repaired}; enumeration={scan.Status}.", complete ? "OK" : "ERROR");
        string reportPath = Path.Combine(destination, "I-total-verification.json");
        LogService.ExportJson(reportPath, new
        {
            CreatedUtc = DateTimeOffset.UtcNow, Device = scan.DeviceName, Backend = _device.Name, Total = total,
            Matching = matching, SourceHashVerified = sourceVerified, DestinationHashVerified = destVerified,
            Missing = missing, WrongSize = wrongSize, WrongHash = wrongHash, Unverifiable = unverifiable, Collisions = collisions,
            Repaired = repaired, Completed = complete, Issues = issues
        });
        return new VerificationResult(total, matching, missing, wrongSize, unverifiable, collisions, complete, issues)
        {
            WrongHash = wrongHash,
            SourceHashVerified = sourceVerified,
            DestinationHashVerified = destVerified,
            Repaired = repaired,
            ReportPath = reportPath
        };
    }

    /// <summary>
    /// Writes the verification verdict to the SQLite queue as well as the JSON manifest, so a recovery
    /// or history path sees the same hashes and states without parsing JSON. Failure to open the
    /// database is logged and never turns a verification result into an exception.
    /// </summary>
    private void PersistVerification(string destination, ScanResult scan, SessionManifest manifest, bool complete)
    {
        SessionDatabase? database = null;
        try
        {
            string sessionId = Guid.NewGuid().ToString("N");
            database = new SessionDatabase(Path.Combine(destination, ".i-total", "session.db"), _log);
            database.Initialize();
            database.BeginSession(sessionId, scan.DeviceName, destination, "VERIFYING",
                _device.Name, scan.Family.ToString(), scan.Status.ToString());
            database.UpsertFiles(destination, manifest.Entries);
            database.EndSession(sessionId, complete ? "VERIFICATION COMPLETE" : "VERIFICATION INCOMPLETE",
                manifest.Entries.Count,
                manifest.Entries.Count(e => e.State is TransferState.IntegrityVerified or TransferState.SizeVerified),
                manifest.Entries.Count(e => e.State == TransferState.Failed),
                manifest.Entries.Sum(e => Math.Max(0, e.BytesWritten)),
                scan.Warnings.Count);
        }
        catch (Exception ex)
        {
            _log.Write($"SQLite unavailable during verification; the JSON manifest remains the record. {ex.Message}", "WARN");
        }
        finally { database?.Dispose(); }
    }

    // ------------------------------------------------------------------ Infrastructure
    private SessionDatabase? TryOpenDatabase(string destination, string sessionId, ScanResult scan, Dictionary<string, TransferEntry> entries)
    {
        SessionDatabase? database = null;
        try
        {
            database = new SessionDatabase(Path.Combine(destination, ".i-total", "session.db"), _log);
            database.Initialize();
            database.BeginSession(sessionId, scan.DeviceName, destination, "RUNNING",
                _device.Name, scan.Family.ToString(), scan.Status.ToString());
            // One transaction for the whole plan. Opening the session used to commit a transaction per
            // planned entry, so a 26,335-file camera roll queued 26,335 commits before the first byte
            // moved - all of it ahead of any user-visible progress.
            database.UpsertFiles(destination, entries.Values.ToList());
            _log.Write("SQLite queue database initialized.");
            return database;
        }
        catch (Exception ex)
        {
            // Dispose the half-opened connection: leaving it to the finalizer keeps the corrupt file
            // (and its WAL sidecars) locked, which can block cleanup or a later repair.
            database?.Dispose();
            _log.Write($"SQLite database unavailable; JSON manifest remains the recovery source. {ex.Message}", "WARN");
            return null;
        }
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, token));
    }

    private void TryDatabase(Action action, string context)
    {
        try { action(); }
        catch (Exception ex) { _log.Write($"SQLite update failed for {context}; JSON manifest remains available: {ex.Message}", "WARN"); }
    }


    /// <summary>
    /// Puts the previous destination back after a copy that could not be verified. The old file is moved
    /// to .i-total\backup before the new one is committed, so a failed overwrite never destroys both
    /// copies; if the restore itself fails, the backup is kept and its path is logged.
    /// </summary>
    private void RestoreReplacedTarget(string target, string? backup)
    {
        if (backup is null || !File.Exists(backup))
        {
            try { File.Delete(target); } catch { }
            return;
        }
        try
        {
            if (File.Exists(target)) File.Delete(target);
            File.Move(backup, target);
            _log.Write($"Restored the previous destination after a failed overwrite: {target}", "WARN");
        }
        catch (Exception ex)
        {
            _log.Write($"The previous destination could not be restored from '{backup}': {ex.Message}. The backup is kept.", "ERROR");
        }
    }
    private static void TrySetTimestamps(string path, DateTimeOffset modified)
    {
        try { File.SetLastWriteTimeUtc(path, modified.UtcDateTime); } catch { }
    }

    private static bool IsInside(string path, string candidate)
    {
        string full = Path.GetFullPath(path).TrimEnd('\\') + "\\";
        string c = Path.GetFullPath(candidate);
        return c.StartsWith(full, StringComparison.OrdinalIgnoreCase);
    }

    private static void ExportReports(string destination, ScanResult scan, SessionManifest manifest, string status,
        VerificationLevel weakest, ReconciliationReport reconciliation)
    {
        // The report describes what happened, per object. The reconciliation is computed from the
        // persisted entries, not from the run's counters, so a resumed session reconciles the whole plan.
        var report = new
        {
            Product = "I-Total Transfer by Deveg",
            CreatedUtc = DateTimeOffset.UtcNow,
            Device = scan.DeviceName,
            Backend = manifest.BackendName,
            ScanStartedUtc = scan.StartedUtc,
            ScanFinishedUtc = scan.FinishedUtc,
            EnumerationStatus = scan.Status.ToString(),
            InventoryScope = scan.Scope,
            DiscoveredFiles = scan.Files.Count,
            DiscoveredBytes = scan.TotalBytes,
            Status = status,
            WeakestVerification = weakest.ToString(),
            Reconciliation = reconciliation,
            manifest.Entries
        };
        LogService.ExportJson(Path.Combine(destination, "I-total-session-report.json"), report);
        var csv = new StringBuilder("RelativePath,CommittedPath,ExpectedBytes,SizeKnown,BytesWritten,State,Outcome,Attempts,Verification,SourceSha256,DestinationSha256,Error\r\n");
        foreach (TransferEntry e in manifest.Entries)
            csv.Append(Csv(e.RelativePath)).Append(',').Append(Csv(e.CommittedPath ?? "")).Append(',').Append(e.ExpectedBytes).Append(',').Append(e.SizeKnown ? 1 : 0).Append(',')
               .Append(e.BytesWritten).Append(',').Append(e.State).Append(',').Append(e.Outcome).Append(',').Append(e.Attempts).Append(',')
               .Append(e.Verification).Append(',').Append(Csv(e.SourceSha256 ?? "")).Append(',')
               .Append(Csv(e.DestinationSha256 ?? "")).Append(',').Append(Csv(e.Error ?? "")).Append("\r\n");
        File.WriteAllText(Path.Combine(destination, "I-total-session-report.csv"), csv.ToString(), new UTF8Encoding(true));

        var summary = new StringBuilder("Metric,Value\r\n");
        foreach ((string metric, string value) in reconciliation.Rows())
            summary.Append(metric).Append(',').Append(value).Append("\r\n");
        File.WriteAllText(Path.Combine(destination, "I-total-reconciliation.csv"), summary.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):N2} GB" : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):N1} MB" : $"{bytes:N0} bytes";

    private static TransferProgress BuildProgress(string phase, int current, int total, long done, long all, long copied,
        Queue<(long Bytes, double Seconds)> rates, Stopwatch timer, string file, string message,
        int copiedCount = 0, int skippedCount = 0, int failedCount = 0, int unverifiableCount = 0)
    {
        double secs = timer.Elapsed.TotalSeconds;
        double speed = rates.Count == 0 ? 0 : rates.Sum(x => x.Bytes) / Math.Max(0.001, rates.Sum(x => x.Seconds));
        double overall = secs > 0 ? copied / secs : 0;
        double fps = secs > 0 ? current / secs : 0;
        TimeSpan? eta = overall > 0 && all > done ? TimeSpan.FromSeconds((all - done) / overall) : null;
        return new TransferProgress(phase, current, total, done, all, speed == 0 ? overall : speed, file, message)
        {
            FilesPerSecond = fps,
            Copied = copiedCount,
            Skipped = skippedCount,
            Failed = failedCount,
            Unverifiable = unverifiableCount,
            Elapsed = timer.Elapsed,
            Eta = eta
        };
    }
}
