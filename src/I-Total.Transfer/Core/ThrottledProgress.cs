namespace ITotalTransfer.Core;

/// <summary>
/// Coalesces a fast stream of progress updates so the consumer is polled at a bounded rate instead of once
/// per file.
///
/// <para>
/// The transfer engine reports after every file, which is the right place to measure and the wrong place to
/// repaint: a 26,000-item library would post 26,000 updates to the UI thread, each repainting a page and
/// re-rendering the Overview's operation band. Nothing is lost by dropping the intermediate values, because
/// the terminal state of a run is set from the returned summary (<c>SetSummary</c>) and from the explicit
/// completion, cancellation and failure paths — this class only paces the live view.
/// </para>
///
/// <para>
/// Reports can arrive from a worker thread, so the decision is made under a lock. The clock is injectable so
/// the pacing can be tested without sleeping.
/// </para>
/// </summary>
public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minimumInterval;
    private readonly Func<long> _milliseconds;

    private readonly object _sync = new();
    private long _lastForwardedMilliseconds = long.MinValue;
    private int _forwarded;
    private int _dropped;

    public ThrottledProgress(IProgress<T> inner, TimeSpan minimumInterval, Func<long>? milliseconds = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (minimumInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        _minimumInterval = minimumInterval;
        _milliseconds = milliseconds ?? (() => Environment.TickCount64);
    }

    /// <summary>Updates actually handed to the wrapped progress.</summary>
    public int Forwarded { get { lock (_sync) return _forwarded; } }

    /// <summary>Updates coalesced away.</summary>
    public int Dropped { get { lock (_sync) return _dropped; } }

    public void Report(T value)
    {
        lock (_sync)
        {
            long now = _milliseconds();
            if (_lastForwardedMilliseconds != long.MinValue &&
                now - _lastForwardedMilliseconds < (long)_minimumInterval.TotalMilliseconds)
            {
                _dropped++;
                return;
            }
            _lastForwardedMilliseconds = now;
            _forwarded++;
        }

        // Handed on outside the lock: the wrapped progress ends up posting to a UI message queue, and
        // holding this lock across that call would make the engine wait on the UI thread.
        _inner.Report(value);
    }
}
