using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class FilterAndConflictTests
{
    private static MediaFile File(string folder, string name, long size, string ext) =>
        new(folder, name, size, ext);

    [TestMethod]
    public void ExtensionListIsNormalised()
    {
        HashSet<string> set = TransferRules.ParseExtensionList("jpg, .png;.MOV");
        CollectionAssert.AreEquivalent(new[] { ".jpg", ".png", ".mov" },
            set.Select(s => s.ToLowerInvariant()).ToArray());
    }

    [TestMethod]
    public void IncludeFilterKeepsOnlyListedExtensions()
    {
        AppSettings settings = AppSettings.Defaults();
        settings.IncludeExtensions = ".heic;.mov";
        FilterSpec filter = TransferRules.BuildFilter(settings);
        Assert.IsTrue(TransferRules.IsIncluded(File("DCIM", "a.heic", 10, ".heic"), filter));
        Assert.IsTrue(TransferRules.IsIncluded(File("DCIM", "b.MOV", 10, ".mov"), filter));
        Assert.IsFalse(TransferRules.IsIncluded(File("DCIM", "c.aae", 10, ".aae"), filter));
    }

    [TestMethod]
    public void ExcludeFolderMatchesAnySegment()
    {
        AppSettings settings = AppSettings.Defaults();
        settings.ExcludeFolders = "PhotoStream";
        FilterSpec filter = TransferRules.BuildFilter(settings);
        Assert.IsFalse(TransferRules.IsIncluded(File("DCIM\\PhotoStream", "a.jpg", 10, ".jpg"), filter));
        Assert.IsTrue(TransferRules.IsIncluded(File("DCIM\\Camera", "a.jpg", 10, ".jpg"), filter));
    }

    [TestMethod]
    public void SidecarExclusionDropsAaeFiles()
    {
        AppSettings settings = AppSettings.Defaults();
        settings.ExcludeSidecars = true;
        FilterSpec filter = TransferRules.BuildFilter(settings);
        Assert.IsFalse(TransferRules.IsIncluded(File("DCIM", "IMG_1.AAE", 10, ".aae"), filter));
        Assert.IsTrue(TransferRules.IsIncluded(File("DCIM", "IMG_1.HEIC", 10, ".heic"), filter));
    }

    [TestMethod]
    public void SizeFiltersApplyWhenKnown()
    {
        AppSettings settings = AppSettings.Defaults();
        settings.MinFileSizeBytes = 100;
        settings.MaxFileSizeBytes = 1000;
        FilterSpec filter = TransferRules.BuildFilter(settings);
        Assert.IsFalse(TransferRules.IsIncluded(File("DCIM", "small.jpg", 50, ".jpg"), filter));
        Assert.IsTrue(TransferRules.IsIncluded(File("DCIM", "ok.jpg", 500, ".jpg"), filter));
        Assert.IsFalse(TransferRules.IsIncluded(File("DCIM", "big.jpg", 5000, ".jpg"), filter));
    }

    [TestMethod]
    public void RenameIncomingFindsAFreeName()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string destination = Path.Combine(Path.GetTempPath(), "itotal-rename");
        taken.Add(Path.Combine(destination, "DCIM", "IMG_1.HEIC"));
        string resolved = TransferRules.ResolveTargetPath(destination, Path.Combine("DCIM", "IMG_1.HEIC"),
            ConflictPolicy.RenameIncoming, taken.Contains);
        Assert.AreEqual(Path.Combine(destination, "DCIM", "IMG_1 (1).HEIC"), resolved);
    }

    [TestMethod]
    public void PreservePolicyNeverRenames()
    {
        string destination = Path.Combine(Path.GetTempPath(), "itotal-rename");
        string resolved = TransferRules.ResolveTargetPath(destination, Path.Combine("DCIM", "IMG_1.HEIC"),
            ConflictPolicy.PreserveAndReport, _ => true);
        Assert.AreEqual(Path.Combine(destination, "DCIM", "IMG_1.HEIC"), resolved);
    }

    [TestMethod]
    public void VerificationClassificationNeverUpgradesWithoutMatchingHashes()
    {
        Assert.AreEqual(VerificationLevel.SourceAndDestinationHash,
            TransferRules.ClassifyVerification("AA", "BB", "AA", true));
        Assert.AreEqual(VerificationLevel.DestinationHash,
            TransferRules.ClassifyVerification(null, "BB", "BB", true));
        Assert.AreEqual(VerificationLevel.SizeOnly,
            TransferRules.ClassifyVerification(null, null, "BB", true));
        Assert.AreEqual(VerificationLevel.None,
            TransferRules.ClassifyVerification(null, null, null, false));
    }

    [TestMethod]
    public void EnumerationClassificationDistinguishesEmptyIncompleteAndComplete()
    {
        Assert.AreEqual(EnumerationStatus.Empty, TransferRules.ClassifyEnumeration(0, 0, 0, 0));
        Assert.AreEqual(EnumerationStatus.Incomplete, TransferRules.ClassifyEnumeration(3, 1, 10, 0));
        Assert.AreEqual(EnumerationStatus.Incomplete, TransferRules.ClassifyEnumeration(3, 0, 10, 1));
        Assert.AreEqual(EnumerationStatus.Complete, TransferRules.ClassifyEnumeration(3, 0, 10, 0));
    }

    [TestMethod]
    public void APhoneThatStopsAnsweringAbortsTheWalkInsteadOfCompletingIt()
    {
        // Observed on hardware: the cable came out mid-scan, every object in one album then failed to
        // read, and the scan still reported "52,574 files, status=Complete" - because each failure was
        // only a warning and the folder was still recorded as read. The tail of the tree was missing
        // from a listing that called itself complete, which is the one outcome this app must never
        // produce. A long unbroken run of failures is a vanished device, so it must abort the walk.
        for (int failures = 0; failures < TransferRules.ConsecutiveObjectFailureLimit; failures++)
            Assert.IsFalse(TransferRules.DeviceStoppedAnswering(failures),
                $"{failures} failed objects in a row can still be damaged objects on an answering device.");

        Assert.IsTrue(TransferRules.DeviceStoppedAnswering(TransferRules.ConsecutiveObjectFailureLimit));
        Assert.IsTrue(TransferRules.DeviceStoppedAnswering(TransferRules.ConsecutiveObjectFailureLimit * 100));
    }

    [TestMethod]
    public void ExistingDestinationClassificationIsConservative()
    {
        Assert.AreEqual(ResumeDecision.SkipVerified,
            TransferRules.ClassifyExistingDestination(100, 100, "AA", "AA"));
        Assert.AreEqual(ResumeDecision.PreserveHashMismatch,
            TransferRules.ClassifyExistingDestination(100, 100, "AA", "BB"));
        Assert.AreEqual(ResumeDecision.RecordBaselineSkip,
            TransferRules.ClassifyExistingDestination(100, 100, null, "BB"));
        Assert.AreEqual(ResumeDecision.PreserveCollision,
            TransferRules.ClassifyExistingDestination(100, 99, "AA", "AA"));
    }

    [TestMethod]
    public void LivePhotoPairsAreCountedByStem()
    {
        var files = new List<MediaFile>
        {
            File("DCIM", "IMG_1.HEIC", 10, ".heic"),
            File("DCIM", "IMG_1.MOV", 10, ".mov"),
            File("DCIM", "IMG_2.HEIC", 10, ".heic")
        };
        Assert.AreEqual(1, TransferRules.CountLivePhotoPairs(files));
    }

    [TestMethod]
    public void ReservedWindowsNamesAreDetected()
    {
        Assert.IsTrue(TransferRules.IsReservedWindowsName("CON"));
        Assert.IsTrue(TransferRules.IsReservedWindowsName("com1.txt"));
        Assert.IsFalse(TransferRules.IsReservedWindowsName("IMG_1.HEIC"));
    }
}
