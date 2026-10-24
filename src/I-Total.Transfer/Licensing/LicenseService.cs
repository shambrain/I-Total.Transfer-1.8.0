using System.Security.Cryptography;

namespace ITotalTransfer.Licensing;

/// <summary>
/// Owns the commercial decision: is a new job allowed, and does a finished job consume a trial use?
///
/// <para>
/// Three rules shape this class. First, the trial is measured in <i>meaningful completed jobs</i>, never in
/// launches or attempts, so a failed or cancelled transfer costs nothing. Second, licence checks happen only
/// when a <b>new</b> job is about to start; nothing here can stop, abort or corrupt a transfer that is
/// already running, and an unfinished session is always resumable even after the trial ends. Third, no
/// verification failure throws: a missing key, a bad signature, a refund or an unreadable store is reported
/// as a verdict and leaves the operator in the trial rather than in a broken application.
/// </para>
///
/// <para>
/// Persistence is deliberately redundant (file plus registry). Both copies are merged pessimistically, so
/// deleting one cannot reset the counter. The design is not DRM and does not pretend to be: it makes the
/// honest path easy and the trivial reset ineffective.
/// </para>
/// </summary>
public sealed class LicenseService : IDisposable
{
    private readonly LicenseOptions _options;
    private readonly ITrialStore _trialStore;
    private readonly ILicenseFileStore _licenseStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<string>? _log;
    private readonly object _sync = new();
    private readonly RSA? _verifier;
    private readonly SteamEntitlementProvider? _steam;

    private TrialLedgerState _trial;
    private EntitlementCheck? _entitlement;
    private string? _steamDetail;
    private bool _entitlementAttempted;
    private IReadOnlySet<string> _revoked = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _meteredWirelessSessions = new(StringComparer.Ordinal);
    private bool _clockRolledBack;
    private string? _storeProblem;

    public LicenseService(
        LicenseOptions options,
        ITrialStore trialStore,
        ILicenseFileStore licenseStore,
        Func<DateTimeOffset>? clock = null,
        SteamEntitlementProvider? steam = null,
        Action<string>? log = null)
    {
        _options = options;
        _trialStore = trialStore;
        _licenseStore = licenseStore;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _log = log;
        _verifier = options.CreateVerifier();
        _steam = steam ?? (options.SteamEditionDetected
            ? new SteamEntitlementProvider(options.SteamDirectory, options, _clock)
            : null);

        DateTimeOffset now = _clock();
        _trial = trialStore.Load() ?? new TrialLedgerState { FirstSeenUtc = now, LastSeenUtc = now };
        _trial = _trial.Normalized();
        if (_trial.FirstSeenUtc == default) _trial.FirstSeenUtc = now;
        if (_trial.LastSeenUtc < _trial.FirstSeenUtc) _trial.LastSeenUtc = _trial.FirstSeenUtc;
        NoteClock(now);
        if (now > _trial.LastSeenUtc) _trial.LastSeenUtc = now;
        Persist();

        RefreshEntitlement();
    }

    /// <summary>The application's own service: file + registry ledger, file entitlement store.</summary>
    public static LicenseService ForThisBuild(Action<string>? log = null) =>
        new(LicenseOptions.ForThisBuild(),
            new CompositeTrialStore(new FileTrialStore(), new RegistryTrialStore()),
            new FileLicenseStore(),
            log: log);

    public LicenseOptions Options => _options;

    /// <summary>Where an accepted entitlement is copied, shown in Settings so the file is never a mystery.</summary>
    public string LicenseFilePath => _licenseStore.Location;

    /// <summary>True when this build can verify a purchased entitlement at all.</summary>
    public bool CanVerifyEntitlements => _verifier is not null;

    public LicenseSnapshot Snapshot()
    {
        lock (_sync) return BuildSnapshot(_clock());
    }

