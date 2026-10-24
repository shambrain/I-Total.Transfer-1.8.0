namespace ITotalTransfer.Licensing;

/// <summary>How a licence was obtained. The channel decides which verifier may accept it.</summary>
public enum LicenseChannel
{
    /// <summary>No entitlement has been accepted yet.</summary>
    None,

    /// <summary>A signed entitlement bought directly, verified against the build's embedded public key.</summary>
    Direct,

    /// <summary>A signed entitlement issued for the Steam edition, bound to a Steam app id.</summary>
    Steam
}

/// <summary>What the operator is currently entitled to do.</summary>
public enum LicenseStanding
{
    /// <summary>The free trial is running and at least one job remains.</summary>
    Trial,

    /// <summary>The trial's jobs have all been used and no licence has been accepted.</summary>
    TrialExhausted,

    /// <summary>A verified entitlement is in force.</summary>
    Licensed
}

/// <summary>The three ways a successful job can start. Used only for diagnostics and the ledger.</summary>
public enum LicenseJobKind { DeviceToWindows, WindowsToDevice, WirelessReceive }

/// <summary>
/// Outcome of verifying one signed entitlement. Every failure has its own value so the operator can be
/// told which one happened instead of a generic "invalid licence".
/// </summary>
public enum EntitlementVerdict
{
    Valid,
    Expired,
    NotYetValid,
    Refunded,
    Revoked,
    WrongProduct,
    WrongChannel,
    Malformed,
    SignatureInvalid,

    /// <summary>This build carries no public key, so it cannot verify a direct entitlement at all.</summary>
    NotConfigured,

    /// <summary>The Steam edition was detected but no usable Steam entitlement was found.</summary>
    ProviderUnavailable
}

/// <summary>
/// The signed facts of a purchase. The app never creates one: it only verifies envelopes produced by the
/// licence backend, so no private key is shipped in the executable.
/// </summary>
public sealed class EntitlementPayload
{
    public int SchemaVersion { get; set; } = 1;
    public string Product { get; set; } = "I-Total Transfer";

    /// <summary>Opaque purchase identifier. Shown masked; it is not a person's name or address.</summary>
    public string LicenseId { get; set; } = "";

    /// <summary>"direct" or "steam". Kept as a string so an unknown value is rejected instead of coerced.</summary>
    public string Channel { get; set; } = "direct";

    public DateTimeOffset IssuedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional start of validity. A missing value means "valid immediately".</summary>
    public DateTimeOffset? NotBeforeUtc { get; set; }

    /// <summary>
    /// Optional end of validity. None means a perpetual one-time purchase, which is the initial business
    /// model; the field exists so a time-limited or subscription entitlement needs no format change.
    /// </summary>
    public DateTimeOffset? NotAfterUtc { get; set; }

    /// <summary>Set by the issuer when the purchase was refunded or charged back.</summary>
    public bool Refunded { get; set; }

    /// <summary>Steam id the entitlement is bound to, when the channel is <c>steam</c>.</summary>
    public string? SteamId { get; set; }

    /// <summary>Steam app id the entitlement is bound to, when the channel is <c>steam</c>.</summary>
    public string? SteamAppId { get; set; }

    /// <summary>Free-form note from the issuer (invoice reference, support ticket). Never required.</summary>
    public string? Note { get; set; }
}

/// <summary>The result of reading and verifying one entitlement envelope.</summary>
public sealed record EntitlementCheck(EntitlementVerdict Verdict, EntitlementPayload? Payload, string Detail)
{
    public bool IsValid => Verdict == EntitlementVerdict.Valid;

    public static EntitlementCheck Fail(EntitlementVerdict verdict, string detail) =>
        new(verdict, null, detail);
}

/// <summary>Everything the UI needs to describe the current entitlement without re-reading a file.</summary>
public sealed record LicenseSnapshot(
    LicenseStanding Standing,
    LicenseChannel Channel,
    int JobsUsed,
    int JobLimit,
    string Headline,
    string Detail,
    string? LicenseId = null,
    DateTimeOffset? ValidUntilUtc = null,
    string? Problem = null,
    DateTimeOffset? CheckedUtc = null)
{
    /// <summary>True when a new transfer may be started. An unfinished session is always resumable.</summary>
    public bool CanStartJob => Standing is LicenseStanding.Trial or LicenseStanding.Licensed;

    public int JobsRemaining => Math.Max(0, JobLimit - JobsUsed);

    /// <summary>The licence id reduced to a prefix, so a screenshot does not publish the whole purchase id.</summary>
    public string LicenseIdShort => string.IsNullOrWhiteSpace(LicenseId)
        ? ""
        : LicenseId.Length <= 8 ? LicenseId : LicenseId[..8] + "…";
}

/// <summary>Answer to "may this job start?". Never used to stop a job that is already running.</summary>
public sealed record LicenseDecision(bool Allowed, string Message)
{
    public static LicenseDecision Yes(string message = "") => new(true, message);
    public static LicenseDecision No(string message) => new(false, message);
}

/// <summary>
/// The facts of a finished job. Only a completed, uncancelled transfer that actually wrote at least one
/// byte is a "meaningful" job; failed, cancelled, empty and already-present-only runs are free.
/// </summary>
public sealed record JobFacts(
    string JobId,
    LicenseJobKind Kind,
    bool Completed,
    int FilesCopied,
    long BytesCopied,
    int FilesFailed,
    bool Cancelled)
{
    public bool IsMeaningful =>
        Completed && !Cancelled && FilesFailed == 0 && FilesCopied > 0 && BytesCopied > 0;
}

/// <summary>What recording a finished job changed.</summary>
public sealed record JobRecordResult(bool Counted, bool AlreadyRecorded, LicenseSnapshot Snapshot, string Detail);

/// <summary>Outcome of trying to activate an entitlement file or text.</summary>
public sealed record LicenseActivationResult(bool Accepted, EntitlementVerdict Verdict, string Message, LicenseSnapshot Snapshot);
