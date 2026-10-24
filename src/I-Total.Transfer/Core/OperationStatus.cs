namespace ITotalTransfer.Core;

/// <summary>
/// Which phase the app is busy with. Scanning and transferring are deliberately separate values: a scan
/// walks the device and a transfer moves bytes, so one percentage must never be presented as if it
/// covered both.
/// </summary>
public enum OperationPhase
{
    Idle,
    Connecting,
    Scanning,
    Transferring,
    Paused,
    Failed,
    /// <summary>
    /// The pass ran to the end but could not be declared complete - for example, files whose source
    /// size the phone refused to report were copied and left unverified. This is not a cancellation and
    /// not a crash, so it must not be labelled "Stopped"; it needs the operator's attention.
    /// </summary>
    Incomplete
}

/// <summary>
/// A snapshot of the single operation the app is running, used by the Overview's status band.
///
/// <para>
/// <see cref="Completed"/> and <see cref="Total"/> are in whatever unit the caller measures (the
/// transfer engine reports bytes, which routinely exceeds <see cref="int.MaxValue"/> for a real iPhone
/// library), so the band never reads them directly — it reads <see cref="Permille"/>, a 0–1000
/// normalised position. While the total is unknown (<see cref="Indeterminate"/>) the band shows a
/// sweeping indicator rather than inventing a figure.
/// </para>
/// </summary>
public sealed record OperationStatus(
    OperationPhase Phase,
    string Detail,
    long Completed = 0,
    long Total = 0,
    bool Indeterminate = true)
{
    /// <summary>Nothing running.</summary>
    public static OperationStatus Idle { get; } =
        new(OperationPhase.Idle, "Nothing is running. Scan the device, then start a transfer.",
            0, 0, Indeterminate: false);

    /// <summary>True while an operation is actually in flight.</summary>
    public bool IsBusy => Phase is not (OperationPhase.Idle or OperationPhase.Failed or OperationPhase.Incomplete);

    /// <summary>Bar position as permille of the known total; 0 when no total is known.</summary>
    public int Permille => Total <= 0 ? 0 : (int)Math.Clamp(1000.0 * Completed / Total, 0, 1000);

    /// <summary>Short heading for the band.</summary>
    public string Heading => Phase switch
    {
        OperationPhase.Connecting => "Connecting",
        OperationPhase.Scanning => "Scanning",
        OperationPhase.Transferring => "Transferring",
        OperationPhase.Paused => "Paused",
        OperationPhase.Failed => "Failed",
        OperationPhase.Incomplete => "Needs attention",
        _ => "Current operation"
    };

    /// <summary>
    /// True when a real, measured total exists and the band may therefore show a determinate bar.
    /// A sweep is shown in every other case, including a phase that is busy but unmeasured.
    /// </summary>
    public bool HasMeasuredTotal => Total > 0 && !Indeterminate;
}
