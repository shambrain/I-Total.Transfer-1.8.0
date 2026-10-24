using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Where the camera roll lives is not a fixed shape. These tests pin both layouts seen in the field:
/// the documented Storage\DCIM\album\file, and the one an iPhone 13 Pro on current iOS actually
/// exposes — the DCIM albums sitting directly in the storage container, with no DCIM level at all.
/// The second shape is the one that made the app report a connected, unlocked, trusted handset as
/// "no DCIM container" and find no files.
/// </summary>
[TestClass]
public sealed class CameraRollTests
{
    /// <summary>Verbatim from a live iPhone 13 Pro: "Internal Storage" contains exactly these.</summary>
    private static readonly string?[] LiveIphone13ProStorage =
        ["202003_a", "202606_a", "202608_b", "201901_a", "202608_a"];

    [TestMethod]
    public void TheStorageContainerIsTheCameraRollWhenItHoldsDcimAlbums()
    {
        Assert.AreEqual(CameraRollShape.StorageContainer, CameraRoll.Decide(LiveIphone13ProStorage));
    }

    [TestMethod]
    public void DcimFolderWinsWhereverItAppearsInTheListing()
    {
        Assert.AreEqual(CameraRollShape.DcimFolder, CameraRoll.Decide(["DCIM"]));
        Assert.AreEqual(CameraRollShape.DcimFolder, CameraRoll.Decide(["Internal Storage", "dcim"]));
        Assert.AreEqual(CameraRollShape.DcimFolder, CameraRoll.Decide(["DCIM", "202608_a"]));
        Assert.AreEqual(CameraRollShape.DcimFolder, CameraRoll.Decide(LiveIphone13ProStorage.Append("DCIM")));
    }

    [TestMethod]
    public void MediaSittingDirectlyInTheContainerAlsoMeansItIsTheCameraRoll()
    {
        Assert.AreEqual(CameraRollShape.StorageContainer, CameraRoll.Decide(["IMG_1486.HEIC", "IMG_1486.AAE"]));
        Assert.AreEqual(CameraRollShape.StorageContainer, CameraRoll.Decide(["clip.MOV"]));
    }

    [TestMethod]
    public void AnEmptyOrUnrelatedContainerIsNotTheCameraRoll()
    {
        Assert.AreEqual(CameraRollShape.Unknown, CameraRoll.Decide([]));
        Assert.AreEqual(CameraRollShape.Unknown, CameraRoll.Decide([null, "", "   "]));
        // The other storage layout ("/var/mobile/Media") must not be mistaken for the camera roll.
        Assert.AreEqual(CameraRollShape.Unknown,
            CameraRoll.Decide(["Music", "Books", "Downloads", "Purchases", "Recordings"]));
        // A metadata sidecar is not evidence of a camera roll.
        Assert.AreEqual(CameraRollShape.Unknown, CameraRoll.Decide(["IMG_1486.AAE"]));
    }

    [TestMethod]
    public void AlbumFolderNamesAreRecognised()
    {
        // iOS camera-roll albums: current naming, older naming, and the fixed slot form.
        Assert.IsTrue(CameraRoll.IsAlbumFolder("202608_a"));
        Assert.IsTrue(CameraRoll.IsAlbumFolder("202003__"));
        Assert.IsTrue(CameraRoll.IsAlbumFolder("201901_a"));
        Assert.IsTrue(CameraRoll.IsAlbumFolder(" 202606_a "));
        Assert.IsTrue(CameraRoll.IsAlbumFolder("100APPLE"));
        Assert.IsTrue(CameraRoll.IsAlbumFolder("101APPLE"));

        Assert.IsFalse(CameraRoll.IsAlbumFolder("DCIM"));
        Assert.IsFalse(CameraRoll.IsAlbumFolder("Internal Storage"));
        Assert.IsFalse(CameraRoll.IsAlbumFolder("Music"));
        Assert.IsFalse(CameraRoll.IsAlbumFolder("20190"));       // too short to be YYYYMM
        Assert.IsFalse(CameraRoll.IsAlbumFolder("201901_a_b"));  // suffix is not a slot
        Assert.IsFalse(CameraRoll.IsAlbumFolder(""));
        Assert.IsFalse(CameraRoll.IsAlbumFolder(null));
    }

    [TestMethod]
    public void DcimContainerMatchingIgnoresCaseAndPadding()
    {
        Assert.IsTrue(CameraRoll.IsDcimContainer("DCIM"));
        Assert.IsTrue(CameraRoll.IsDcimContainer("dcim"));
        Assert.IsTrue(CameraRoll.IsDcimContainer(" Dcim "));
        Assert.IsFalse(CameraRoll.IsDcimContainer("DCIM_OLD"));
        Assert.IsFalse(CameraRoll.IsDcimContainer(null));
    }

    [TestMethod]
    public void CameraMediaIsPhotosAndVideosOnly()
    {
        Assert.IsTrue(CameraRoll.LooksLikeMedia("IMG_1486.HEIC"));
        Assert.IsTrue(CameraRoll.LooksLikeMedia("IMG_0313.JPG"));
        Assert.IsTrue(CameraRoll.LooksLikeMedia("clip.mov"));
        Assert.IsFalse(CameraRoll.LooksLikeMedia("IMG_1486.AAE"));
        Assert.IsFalse(CameraRoll.LooksLikeMedia("notes.txt"));
        Assert.IsFalse(CameraRoll.LooksLikeMedia(""));
        Assert.IsFalse(CameraRoll.LooksLikeMedia(null));
    }

    [TestMethod]
    public void TheCanonicalRootLabelKeepsTheDestinationLayoutStable()
    {
        // Both shapes resolve to the same root label, so "preserve device folder structure (DCIM\…)"
        // writes DCIM\202608_a\IMG_1486.HEIC whichever way the driver presented the tree.
        Assert.AreEqual("DCIM", CameraRoll.RootLabel);
        Assert.AreEqual(Path.Combine("DCIM", "202608_a"), Path.Combine(CameraRoll.RootLabel, "202608_a"));
    }
}
