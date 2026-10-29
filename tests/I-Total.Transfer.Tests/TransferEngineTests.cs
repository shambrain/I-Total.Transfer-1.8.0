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
    public async Task ResumingAZeroByteUnverifiedFilePreservesItWithoutCallingItAFailure()
    {
        // The Huawei exposes some zero-byte placeholders whose source size it will not report. The first
        // pass copies them and honestly marks them unverified. A resume must not then call the same
        // files "collisions" and mark them failed: the destination is byte-identical to what the first
        // pass recorded, so the state stays CopiedUnverified and the file is preserved.
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1, bytesPerFile: 0);
        string destination = TempDestination();
        try
        {
            var logger = new LogService(destination);
            var service = new TransferService(backend, logger);
            ScanResult scan = await service.ScanAsync(CancellationToken.None);

            TransferSummary first = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);
            Assert.AreEqual(0, first.Failed);
            Assert.AreEqual(1, first.Unverifiable, "a file with no trustworthy source size is copied but unverified");

            string path = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(0, new FileInfo(path).Length);

            TransferSummary second = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(0, second.Failed, "preserving an unverified file is not a new failure");
            Assert.AreEqual(1, second.Unverifiable);
            Assert.AreEqual(0, second.Copied, "the unchanged destination is not copied a second time");
            StringAssert.StartsWith(second.Status, "COPIED WITH UNVERIFIABLE");
            Assert.AreEqual(0, new FileInfo(path).Length);
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
