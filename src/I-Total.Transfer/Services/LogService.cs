using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITotalTransfer.Services;

/// <summary>
/// Centralised structured logging. Lines are timestamped, level-filtered, optionally redacted, and the
/// file rolls over at a bounded size so a long backup cannot fill the destination volume.
/// </summary>
public sealed class LogService : IDisposable
{
    private static readonly string[] Levels = ["DEBUG", "INFO", "WARN", "ERROR"];
    private const long MaxBytes = 10L * 1024 * 1024;

    private readonly object _sync = new();
    private StreamWriter? _writer;
    private long _writtenBytes;
    public string LogPath { get; }
    public bool Redact { get; set; }
    public string MinimumLevel { get; set; } = "INFO";
    public event Action<string, string>? LineWritten;

    public LogService(string destination, bool redact = true, string minimumLevel = "INFO")
    {
        Directory.CreateDirectory(destination);
        LogPath = Path.Combine(destination, "I-total-transfer.log");
        Redact = redact;
        MinimumLevel = minimumLevel;
        try { _writtenBytes = new FileInfo(LogPath) is { Exists: true } info ? info.Length : 0; } catch { }
    }

    /// <summary>
    /// Appends one timestamped line, keeping the file open between writes.
    ///
    /// <para>
    /// The copy path logs a line per file, so a 26,000-item library writes tens of thousands of lines. This
    /// used to stat the file for rollover and then open, append and close it on every single line, which cost
    /// a measurable slice of the transfer's per-file budget for no benefit. The writer is now opened once and
    /// flushed per line (so a live transfer can still be watched with a tail), and rollover is decided from a
    /// running byte count instead of a stat.
    /// </para>
    /// </summary>
    public void Write(string message, string level = "INFO")
    {
        if (!ShouldLog(level)) return;
        string body = Redact ? RedactSensitive(message) : message;
        string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {body}";
        int bytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        lock (_sync)
        {
            try
            {
                if (_writtenBytes + bytes > MaxBytes) Roll();
                _writer ??= OpenWriter();
                _writer.WriteLine(line);
                _writtenBytes += bytes;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Log write failed: {ex.Message}"); }
        }
        LineWritten?.Invoke(line, level);
    }

    private StreamWriter OpenWriter() => new(
        // ReadWrite|Delete, not Read: the log lives in the destination folder, so the operator must stay
        // free to read, rename or delete that folder while the app is running. Holding the handle without
        // FileShare.Delete locked the whole destination against maintenance.
        new FileStream(LogPath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete),
        new UTF8Encoding(false)) { AutoFlush = true };

    private void Roll()
    {
        try
        {
            _writer?.Dispose();
            _writer = null;
            _writtenBytes = 0;
            if (File.Exists(LogPath))
            {
                string rolled = LogPath + ".1";
                if (File.Exists(rolled)) File.Delete(rolled);
                File.Move(LogPath, rolled);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            try { _writer?.Dispose(); } catch { }
            _writer = null;
        }
    }

    private bool ShouldLog(string level)
    {
        int min = Array.IndexOf(Levels, MinimumLevel);
        int actual = Array.IndexOf(Levels, level);
        if (actual < 0) actual = 1;
        return actual >= Math.Max(0, min);
    }

    /// <summary>
    /// Masks what must not leave the machine: device identifiers (a full Apple serial or UDID is a
    /// hardware identifier, and the diagnostics bundle promises in writing that it ships none), long
    /// hex digests, and credentials. Everything else is left exactly as written — an over-eager redactor
    /// that mangles ordinary log text is its own kind of dishonest.
    /// </summary>
    public static string RedactSensitive(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        message = HexLikeRegex.Replace(message, m => m.Value[..4] + "…REDACTED");
        message = LabelledIdentifierRegex.Replace(message, m =>
            PlaceholderName(m.Groups[2].Value) ? m.Value : m.Value[..(m.Groups[2].Index - m.Index)] + Mask(m.Groups[2].Value));
        message = SecretAssignmentRegex.Replace(message, "$1=…REDACTED");
        return message;
    }

    /// <summary>
    /// Keeps a short prefix so support can still tell two devices apart in a log, while the identifier
    /// itself never lands on disk.
    /// </summary>
    private static string Mask(string value) => value.Length <= 4 ? "…REDACTED" : value[..4] + "…REDACTED";

    /// <summary>
    /// An identifier that is absent is not sensitive; masking the word "unavailable" would only make the
    /// log harder to read.
    /// </summary>
    private static bool PlaceholderName(string value) => value.ToLowerInvariant() is
        "unavailable" or "unknown" or "none" or "null" or "empty" or "n/a" or "not-set" or "notavailable";

    public static void ExportJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    // Deliberately not [GeneratedRegex]. The regex source generator stamps a non-deterministic hash into
    // the generated type name, which made the published single-file executable differ between two builds
    // of the same sources in different directories. These patterns are small and evaluated only while a
    // log line is being redacted, so the static Regex keeps the build byte-reproducible.
    private static readonly Regex HexLikeRegex = new(@"\b[0-9a-fA-F]{40,64}\b");

    /// <summary>
    /// A labelled device identifier, in the forms this application and its reports actually write them:
    /// <c>Serial: FCC4L3VYCW</c>, <c>UDID=…</c>, <c>"DeviceStableId": "…"</c>. The separator is required, so
    /// ordinary prose that merely mentions the word (a folder named "Serial number photos") is untouched.
    /// </summary>
    private static readonly Regex LabelledIdentifierRegex = new(
        @"(?i)\b(serial(?:\s*(?:number|no\.?))?|udid|ecid|imei|meid|device[ _-]?stable[ _-]?id|stable[ _-]?id|device[ _-]?id)\b[""']?\s*[:=#]\s*[""']?([A-Za-z0-9][A-Za-z0-9\-]{5,})");

    /// <summary>
    /// The label <em>and its value</em>. The value used to be left in place — <c>password: hunter2</c> came
    /// back as <c>password=…REDACTED hunter2</c>, which reads as redacted and is not — so the assignment now
    /// consumes the value too.
    /// </summary>
    private static readonly Regex SecretAssignmentRegex = new(
        @"(?i)\b(pairing[_ ]?token|token|password|secret|apikey|api[_ ]?key|authorization)\b\s*[:=]\s*\S+");
}
