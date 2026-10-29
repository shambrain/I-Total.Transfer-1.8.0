using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class TransferEngineTests
{
    private static string TempDestination()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<TransferSummary> RunCopyAsync(MockBackend backend, string destination, int retries = 1)
    {
        var logger = new LogService(destination);
        var service = new TransferService(backend, logger);
        ScanResult scan = await service.ScanAsync(CancellationToken.None);
        var progress = new Progress<TransferProgress>(_ => { });
        return await service.CopyAsync(scan, destination, retries, progress, CancellationToken.None);
    }

    [TestMethod]
    public async Task AMidStreamDisconnectAbortsWithoutCommittingOrReportingComplete()
    {
        // A device that disappears after bytes have been written must not leave a destination file, a
        // stale .partial, a verified entry or a "complete" report. The run aborts and the manifest says so.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        backend.MidStreamThrowPaths.Add("DCIM\\IMG_0000.bin");
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            await Assert.ThrowsExceptionAsync<IOException>(() =>
                service.CopyAsync(scan, destination, 1, new Progress<TransferProgress>(_ => { }), CancellationToken.None));

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.AreEqual("ABORTED", manifest.LastSessionStatus);
            TransferEntry entry = manifest.Entries.Single();
            Assert.AreEqual(TransferState.Copying, entry.State, "an interrupted item stays outstanding, never verified");
            Assert.IsTrue(string.IsNullOrEmpty(entry.DestinationSha256), "no destination digest exists for an aborted transfer");

            Assert.IsFalse(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")),
                "a truncated copy must not become the destination file");
            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(destination, ".i-total", "staging"), "*.partial",
                SearchOption.AllDirectories).Length, "the backend's partial is cleaned up");
            Assert.IsFalse(File.Exists(Path.Combine(destination, "I-total-session-report.json")),
                "an aborted run writes no completion report");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AResumeAfterAMidStreamDisconnectReReadsTheItemAndCompletes()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        backend.MidStreamThrowPaths.Add("DCIM\\IMG_0000.bin");
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            var progress = new Progress<TransferProgress>(_ => { });
            await Assert.ThrowsExceptionAsync<IOException>(() => service.CopyAsync(scan, destination, 1, progress, CancellationToken.None));

            backend.MidStreamThrowPaths.Clear();
            TransferSummary resumed = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);

            Assert.IsTrue(resumed.Completed);
            Assert.AreEqual(1, resumed.Copied);
            Assert.AreEqual(0, resumed.Failed);
            string path = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(1024, new FileInfo(path).Length);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ATruncatedStageIsRejectedAndNothingIsCommitted()
    {
        // A provider that ends the stream early must not produce a destination file: the staged size is
        // checked against the measured source size before the commit.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        backend.TruncatePaths.Add("DCIM\\IMG_0000.bin");
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination, retries: 1);

            Assert.AreEqual(0, summary.Copied);
            Assert.AreEqual(1, summary.Failed);
            Assert.IsFalse(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")),
                "a short stage must never be committed");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ACorruptSqliteFileDoesNotStopTheRun()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            Directory.CreateDirectory(Path.Combine(destination, ".i-total"));
            File.WriteAllBytes(Path.Combine(destination, ".i-total", "session.db"), [1, 2, 3, 4, 5, 6, 7, 8]);

            TransferSummary summary = await RunCopyAsync(backend, destination);

            Assert.IsTrue(summary.Completed);
            Assert.AreEqual(1, summary.Copied);
            Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ATransferStillCompletesWhenTheSqliteDatabaseCannotBeOpened()
    {
        // SQLite is the queryable history; the JSON manifest is the recovery plan. If the database is
        // unusable (locked, corrupt, replaced by a folder), the run must continue and report honestly
        // rather than aborting or pretending the queue was written.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2);
        string destination = TempDestination();
        try
        {
            Directory.CreateDirectory(Path.Combine(destination, ".i-total", "session.db"));

            TransferSummary summary = await RunCopyAsync(backend, destination);

            Assert.IsTrue(summary.Completed);
            Assert.AreEqual(2, summary.Copied);
            Assert.IsTrue(File.Exists(Path.Combine(destination, ".i-total", "session.json")),
                "the JSON manifest remains the durable record");
            Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000.bin")));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ARerunWhereEverythingIsAlreadyPresentIsANoOpNotAFreshVerifiedCopy()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2);
        string destination = TempDestination();
        try
        {
            var logger = new LogService(destination);
            var service = new TransferService(backend, logger);
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            var progress = new Progress<TransferProgress>(_ => { });

            TransferSummary first = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);
            Assert.AreEqual(2, first.Copied);

            TransferSummary second = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);

            Assert.IsTrue(second.Completed);
            Assert.AreEqual(0, second.Copied, "nothing was written on the second run");
            Assert.AreEqual(2, second.AlreadyPresent, "both files were verified against the session record");
            Assert.AreEqual(0, second.BytesCopied);
            StringAssert.StartsWith(second.Status, "ALREADY PRESENT",
                "a no-op must not claim this run produced a verified copy");
            Assert.AreEqual(2, second.Reconciliation!.AlreadyPresentFiles);
            Assert.AreEqual(0, second.Reconciliation.CopiedFiles);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task APermanentFailureIsNotRetriedButATransientOneIs()
    {
        // A denied or missing object fails identically on every attempt. Retrying it triples the delay
        // and makes the log read like an unstable phone. A transient refusal is retried.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2);
        backend.PermanentFailurePaths.Add("DCIM\\IMG_0000.bin");
        backend.FailPaths.Add("DCIM\\IMG_0001.bin");
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination, retries: 2);

            Assert.AreEqual(2, summary.Failed);
            Assert.AreEqual(1, backend.FetchedPaths.Count(path => path == "DCIM\\IMG_0000.bin"),
                "a permanent failure must be attempted once");
            Assert.AreEqual(2, backend.FetchedPaths.Count(path => path == "DCIM\\IMG_0001.bin"),
                "a transient failure is retried");

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.AreEqual(1, manifest.Entries.Single(e => e.RelativePath.EndsWith("IMG_0000.bin", StringComparison.Ordinal)).Attempts);
            Assert.AreEqual(2, manifest.Entries.Single(e => e.RelativePath.EndsWith("IMG_0001.bin", StringComparison.Ordinal)).Attempts);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void PlanSummaryUsesTheActiveFilterAndNeverTheWholeScan()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var filter = new FilterSpec(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".heic", ".mov" },
                [], [], false, 0, 409_600);
            var service = new TransferService(
                backend, new LogService(destination), TransferOptions.FromSettings(AppSettings.Defaults()), filter);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var scan = new ScanResult("Mock iPhone", now, now,
            [
                new MediaFile("DCIM", "a.HEIC", 1_000, ".heic"),
                new MediaFile("DCIM", "b.MOV", 2_000, ".mov"),
                new MediaFile("DCIM", "c.JPG", 3_000, ".jpg"),
                new MediaFile("DCIM", "too-large.MOV", 500_000, ".mov")
            ], []);

            (int count, long bytes) = service.PlanSummary(scan);

            Assert.AreEqual(2, count, "Only the HEIC and the small MOV pass the active filter.");
            Assert.AreEqual(3_000, bytes);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task SourceHashingBackendSkipsTheRedundantStagedReadBack()
    {
        // A backend that hashes raw source bytes while streaming already knows the source digest, so the
        // engine must not re-read the staged payload: that would cost a second full pass over every byte
        // (106 GB on a full camera roll) and would only compare the copy with itself. The staged digest is
        // therefore absent, while the strongest verification level — source digest against the committed
        // destination — is still recorded.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 2);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.IsTrue(summary.Completed);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Entries.All(e => e.StagedPayloadSha256 is null),
                "A source-hashing backend must not pay for a second read of the staged payload.");
            Assert.IsTrue(manifest.Entries.All(e =>
                    e.Verification == VerificationLevel.SourceAndDestinationHash &&
                    !string.IsNullOrWhiteSpace(e.SourceSha256) &&
                    string.Equals(e.SourceSha256, e.DestinationSha256, StringComparison.OrdinalIgnoreCase)),
                "Skipping the staged read-back must not weaken the recorded verification.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ADuplicateSourcePathIsRefusedBeforeAnythingIsWritten()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var scan = new ScanResult("Mock iPhone", now, now,
            [
                new MediaFile("DCIM", "same.bin", 1024, ".bin"),
                new MediaFile("DCIM", "same.bin", 2048, ".bin")
            ], []);
            var service = new TransferService(backend, new LogService(destination));

            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.CopyAsync(scan, destination, 1, new Progress<TransferProgress>(_ => { }), CancellationToken.None));

            StringAssert.Contains(error.Message, "same destination path more than once");
            StringAssert.Contains(error.Message, "DCIM\\same.bin");
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, ".i-total")),
                "The refusal must happen before a session record is created.");
            Assert.AreEqual(0, Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ShellLikeBackendStillReadsTheStagedPayloadBack()
    {
        // The opposite case: with no source digest available, the staged read-back is the only independent
        // comparison the engine has, so it must still happen.
        using var backend = new MockBackend(canHashSourceBytes: false, fileCount: 2);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.IsTrue(summary.Completed);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Entries.All(e => !string.IsNullOrWhiteSpace(e.StagedPayloadSha256)),
                "Without a source digest the staged read-back is the only comparison available.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AZeroByteSourceFileIsHashVerifiedInsteadOfBeingCalledUnverifiable()
    {
        // A genuine zero-byte file is a measurement, not a missing one. It used to be conflated with
        // "the backend could not read the size", so an entire session that copied nothing but empty
        // placeholders could never reach a verified status. The empty payload has a well-known
        // SHA-256, so a source-hashing backend can prove it like any other file.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 0);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.AreEqual(0, summary.Failed);
            Assert.AreEqual(0, summary.Unverifiable, "a zero-byte file with a known size is verifiable");
            Assert.AreEqual(1, summary.Copied);
            Assert.IsTrue(summary.Completed);

            string path = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(0, new FileInfo(path).Length);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            TransferEntry entry = manifest.Entries.Single();
            Assert.IsTrue(entry.SizeKnown, "the size was reported as 0, which is a measurement");
            Assert.AreEqual(TransferState.IntegrityVerified, entry.State);
            Assert.AreEqual(VerificationLevel.SourceAndDestinationHash, entry.Verification);
            Assert.AreEqual("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
                entry.SourceSha256, "the empty payload's SHA-256 is the proof the file was read");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AnUnmeasuredSourceSizeStaysVisibleAndResumesWithoutBeingCalledACollision()
    {
        // The HUAWEI exposed placeholder files whose bytes could be read but whose size it would not
        // report. A source-hashing backend still proves the copied bytes, so the item is accepted, but
        // the unknown size must stay recorded and the resume must not reclassify the file as a
        // collision just because 0 is not its real size.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 20, bytesPerFile: 512, unknownSizeFiles: 1);
        string destination = TempDestination();
        try
        {
            var logger = new LogService(destination);
            var service = new TransferService(backend, logger);
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            var progress = new Progress<TransferProgress>(_ => { });

            TransferSummary first = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);
            Assert.AreEqual(0, first.Failed);
            Assert.AreEqual(0, first.Unverifiable, "the source-to-destination hash proves the bytes even without a size");
            Assert.AreEqual(20 * 512, first.BytesCopied, "the report counts the bytes actually written, not the 0 placeholder");

            string path = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Assert.AreEqual(512, new FileInfo(path).Length);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.IsFalse(manifest.Entries.Single(e => e.RelativePath.EndsWith("IMG_0000.bin", StringComparison.Ordinal)).SizeKnown,
                "an unreadable size must remain unknown, not become 0");

            TransferSummary second = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);
            Assert.AreEqual(0, second.Failed, "an already verified unknown-size file is not a collision");
            Assert.AreEqual(0, second.Copied, "the verified destination is preserved, not copied again");
            Assert.AreEqual(0, second.Unverifiable);
            Assert.IsTrue(second.Completed);
            Assert.AreEqual(512, new FileInfo(path).Length);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AnUnmeasuredSizeWithoutASourceHashIsCopiedButNotCalledVerified()
    {
        // Shell CopyHere cannot hash raw source bytes. With no size and no source digest, nothing proves
        // the destination equals the source, so the item must stay CopiedUnverified and the session must
        // not claim a verified complete backup.
        using var backend = new MockBackend(canHashSourceBytes: false, fileCount: 20, bytesPerFile: 512, unknownSizeFiles: 1);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.AreEqual(0, summary.Failed);
            Assert.AreEqual(20, summary.Copied);
            Assert.AreEqual(1, summary.Unverifiable);
            Assert.IsFalse(summary.Completed);
            StringAssert.StartsWith(summary.Status, "COPIED WITH UNVERIFIABLE");
            Assert.AreEqual(512, new FileInfo(Path.Combine(destination, "DCIM", "IMG_0000.bin")).Length);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task FilteredRunReportsThePlansBytesNotTheWholeDevice()
    {
        // A filter that keeps two of four files must make the run promise 2 files / 2,048 bytes, not
        // 4 files' worth of space. The live Android drill found the opposite: a 520-file plan was shown
        // beside the 15.01 GB size of the whole 4,106-file device, so the two headline numbers
        // contradicted each other. The free-space check and the progress bar consume this same value.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 4, bytesPerFile: 1024);
        string destination = TempDestination();
        try
        {
            var logger = new LogService(destination);
            var filter = new FilterSpec(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                new[] { "IMG_0000", "IMG_0002" },
                ExcludeSidecars: false,
                MinSize: 0,
                MaxSize: 0);
            var service = new TransferService(backend, logger, TransferOptions.FromSettings(AppSettings.Defaults()), filter);
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            Assert.AreEqual(4096, scan.TotalBytes, "the scan itself still covers every file");

            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(2, summary.Total, "the run plans only the two files the filter keeps");
            Assert.AreEqual(2048, summary.ExpectedBytes, "the run's size is the filtered plan, not the device total");
            Assert.AreEqual(2, summary.Copied);
            Assert.IsTrue(summary.Completed);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task SourceHashingBackendDeclaresSourceToDestinationIntegrity()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.IsTrue(summary.Completed);
            Assert.AreEqual(3, summary.Copied);
            // Read the persisted manifest to confirm the strongest verification level was recorded.
            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Entries.All(e =>
                e.State == TransferState.IntegrityVerified &&
                e.Verification == VerificationLevel.SourceAndDestinationHash &&
                !string.IsNullOrWhiteSpace(e.SourceSha256) &&
                string.Equals(e.SourceSha256, e.DestinationSha256, StringComparison.OrdinalIgnoreCase)));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ShellLikeBackendDeclaresOnlyDestinationHashIntegrity()
    {
        using var backend = new MockBackend(canHashSourceBytes: false, fileCount: 2);
        string destination = TempDestination();
        try
        {
            TransferSummary summary = await RunCopyAsync(backend, destination);
            Assert.IsTrue(summary.Completed);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Entries.All(e =>
                e.State == TransferState.IntegrityVerified &&
                e.Verification == VerificationLevel.DestinationHash &&
                string.IsNullOrWhiteSpace(e.SourceSha256) &&
                string.Equals(e.StagedPayloadSha256, e.DestinationSha256, StringComparison.OrdinalIgnoreCase)));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ResumedRunSkipsAlreadyVerifiedFiles()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 4);
        string destination = TempDestination();
        try
        {
            await RunCopyAsync(backend, destination);
            TransferSummary second = await RunCopyAsync(backend, destination);
            Assert.IsTrue(second.Completed);
            Assert.AreEqual(4, second.Skipped);
            Assert.AreEqual(0, second.Copied);
            Assert.AreEqual(0, second.Failed);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ExistingCollisionIsPreservedAndReportedFailed()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            // Reuse the backend's own known file to determine the expected path and place a wrong-size collision.
            ScanResult scan = await new TransferService(backend, new LogService(destination)).ScanAsync(CancellationToken.None);
            MediaFile file = scan.Files[0];
            string safeRelative = TransferRules.ValidateRelativePath(file.Folder, file.Name);
            string target = Path.Combine(destination, safeRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "collision content that is a different length than expected");

            TransferSummary summary = await new TransferService(backend, new LogService(destination)).CopyAsync(scan, destination, 1, new Progress<TransferProgress>(_ => { }), CancellationToken.None);
            Assert.IsFalse(summary.Completed);
            Assert.AreEqual(1, summary.Failed);
            Assert.AreEqual("collision content that is a different length than expected", File.ReadAllText(target));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task EmptyScanIsRejectedRatherThanReportedComplete()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 0);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                service.CopyAsync(new ScanResult("Mock", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []),
                    destination, 1, new Progress<TransferProgress>(_ => { }), CancellationToken.None));
        }
        finally { Directory.Delete(destination, true); }
    }

    private static ScanResult ScanWithTimestamp(MediaFile file, DateTimeOffset? modified)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var timed = new MediaFile(file.Folder, file.Name, file.Size, file.Extension) { ModifiedUtc = modified };
        return new ScanResult("Mock iPhone", now, now, [timed], []);
    }

    private static TransferOptions OptionsWith(ConflictPolicy policy, bool skipUnchanged) =>
        TransferOptions.FromSettings(AppSettings.Defaults()) with
        {
            ConflictPolicy = policy,
            SkipUnchanged = skipUnchanged
        };

    [TestMethod]
    public async Task AChangedSourceIsCopiedAgainWhenOverwriteIsChosen()
    {
        // A photo edited on the phone can keep its size. The scan's modification time is the only signal
        // the engine has without re-reading every source byte, so a newer timestamp must defeat
        // SkipUnchanged and replace the stale destination.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination),
                OptionsWith(ConflictPolicy.Overwrite, skipUnchanged: true));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            MediaFile file = scan.Files[0];
            DateTimeOffset firstSeen = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

            TransferSummary first = await service.CopyAsync(ScanWithTimestamp(file, firstSeen), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);
            Assert.IsTrue(first.Completed);
            Assert.AreEqual(1, backend.FetchedPaths.Count);

            TransferSummary second = await service.CopyAsync(ScanWithTimestamp(file, firstSeen.AddMinutes(5)), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(2, backend.FetchedPaths.Count,
                "A changed source must be fetched again even when the destination still matches its old hash.");
            Assert.AreEqual(1, second.Copied);
            Assert.AreEqual(0, second.Failed);
            Assert.IsTrue(second.Completed);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AChangedSourceIsPreservedAndReportedByDefault()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            MediaFile file = scan.Files[0];
            DateTimeOffset firstSeen = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

            TransferSummary first = await service.CopyAsync(ScanWithTimestamp(file, firstSeen), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);
            Assert.IsTrue(first.Completed);

            TransferSummary second = await service.CopyAsync(ScanWithTimestamp(file, firstSeen.AddMinutes(5)), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(1, backend.FetchedPaths.Count, "PreserveAndReport must not overwrite the existing file.");
            Assert.AreEqual(1, second.Failed);
            Assert.AreEqual(0, second.Copied);
            Assert.IsFalse(second.Completed);
            Assert.IsTrue(File.Exists(Path.Combine(destination, "DCIM", file.Name)));

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            Assert.AreEqual(TransferState.Failed, manifest.Entries.Single().State);
            StringAssert.Contains(manifest.Entries.Single().Error, "source file changed");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AnUnchangedSourceIsStillSkipped()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            MediaFile file = scan.Files[0];
            DateTimeOffset seen = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

            Assert.IsTrue((await service.CopyAsync(ScanWithTimestamp(file, seen), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None)).Completed);

            TransferSummary second = await service.CopyAsync(ScanWithTimestamp(file, seen), destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(1, backend.FetchedPaths.Count, "An unchanged source must not be re-read.");
            Assert.AreEqual(0, second.Copied);
            Assert.AreEqual(0, second.Failed);
            Assert.IsTrue(second.Completed);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AFailedOverwriteRestoresTheOriginalDestination()
    {
        // Overwrite is explicit, but a corrupt new copy must not leave the operator with neither version.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        backend.CorruptPaths.Add(Path.Combine("DCIM", "IMG_0000.bin"));
        string destination = TempDestination();
        try
        {
            string target = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "ORIGINAL-DESTINATION");

            var service = new TransferService(backend, new LogService(destination),
                OptionsWith(ConflictPolicy.Overwrite, skipUnchanged: false));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(1, summary.Failed);
            Assert.IsFalse(summary.Completed);
            Assert.IsTrue(File.Exists(target), "The previous destination must survive a failed overwrite.");
            Assert.AreEqual("ORIGINAL-DESTINATION", File.ReadAllText(target));
            string backupDirectory = Path.Combine(destination, ".i-total", "backup");
            Assert.AreEqual(0, Directory.Exists(backupDirectory)
                ? Directory.GetFiles(backupDirectory).Length : 0,
                "A successful restore removes the backup copy.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ASuccessfulOverwriteReplacesTheFileAndRemovesTheBackup()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 1024);
        string destination = TempDestination();
        try
        {
            string target = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "OLD");

            var service = new TransferService(backend, new LogService(destination),
                OptionsWith(ConflictPolicy.Overwrite, skipUnchanged: false));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.IsTrue(summary.Completed);
            Assert.AreEqual(1024, new FileInfo(target).Length);
            string backupDirectory = Path.Combine(destination, ".i-total", "backup");
            Assert.AreEqual(0, Directory.Exists(backupDirectory)
                ? Directory.GetFiles(backupDirectory).Length : 0);
        }
        finally { Directory.Delete(destination, true); }
    }

}
