using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// A zero-byte file and a file whose size the device would not report are different facts. The scan
/// report, the completeness rule and the transfer plan must all agree on which is which, or a healthy
/// listing of empty placeholders is called unhealthy while a listing of unmeasurable files is called
/// complete.
/// </summary>
[TestClass]
public sealed class EnumerationAccountingTests
{
    private static MediaFile File(string name, long size, bool sizeKnown = true) =>
        new("DCIM", name, size, ".bin") { SizeKnown = sizeKnown };

    [TestMethod]
    public void AZeroByteFileIsCountedAsZeroAndNotAsUnknown()
    {
        ScanAccounting accounting = ScanAccounting.Of([File("empty.aae", 0), File("IMG_0001.HEIC", 100)]);

        Assert.AreEqual(2, accounting.FileCount);
        Assert.AreEqual(1, accounting.ZeroByteCount);
        Assert.AreEqual(0, accounting.UnknownSizeCount);
        Assert.AreEqual(100, accounting.KnownBytes);
    }

    [TestMethod]
    public void AnUnreadableSizeIsCountedAsUnknownAndNotAsZero()
    {
        ScanAccounting accounting = ScanAccounting.Of([File("IMG_0001.HEIC", 0, sizeKnown: false), File("IMG_0002.HEIC", 100)]);

        Assert.AreEqual(0, accounting.ZeroByteCount, "nothing was measured as zero");
        Assert.AreEqual(1, accounting.UnknownSizeCount);
        Assert.AreEqual(100, accounting.KnownBytes);
    }

    [TestMethod]
    public void ANegativeSizeIsTreatedAsUnreadableRatherThanSubtracted()
    {
        ScanAccounting accounting = ScanAccounting.Of([File("weird.bin", -5), File("IMG_0001.HEIC", 100)]);

        Assert.AreEqual(1, accounting.UnknownSizeCount);
        Assert.AreEqual(100, accounting.KnownBytes);
    }

    [TestMethod]
    public void AListingOfEmptyPlaceholdersIsCompleteButAListingOfUnmeasurableFilesIsNot()
    {
        var empty = new List<MediaFile> { File("a.aae", 0), File("b.aae", 0), File("c.aae", 0) };
        var unmeasured = new List<MediaFile> { File("a.bin", 0, false), File("b.bin", 0, false), File("c.bin", 0, false) };

        EnumerationReport emptyReport = ScanAccounting.BuildReport("test", empty, [], 0, []);
        EnumerationReport unmeasuredReport = ScanAccounting.BuildReport("test", unmeasured, [], 0, []);

        Assert.AreEqual(EnumerationStatus.Complete, emptyReport.Status,
            "zero-byte files are a measurement and do not make a scan incomplete");
        Assert.AreEqual(3, emptyReport.ZeroByteCount);
        Assert.AreEqual(0, emptyReport.UnknownSizeCount);

        Assert.AreEqual(EnumerationStatus.Incomplete, unmeasuredReport.Status,
            "a listing whose sizes could not be read is not a healthy complete scan");
        Assert.AreEqual(3, unmeasuredReport.UnknownSizeCount);
        Assert.AreEqual(0, unmeasuredReport.ZeroByteCount);
    }

    [TestMethod]
    public void AScanWithNoFilesIsEmptyAndAFailedFolderIsIncomplete()
    {
        Assert.AreEqual(EnumerationStatus.Empty, ScanAccounting.BuildReport("test", [], [], 0, []).Status);

        var folders = new List<FolderScanInfo> { new("DCIM", 0, 0, false, "access denied") };
        Assert.AreEqual(EnumerationStatus.Incomplete,
            ScanAccounting.BuildReport("test", [], folders, 0, []).Status);
    }

    [TestMethod]
    public void TheReportTotalsMatchTheAccounting()
    {
        var files = new List<MediaFile> { File("a.bin", 0), File("b.bin", 10), File("c.bin", 0, false) };
        EnumerationReport report = ScanAccounting.BuildReport("backend", files, [], 0, []);

        Assert.AreEqual(3, report.FileCount);
        Assert.AreEqual(10, report.TotalBytes);
        Assert.AreEqual(1, report.ZeroByteCount);
        Assert.AreEqual(1, report.UnknownSizeCount);
    }

    [TestMethod]
    public void AMatchingRecordedDigestResumesAnUnknownSizeFileWithoutCallingItACollision()
    {
        Assert.AreEqual(ResumeDecision.SkipVerified,
            TransferRules.ClassifyExistingDestination(0, 512, "abc", "abc", sourceSizeKnown: false));
    }

    [TestMethod]
    public void AnUnknownSizeWithoutADigestIsStillACollision()
    {
        Assert.AreEqual(ResumeDecision.PreserveCollision,
            TransferRules.ClassifyExistingDestination(0, 512, null, null, sourceSizeKnown: false));
    }

    [TestMethod]
    public void AKnownZeroByteDestinationWithoutADigestRecordsABaselineInsteadOfFailing()
    {
        Assert.AreEqual(ResumeDecision.RecordBaselineSkip,
            TransferRules.ClassifyExistingDestination(0, 0, null, null));
    }

    [TestMethod]
    public void ADifferentSavedDigestIsPreservedForReviewEvenWhenTheSizeMatches()
    {
        Assert.AreEqual(ResumeDecision.PreserveHashMismatch,
            TransferRules.ClassifyExistingDestination(100, 100, "abc", "def"));
    }
}
