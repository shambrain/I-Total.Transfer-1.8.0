using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ITotalTransfer.Devices.Wpd;

/// <summary>
/// Runs every WPD COM call on a single dedicated MTA thread. WPD requires an MTA apartment, and
/// serializing all calls on one thread both satisfies that requirement and guarantees the
/// single-active-operation-per-device invariant.
/// </summary>
internal sealed class ComApartment : IDisposable
{
    private const uint CoInitMultithreaded = 0x0;

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private volatile bool _disposed;

    public ComApartment()
    {
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "WPD-MTA"
        };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    private void Pump()
    {
        CoInitializeEx(IntPtr.Zero, CoInitMultithreaded);
        try
        {
            foreach (Action work in _queue.GetConsumingEnumerable())
            {
                try { work(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"WPD worker error: {ex}"); }
            }
        }
        finally { CoUninitialize(); }
    }

    public T Invoke<T>(Func<T> work)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ComApartment));
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action work) => Invoke<object?>(() => { work(); return null; });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _queue.CompleteAdding(); } catch { }
    }
}
