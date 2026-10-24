using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Recovery end-to-end through the real engine: a session that stops half way has to leave a record that
/// says so, and continuing from that record has to fetch only what is outstanding while still hashing
/// every byte it does fetch. No physical device is involved.
/// </summary>
[TestClass]
public sealed class RecoveryFlowTests
{
    private static string TempDestination()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ManifestPath(string destination) => Path.Combine(destination, ".i-total", "session.json");

    private static SessionManifest? ReadManifest(string destination)
    {
        try
        {
            string path = ManifestPath(destination);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SessionManifest>(File.ReadAllText(path))
                : null;
        }
        catch { return null; }
    }

    private static Progress<TransferProgress> Quiet() => new(_ => { });

    [TestMethod]
    public async Task TheSessionIsStampedAsRunningBeforeAnyFileMoves()
    {
        // A run that is killed without a summary (power loss, a disconnect, Task Manager) leaves no
        // cancellation record, so the folder has to be able to say "this was in flight" on its own.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2);
        string destination = TempDestination();
        try
        {
            string? observed = null;
            int fetches = 0;
            backend.OnCopy = _ =>
            {
                if (fetches++ == 0) observed = ReadManifest(destination)?.LastSessionStatus;
            };

            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            TransferSummary summary = await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);

            Assert.IsTrue(summary.Completed);
            Assert.AreEqual("RUNNING", observed,
                "The record must describe the session as in-flight before the first byte is fetched.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task StoppingATransferRecordsWhatFinishedAndWhatDidNot()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        string destination = TempDestination();
        try
        {
            using var cts = new CancellationTokenSource();
            // Stop once the first file is fully committed and verified, i.e. while the second is in flight.
            backend.OnCopy = file => { if (file.Name == "IMG_0001.bin") cts.Cancel(); };

            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            bool cancelled = false;
            try { await service.CopyAsync(scan, destination, 1, Quiet(), cts.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "A stopped transfer must surface as a cancellation.");

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.AreEqual("CANCELLED", state.Status);
            Assert.IsTrue(state.CanResume);
            Assert.IsFalse(state.Finished);
            Assert.AreEqual(1, state.Verified, "The file that finished before the stop stays verified.");
            Assert.AreEqual(1, state.Failed, "The file the stop interrupted is recorded as failed, not as done.");
            Assert.AreEqual(1, state.Pending, "The file the stopped run never reached is still work.");

            // The interrupted file had already been written into the folder when its verification was cut
            // short. Nothing in the destination may exist without the record vouching for it, so it is not
            // left behind: an unverified file kept in place would later be re-classified as a size-only
            // baseline and quietly weaken the session's claim.
            Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "DCIM", "IMG_0001.bin")),
                "A copy whose verification was interrupted must not be left in the destination.");
            Assert.IsFalse(File.Exists(Path.Combine(destination, "DCIM", "IMG_0002.bin")));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AStoppedSessionCanStillReachAVerifiedCompleteResult()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        string destination = TempDestination();
        try
        {
            using var cts = new CancellationTokenSource();
            backend.OnCopy = file => { if (file.Name == "IMG_0001.bin") cts.Cancel(); };

            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            try { await service.CopyAsync(scan, destination, 1, Quiet(), cts.Token); }
            catch (OperationCanceledException) { }

            SessionManifest manifest = SessionRecovery.Load(destination, out _)!;
            ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out _);
            backend.FetchedPaths.Clear();

            TransferSummary resumed = await service.CopyAsync(plan, destination, 1, Quiet(), CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "DCIM\\IMG_0001.bin", "DCIM\\IMG_0002.bin" }, backend.FetchedPaths.ToArray());
            Assert.IsTrue(resumed.Completed);
            Assert.AreEqual(VerificationLevel.SourceAndDestinationHash, resumed.WeakestLevel,
                "Continuing a stopped session must be able to reach full source-to-destination verification.");
            foreach (string name in new[] { "IMG_0000.bin", "IMG_0001.bin", "IMG_0002.bin" })
                Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", name)));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ABackendFaultAbortsTheSessionAndLeavesItResumable()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        string destination = TempDestination();
        try
        {
            backend.ThrowPaths.Add("DCIM\\IMG_0001.bin");
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            await Assert.ThrowsExceptionAsync<IOException>(() =>
                service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None));

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.AreEqual("ABORTED", state.Status);
            Assert.IsTrue(state.CanResume, "An aborted session is exactly what continuing exists for.");
            Assert.AreEqual(1, state.Verified);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ContinuingASessionFetchesOnlyWhatIsOutstandingAndStillVerifiesIt()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        string destination = TempDestination();
        try
        {
            backend.FailPaths.Add("DCIM\\IMG_0001.bin");
            backend.FailPaths.Add("DCIM\\IMG_0002.bin");
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            TransferSummary first = await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);
            Assert.IsFalse(first.Completed);
            Assert.AreEqual(1, first.Copied);
            Assert.AreEqual(2, first.Failed);

            RecoveryState interrupted = SessionRecovery.Inspect(destination);
            Assert.IsTrue(interrupted.CanResume);
            Assert.AreEqual(2, interrupted.Outstanding);
            Assert.AreEqual(1, interrupted.Verified);

