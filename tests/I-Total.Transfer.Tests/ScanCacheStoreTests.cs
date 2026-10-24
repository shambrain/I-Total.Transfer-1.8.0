using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Persistence rules for the listing cache. Every case here is about the store refusing to be clever: it must
/// never hand back a listing it cannot justify, and it must never be the reason a scan cannot run.
/// </summary>
[TestClass]
public sealed class ScanCacheStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 22, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

    private string _directory = "";

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "itotal-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }

    private ScanCacheStore Store() => new(_directory);

    private static ScanCacheKey Key(string backend = "Windows Portable Devices (native)", string device = "FCC4L3VYCW") =>
        new(backend, device);

    private static ScanResult Scan(params (string Folder, string Name, long Size)[] files)
    {
        var list = files.Select(f => new MediaFile(f.Folder, f.Name, f.Size, Path.GetExtension(f.Name))).ToList();
        return new ScanResult("Apple iPhone", Now.AddMinutes(-3), Now, list, ["sidecar files such as .AAE are included"]);
    }

    private static ScanResult IncompleteScan() => new(
        "Apple iPhone", Now.AddMinutes(-3), Now,
        [new MediaFile("DCIM", "IMG_0001.HEIC", 100, ".HEIC")],
        ["album 202608_a could not be read"])
    {
        Report = new EnumerationReport(EnumerationStatus.Incomplete, 2, 1, 1, 100, 0, 0, 0, 0, "WPD", [], [])
    };

    [TestMethod]
    public void ACompleteListingRoundTripsThroughDisk()
    {
        ScanCacheStore store = Store();
        Assert.IsTrue(store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 4096), ("DCIM", "IMG_0002.MOV", 8192)), "1.8.0", out string? note), note);

        CachedLibrary? loaded = Store().TryLoad(Key(), Now, MaxAge);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, loaded.Files.Count);
        Assert.AreEqual(12_288, loaded.TotalBytes);
        Assert.AreEqual("Apple iPhone", loaded.DeviceName);
        Assert.AreEqual("1.8.0", loaded.ProductVersion);
        Assert.AreEqual(EnumerationStatus.Complete, loaded.Status);
        Assert.AreEqual(Now, loaded.CapturedUtc);
        Assert.AreEqual(1, loaded.Warnings.Count, "The scan's own warnings travel with the listing.");
        Assert.IsNull(Store().LastNote, "A listing that loads must not also report a reason for refusing one.");
    }

    [TestMethod]
    public void WritingLeavesNoTemporaryFileBehind()
    {
        ScanCacheStore store = Store();
        store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out _);
        store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 200)), "1.8.0", out _);

        string[] leftovers = Directory.GetFiles(_directory, "*.tmp");
        Assert.AreEqual(0, leftovers.Length, "An atomic replace must not leave the temporary file on disk.");
        Assert.AreEqual(1, Directory.GetFiles(_directory, "library-*.json").Length, "One listing per key, replaced in place.");
        Assert.AreEqual(200, Store().TryLoad(Key(), Now, MaxAge)!.Files[0].Size, "The newer listing wins.");
    }

    [TestMethod]
    public void APartialOrEmptyScanNeverReplacesAGoodListing()
    {
        ScanCacheStore store = Store();
        Assert.IsTrue(store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out _));

        Assert.IsFalse(store.Save(Key(), IncompleteScan(), "1.8.0", out string? partialNote));
        StringAssert.Contains(partialNote, "Incomplete");

        Assert.IsFalse(store.Save(Key(), Scan(), "1.8.0", out string? emptyNote));
        StringAssert.Contains(emptyNote, "no files");

        CachedLibrary? still = Store().TryLoad(Key(), Now, MaxAge);
        Assert.IsNotNull(still, "The good listing must survive a partial and an empty scan.");
        Assert.AreEqual(1, still.Files.Count);
    }

    [TestMethod]
    public void AListingForAnotherDeviceOrBackendIsRefused()
    {
        Store().Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out _);

        // A different device or backend addresses a different file, so it simply has no listing: one
        // iPhone's library can never be shown as another's.
        Assert.IsNull(Store().TryLoad(Key(device: "DIFFERENT-SERIAL"), Now, MaxAge));
        Assert.IsNull(Store().TryLoad(Key(backend: "AFC / libimobiledevice"), Now, MaxAge));

        // Defence in depth: if the file at this key's path describes something else - a hand-edited file, or
        // a key scheme that changed between versions - it is refused rather than displayed.
        string path = Store().PathFor(Key());
        File.WriteAllText(path, File.ReadAllText(path).Replace("FCC4L3VYCW", "SOMEONE-ELSES-PHONE"));
        ScanCacheStore store = Store();
        Assert.IsNull(store.TryLoad(Key(), Now, MaxAge));
        StringAssert.Contains(store.LastNote, "different device");
    }

    [TestMethod]
    public void AListingOlderThanTheLimitIsRefusedWithAReason()
    {
        Store().Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out _);

        ScanCacheStore store = Store();
        Assert.IsNull(store.TryLoad(Key(), Now.AddDays(15), MaxAge));
        StringAssert.Contains(store.LastNote, "15.0 day(s) old");

        Assert.IsNotNull(Store().TryLoad(Key(), Now.AddDays(13), MaxAge), "Inside the limit it is still usable.");
    }

    [TestMethod]
    public void ACorruptFileIsSetAsideRatherThanUsedOrDeleted()
    {
        string path = Store().PathFor(Key());
        File.WriteAllText(path, "{ this is not json");

        ScanCacheStore store = Store();
        Assert.IsNull(store.TryLoad(Key(), Now, MaxAge), "Unreadable history must not be presented as history.");
        Assert.IsNotNull(store.LastNote);

        Assert.IsTrue(Directory.GetFiles(_directory, "*.corrupt-*").Length == 1, "The unreadable file is kept as evidence.");
        Assert.IsFalse(File.Exists(path), "It is moved aside, so the next save starts clean.");
    }

    [TestMethod]
    public void AListingFromAnOlderSchemaIsIgnoredRatherThanGuessedAt()
    {
        string path = Store().PathFor(Key());
        File.WriteAllText(path, "{\"SchemaVersion\":0,\"BackendName\":\"Windows Portable Devices (native)\",\"DeviceKey\":\"FCC4L3VYCW\",\"Files\":[{\"Folder\":\"DCIM\",\"Name\":\"a.HEIC\",\"Size\":1,\"Extension\":\".HEIC\"}]}");

        ScanCacheStore store = Store();
        Assert.IsNull(store.TryLoad(Key(), Now, MaxAge));
        StringAssert.Contains(store.LastNote, "schema");
        Assert.IsTrue(File.Exists(path), "A schema mismatch is a version question, not corruption: the file stays put.");
    }

    [TestMethod]
    public void ForgetRemovesOneListingAndForgetAllRemovesTheRest()
    {
        ScanCacheStore store = Store();
        store.Save(Key(device: "A"), Scan(("DCIM", "a.HEIC", 1)), "1.8.0", out _);
        store.Save(Key(device: "B"), Scan(("DCIM", "b.HEIC", 1)), "1.8.0", out _);

        Assert.IsTrue(store.Forget(Key(device: "A")));
        Assert.IsFalse(store.Forget(Key(device: "A")), "Forgetting something that is gone is not an error, just not a removal.");
        Assert.IsNull(Store().TryLoad(Key(device: "A"), Now, MaxAge));
        Assert.IsNotNull(Store().TryLoad(Key(device: "B"), Now, MaxAge));

        Assert.AreEqual(1, store.ForgetAll());
        Assert.AreEqual(0, store.List().Count);

        ScanCacheStore empty = Store();
        Assert.AreEqual(0, empty.ForgetAll(), "Clearing an empty cache is a no-op, not a failure.");
        Assert.IsNull(empty.TryLoad(Key(), Now, MaxAge));
    }

    [TestMethod]
    public void TheInventoryReportsWhatIsOnDiskIncludingUnreadableFiles()
    {
        ScanCacheStore store = Store();
        store.Save(Key(device: "A"), Scan(("DCIM", "a.HEIC", 1)), "1.8.0", out _);
        File.WriteAllText(Path.Combine(_directory, "library-broken-deadbeef.json"), "{ not json");

        IReadOnlyList<CachedListing> listings = store.List();

        Assert.AreEqual(2, listings.Count, "An unreadable listing is still something the operator should be able to see and delete.");
        Assert.AreEqual(1, listings.Count(listing => listing.IsReadable));
        Assert.AreEqual(1, listings.Count(listing => !listing.IsReadable));
        Assert.IsTrue(listings.All(listing => listing.Bytes > 0));
        Assert.AreEqual("Apple iPhone", listings.Single(listing => listing.IsReadable).Library!.DeviceName);
    }

    [TestMethod]
    public void ACorruptListingDoesNotStopALaterSaveOrLoad()
    {
        // The whole point of the cache being optional: whatever is on disk, the next scan can still cache.
        File.WriteAllText(Store().PathFor(Key()), "garbage");
        Assert.IsNull(Store().TryLoad(Key(), Now, MaxAge));

        ScanCacheStore store = Store();
        Assert.IsTrue(store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out string? note), note);
        Assert.IsNotNull(Store().TryLoad(Key(), Now, MaxAge));
    }

    [TestMethod]
    public void AnUnwritableCacheFolderIsReportedRatherThanThrown()
    {
        // A feature that only makes the window prettier must never be able to break a scan.
        var store = new ScanCacheStore(Path.Combine(_directory, "a-file-not-a-folder"));
        File.WriteAllText(Path.Combine(_directory, "a-file-not-a-folder"), "in the way");

        Assert.IsFalse(store.Save(Key(), Scan(("DCIM", "IMG_0001.HEIC", 100)), "1.8.0", out string? note));
        Assert.IsNotNull(note, "The caller is told why nothing was cached.");
        Assert.IsNull(store.TryLoad(Key(), Now, MaxAge));
    }
}
