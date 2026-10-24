namespace ITotalTransfer.Core;

/// <summary>
/// Throttles scan progress so a slow device transport still feels alive.
///
/// <para>
/// An iPhone camera-roll album can hold tens of thousands of objects, and MTP answers metadata at
/// roughly 25–30 ms per object — a measured 26,320-item folder takes about twelve minutes to enumerate
/// the first time, and that cost is the device's, not the app's. Reporting every item would post
/// thousands of messages to the UI thread; reporting only per folder leaves the screen frozen for ten
/// minutes, which reads as a hang. This reports at a few messages a second, and always when the walk
/// moves to a new folder so the operator can see where it is.
/// </para>
/// </summary>
public sealed class ScanProgressReporter
{
    /// <summary>Default gap between reports. Slow enough to stay cheap, fast enough to look live.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan _interval;
    private long _lastReportedTicks = long.MinValue / 2;
    private string _lastFolder = "";

    public ScanProgressReporter(TimeSpan? interval = null) => _interval = interval ?? DefaultInterval;

    /// <summary>True when a report is due for this folder at this elapsed time.</summary>
    public bool ShouldReport(string? folder, TimeSpan elapsed)
    {
        string current = folder ?? "";
        if (!string.Equals(current, _lastFolder, StringComparison.Ordinal))
        {
            _lastFolder = current;
            _lastReportedTicks = elapsed.Ticks;
            return true;
        }
        if (elapsed.Ticks - _lastReportedTicks < _interval.Ticks) return false;
        _lastReportedTicks = elapsed.Ticks;
        return true;
    }

    /// <summary>Human-readable progress line: where the walk is, how much it has found, and how fast.</summary>
    public static string Describe(string? folder, int files, long bytes, TimeSpan elapsed)
    {
        string where = string.IsNullOrWhiteSpace(folder) ? CameraRoll.RootLabel : folder!;
        double seconds = elapsed.TotalSeconds;
        string rate = files > 0 && seconds >= 0.5 ? $" · {files / seconds:N0} files/s" : "";
        return $"Scanning {where} · {files:N0} files · {MediaSummary.FormatBytes(bytes)}{rate} · {elapsed:mm\\:ss} elapsed";
    }
}
