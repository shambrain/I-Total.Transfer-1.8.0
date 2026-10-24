namespace ITotalTransfer.Services;

/// <summary>
/// Starts a library operation on a thread-pool thread so the caller's <see cref="SynchronizationContext"/>
/// cannot be captured by the awaits inside it.
///
/// <para>
/// An <c>await</c> captures the context that is current when it completes, and a UI context only runs
/// posted work when its message pump is free. A host that blocks waiting for the result instead of
/// pumping therefore deadlocks: the work is finished, but its continuation is sitting in the UI queue
/// behind the very frame that is waiting for it. This is not theoretical — a whole test run was aborted
/// inside <c>ScanningASingleFolderOfTwentySixThousandEntries…</c> because the UI test classes that ran
/// before it had installed a WinForms context on the test thread.
/// </para>
///
/// <para>
/// Starting on the pool removes the caller's context from the picture while leaving the caller's own
/// continuations exactly where the caller expects them, which is why every public entry point of
/// <see cref="TransferService"/> goes through here.
/// </para>
/// </summary>
internal static class ContextFree
{
    /// <summary>Runs <paramref name="work"/> on the thread pool and returns its unwrapped result.</summary>
    public static Task<T> Run<T>(Func<Task<T>> work, CancellationToken token) => Task.Run(work, token);
}