    /// <summary>
    /// The gate for starting work. <paramref name="resuming"/> is always allowed: an unfinished session
    /// belongs to work the operator already paid for with a trial use or with time, and stranding its
    /// remaining files because the trial ran out would be a data-integrity problem, not a paywall.
    /// </summary>
    public LicenseDecision TryStartJob(bool resuming)
    {
        if (resuming)
            return LicenseDecision.Yes("Continuing an unfinished session is always allowed.");

        LicenseSnapshot snapshot = Snapshot();
        if (snapshot.CanStartJob) return LicenseDecision.Yes();

        string message = _options.TrialJobs <= 0
            ? "This build has no free trial jobs. Activate a licence to start a transfer."
            : $"The trial's {_options.TrialJobs} successful transfer jobs have been used. Activate a licence to start a new transfer.\n\n" +
              "An unfinished session can still be continued from the Overview.";
        if (!string.IsNullOrWhiteSpace(snapshot.Problem)) message += "\n\n" + snapshot.Problem;
        return LicenseDecision.No(message);
    }

    /// <summary>
    /// Records a finished job. Safe to call more than once for the same job: the second call reports
    /// <see cref="JobRecordResult.AlreadyRecorded"/> instead of consuming another use. Never throws.
    /// </summary>
    public JobRecordResult RecordJob(JobFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        lock (_sync)
        {
            DateTimeOffset now = _clock();
            NoteClock(now);
            if (now > _trial.LastSeenUtc) _trial.LastSeenUtc = now;

            if (!facts.IsMeaningful)
            {
                Persist();
                return new JobRecordResult(false, false, BuildSnapshot(now),
                    "Not counted: a trial job is used only by a completed transfer that wrote at least one file and had no failures.");
            }

            LicenseSnapshot current = BuildSnapshot(now);
            if (current.Standing == LicenseStanding.Licensed)
            {
                Persist();
                return new JobRecordResult(false, false, current,
                    "Licensed build: the job was recorded for diagnostics only and used no trial credit.");
            }

            string fingerprint = TrialLedgerState.Fingerprint(
                string.IsNullOrWhiteSpace(facts.JobId) ? "unidentified-" + now.UtcTicks : facts.JobId);

            if (_trial.HasConsumed(fingerprint))
            {
                Persist();
                return new JobRecordResult(false, true, BuildSnapshot(now),
                    "Already counted: a repeated report of the same finished job does not use another trial job.");
            }

            if (_trial.JobsUsed >= _options.TrialJobs)
            {
                Persist();
                return new JobRecordResult(false, false, BuildSnapshot(now),
                    "The trial was already finished; this completion added no further use.");
            }

            _trial.Consume(fingerprint, now);
            Persist();
            LicenseSnapshot after = BuildSnapshot(now);
            string detail = after.Standing == LicenseStanding.TrialExhausted
                ? "That was the trial's last job. Activate a licence to start another transfer; unfinished work can still be continued."
                : $"Trial job counted — {after.JobsRemaining} of {_options.TrialJobs} left.";
            return new JobRecordResult(true, false, after, detail);
        }
    }

