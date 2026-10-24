using System.Diagnostics;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Measures what the application itself controls: staging a payload, hashing it while it streams, committing
/// it atomically, and persisting the manifest. The source is a deterministic in-memory backend, so these
/// numbers describe the <em>engine</em> and the destination disk - never the phone.
///
/// <para>
/// This is the measurement that decides whether tuning the engine is worth doing at all. If the engine
/// sustains hundreds of MB/s on local storage while a real camera roll copies at a few MB/s, then the device
/// leg is the whole story and further engine work would be optimisation theatre. The figures are printed
/// (<c>dotnet test --logger "console;verbosity=detailed"</c>) and the assertions are deliberately generous:
/// they exist to catch a catastrophic regression such as accidentally hashing the payload twice, not to
/// benchmark the build machine.
/// </para>
/// </summary>
[TestClass]
public sealed class EngineThroughputBenchmarkTests
{
    public TestContext TestContext { get; set; } = null!;

    private static LogService Log(string name)
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-bench", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new LogService(path);
    }

    private static string Destination(string name)
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-bench-dest", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CountingProgress : IProgress<TransferProgress>
    {
        private int _reports;
        public int Reports => Volatile.Read(ref _reports);
        public void Report(TransferProgress value) => Interlocked.Increment(ref _reports);
    }

    [TestMethod]
    [Timeout(300_000)]
    public void ManySmallFilesSustainAReasonableFileRate()
    {
        const int fileCount = 4_000;
        const int bytesPerFile = 8 * 1024;
        const long payload = (long)fileCount * bytesPerFile;

        string destination = Destination("small");
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount, bytesPerFile);
        var service = new TransferService(backend, Log("small"));
        ScanResult scan = service.ScanAsync(CancellationToken.None).GetAwaiter().GetResult();

        var stopwatch = Stopwatch.StartNew();
        TransferSummary summary = service
            .CopyAsync(scan, destination, maxRetries: 1, new CountingProgress(), CancellationToken.None)
            .GetAwaiter().GetResult();
        stopwatch.Stop();

        double filesPerSecond = fileCount / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        double megabytesPerSecond = payload / 1048576.0 / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        TestContext.WriteLine(
            $"ENGINE-BENCH small-files: {fileCount:N0} files x {bytesPerFile / 1024} KiB = " +
            $"{payload / 1048576.0:N1} MiB in {stopwatch.Elapsed.TotalSeconds:N2} s " +
            $"= {filesPerSecond:N0} files/s, {megabytesPerSecond:N1} MiB/s " +
            $"(copied {summary.Copied:N0}, skipped {summary.Skipped:N0}, failed {summary.Failed:N0}, " +
            $"level {summary.WeakestLevel})");

        Assert.IsTrue(summary.Completed, "The benchmark must actually complete: " + summary.Status);
        Assert.AreEqual(fileCount, summary.Copied);
        Assert.AreEqual(0, summary.Failed);
        Assert.AreEqual(0, summary.Unverifiable, "A source-hashing backend must leave nothing unverified.");
        Assert.IsTrue(filesPerSecond > 50,
            $"The engine managed only {filesPerSecond:N0} files/s; that is a regression, not a slow machine.");
        Directory.Delete(destination, recursive: true);
    }

    [TestMethod]
    [Timeout(300_000)]
    public void LargeFilesSustainAReasonableByteRate()
    {
        const int fileCount = 4;
        const int bytesPerFile = 32 * 1024 * 1024;
        const long payload = (long)fileCount * bytesPerFile;

        string destination = Destination("large");
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount, bytesPerFile);
        var service = new TransferService(backend, Log("large"));
        ScanResult scan = service.ScanAsync(CancellationToken.None).GetAwaiter().GetResult();

        var stopwatch = Stopwatch.StartNew();
        TransferSummary summary = service
            .CopyAsync(scan, destination, maxRetries: 1, new CountingProgress(), CancellationToken.None)
            .GetAwaiter().GetResult();
        stopwatch.Stop();

        double megabytesPerSecond = payload / 1048576.0 / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        TestContext.WriteLine(
            $"ENGINE-BENCH large-files: {fileCount} files x {bytesPerFile / 1048576} MiB = " +
            $"{payload / 1048576.0:N0} MiB in {stopwatch.Elapsed.TotalSeconds:N2} s = {megabytesPerSecond:N1} MiB/s " +
            $"(copied {summary.Copied}, failed {summary.Failed}, level {summary.WeakestLevel})");

        Assert.IsTrue(summary.Completed, "The benchmark must actually complete: " + summary.Status);
        Assert.AreEqual(fileCount, summary.Copied);
        Assert.AreEqual(0, summary.Failed);
        // A staged read-back was removed because it cost a second full pass over the payload. If that ever
        // comes back, this floor is what notices.
        Assert.IsTrue(megabytesPerSecond > 20,
            $"The engine managed only {megabytesPerSecond:N1} MiB/s on local storage; a full extra pass over " +
            "the payload looks like it has returned.");
        Directory.Delete(destination, recursive: true);
    }

    [TestMethod]
    [Timeout(300_000)]
    public void ProgressReportingIsBoundedRatherThanOneUpdatePerFile()
    {
        const int fileCount = 4_000;
        string destination = Destination("progress");
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount, 1024);
        var service = new TransferService(backend, Log("progress"));
        ScanResult scan = service.ScanAsync(CancellationToken.None).GetAwaiter().GetResult();

        var raw = new CountingProgress();
        TransferSummary summary = service
            .CopyAsync(scan, destination, maxRetries: 1, raw, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Now the same copy as the UI actually receives it: paced, so the message queue is not flooded.
        string pacedDestination = Destination("progress-paced");
        var paced = new CountingProgress();
        var throttle = new ThrottledProgress<TransferProgress>(paced, TimeSpan.FromMilliseconds(250));
        TransferSummary pacedSummary = service
            .CopyAsync(scan, pacedDestination, maxRetries: 1, throttle, CancellationToken.None)
            .GetAwaiter().GetResult();
        TestContext.WriteLine(
            $"ENGINE-BENCH progress: engine emits {raw.Reports:N0} updates for {fileCount:N0} files; " +
            $"the UI receives {throttle.Forwarded:N0} (dropped {throttle.Dropped:N0})");

        Assert.AreEqual(fileCount, summary.Copied);
        Assert.AreEqual(fileCount, pacedSummary.Copied);
        // The UI thread must not be asked to repaint once per file on a 26,000-item library.
        Assert.IsTrue(raw.Reports <= fileCount, "Progress must not exceed one report per file.");
        Assert.IsTrue(throttle.Forwarded < raw.Reports / 4,
            $"Pacing delivered {throttle.Forwarded:N0} of {raw.Reports:N0} updates; the throttle is not working.");
        Assert.IsTrue(throttle.Forwarded > 0, "The UI must still receive updates while a copy runs.");
        Directory.Delete(destination, recursive: true);
        Directory.Delete(pacedDestination, recursive: true);
    }
}
