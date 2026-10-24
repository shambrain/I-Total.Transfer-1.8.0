using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The path lookup a resumed transfer depends on. Object ids are deliberately not persisted — they do not
/// survive a reconnect — so every file in a recovered run is found by its relative path, and the cost of
/// that lookup decides whether continuing a 26,000-item session is minutes of device chatter or hours of it.
/// The property that matters is asserted directly: folders are read once each, never once per file.
/// </summary>
[TestClass]
public sealed class FolderNameIndexTests
{
    private const string Root = "root-object-id";

    /// <summary>A device tree, standing in for the phone's WPD object graph.</summary>
    private sealed class FakeDevice
    {
        private readonly Dictionary<string, List<DeviceChild>> _folders = new(StringComparer.Ordinal);

        /// <summary>How many times a folder had to be read off the device.</summary>
        public int Reads { get; private set; }

        public FakeDevice Add(string folderId, params DeviceChild[] children)
        {
            _folders[folderId] = children.ToList();
            return this;
        }

        public IEnumerable<DeviceChild> Children(string folderId)
        {
            Reads++;
            return _folders.TryGetValue(folderId, out List<DeviceChild>? children) ? children : [];
        }
    }

    private static DeviceChild Folder(string id, string name) => new(id, name, IsFolder: true);

    private static DeviceChild File(string id, string name) => new(id, name, IsFolder: false);

    [TestMethod]
    public void EveryFileInOneAlbumReadsThatAlbumOnce()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", Enumerable.Range(0, 5_000).Select(i => File($"obj-{i}", $"IMG_{i:D4}.HEIC")).ToArray());
        var index = new FolderNameIndex(device.Children);

        for (int i = 0; i < 5_000; i++)
        {
            ObjectLocation found = index.Locate(Root, "DCIM/100APPLE", $"IMG_{i:D4}.HEIC");
            Assert.AreEqual(ObjectLookup.Found, found.Outcome);
            Assert.AreEqual($"obj-{i}", found.Id);
        }

