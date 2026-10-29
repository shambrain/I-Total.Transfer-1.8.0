using System.Runtime.InteropServices;

namespace ITotalTransfer.Devices;

/// <summary>
/// Whether retrying a failed copy can plausibly succeed. Retrying a permission denial or a missing
/// object three times only multiplies the delay and makes the log read like an unstable device; those
/// failures are permanent for the run. Timeouts, busy objects and transient I/O errors are retried.
/// </summary>
public enum CopyFailureKind
{
    None = 0,
    Transient = 1,
    Permanent = 2
}

public static class CopyFailures
{
    /// <summary>Classifies an exception thrown while staging or committing one file.</summary>
    public static CopyFailureKind Classify(Exception? ex) => ex switch
    {
        null => CopyFailureKind.Transient,
        UnauthorizedAccessException => CopyFailureKind.Permanent,
        FileNotFoundException => CopyFailureKind.Permanent,
        DirectoryNotFoundException => CopyFailureKind.Permanent,
        NotSupportedException => CopyFailureKind.Permanent,
        ArgumentException => CopyFailureKind.Permanent,
        TimeoutException => CopyFailureKind.Transient,
        COMException com when IsPermanentWin32(com.HResult) => CopyFailureKind.Permanent,
        IOException io when IsPermanentWin32(io.HResult) => CopyFailureKind.Permanent,
        IOException io when MentionsMissingSource(io) => CopyFailureKind.Permanent,
        _ => CopyFailureKind.Transient
    };

    /// <summary>AFC status codes that will not change without user action or a different device state.</summary>
    internal static CopyFailureKind ClassifyAfcStatus(int status) => status switch
    {
        7 or 8 or 9 or 10 or 15 or 18 => CopyFailureKind.Permanent, // invalid arg, not found, is dir, denied, unsupported, no space
        _ => CopyFailureKind.Transient
    };

    private static bool IsPermanentWin32(int hresult)
    {
        int code = hresult & 0xFFFF;
        return code is 2   // ERROR_FILE_NOT_FOUND
            or 3           // ERROR_PATH_NOT_FOUND
            or 5           // ERROR_ACCESS_DENIED
            or 87          // ERROR_INVALID_PARAMETER
            or 123         // ERROR_INVALID_NAME
            or 0x48F;      // ERROR_DEVICE_NOT_CONNECTED - retrying cannot bring the device back
    }

    private static bool MentionsMissingSource(IOException ex) =>
        ex.Message.Contains("no longer accessible", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("disappeared", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
