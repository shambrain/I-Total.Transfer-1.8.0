using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The banner advertises Photos, Videos, Music and Files. These tests pin the grouping rules and the
/// capability gate that stops the UI advertising a group a backend cannot actually read.
/// </summary>
[TestClass]
public sealed class MediaCategoriesTests
{
    [TestMethod]
    public void ExtensionsMapToTheBannerGroups()
    {
        Assert.AreEqual(MediaCategory.Photos, MediaCategories.Of(".HEIC"));
        Assert.AreEqual(MediaCategory.Photos, MediaCategories.Of("jpg"));
        Assert.AreEqual(MediaCategory.Photos, MediaCategories.Of(" .PNG "));
        Assert.AreEqual(MediaCategory.Videos, MediaCategories.Of(".MOV"));
        Assert.AreEqual(MediaCategory.Videos, MediaCategories.Of("mp4"));
        Assert.AreEqual(MediaCategory.Music, MediaCategories.Of(".m4a"));
        Assert.AreEqual(MediaCategory.Music, MediaCategories.Of(".FLAC"));
        Assert.AreEqual(MediaCategory.Files, MediaCategories.Of(".pdf"));
        Assert.AreEqual(MediaCategory.Files, MediaCategories.Of(""));
    }

    [TestMethod]
    public void MediaFileCategoryFollowsItsExtension()
    {
        Assert.AreEqual(MediaCategory.Photos, new MediaFile("DCIM", "IMG_0001.HEIC", 10, ".heic").Category);
        Assert.AreEqual(MediaCategory.Videos, new MediaFile("DCIM", "IMG_0001.MOV", 10, ".mov").Category);
        Assert.AreEqual(MediaCategory.Music, new MediaFile("Media", "song.m4a", 10, ".m4a").Category);
        Assert.AreEqual(MediaCategory.Files, new MediaFile("Docs", "notes.pdf", 10, ".pdf").Category);
    }

    [TestMethod]
    public void EveryGroupHasATitleTaglineAndSource()
    {
        foreach (MediaCategory category in MediaCategories.All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(category.Title()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(category.Tagline()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(category.Source()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(category.Source(DeviceFamily.Android)));
        }

        Assert.AreEqual(4, MediaCategories.All.Length);
        Assert.AreEqual("Keep your memories", MediaCategory.Photos.Tagline());
    }

    [TestMethod]
    public void TheSourceNamesTheDeviceFamilyThatHoldsTheGroup()
    {
        // An Android music file does not come "through the iPhone media area": it is exposed by the
        // phone's own storage, and the explanation must not tell the operator otherwise.
        foreach (MediaCategory category in MediaCategories.All)
        {
            string apple = category.Source(DeviceFamily.Apple);
            string android = category.Source(DeviceFamily.Android);
            Assert.AreNotEqual(apple, android);
            Assert.IsFalse(android.Contains("iPhone", StringComparison.OrdinalIgnoreCase));
            if (category is MediaCategory.Music or MediaCategory.Files)
                Assert.IsFalse(android.Contains("DCIM", StringComparison.OrdinalIgnoreCase),
                    "Music and documents are not stored in DCIM on either family.");
        }

        StringAssert.Contains(MediaCategory.Photos.Source(DeviceFamily.Android), "DCIM");
        StringAssert.Contains(MediaCategory.Photos.Source(DeviceFamily.Apple), "DCIM");
    }

    [TestMethod]
    public void BackendWithoutEnumeratorsExposesNoGroups()
    {
        BackendCapabilities none = BackendCapabilities.None("inert");

        Assert.IsFalse(none.Exposes(MediaCategory.Photos));
        Assert.IsFalse(none.Exposes(MediaCategory.Files));
        Assert.AreEqual(0, none.Categories.Count);
    }

    [TestMethod]
    public void DcimBackendExposesPhotosAndVideosOnly()
    {
        // WPD and Shell can only reach the DCIM container: Music and Files must stay unavailable.
        var dcim = new BackendCapabilities(true, true, false, false, false, true, "DCIM only");

        Assert.IsTrue(dcim.Exposes(MediaCategory.Photos));
        Assert.IsTrue(dcim.Exposes(MediaCategory.Videos));
        Assert.IsFalse(dcim.Exposes(MediaCategory.Music));
        Assert.IsFalse(dcim.Exposes(MediaCategory.Files));
    }

    [TestMethod]
    public void MediaAreaBackendExposesAllFourGroups()
    {
        var media = new BackendCapabilities(true, true, true, true, true, true, "media area")
        {
            Categories = [.. MediaCategories.All]
        };

        foreach (MediaCategory category in MediaCategories.All) Assert.IsTrue(media.Exposes(category));
    }
}
