using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITotalTransfer.Core;

/// <summary>
/// User-visible configuration. Every value is validated/clamped by <see cref="Normalize"/> so invalid
/// preferences can never destabilise the transfer engine. Persisted as JSON under %APPDATA%.
/// </summary>
public sealed class AppSettings
{
    // ---- Destination & layout ----
    public string Destination { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "I-Total iPhone Backup");
    public bool AutoOrganizeByDate { get; set; }
    public bool PreserveFolderStructure { get; set; } = true;
    public bool PreserveTimestamps { get; set; } = true;

    // ---- Direction & scope ----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransferDirection Direction { get; set; } = TransferDirection.DeviceToWindows;

    /// <summary>Comma/semicolon separated extension allow-list (empty = all). Example: ".heic;.jpg;.mov".</summary>
    public string IncludeExtensions { get; set; } = "";
    /// <summary>Comma/semicolon separated folder-name exclude list (matched case-insensitively on any segment).</summary>
    public string ExcludeFolders { get; set; } = "";
    /// <summary>Comma/semicolon separated filename substring exclude list.</summary>
    public string ExcludeNameContains { get; set; } = "";
    public bool ExcludeSidecars { get; set; }
    public long MinFileSizeBytes { get; set; }
    public long MaxFileSizeBytes { get; set; }

    // ---- Conflict & incrementality ----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConflictPolicy ConflictPolicy { get; set; } = ConflictPolicy.PreserveAndReport;
    public bool SkipUnchanged { get; set; } = true;
    public bool VerifyAfterCopy { get; set; } = true;

    // ---- Performance ----
    public int BufferSizeKb { get; set; } = 256;             // 64 / 128 / 256 / 512 / 1024
    public int MaxRetries { get; set; } = 5;                  // 1..10
    public int CopyTimeoutSeconds { get; set; } = 600;        // per-file base timeout

    /// <summary>
    /// The copy buffer in bytes. Reading this is what makes the Settings "copy buffer size" option real:
    /// the WPD copy path used to hard-code 1 MB and ignore the choice entirely.
    /// </summary>
    public int BufferSize => BufferBytes(BufferSizeKb);

    /// <summary>Single source for the buffer clamp, so the engine and the backends cannot disagree.</summary>
    public static int BufferBytes(int bufferSizeKb) => Math.Clamp(bufferSizeKb, 64, 1024) * 1024;
    /// <summary>
    /// Reserved. Every current backend talks to one device session, which serialises copies, so the engine
    /// always copies one file at a time and this value is pinned to 1. The Settings control is disabled and
    /// labelled with that reason. Kept so a future companion-app or wireless backend that <i>can</i> copy
    /// concurrently has somewhere to read its bound from.
    /// </summary>
    public int FileConcurrency { get; set; } = 1;
    public bool KeepAwake { get; set; } = true;

    // ---- Verification ----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerificationPolicy VerificationPolicy { get; set; } = VerificationPolicy.SourceAndDestinationHash;
    public bool ConfirmScanWithSecondPass { get; set; }

    // ---- Scan listing cache ----
    /// <summary>
    /// Keeps the last complete listing of each device on disk and shows it while a cold scan runs. A cold
    /// camera roll takes many minutes to enumerate, and the point of this switch is that the window is not
    /// empty for that whole time. It changes nothing about what is scanned or copied: the live scan still
    /// runs, still replaces the view, and a listing is only ever shown with the moment it was captured.
    /// Turning this off also stops new listings being written.
    /// </summary>
    public bool ReuseLastKnownLibrary { get; set; } = true;

    /// <summary>How old a cached listing may be before it is refused instead of shown. Clamped to 1..365.</summary>
    public int ScanCacheMaxAgeDays { get; set; } = 14;

    // ---- Interrupted-session recovery ----
    /// <summary>
    /// Identifies the unfinished session the operator chose not to continue, so the app stops offering
    /// it. It is the unfinished session's own fingerprint (destination, last update, size, outstanding
    /// count), never a device identity: any further progress, any new entry or a different destination
    /// produces a different value, so a dismissal can never hide a different problem.
    /// </summary>
    public string DismissedRecovery { get; set; } = "";

    // ---- Backend ----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BackendPreference Backend { get; set; } = BackendPreference.Auto;
    public bool AllowShellFallback { get; set; } = true;
    public bool AllowAfcBackend { get; set; } = true;

    /// <summary>
    /// Which phone to read when an iPhone and an Android phone are attached at the same time. Both are
    /// on the same portable-device bus, so "whichever Windows lists first" would not be a decision the
    /// operator made. The other family is still used when the preferred one is not attached.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HandsetPreference PreferredHandset { get; set; } = HandsetPreference.AppleFirst;

    // ---- Wireless ----
    public bool WirelessEnabled { get; set; }
    public int WirelessPort { get; set; } = 47821;
    public string WirelessBindAddress { get; set; } = "";
    public bool WirelessRequireTls { get; set; } = true;
    public int PairingTokenMinutes { get; set; } = 10;

