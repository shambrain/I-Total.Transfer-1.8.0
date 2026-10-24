namespace ITotalTransfer.Core;

/// <summary>
/// Cooperative pause/resume gate. The copy loop awaits <see cref="WaitWhilePausedAsync"/> between
/// files and while waiting, so a paused transfer never blocks the UI thread and resumes deterministically.
/// </summary>
public sealed class PauseController
{
    private volatile TaskCompletionSource _resume = CompletedSource();
    public bool IsPaused { get; private set; }

    private static TaskCompletionSource CompletedSource()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        _resume.TrySetResult();
    }

    public Task WaitWhilePausedAsync(CancellationToken token = default) =>
        IsPaused ? _resume.Task.WaitAsync(token) : Task.CompletedTask;
}
