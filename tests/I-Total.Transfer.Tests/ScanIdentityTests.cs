using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotal.Transfer.Tests;

/// <summary>
/// A scan is only the selected phone's listing. These cases lock the identity check that stops a live
/// scan from one attached handset being accepted as the other handset's tree (and cached under its key).
/// </summary>
[TestClass]
public sealed class ScanIdentityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 7, 0, 0, TimeSpan.Zero);

    private static DeviceInfo Selected(string name, DeviceFamily family, string? stableId) =>
        new(DeviceStatus.Ready, name, "ready")
        {
            Family = family,
            StableId = stableId
        };

    private static ScanResult Scan(string name, DeviceFamily family, string? stableId) =>
        new(name, Now.AddSeconds(-5), Now, [], [])
        {
            Family = family,
            StableId = stableId
        };

    [TestMethod]
    public void SameStableIdIsTheSelectedPhone()
    {
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, "wpd-apple-1");
        ScanResult scan = Scan("Apple iPhone", DeviceFamily.Apple, "wpd-apple-1");

        Assert.IsTrue(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void DifferentStableIdIsRejectedEvenWhenTheFriendlyNameMatches()
    {
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, "wpd-apple-1");
        ScanResult scan = Scan("Apple iPhone", DeviceFamily.Apple, "wpd-apple-2");

        Assert.IsFalse(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void StableIdTakesPriorityOverAMatchingFamilyAndName()
    {
        DeviceInfo selected = Selected("Android phone", DeviceFamily.Android, "mtp-huawei");
        ScanResult scan = Scan("Android phone", DeviceFamily.Android, "mtp-samsung");

        Assert.IsFalse(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void DifferentFamiliesAreRejectedWhenNoStableIdWasReported()
    {
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, null);
        ScanResult scan = Scan("HUAWEI Y7 Prime 2018", DeviceFamily.Android, null);

        Assert.IsFalse(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void SameFamilyWithADifferentNameIsRejectedWithoutAStableId()
    {
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, null);
        ScanResult scan = Scan("A different iPhone", DeviceFamily.Apple, null);

        Assert.IsFalse(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void SameFamilyAndNameIsAcceptedWithoutAStableId()
    {
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, null);
        ScanResult scan = Scan("apple iphone", DeviceFamily.Apple, null);

        Assert.IsTrue(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void UnknownIdentityOnBothSidesKeepsLegacyBackendsWorking()
    {
        DeviceInfo selected = Selected("Test device", DeviceFamily.Unknown, null);
        ScanResult scan = Scan("Test device", DeviceFamily.Unknown, null);

        Assert.IsTrue(scan.BelongsTo(selected));
    }

    [TestMethod]
    public void AStableIdOnOneSideIsNotTreatedAsAMismatch()
    {
        // The Shell backend has no stable id; the WPD identity is not evidence that the listing is wrong.
        DeviceInfo selected = Selected("Apple iPhone", DeviceFamily.Apple, "wpd-apple-1");
        ScanResult scan = Scan("Apple iPhone", DeviceFamily.Apple, null);

        Assert.IsTrue(scan.BelongsTo(selected));
    }
}