    /// <summary>
    /// Verifies and stores an entitlement file. A rejected file leaves the current entitlement and the
    /// trial counter exactly as they were, and a file that verifies but cannot be copied is not reported as
    /// accepted.
    /// </summary>
    public LicenseActivationResult ActivateFromFile(string path)
    {
        string? text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex)
        {
            return new LicenseActivationResult(false, EntitlementVerdict.Malformed,
                "The licence file could not be read: " + ex.Message, Snapshot());
        }
        return ActivateText(text, path);
    }

    /// <summary>Verifies and stores an entitlement supplied as text (paste, or a file already read).</summary>
    public LicenseActivationResult ActivateText(string? text, string source)
    {
        lock (_sync)
        {
            EntitlementCheck check = EntitlementCodec.Verify(
                text, _verifier, _clock(), _options.Channel, _options.ProductName, _options.SteamAppId);

            if (!check.IsValid)
                return new LicenseActivationResult(false, check.Verdict,
                    check.Detail + "\n\nThe existing licence, if any, and the trial counter were left unchanged.", Snapshot());

            try { _licenseStore.Write(text!.Trim()); }
            catch (Exception ex)
            {
                return new LicenseActivationResult(false, EntitlementVerdict.Malformed,
                    "The entitlement verified, but it could not be saved to " + _licenseStore.Location +
                    ": " + ex.Message, Snapshot());
            }

            RefreshEntitlement();
            _log?.Invoke($"Licence accepted from {source}.");
            LicenseSnapshot snapshot = BuildSnapshot(_clock());
            return new LicenseActivationResult(snapshot.Standing == LicenseStanding.Licensed,
                check.Verdict, check.Detail, snapshot);
        }
    }

    /// <summary>
    /// Re-reads the stored entitlement and, on a Steam build, the Steam entitlement. The operator calls
    /// this after replacing a file or receiving a renewed licence; nothing runs on a timer.
    /// </summary>
    public LicenseSnapshot Refresh()
    {
        lock (_sync)
        {
            RefreshEntitlement();
            return BuildSnapshot(_clock());
        }
    }

    /// <summary>
    /// Applies a signed revocation list (refund, chargeback, stolen key). A malformed or unsigned list
    /// changes nothing; a valid one persists and revokes the ids it names. Returns how many ids it carried.
    /// </summary>
    public int ApplyRevocations(string? document)
    {
        lock (_sync)
        {
            IReadOnlySet<string> ids = EntitlementCodec.VerifyRevocations(document, _verifier);
            if (ids.Count == 0) return 0;
            var merged = new HashSet<string>(_revoked, StringComparer.Ordinal);
            merged.UnionWith(ids);
            _revoked = merged;
            try { _licenseStore.WriteRevocations(document!); }
            catch (Exception) { /* The in-memory decision still applies for this run. */ }
            return ids.Count;
        }
    }

    /// <summary>Removes the stored entitlement (QA and support). The trial ledger is untouched.</summary>
    public LicenseSnapshot RemoveEntitlement()
    {
        lock (_sync)
        {
            _licenseStore.Delete();
            RefreshEntitlement();
            return BuildSnapshot(_clock());
        }
    }

    /// <summary>
    /// Gate for the wireless receiver. One sender session (one redeemed pairing secret) costs at most one
    /// trial job, because the receiver protocol has no batch boundary: charging per file would punish a
    /// sender for sending a folder, and charging nothing would be a loophole. The session is remembered in
    /// memory only; a restart re-issues every pairing secret, so there is nothing durable to remember.
    /// </summary>
    public LicenseDecision TryStartWirelessUpload(string sessionToken)
    {
        lock (_sync)
        {
            LicenseSnapshot snapshot = BuildSnapshot(_clock());
            if (snapshot.CanStartJob) return LicenseDecision.Yes();
            if (_meteredWirelessSessions.Contains(WirelessKey(sessionToken))) return LicenseDecision.Yes();
            return LicenseDecision.No(
                $"The trial's {_options.TrialJobs} successful transfer jobs have been used. Activate a licence on the receiving PC to accept wireless uploads.\n\n" +
                "Files already received are unaffected.");
        }
    }

    /// <summary>
    /// Charges the sender's session once, after a file was committed and SHA-256 verified. A second file
    /// from the same session reports <see cref="JobRecordResult.AlreadyRecorded"/>.
    /// </summary>
    public JobRecordResult RecordWirelessReceipt(string sessionToken, string relativePath, long bytesWritten)
    {
        lock (_sync)
        {
            string key = WirelessKey(sessionToken);
            if (!_meteredWirelessSessions.Add(key))
                return new JobRecordResult(false, true, BuildSnapshot(_clock()),
                    "This sender session was already counted; the additional file used no trial credit.");

            JobRecordResult result = RecordJob(new JobFacts(
                "wireless:" + key, LicenseJobKind.WirelessReceive, true, 1, Math.Max(0, bytesWritten),
                FilesFailed: 0, Cancelled: false));
            if (!result.Counted)
            {
                // The upload was allowed because the session was metered, so it must stay metered even if
                // the ledger had no room left. Keeping the key prevents a later file being charged twice.
                _log?.Invoke("Wireless receipt for " + relativePath + ": " + result.Detail);
            }
            return result;
        }
    }

    private static string WirelessKey(string sessionToken) => "session-" + TrialLedgerState.Fingerprint(sessionToken ?? "");

    public void Dispose() => _verifier?.Dispose();

    private void RefreshEntitlement()
    {
        _entitlement = null;
        _steamDetail = null;
        _entitlementAttempted = false;
        _revoked = EntitlementCodec.VerifyRevocations(_licenseStore.ReadRevocations(), _verifier);

        string? stored = _licenseStore.Read();
        if (!string.IsNullOrWhiteSpace(stored))
        {
            _entitlementAttempted = true;
            _entitlement = EntitlementCodec.Verify(
                stored, _verifier, _clock(), _options.Channel, _options.ProductName, _options.SteamAppId);
            return;
        }

        if (_steam is null) return;

        _entitlementAttempted = true;
        SteamCheck check = _steam.Check();
        _steamDetail = check.Detail;
        // A detail-only check (no file) keeps the operator in the trial; an invalid entitlement is kept so
        // its verdict can be shown instead of a vague "trial" label.
        _entitlement = check.Entitlement;
    }

    private LicenseSnapshot BuildSnapshot(DateTimeOffset now)
    {
        if (_entitlement is { IsValid: true, Payload: { } payload })
        {
            if (_revoked.Contains(payload.LicenseId))
            {
                return TrialSnapshot(now,
                    $"Licence {EntitlementCodec.ShortId(payload.LicenseId)} was revoked by a signed revocation list (refund or chargeback). The trial rules apply again.");
            }

            string channel = _options.Channel == LicenseChannel.Steam ? "Steam" : "Direct";
            string validity = payload.NotAfterUtc is { } until
                ? $" Valid until {until.ToLocalTime():yyyy-MM-dd}."
                : " No expiry (one-time purchase).";
            return new LicenseSnapshot(
                LicenseStanding.Licensed,
                _options.Channel,
                _trial.JobsUsed,
                _options.TrialJobs,
                $"Licensed — {channel}",
                $"Licence {EntitlementCodec.ShortId(payload.LicenseId)} verified offline with this build's public key.{validity}",
                payload.LicenseId,
                payload.NotAfterUtc,
                null,
                now);
        }

        string? problem = null;
        if (_entitlement is { IsValid: false })
            problem = _entitlement.Detail;
        else if (_entitlementAttempted && !string.IsNullOrWhiteSpace(_steamDetail))
            problem = _steamDetail;

        return TrialSnapshot(now, problem);
    }

    private LicenseSnapshot TrialSnapshot(DateTimeOffset now, string? problem)
    {
        int used = _trial.JobsUsed;
        int limit = _options.TrialJobs;
        bool exhausted = used >= limit;
        string headline = exhausted
            ? "Trial finished"
            : $"Trial — {Math.Max(0, limit - used)} of {limit} transfer jobs left";
        string detail = exhausted
            ? limit <= 0
                ? "This build has no free trial jobs. Activate a licence to start a transfer. An unfinished session can still be continued."
                : $"The trial's {limit} successful transfer jobs have been used. Activate a licence to start a new transfer; an unfinished session can still be continued."
            : "A job is counted only when a transfer writes at least one file and finishes with no failures. Cancelling, failing, or finding every file already present costs nothing.";
        if (_clockRolledBack)
            detail += " The system clock is earlier than the last recorded run, so the counter was not reset.";
        if (!string.IsNullOrWhiteSpace(_storeProblem))
            detail += " " + _storeProblem;
        if (!string.IsNullOrWhiteSpace(problem))
            detail += " " + problem;

        return new LicenseSnapshot(
            exhausted ? LicenseStanding.TrialExhausted : LicenseStanding.Trial,
            LicenseChannel.None,
            used,
            limit,
            headline,
            detail,
            null,
            null,
            problem,
            now);
    }

    private void Persist()
    {
        try
        {
            _trialStore.Save(_trial);
            _storeProblem = null;
        }
        catch (Exception ex)
        {
            _storeProblem = "The trial counter could not be written (" + ex.Message + "); it is kept for this run.";
            _log?.Invoke("Trial ledger save failed: " + ex.Message);
        }
    }

    private void NoteClock(DateTimeOffset now)
    {
        _clockRolledBack = now < _trial.LastSeenUtc.AddHours(-24);
        if (!_clockRolledBack && _trial.LastSeenUtc < now) _trial.LastSeenUtc = now;
    }
}
