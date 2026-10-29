using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The reconciliation is the report's arithmetic: every scanned object counted once, at the grade it
/// actually reached. These tests pin the buckets and prove the file written to disk carries them.
/// </summary>
[TestClass]
public sealed class ReconciliationReportTests
{
    private static MediaFile Media(string name, long size, bool known = true) =>
        new("DCIM", name, size, ".jpg") { SizeKnown = known };

    private static TransferEntry Entry(string name, OutcomeReason outcome, TransferState state,
        VerificationLevel grade = VerificationLevel.None, long expected = 100, long written = 0, bool sizeKnown = true) => new()
    {
        RelativePath = "DCIM\\" + name,
        ExpectedBytes = expected,
        SizeKnown = sizeKnown,
        BytesWritten = written,
        Outcome = outcome,
        State = state,
        Verification = grade
    };

    [TestMethod]
    public void EveryObjectIsCountedUnderExactlyOneOutcome()
    {
        var scan = new ScanResult("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [Media("a.jpg", 100), Media("b.jpg", 200), Media("c.jpg", 300), Media("d.jpg", 400)], []);
        var entries = new List<TransferEntry>
        {
            Entry("a.jpg", OutcomeReason.Copied, TransferState.IntegrityVerified, VerificationLevel.SourceAndDestinationHash, 100, 100),
            Entry("b.jpg", OutcomeReason.AlreadyPresent, TransferState.IntegrityVerified, VerificationLevel.SourceAndDestinationHash, 200, 200),
            Entry("c.jpg", OutcomeReason.Failed, TransferState.Failed, VerificationLevel.None, 300),
            Entry("d.jpg", OutcomeReason.FilterExcluded, TransferState.Skipped, VerificationLevel.None, 400)
        };

        ReconciliationReport report = ReconciliationReport.Build(scan, entries);

        Assert.AreEqual(4, report.DiscoveredFiles);
        Assert.AreEqual(1000, report.DiscoveredKnownBytes);
        Assert.AreEqual(3, report.EligibleFiles, "the filter-excluded file is not eligible");
        Assert.AreEqual(600, report.EligibleBytes);
        Assert.AreEqual(1, report.ExcludedFiles);
        Assert.AreEqual(1, report.CopiedFiles);
        Assert.AreEqual(100, report.CopiedBytes);
        Assert.AreEqual(2, report.SourceHashVerifiedFiles, "the copied file and the already-present file both carry that grade");
        Assert.AreEqual(1, report.AlreadyPresentFiles);
        Assert.AreEqual(200, report.AlreadyPresentBytes);
        Assert.AreEqual(1, report.FailedFiles);
        Assert.AreEqual(300, report.FailedBytes);
        Assert.AreEqual(0, report.OutstandingFiles);
        Assert.AreEqual(0, report.UnknownSizeFiles);
    }

    [TestMethod]
    public void AnUnmeasuredSizeContributesItsCommittedBytesButNoDiscoveredBytes()
    {
        var scan = new ScanResult("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [Media("a.bin", 0, known: false), Media("b.jpg", 100)], []);
        var entries = new List<TransferEntry>
        {
            Entry("a.bin", OutcomeReason.Copied, TransferState.IntegrityVerified, VerificationLevel.SourceAndDestinationHash,
                expected: 0, written: 512, sizeKnown: false),
            Entry("b.jpg", OutcomeReason.Copied, TransferState.IntegrityVerified, VerificationLevel.SourceAndDestinationHash,
                expected: 100, written: 100)
        };

        ReconciliationReport report = ReconciliationReport.Build(scan, entries);

        Assert.AreEqual(1, report.UnknownSizeFiles);
        Assert.AreEqual(100, report.DiscoveredKnownBytes, "an unknown size is not invented as zero bytes of content");
        Assert.AreEqual(612, report.CopiedBytes, "the committed length is the honest byte figure");
        Assert.AreEqual(2, report.CopiedFiles);
    }

    [TestMethod]
    public void AMissingManifestRowIsOutstandingAndAnUnsafePathIsInaccessible()
    {
        var scan = new ScanResult("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [Media("a.jpg", 100), new MediaFile("DCIM", "CON.jpg", 200, ".jpg"), Media("b.jpg", 300)], []);
        var entries = new List<TransferEntry> { Entry("a.jpg", OutcomeReason.Copied, TransferState.IntegrityVerified) };

        ReconciliationReport report = ReconciliationReport.Build(scan, entries);

        Assert.AreEqual(1, report.InaccessibleFiles, "a reserved device name cannot be written and is counted as inaccessible");
        Assert.AreEqual(1, report.OutstandingFiles, "a scanned object with no manifest row is unresolved, not copied");
        Assert.AreEqual(3, report.DiscoveredFiles);
        Assert.AreEqual(1, report.CopiedFiles);
    }

    [TestMethod]
    public void AnUnverifiableCopyIsCountedAsUnverifiableNotAsVerified()
    {
        var scan = new ScanResult("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [Media("a.bin", 0, known: false)], []);
        var entries = new List<TransferEntry>
        {
            Entry("a.bin", OutcomeReason.Copied, TransferState.CopiedUnverified, VerificationLevel.None,
                expected: 0, written: 512, sizeKnown: false)
        };

        ReconciliationReport report = ReconciliationReport.Build(scan, entries);

        Assert.AreEqual(1, report.CopiedFiles);
        Assert.AreEqual(1, report.UnverifiableFiles);
        Assert.AreEqual(0, report.SourceHashVerifiedFiles);
        Assert.AreEqual(0, report.DestinationHashVerifiedFiles);
    }

    [TestMethod]
    public void ALegacyRowWithoutAnOutcomeIsMappedFromItsStateRatherThanCountedAsOutstanding()
    {
        var scan = new ScanResult("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [Media("a.jpg", 100)], []);
        var legacy = new TransferEntry
        {
            RelativePath = "DCIM\\a.jpg",
            ExpectedBytes = 100,
            State = TransferState.IntegrityVerified,
            Verification = VerificationLevel.SourceAndDestinationHash,
            Outcome = OutcomeReason.None
        };

        ReconciliationReport report = ReconciliationReport.Build(scan, [legacy]);

        Assert.AreEqual(1, report.AlreadyPresentFiles, "a verified row from an older manifest was already present");
        Assert.AreEqual(0, report.OutstandingFiles);
        Assert.AreEqual(1, report.SourceHashVerifiedFiles, "the persisted verification grade still counts");
    }

    [TestMethod]
    public async Task AChangedInventoryScopeIsWrittenToTheReportAndDoesNotBlockTheCopy()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = Path.Combine(Path.GetTempPath(), "itotal-scope-report", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        try
        {
            var service = new TransferService(backend, new LogService(destination));
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            scan = scan with
            {
                Scope = new InventoryScope(InventoryScopeStatus.Changed, 57731, 1,
                    [new FolderInventory("DCIM\\202610_a", 2543, 100)], [], "scope changed in test")
            };

            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.IsTrue(summary.Completed, "a scope warning is not an enumeration failure");
            Assert.AreEqual(1, summary.Copied);

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(destination, "I-total-session-report.json")));
            JsonElement scope = document.RootElement.GetProperty("InventoryScope");
            Assert.AreEqual((int)InventoryScopeStatus.Changed, scope.GetProperty("Status").GetInt32());
            Assert.AreEqual(57731, scope.GetProperty("PreviousFiles").GetInt32());
            StringAssert.Contains(scope.GetProperty("Detail").GetString(), "scope changed in test");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task TheWrittenReportCarriesTheReconciliationAndAMatchingCsv()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 3);
        backend.FailPaths.Add("DCIM\\IMG_0002.bin");
        string destination = Path.Combine(Path.GetTempPath(), "itotal-recon", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        try
        {
            var filter = new FilterSpec(new HashSet<string>(StringComparer.OrdinalIgnoreCase), [], ["IMG_0001"], false, 0, 0);
            var service = new TransferService(backend, new LogService(destination),
                TransferOptions.FromSettings(AppSettings.Defaults()), filter);
            ScanResult scan = await service.ScanAsync(CancellationToken.None);
            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.IsNotNull(summary.Reconciliation);
            Assert.AreEqual(3, summary.Reconciliation.DiscoveredFiles);
            Assert.AreEqual(1, summary.Reconciliation.ExcludedFiles);
            Assert.AreEqual(2, summary.Reconciliation.EligibleFiles);
            Assert.AreEqual(1, summary.Reconciliation.CopiedFiles);
            Assert.AreEqual(1, summary.Reconciliation.FailedFiles);
            Assert.AreEqual(1, summary.Reconciliation.SourceHashVerifiedFiles);

            string json = File.ReadAllText(Path.Combine(destination, "I-total-session-report.json"));
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement reconciliation = document.RootElement.GetProperty("Reconciliation");
            Assert.AreEqual(3, reconciliation.GetProperty("DiscoveredFiles").GetInt32());
            Assert.AreEqual(1, reconciliation.GetProperty("ExcludedFiles").GetInt32());

            string csvPath = Path.Combine(destination, "I-total-reconciliation.csv");
            Assert.IsTrue(File.Exists(csvPath), "the reconciliation must be exported as CSV too");
            string csv = File.ReadAllText(csvPath);
            StringAssert.Contains(csv, "Metric,Value");
            string perFile = File.ReadAllText(Path.Combine(destination, "I-total-session-report.csv"));
            StringAssert.StartsWith(perFile, "RelativePath,CommittedPath,", "the per-file CSV records where each copy landed");
            StringAssert.Contains(csv, "CopiedFiles,1");
            StringAssert.Contains(csv, "FailedFiles,1");
            StringAssert.Contains(csv, "ExcludedByFilterFiles,1");
        }
        finally { Directory.Delete(destination, true); }
    }
}
