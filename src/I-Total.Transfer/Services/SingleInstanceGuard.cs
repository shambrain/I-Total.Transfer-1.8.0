using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ITotalTransfer.Services;

/// <summary>
/// A per-user, per-session single-instance guard. The application reads one phone at a time by design,
/// but nothing used to stop a second copy from starting and sharing the WPD provider, which made one
/// copy abort with "the requested resource is in use" (KI-27). The second copy now hands control back
/// to the first instead.
///
/// <para>
/// The mutex is <c>Local\</c>, so it is scoped to the signed-in Windows session rather than the whole
/// machine; a fast user switch does not collide with another user's copy. A crashed previous instance
/// leaves an abandoned mutex, which the next launch acquires normally.
/// </para>
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultName = @"Local\I-Total.Transfer.SingleInstance";

    private Mutex? _mutex;

    /// <summary>True when this process owns the guard and should run.</summary>
    public bool IsFirstInstance { get; private set; }

    /// <summary>The mutex name this guard used (useful in diagnostics and tests).</summary>
    public string Name { get; }

    public SingleInstanceGuard(string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name;
        try
        {
            _mutex = new Mutex(initiallyOwned: false, Name);
            try
            {
                IsFirstInstance = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner died without releasing it. Ownership is granted to us.
                IsFirstInstance = true;
            }
            if (!IsFirstInstance)
            {
                _mutex.Dispose();
                _mutex = null;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // A same-user Local mutex should always be accessible. Fail closed: do not start a second
            // copy that could contend for the phone.
            _mutex?.Dispose();
            _mutex = null;
            IsFirstInstance = false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            _mutex?.Dispose();
            _mutex = null;
            IsFirstInstance = false;
        }
    }

    /// <summary>
    /// Brings the already-running copy's main window forward. Returns false when no other window could
    /// be found, so the caller can still tell the user that the app is already running.
    /// </summary>
    public static bool ActivateExistingWindow()
    {
        foreach (Process process in Process.GetProcessesByName("I-Total.Transfer"))
        {
            if (process.Id == Environment.ProcessId) continue;
            IntPtr handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                process.Refresh();
                handle = process.MainWindowHandle;
            }
            if (handle == IntPtr.Zero) continue;
            ShowWindowAsync(handle, SwRestore);
            return SetForegroundWindow(handle);
        }
        return false;
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    public void Dispose()
    {
        if (_mutex is null) return;
        try
        {
            if (IsFirstInstance) _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Released from a different thread than the one that acquired it; closing the handle is
            // still enough for the next process to acquire it (as an abandoned mutex).
        }
        _mutex.Dispose();
        _mutex = null;
        IsFirstInstance = false;
    }
}