            // The device recovers, and the operator continues from the folder's own record.
            backend.FailPaths.Clear();
            SessionManifest manifest = SessionRecovery.Load(destination, out _)!;
            ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out int unsafeSkipped);
            Assert.AreEqual(0, unsafeSkipped);
            backend.FetchedPaths.Clear();

            TransferSummary resumed = await service.CopyAsync(plan, destination, 1, Quiet(), CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "DCIM\\IMG_0001.bin", "DCIM\\IMG_0002.bin" },
                backend.FetchedPaths.ToArray(),
                "Continuing must fetch what is outstanding and leave verified files alone.");
            Assert.IsTrue(resumed.Completed, "Continuing the last two files has to finish the session.");
            Assert.AreEqual(2, resumed.Total);
            Assert.AreEqual(2, resumed.Copied);
            StringAssert.Contains(resumed.Status, "HASH-VERIFIED COMPLETE");

            foreach (string name in new[] { "IMG_0000.bin", "IMG_0001.bin", "IMG_0002.bin" })
                Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", name)), $"Missing {name} after continuing.");

            // The record now describes the whole session, not just the part that was continued.
            SessionManifest finished = ReadManifest(destination)!;
            Assert.AreEqual(3, finished.Entries.Count(e => e.State == TransferState.IntegrityVerified));
            Assert.IsTrue(finished.Entries.All(e => e.Verification == VerificationLevel.SourceAndDestinationHash &&
                                                    string.Equals(e.SourceSha256, e.DestinationSha256, StringComparison.OrdinalIgnoreCase)),
                "Every file in a hash-verified complete session must carry a source digest that matches its destination.");

            RecoveryState after = SessionRecovery.Inspect(destination);
            Assert.IsTrue(after.Finished);
            Assert.IsFalse(after.CanResume, "A finished session must stop offering to continue.");
            Assert.AreEqual(3, after.Verified);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ARecordedEntryThatCanNeverBeCopiedKeepsTheSessionFromClaimingCompletion()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            // A record can hold a path the engine refuses (a damaged file, an older build, an editor). The
            // plan leaves it out, and the session must then still refuse to announce a clean result: the
            // file it names was never copied anywhere.
            var manifest = new SessionManifest
            {
                Destination = destination,
                DeviceName = "Mock iPhone",
                LastSessionStatus = "RUNNING"
            };
            manifest.Entries.Add(new TransferEntry { RelativePath = "DCIM\\IMG_0000.bin", ExpectedBytes = 1024, State = TransferState.Queued });
            manifest.Entries.Add(new TransferEntry { RelativePath = "..\\escape.bin", ExpectedBytes = 10, State = TransferState.Queued });
            Directory.CreateDirectory(Path.Combine(destination, ".i-total"));
            File.WriteAllText(ManifestPath(destination), JsonSerializer.Serialize(manifest));

            SessionManifest loaded = SessionRecovery.Load(destination, out string? problem)!;
            Assert.IsNull(problem);
            ScanResult plan = SessionRecovery.BuildResumePlan(loaded, out int unsafeSkipped);
            Assert.AreEqual(1, plan.Files.Count);
            Assert.AreEqual(1, unsafeSkipped);

            var service = new TransferService(backend, new LogService(destination));
            TransferSummary summary = await service.CopyAsync(plan, destination, 1, Quiet(), CancellationToken.None);

            Assert.IsFalse(summary.Completed, "An entry the session never copied must not be absorbed into a clean result.");
            Assert.AreEqual(1, summary.Copied);
            StringAssert.Contains(summary.Status, "INCOMPLETE");

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsTrue(state.CanResume);
            Assert.AreEqual(1, state.Pending, "The path the engine refuses stays visible as outstanding work.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AnIncompleteEnumerationNeverBecomesACompleteBackup()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = (await service.ScanAsync(CancellationToken.None)) with
            {
                Report = new EnumerationReport(EnumerationStatus.Incomplete, 1, 1, 1, 1024,
                    0, 0, 0, 0, "Mock", [], ["DCIM/100APPLE: directory read failed"])
            };

            TransferSummary summary = await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);

            Assert.IsFalse(summary.Completed,
                "Every planned file can copy while the listing that produced the plan is still partial.");
            Assert.AreEqual(1, summary.Copied);
            Assert.AreEqual(0, summary.Failed);
            StringAssert.Contains(summary.Status, "INCOMPLETE ENUMERATION");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ADifferentPhoneCannotContinueAnotherPhonesSession()
    {
        using var first = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var firstService = new TransferService(first, new LogService(destination));
            ScanResult firstScan = (await firstService.ScanAsync(CancellationToken.None)) with { StableId = "phone-a" };
            TransferSummary firstRun = await firstService.CopyAsync(firstScan, destination, 1, Quiet(), CancellationToken.None);
            Assert.IsTrue(firstRun.Completed);
            Assert.AreEqual(1, first.FetchedPaths.Count);

            using var second = new MockBackend(canHashSourceBytes: true, fileCount: 1);
            var secondService = new TransferService(second, new LogService(destination));
            ScanResult secondScan = (await secondService.ScanAsync(CancellationToken.None)) with { StableId = "phone-b" };

            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => secondService.CopyAsync(secondScan, destination, 1, Quiet(), CancellationToken.None));

            StringAssert.Contains(error.Message, "different phone");
            Assert.AreEqual(0, second.FetchedPaths.Count,
                "The engine must refuse the source identity before fetching a single byte from the other phone.");
            Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")),
                "The original phone's verified destination file is preserved.");
            Assert.AreEqual("phone-a", ReadManifest(destination)?.DeviceStableId,
                "The session record still belongs to the original phone.");
        }
        finally { Directory.Delete(destination, true); }
    }
}