    /// <summary>
    /// When true, files offered by a paired sender are written without waiting for the operator.
    /// Off by default: the receiver shows what is about to be written and waits for an explicit
    /// approval, so a paired client cannot choose file names on the operator's disk unattended.
    /// </summary>
    public bool WirelessAutoApprove { get; set; }

    // ---- UI ----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.Neon;
    public string LogLevel { get; set; } = "INFO";
    public bool ShowCompletionDialog { get; set; } = true;
    public bool RedactSensitiveData { get; set; } = true;

    /// <summary>Plays the short synthesised ticks and chimes for buttons and notifications.</summary>
    public bool PlayUiSounds { get; set; } = true;

    /// <summary>Keeps the window always on top of other windows.</summary>
    public bool AlwaysOnTop { get; set; }

    /// <summary>Clamps every field into a safe range and repopulates derived/empty values.</summary>
    public AppSettings Normalize()
    {
        BufferSizeKb = Math.Clamp(BufferSizeKb switch
        {
            <= 64 => 64,
            <= 128 => 128,
            <= 256 => 256,
            <= 512 => 512,
            _ => 1024
        }, 64, 1024);
        MaxRetries = Math.Clamp(MaxRetries, 1, 10);
        CopyTimeoutSeconds = Math.Clamp(CopyTimeoutSeconds, 30, 7200);
        ScanCacheMaxAgeDays = Math.Clamp(ScanCacheMaxAgeDays, 1, 365);
        FileConcurrency = Math.Clamp(FileConcurrency, 1, 4);
        WirelessPort = Math.Clamp(WirelessPort, 1024, 65535);
        PairingTokenMinutes = Math.Clamp(PairingTokenMinutes, 1, 120);
        MinFileSizeBytes = Math.Max(0, MinFileSizeBytes);
        MaxFileSizeBytes = MaxFileSizeBytes <= 0 ? 0 : Math.Max(MinFileSizeBytes, MaxFileSizeBytes);
        if (string.IsNullOrWhiteSpace(Destination))
            Destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "I-Total iPhone Backup");
        LogLevel = string.IsNullOrWhiteSpace(LogLevel) ? "INFO" : LogLevel.Trim().ToUpperInvariant();
        return this;
    }

    public static AppSettings Defaults() => new AppSettings().Normalize();

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(
        JsonSerializer.Serialize(this, JsonOpts)) ?? Defaults();

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public enum AppTheme
{
    /// <summary>Deep navy surfaces with a cyan&#8594;blue&#8594;violet&#8594;magenta accent ramp. The default.</summary>
    Neon,

    /// <summary>Neutral near-black greys with the brand blue accent.</summary>
    Slate,

    /// <summary>Light office palette.</summary>
    Light,

    /// <summary>Follows the Windows "apps use light/dark" personalization setting.</summary>
    System,

    /// <summary>Legacy alias for <see cref="Neon"/>, still accepted when reading older settings files.</summary>
    Dark
}

public enum BackendPreference { Auto, Wpd, Afc, Shell }

/// <summary>
/// Which family to read first when both are attached. Both values still fall back to the other family
/// when the preferred one is absent, because refusing to read the only phone attached would be worse
/// than reading the one the operator did not name.
/// </summary>
public enum HandsetPreference { AppleFirst, AndroidFirst }

/// <summary>Immutable, validated snapshot handed to the transfer engine for one run.</summary>
public sealed record TransferOptions
{
    public TransferDirection Direction { get; init; } = TransferDirection.DeviceToWindows;
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.PreserveAndReport;
    public VerificationPolicy VerificationPolicy { get; init; } = VerificationPolicy.SourceAndDestinationHash;
    public bool SkipUnchanged { get; init; } = true;
    public bool PreserveTimestamps { get; init; } = true;
    public bool PreserveFolderStructure { get; init; } = true;
    public bool AutoOrganizeByDate { get; init; }
    public int BufferSizeKb { get; init; } = 256;
    public int MaxRetries { get; init; } = 5;
    public int CopyTimeoutSeconds { get; init; } = 600;
    public int FileConcurrency { get; init; } = 1;

    public int BufferSize => AppSettings.BufferBytes(BufferSizeKb);

    public static TransferOptions FromSettings(AppSettings s) => new()
    {
        Direction = s.Direction,
        ConflictPolicy = s.ConflictPolicy,
        VerificationPolicy = s.VerificationPolicy,
        SkipUnchanged = s.SkipUnchanged,
        PreserveTimestamps = s.PreserveTimestamps,
        PreserveFolderStructure = s.PreserveFolderStructure,
        AutoOrganizeByDate = s.AutoOrganizeByDate,
        BufferSizeKb = s.BufferSizeKb,
        MaxRetries = s.MaxRetries,
        CopyTimeoutSeconds = s.CopyTimeoutSeconds,
        FileConcurrency = s.FileConcurrency
    };
}
