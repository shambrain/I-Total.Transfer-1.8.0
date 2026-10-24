using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Reproduces the hang that aborted a whole test run, without a phone and without a UI.
///
/// <para>
/// A WinForms thread has a <see cref="SynchronizationContext"/> that only runs posted work while its
/// message pump is idle. The stress suite runs before the UI test classes in some orders and after them
/// in others; when a UI class had installed a WinForms context on the test thread, the blocking
/// <c>GetAwaiter().GetResult()</c> in <c>LargeLibraryStressTests</c> deadlocked — the work finished, but
/// its continuation waited in a queue nobody was pumping. The service entry points therefore start on
/// the thread pool, and this test pins that: with a pump-required context installed, a blocking caller
/// must still get its answer and must leave nothing queued behind.
/// </para>
/// </summary>
[TestClass]
public sealed class ContextFreeExecutionTests
{
    /// <summary>Behaves like a UI thread with no message loop running: work is queued, never executed.</summary>
    private sealed class MessagePumpContext : SynchronizationContext
    {
        private readonly List<SendOrPostCallback> _queued = new();

        public int Pending { get { lock (_queued) return _queued.Count; } }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_queued) _queued.Add(d);
        }
    }

    private sealed class RecordingProgress : IProgress<string>
    {
        private readonly List<string> _messages = new();
        public int Count { get { lock (_messages) return _messages.Count; } }
        public void Report(string value) { lock (_messages) _messages.Add(value); }
    }

    private static LogService Log()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-context", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new LogService(path);
    }

    [TestMethod]
    [Timeout(30_000)]
    public void ABlockingCallerWithAUiContextDoesNotDeadlockTheScan()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        var pump = new MessagePumpContext();
        SynchronizationContext.SetSynchronizationContext(pump);
        try
        {
            using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2_000);
            var service = new TransferService(backend, Log());
            var progress = new RecordingProgress();

            // No pump runs here. Before the fix this call never returned.
            ScanResult scan = service.ScanAsync(CancellationToken.None, progress).GetAwaiter().GetResult();

            Assert.AreEqual(2_000, scan.Files.Count);
            Assert.IsTrue(progress.Count > 0, "Progress must still reach the caller's own IProgress.");
            Assert.AreEqual(0, pump.Pending,
                "The service posted work back to the caller's UI context; a host without a pump would deadlock.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [TestMethod]
    [Timeout(30_000)]
    public void ABlockingCallerWithAUiContextCanAlsoCancelAndCopy()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new MessagePumpContext());
        try
        {
            using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 200);
            var service = new TransferService(backend, Log());
            string destination = Path.Combine(Path.GetTempPath(), "itotal-context-dest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);

            ScanResult scan = service.ScanAsync(CancellationToken.None, new RecordingProgress()).GetAwaiter().GetResult();
            TransferSummary summary = service
                .CopyAsync(scan, destination, maxRetries: 1, new NoOpProgress(), CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(200, summary.Total);
            Assert.AreEqual(200, summary.Copied, "A context-free copy must still copy every file.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), "itotal-context-dest"), recursive: true); }
            catch { /* best effort */ }
        }
    }

    [TestMethod]
    [Timeout(30_000)]
    public void CancellationFromABlockingCallerIsStillHonoured()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new MessagePumpContext());
        try
        {
            using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 5_000);
            var service = new TransferService(backend, Log());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            try
            {
                service.ScanAsync(cancellation.Token, new RecordingProgress()).GetAwaiter().GetResult();
                Assert.Fail("A cancelled scan must not report success.");
            }
            catch (OperationCanceledException) { /* the only acceptable outcome, and it must not hang */ }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class NoOpProgress : IProgress<TransferProgress>
    {
        public void Report(TransferProgress value) { }
    }
}
