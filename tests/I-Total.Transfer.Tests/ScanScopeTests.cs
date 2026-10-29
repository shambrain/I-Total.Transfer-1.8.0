using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Inventory scope is a separate fact from enumeration status. These tests pin the comparison rules: a
/// Complete scan may still describe a smaller set of folders than the last complete listing, and that must
/// be surfaced as a scope change rather than silently treated as the whole library.
/// </summary>
[TestClass]
public sealed class ScanScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

    private static MediaFile File(string folder, string name, long size = 100) =>
        new(folder, name, size, Path.GetExtension(name));

    private static ScanResult Scan(params (string Folder, string Name, long Size)[] files) =>
        new("iPhone", Now.AddMinutes(-1), Now,
            files.Select(f => File(f.Folder, f.Name, f.Size)).ToList(), []);

    private static CachedLibrary Cached(params (string Folder, string Name, long Size)[] files)
    {
        var cached = new CachedLibrary
        {
            CapturedUtc = Now.AddHours(-2),
            EnumerationStatus = nameof(EnumerationStatus.Complete),
            DeviceName = "iPhone"
        };
        foreach ((string folder, string name, long size) in files)
            cached.Files.Add(new CachedFile(folder, name, size, Path.GetExtension(name), null));
        return cached;
    }

    private static (string, string, long)[] Many(string folder, int count) =>
        Enumerable.Range(0, count).Select(i => (folder, $"IMG_{i:D5}.JPG", 100L)).ToArray();

    [TestMethod]
    public void NoPreviousListingIsUnknownNotStable()
    {
        InventoryScope scope = ScanScope.Compare(null, Scan(("DCIM\\A", "a.jpg", 100)));

        Assert.AreEqual(InventoryScopeStatus.Unknown, scope.Status);
        Assert.IsFalse(scope.IsChanged);
        StringAssert.Contains(scope.Detail, "No previous complete listing");
    }

    [TestMethod]
    public void AnEmptyPreviousListingIsUnknown()
    {
        InventoryScope scope = ScanScope.Compare(new CachedLibrary(), Scan(("DCIM\\A", "a.jpg", 100)));

        Assert.AreEqual(InventoryScopeStatus.Unknown, scope.Status);
    }

    [TestMethod]
    public void AnIdenticalInventoryIsStable()
    {
        var files = Many("DCIM\\A", 500).Concat(Many("DCIM\\B", 700)).ToArray();

        InventoryScope scope = ScanScope.Compare(Cached(files), Scan(files));

        Assert.AreEqual(InventoryScopeStatus.Stable, scope.Status);
        Assert.AreEqual(1200, scope.CurrentFiles);
        Assert.AreEqual(1200, scope.PreviousFiles);
        Assert.AreEqual(0, scope.MissingFolders.Count);
    }

    [TestMethod]
    public void AMissingAlbumMakesTheScopeChangedAndNamesIt()
    {
        var before = Many("DCIM\\202610_a", 2543).Concat(Many("DCIM\\202608_a", 26320)).ToArray();
        var after = Many("DCIM\\202608_a", 26320);

        InventoryScope scope = ScanScope.Compare(Cached(before), Scan(after));

        Assert.AreEqual(InventoryScopeStatus.Changed, scope.Status);
        Assert.AreEqual(1, scope.MissingFolders.Count, string.Join(" | ", scope.MissingFolders.Select(f => f.Folder + ":" + f.Files)));
        Assert.AreEqual("DCIM\\202610_a", scope.MissingFolders[0].Folder);
        Assert.AreEqual(2543, scope.MissingFolders[0].Files);
        StringAssert.Contains(scope.Detail, "DCIM\\202610_a");
        StringAssert.Contains(scope.Detail, "cause is not known");
        StringAssert.Contains(scope.Detail, "Rescan");
    }

    [TestMethod]
    public void ASmallCountChangeIsStable()
    {
        var before = Many("DCIM\\A", 1000);
        var after = Many("DCIM\\A", 950); // 50 fewer, under the 100-file floor

        Assert.AreEqual(InventoryScopeStatus.Stable, ScanScope.Compare(Cached(before), Scan(after)).Status);
    }

    [TestMethod]
    public void AMaterialDropInAnExistingFolderIsChanged()
    {
        var before = Many("DCIM\\A", 2000);
        var after = Many("DCIM\\A", 1000); // folder still present, count halved

        InventoryScope scope = ScanScope.Compare(Cached(before), Scan(after));

        Assert.AreEqual(InventoryScopeStatus.Changed, scope.Status);
        Assert.AreEqual(0, scope.MissingFolders.Count, "the folder still exists; the drop itself is the signal");
        StringAssert.Contains(scope.Detail, "dropped by 1,000");
    }

    [TestMethod]
    public void ANewFolderAloneIsNotAChange()
    {
        var before = Many("DCIM\\A", 1000);
        var after = Many("DCIM\\A", 1000).Concat(Many("DCIM\\B", 50)).ToArray();

        InventoryScope scope = ScanScope.Compare(Cached(before), Scan(after));

        Assert.AreEqual(InventoryScopeStatus.Stable, scope.Status, "new photos are normal, not a scope loss");
        Assert.AreEqual(1, scope.AddedFolders.Count);
        Assert.AreEqual("DCIM\\B", scope.AddedFolders[0].Folder);
    }

    [TestMethod]
    public void AChangedScopeDoesNotReplaceTheCachedBaseline()
    {
        var changed = new InventoryScope(InventoryScopeStatus.Changed, 57731, 26336, [], [], "changed");
        var stable = new InventoryScope(InventoryScopeStatus.Stable, 100, 100, [], [], "stable");

        Assert.IsFalse(ScanCachePolicy.ShouldReplace(changed), "a partial mount must not erase the larger baseline");
        Assert.IsTrue(ScanCachePolicy.ShouldReplace(stable));
        Assert.IsTrue(ScanCachePolicy.ShouldReplace(null), "a first scan has no baseline to protect");
    }

    [TestMethod]
    public void FolderNamesAreComparedCaseInsensitivelyLikeWindows()
    {
        var before = Many("DCIM\\A", 200);
        var after = Many("dcim\\a", 200);

        Assert.AreEqual(InventoryScopeStatus.Stable, ScanScope.Compare(Cached(before), Scan(after)).Status);
    }
}
