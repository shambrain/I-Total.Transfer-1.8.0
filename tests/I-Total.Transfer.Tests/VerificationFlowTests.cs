using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Verification through the real engine. A verification result is allowed to say "matched" only for
/// files the scan actually covered, and it must not turn a partial enumeration into a complete backup.
/// These tests use the deterministic mock backend; no physical phone is involved.
/// </summary>
[TestClass]
public sealed class VerificationFlowTests
{
    private static string TempDestination()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-verify", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Progress<TransferProgress> Quiet() => new(_ => { });

    private static ScanResult MarkIncomplete(ScanResult scan) => scan with
    {
        Report = new EnumerationReport(EnumerationStatus.Incomplete, 1, 1, scan.Files.Count,
            scan.TotalBytes, 0, 0, 0, 0, "Mock", [], ["DCIM/100APPLE: directory read failed"])
    };

    [TestMethod]
    public async Task AMatchingDestinationVerifiesTheListedFile()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);

            VerificationResult result = await service.VerifyAsync(scan, destination, Quiet(), CancellationToken.None);

            Assert.IsTrue(result.Completed);
            Assert.AreEqual(1, result.Total);
            Assert.AreEqual(1, result.Matching);
            Assert.AreEqual(0, result.Missing);
            Assert.AreEqual(0, result.WrongSize);
            Assert.AreEqual(1, result.DestinationHashVerified);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ASizeChangeOnTheDestinationIsNotAMatch()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);

            string file = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            File.AppendAllText(file, "changed");

            VerificationResult result = await service.VerifyAsync(scan, destination, Quiet(), CancellationToken.None);

            Assert.IsFalse(result.Completed);
            Assert.AreEqual(1, result.WrongSize);
            Assert.AreEqual(0, result.Matching);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AnIncompleteEnumerationIsNeverReportedAsACompletedVerification()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult complete = await service.ScanAsync(CancellationToken.None);
            await service.CopyAsync(complete, destination, 1, Quiet(), CancellationToken.None);

            VerificationResult result = await service.VerifyAsync(
                MarkIncomplete(complete), destination, Quiet(), CancellationToken.None);

            Assert.IsFalse(result.Completed,
                "Every listed file can match while the listing itself is known to be partial.");
            Assert.AreEqual(1, result.Matching);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("ENUMERATION INCOMPLETE", StringComparison.Ordinal)),
                "The partial enumeration must be named in the report.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task AVerificationWithADuplicateSourcePathIsRefusedBeforeTheRecordIsTouched()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var scan = new ScanResult("Mock iPhone", now, now,
            [
                new MediaFile("DCIM", "same.bin", 1024, ".bin"),
                new MediaFile("DCIM", "same.bin", 1024, ".bin")
            ], []);
            var service = new TransferService(backend, new LogService(destination));

            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.VerifyAsync(scan, destination, Quiet(), CancellationToken.None));

            StringAssert.Contains(error.Message, "same destination path more than once");
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, ".i-total")));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task RepairFetchesOnlyTheMissingFileAndThenVerifiesTheListing()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            await service.CopyAsync(scan, destination, 1, Quiet(), CancellationToken.None);

            string file = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            File.Delete(file);
            int fetchesBefore = backend.FetchedPaths.Count;

            VerificationResult result = await service.VerifyAsync(
                scan, destination, Quiet(), CancellationToken.None, repair: true);

            Assert.IsTrue(result.Completed);
            Assert.AreEqual(1, result.Repaired);
            Assert.AreEqual(1, result.Matching);
            Assert.AreEqual(fetchesBefore + 1, backend.FetchedPaths.Count,
                "Repair must fetch the missing file once, not re-read the whole listing.");
            Assert.IsTrue(File.Exists(file));
        }
        finally { Directory.Delete(destination, true); }
    }
}
