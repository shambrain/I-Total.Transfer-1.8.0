using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The storage rules for a generic MTP device. Two of them keep a backup honest rather than merely
/// working: the app-private trees an Android phone refuses to expose are skipped instead of being
/// descended into and reported as failures, and a phone left in "Transfer photos" mode — which really
/// does expose only DCIM and Pictures — raises a warning instead of producing a scan that looks
/// complete and silently is not.
/// </summary>
[TestClass]
public sealed class PortableStorageTests
{
    // ------------------------------------------------------------------ which folders are off limits

    [TestMethod]
    public void TheAppPrivateTreesAreNotDescendedIntoButTheRestOfAndroidIs()
    {
        Assert.IsTrue(PortableStorage.ShouldSkipFolder(@"Internal shared storage\Android\data", "data"),
            "Android denies a file-transfer client its per-app data; walking it costs an error per app.");
        Assert.IsTrue(PortableStorage.ShouldSkipFolder(@"Internal shared storage\Android\obb", "obb"));

        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"Internal shared storage\Android", "Android"),
            "The folder itself must still be walked: Android\\media is where apps such as WhatsApp keep photos.");
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"Internal shared storage\Android\media", "media"));
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(
            @"Internal shared storage\Android\media\com.whatsapp\WhatsApp Images", "WhatsApp Images"));
    }

    [TestMethod]
    public void OnlyTheExactAndroidDataPositionIsRefusedNotAnyFolderCalledData()
    {
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"Internal shared storage\Download\data", "data"),
            "A folder the operator created called 'data' is theirs and is backed up normally.");
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"SD card\Holiday\obb", "obb"));
    }

    [TestMethod]
    public void ThumbnailCachesAreSkippedBecauseTheyAreCopiesOfTheImagesAlreadyBeingCopied()
    {
        Assert.IsTrue(PortableStorage.ShouldSkipFolder(@"Internal shared storage\DCIM\.thumbnails", ".thumbnails"));
        Assert.IsTrue(PortableStorage.ShouldSkipFolder(@"Internal shared storage\DCIM", ".THUMBNAILS"),
            "The check is case-insensitive, as the filesystem is.");
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"Internal shared storage\DCIM\Camera", "Camera"));
    }

    [TestMethod]
    public void AnEmptyOrMissingPathIsNeverSkipped()
    {
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(null, null));
        Assert.IsFalse(PortableStorage.ShouldSkipFolder("", ""));
        Assert.IsFalse(PortableStorage.ShouldSkipFolder(@"Internal shared storage", null));
    }

    // ------------------------------------------------------------------ naming a volume's destination

    [TestMethod]
    public void AVolumeKeepsItsOwnNameAsItsDestinationFolder()
    {
        Assert.AreEqual("Internal shared storage", PortableStorage.RootLabel("Internal shared storage"));
        Assert.AreEqual("SD card", PortableStorage.RootLabel("  SD card  "));
        Assert.AreEqual("Phone storage", PortableStorage.RootLabel("Phone storage"));
    }

    [TestMethod]
    public void AVolumeNameIsMadeSafeToUseAsAFolderWithoutInventingANewOne()
    {
        Assert.AreEqual("My Card", PortableStorage.RootLabel("My:Card?"), "Windows rejects ':' and '?' in a folder name.");
        Assert.AreEqual("Card", PortableStorage.RootLabel("Card."), "A folder name may not end in a dot.");
        Assert.AreEqual("A B", PortableStorage.RootLabel("A   B"), "Runs of whitespace collapse to one space.");
        Assert.AreEqual("A B", PortableStorage.RootLabel("A\\B"), "A separator cannot survive inside one segment.");
        Assert.AreEqual(PortableStorage.FallbackRootLabel, PortableStorage.RootLabel(null));
        Assert.AreEqual(PortableStorage.FallbackRootLabel, PortableStorage.RootLabel("   "));
        Assert.AreEqual(PortableStorage.FallbackRootLabel, PortableStorage.RootLabel("???"),
            "A name with nothing usable left falls back rather than producing an empty folder name.");
    }

    // ------------------------------------------------------------------ the wrong USB mode

    [TestMethod]
    public void ACameraOnlyListingLooksLikePhotoTransferMode()
    {
        Assert.IsTrue(PortableStorage.LooksLikePhotoTransferOnly(["DCIM", "Pictures"]),
            "Photo-transfer (PTP) mode exposes exactly the camera folders and nothing else.");
        Assert.IsTrue(PortableStorage.LooksLikePhotoTransferOnly(["DCIM"]));
    }

    [TestMethod]
    public void OneFolderThatOnlyFileTransferModeExposesClearsTheWarning()
    {
        foreach (string folder in new[] { "Download", "Music", "Movies", "Documents", "Android", "Podcasts" })
        {
            Assert.IsFalse(PortableStorage.LooksLikePhotoTransferOnly(["DCIM", "Pictures", folder]),
                $"'{folder}' is only exposed in file-transfer mode, so the phone is not in photo mode.");
        }
    }

    [TestMethod]
    public void AWarningIsNotRaisedWithoutCameraFoldersOrWithNothingAtAll()
    {
        Assert.IsFalse(PortableStorage.LooksLikePhotoTransferOnly([]));
        Assert.IsFalse(PortableStorage.LooksLikePhotoTransferOnly([null, "", "  "]));
        Assert.IsFalse(PortableStorage.LooksLikePhotoTransferOnly(["Download", "Music"]),
            "A listing with no camera folder at all is a different situation, not this one.");
    }

    [TestMethod]
    public void TheWarningNamesThePhoneSettingToChange()
    {
        StringAssert.Contains(PortableStorage.PhotoTransferOnlyWarning, "PTP");
        StringAssert.Contains(PortableStorage.PhotoTransferOnlyWarning, "File transfer");
    }

    [TestMethod]
    public void TheWarningRejectsAMissingListing()
    {
        Assert.ThrowsException<ArgumentNullException>(() => PortableStorage.LooksLikePhotoTransferOnly(null!));
    }

    // ------------------------------------------------------------------ which groups are advertised

    [TestMethod]
    public void AnAndroidDeviceAdvertisesEveryGroupItsStorageActuallyHolds()
    {
        IReadOnlyList<MediaCategory> android = PortableStorage.CategoriesFor(DeviceFamily.Android);

        CollectionAssert.AreEquivalent(MediaCategories.All, android.ToArray(),
            "An Android phone really does hold photos, video, music and documents; hiding three of them " +
            "would describe the device wrongly.");
    }

    [TestMethod]
    public void AnAppleOrUnidentifiedDeviceAdvertisesOnlyTheCameraGroups()
    {
        foreach (DeviceFamily family in new[] { DeviceFamily.Apple, DeviceFamily.Unknown })
        {
            IReadOnlyList<MediaCategory> categories = PortableStorage.CategoriesFor(family);
            CollectionAssert.AreEquivalent(
                new[] { MediaCategory.Photos, MediaCategory.Videos }, categories.ToArray());
        }

        Assert.AreEqual(DeviceFamily.Unknown, default(DeviceFamily),
            "An unprobed backend must fall back to the iPhone answer, never advertise uncovered groups.");
    }
}