        Assert.AreEqual(2, index.FolderEnumerations,
            "The media root and the album are each read once; a per-file walk would be 5,000 reads of the album.");
        Assert.AreEqual(2, device.Reads);
    }

    [TestMethod]
    public void ThreeAlbumsOfTwentySixThousandFilesReadFourFolders()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("a", "100APPLE"), Folder("b", "101APPLE"), Folder("c", "102APPLE"));
        var expected = new List<(string Folder, string Name)>();
        foreach ((string id, string name) in new[] { ("a", "100APPLE"), ("b", "101APPLE"), ("c", "102APPLE") })
        {
            var children = Enumerable.Range(0, 8_700).Select(i => File($"{id}-{i}", $"IMG_{i:D5}.HEIC")).ToArray();
            device.Add(id, children);
            expected.AddRange(children.Select(c => (name, c.Name)));
        }

        var index = new FolderNameIndex(device.Children);
        foreach ((string folder, string name) in expected)
        {
            ObjectLocation found = index.Locate(Root, "DCIM/" + folder, name);
            Assert.AreEqual(ObjectLookup.Found, found.Outcome, $"'{folder}/{name}' must resolve.");
        }

        Assert.AreEqual(4, device.Reads, "One read for the root and one per album, for 26,100 files.");
    }

    [TestMethod]
    public void ThePathIsFollowedThroughNestedFolders()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", Folder("bursts", "Bursts"), File("other", "IMG_9999.HEIC"))
            .Add("bursts", File("deep", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        ObjectLocation found = index.Locate(Root, "DCIM/100APPLE/Bursts", "IMG_0001.HEIC");

        Assert.AreEqual(ObjectLookup.Found, found.Outcome);
        Assert.AreEqual("deep", found.Id);
    }

    [TestMethod]
    public void TheCanonicalRootLabelNamesTheRootItself()
    {
        var device = new FakeDevice().Add(Root, File("at-root", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        ObjectLocation found = index.Locate(Root, "DCIM", "IMG_0001.HEIC");

        Assert.AreEqual(ObjectLookup.Found, found.Outcome);
        Assert.AreEqual("at-root", found.Id, "A DCIM segment must not be looked for underneath the root.");
    }

    [TestMethod]
    public void AMissingFileIsReportedAsSuchAndAMissingFolderNamesItself()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", File("one", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        Assert.AreEqual(ObjectLookup.FileMissing, index.Locate(Root, "DCIM/100APPLE", "IMG_9999.HEIC").Outcome);

        ObjectLocation gone = index.Locate(Root, "DCIM/101APPLE", "IMG_0001.HEIC");
        Assert.AreEqual(ObjectLookup.FolderMissing, gone.Outcome);
        Assert.AreEqual("101APPLE", gone.MissingSegment);
    }

    [TestMethod]
    public void MatchingIgnoresCaseTheWayTheDeviceDoes()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", File("one", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        Assert.AreEqual("one", index.Locate(Root, "dcim/100apple", "img_0001.heic").Id);
    }

    [TestMethod]
    public void ANamelessChildIsNeverTheMatch()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", File("nameless", ""), File("named", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        Assert.AreEqual("named", index.Locate(Root, "DCIM/100APPLE", "IMG_0001.HEIC").Id);
    }

    [TestMethod]
    public void AMissingLookupDoesNotPoisonALaterOne()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", File("one", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);

        Assert.AreEqual(ObjectLookup.FileMissing, index.Locate(Root, "DCIM/100APPLE", "IMG_0002.HEIC").Outcome);
        Assert.AreEqual("one", index.Locate(Root, "DCIM/100APPLE", "IMG_0001.HEIC").Id);
        Assert.AreEqual(2, device.Reads, "A miss must not cost a second read of the same folder.");
    }

    [TestMethod]
    public void ClearingForgetsTheDeviceSoAReconnectReadsItAgain()
    {
        var device = new FakeDevice()
            .Add(Root, Folder("album", "100APPLE"))
            .Add("album", File("one", "IMG_0001.HEIC"));
        var index = new FolderNameIndex(device.Children);
        Assert.AreEqual("one", index.Locate(Root, "DCIM/100APPLE", "IMG_0001.HEIC").Id);
        Assert.AreEqual(2, device.Reads);

        index.Clear();

        Assert.AreEqual(0, index.FolderEnumerations, "A dropped session must not keep stale object ids.");
        Assert.AreEqual("one", index.Locate(Root, "DCIM/100APPLE", "IMG_0001.HEIC").Id);
        Assert.AreEqual(4, device.Reads, "The folders are read afresh after a reconnect.");
    }

    [TestMethod]
    public void AVolumeWhoseRootIsNotCalledDcimIsLocatedFromItsOwnLabel()
    {
        // A generic MTP device walks its storage volumes with the volume's own name as the root label,
        // so the relative paths of everything found begin with that name. The lookup has to skip the
        // caller's own root label — while still descending into a folder that happens to be called
        // "DCIM" underneath it, which is where an Android camera roll actually lives.
        const string volume = "volume-object-id";
        var device = new FakeDevice()
            .Add(volume, Folder("dcim", "DCIM"), Folder("pictures", "Pictures"))
            .Add("dcim", Folder("camera", "Camera"))
            .Add("camera", File("shot", "IMG_20260801_120000.jpg"));
        var index = new FolderNameIndex(device.Children);

        Assert.AreEqual("shot", index.Locate(volume, @"Internal shared storage\DCIM\Camera",
            "IMG_20260801_120000.jpg", "Internal shared storage").Id,
            "The volume's own name is the root, not a folder under it, so it must be skipped as a segment.");

        // The iPhone's default label still applies when the caller does not name one.
        Assert.AreEqual(ObjectLookup.FolderMissing, index.Locate(volume, "Internal shared storage", "x.jpg").Outcome);
    }
}
