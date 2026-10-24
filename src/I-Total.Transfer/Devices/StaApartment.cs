using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ITotalTransfer.Devices;

/// <summary>
/// Runs every Shell COM call on one dedicated STA thread.
///
/// <para>
/// This is not a nicety. <c>Shell.Application</c> is a single-threaded-apartment object, and the
/// "This PC" folder only enumerates attached <em>portable devices</em> for an STA caller. When
/// <c>Shell.Application</c> is created on an MTA thread-pool thread — which is exactly where a
/// <c>Task.Run(...)</c> probe runs — <c>NameSpace(17).Items()</c> answers with the drive letters and
/// nothing else. The Shell backend therefore reported "No accessible Apple mobile device was found in
/// Windows Shell" for a handset that Explorer, and the STA presence check, could see perfectly well:
/// the app was looking at an empty list, not at a missing phone.
/// </para>
///
/// <para>
/// The worker also pumps messages, which Shell's asynchronous <c>CopyHere</c> expects on the calling
/// thread.
/// </para>
/// </summary>
internal sealed class StaApartment : IDisposable
{
    private const uint CoInitApartmentThreaded = 0x2;
    private const uint PmRemove = 0x0001;

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    // user32 exports these only with an A/W suffix, so the charset must be declared (no ExactSpelling).
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private volatile bool _disposed;

    public StaApartment()
    {
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "Shell-STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Pump()
    {
        CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        try
        {
            while (!_disposed || _queue.Count > 0)
            {
                // An exception escaping this thread would take the whole app down with it, so every
                // step is contained: this worker exists to keep COM happy, not to end the process.
                try { DrainMessages(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shell pump error: {ex}"); }

                if (_queue.TryTake(out Action? work, 40))
                {
                    try { work(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shell worker error: {ex}"); }
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shell worker ended: {ex}"); }
        finally { CoUninitialize(); }
    }

    /// <summary>Dispatches anything the shell posted back to this apartment without consuming the app's queue.</summary>
    private static void DrainMessages()
    {
        while (PeekMessage(out NativeMessage message, IntPtr.Zero, 0, 0, PmRemove) != 0)
            DispatchMessage(ref message);
    }

    public T Invoke<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (_disposed) throw new ObjectDisposedException(nameof(StaApartment));
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try { completion.SetResult(work()); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action work) => Invoke<object?>(() => { work(); return null; });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _queue.CompleteAdding(); } catch { }
    }
}
