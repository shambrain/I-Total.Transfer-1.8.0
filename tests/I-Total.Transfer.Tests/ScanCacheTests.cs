using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The rules that stop a cached listing from being mistaken for a live scan, plus the difference report the
/// operator sees when the live scan disagrees with what was on screen.
/// </summary>
[TestClass]
public sealed class ScanCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 22, 0, 0, TimeSpan.Zero);

    private static ScanResult Scan(params (string Folder, string Name, long Size)[] files)
    {
        var list = files.Select(f => new MediaFile(f.Folder, f.Name, f.Size, Path.GetExtension(f.Name))).ToList();
        return new ScanResult("Apple iPhone", Now.AddMinutes(-1), Now, list, []);
    }

    private static CachedLibrary Cache(ScanResult scan, ScanCacheKey? key = null) =>
        CachedLibrary.FromScan(key ?? Key(), scan, "1.8.0");

    private static ScanCacheKey Key(string backend = "Windows Portable Devices (native)", string device = "FCC4L3VYCW") =>
        new(backend, device);

    // ------------------------------------------------------------------ the key

    [TestMethod]
    public void TheKeyPrefersStableIdentityThenSerialThenModel()
    {
        Assert.AreEqual("stable-id", ScanCacheKey.From("WPD", "stable-id", "serial", "iPhone 13 Pro").DeviceKey);
        Assert.AreEqual("serial", ScanCacheKey.From("WPD", null, "serial", "iPhone 13 Pro").DeviceKey);
        // A model name is the weakest identity: two phones of one model must not obviously share a listing,
        // so it is only used when nothing better exists - and it still lands in a different file name than
        // any real serial would.
        Assert.AreEqual("iPhone 13 Pro", ScanCacheKey.From("WPD", "  ", null, "iPhone 13 Pro").DeviceKey);
        Assert.AreEqual("unknown-device", ScanCacheKey.From("WPD", null, null, null).DeviceKey);
    }

    [TestMethod]
    public void TheKeyNeverPutsTheDeviceIdentityInTheFileName()
    {
        string name = Key(device: "FCC4L3VYCW").FileName;
        StringAssert.StartsWith(name, "library-");
        StringAssert.EndsWith(name, ".json");
        Assert.IsFalse(name.Contains("FCC4L3VYCW", StringComparison.OrdinalIgnoreCase),
            "A serial number must not appear in a file name that archival or support tooling may copy around.");
        // Different devices and different backends must not collide on one file.
        Assert.AreNotEqual(Key(device: "A").FileName, Key(device: "B").FileName);
        Assert.AreNotEqual(Key(backend: "A").FileName, Key(backend: "B").FileName);
        Assert.AreEqual(Key().FileName, Key().FileName, "The same key must always address the same file.");
    }

    // ------------------------------------------------------------------ what may be shown

    [TestMethod]
    public void ACompleteListingIsUsableWithinItsAge()
    {
        CachedLibrary cached = Cache(Scan(("DCIM", "IMG_0001.HEIC", 100)));
        Assert.IsTrue(cached.IsUsableAt(Now, TimeSpan.FromDays(14)));
        Assert.IsTrue(cached.IsUsableAt(Now.AddDays(14), TimeSpan.FromDays(14)), "The limit is inclusive.");
        Assert.IsFalse(cached.IsUsableAt(Now.AddDays(14).AddSeconds(1), TimeSpan.FromDays(14)));
    }

    [TestMethod]
    public void AnIncompleteOrEmptyListingIsNeverShown()
    {
        ScanResult scan = Scan(("DCIM", "IMG_0001.HEIC", 100));
        var incomplete = new ScanResult(scan.DeviceName, scan.StartedUtc, scan.FinishedUtc, scan.Files, ["album 202608_a could not be read"])
        {
            Report = new EnumerationReport(EnumerationStatus.Incomplete, 2, 1, 1, 100, 0, 0, 0, 0, "WPD", [], [])
        };
        Assert.IsFalse(Cache(incomplete).IsUsableAt(Now, TimeSpan.FromDays(14)),
            "A partial listing must not be offered as the last known library.");

        var empty = new ScanResult(scan.DeviceName, scan.StartedUtc, scan.FinishedUtc, [], []);
        Assert.IsFalse(Cache(empty).IsUsableAt(Now, TimeSpan.FromDays(14)),
            "An enumeration that found nothing is not a library worth remembering in place of one.");
    }

    [TestMethod]
    public void AListingFromAnotherSchemaIsRefused()
    {
        CachedLibrary cached = Cache(Scan(("DCIM", "IMG_0001.HEIC", 100)));
        cached.SchemaVersion = CachedLibrary.CurrentSchemaVersion + 1;
        Assert.IsFalse(cached.IsUsableAt(Now, TimeSpan.FromDays(14)));
    }

    // ------------------------------------------------------------------ what is stored

    [TestMethod]
    public void TheBackendObjectIdIsDeliberatelyNotPersisted()
    {
        var file = new MediaFile("DCIM", "IMG_0001.HEIC", 100, ".HEIC")
        {
            ModifiedUtc = Now.AddDays(-3),
            SourceObjectId = "opaque-object-id-12345"
        };
        var scan = new ScanResult("Apple iPhone", Now.AddMinutes(-1), Now, [file], []);

        CachedLibrary cached = Cache(scan);

        // The id is documented as not durable across reconnects, so it is dropped rather than resurrected
        // to point at whatever now answers to it.
        Assert.IsNull(cached.Files[0].ToMediaFile().SourceObjectId);
        Assert.AreEqual("DCIM", cached.Files[0].Folder);
        Assert.AreEqual("IMG_0001.HEIC", cached.Files[0].Name);
        Assert.AreEqual(100, cached.Files[0].Size);
        Assert.AreEqual(".HEIC", cached.Files[0].Extension);
        Assert.AreEqual(Now.AddDays(-3), cached.Files[0].ModifiedUtc, "The timestamp is durable and is kept.");
        Assert.AreEqual(100, cached.TotalBytes);
        Assert.AreEqual(EnumerationStatus.Complete, cached.Status);
    }

    // ------------------------------------------------------------------ the difference report

    [TestMethod]
    public void TheDifferenceCountsAdditionsRemovalsAndSizeChanges()
    {
        CachedLibrary cached = Cache(Scan(
            ("DCIM", "IMG_0001.HEIC", 100),
            ("DCIM", "IMG_0002.HEIC", 200),
            ("DCIM", "IMG_0003.HEIC", 300)));

        ScanResult fresh = Scan(
            ("DCIM", "IMG_0001.HEIC", 100),   // unchanged
            ("DCIM", "IMG_0002.HEIC", 999),   // size changed
            ("DCIM", "IMG_0004.HEIC", 400));  // added; 0003 removed

        ScanCacheDiff diff = ScanCacheDiff.Between(cached, fresh);

        Assert.AreEqual(1, diff.Added);
        Assert.AreEqual(1, diff.Removed);
        Assert.AreEqual(1, diff.SizeChanged);
        Assert.AreEqual(1, diff.Unchanged);
        Assert.AreEqual(3, diff.ChangedCount);
        Assert.IsFalse(diff.IsIdentical);
        StringAssert.Contains(diff.Describe(), "1 added");
        StringAssert.Contains(diff.Describe(), "1 removed");
        StringAssert.Contains(diff.Describe(), "1 changed size");
        Assert.IsTrue(diff.Examples.Any(e => e.Contains("IMG_0004.HEIC")), "An added path is named.");
        Assert.IsTrue(diff.Examples.Any(e => e.Contains("IMG_0003.HEIC")), "A removed path is named.");
    }

    [TestMethod]
    public void MatchingIgnoresCaseAndPathSeparatorsTheWayTheFilesystemDoes()
    {
        CachedLibrary cached = Cache(Scan(("DCIM/Sub", "IMG_0001.HEIC", 100)));
        ScanResult fresh = Scan(("dcim/sub", "img_0001.heic", 100));

        ScanCacheDiff diff = ScanCacheDiff.Between(cached, fresh);

        Assert.IsTrue(diff.IsIdentical, "Windows paths are case-insensitive; a case-only change is not a change.");
        Assert.AreEqual(1, diff.Unchanged);
        Assert.AreEqual("the live scan matched the cached listing exactly", diff.Describe());
    }

    [TestMethod]
    public void TheFirstScanEverIsReportedAsAllAdded()
    {
        ScanCacheDiff diff = ScanCacheDiff.Between(null, Scan(("DCIM", "IMG_0001.HEIC", 100)));
        Assert.AreEqual(1, diff.Added);
        Assert.AreEqual(0, diff.Removed);
        Assert.AreEqual(1, diff.ChangedCount, "Only the addition is a change.");
    }

    [TestMethod]
    public void TheExcerptIsBoundedSoAHugeChangeCannotFloodTheLog()
    {
        var previous = Enumerable.Range(0, 500).Select(i => ("DCIM", $"IMG_{i:D4}.HEIC", 100L)).ToArray();
        var current = Enumerable.Range(1_000, 500).Select(i => ("DCIM", $"IMG_{i:D4}.HEIC", 100L)).ToArray();

        ScanCacheDiff diff = ScanCacheDiff.Between(Cache(Scan(previous)), Scan(current));

        Assert.AreEqual(500, diff.Added);
        Assert.AreEqual(500, diff.Removed);
        Assert.AreEqual(5, diff.Examples.Count, "Only a handful of paths are quoted, however large the change.");
    }
}
