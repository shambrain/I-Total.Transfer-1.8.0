using ITotalTransfer.Core;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ITotalTransfer.Tests;

/// <summary>
/// The engine reads two handset families, so the words in front of the operator have to follow the family
/// that is actually attached. A sentence that says "iPhone" while an Android phone is on the cable is not
/// cosmetic: it makes the operator believe the app selected the other device.
/// </summary>
[TestClass]
public sealed class DeviceCopyTests : WinFormsTestBase
{
    [TestMethod]
    public void TheNounNamesEachFamilyAndFallsBackToTheNeutralWord()
    {
        Assert.AreEqual("Android phone", DeviceCopy.Noun(DeviceFamily.Android));
        Assert.AreEqual("iPhone", DeviceCopy.Noun(DeviceFamily.Apple));
        Assert.AreEqual("phone", DeviceCopy.Noun(DeviceFamily.Unknown));
    }

    [TestMethod]
    public void TheArticleFormReadsNaturally()
    {
        Assert.AreEqual("the Android phone", DeviceCopy.WithArticle(DeviceFamily.Android));
        Assert.AreEqual("the iPhone", DeviceCopy.WithArticle(DeviceFamily.Apple));
        Assert.AreEqual("the phone", DeviceCopy.WithArticle(DeviceFamily.Unknown));
    }

    [TestMethod]
    public void AnAndroidDeviceIsNeverDescribedAsAnIPhoneOnTheOverview()
    {
        using var page = new DashboardPage();
        page.SetDevice(
            new DeviceInfo(DeviceStatus.Ready, "HUAWEI Y7 Prime 2018",
                "Device storage is accessible through native WPD (hash-verified transfers available).")
            {
                Family = DeviceFamily.Android,
                Model = "HUAWEI Y7 Prime 2018",
                ConnectionType = "USB (MTP)"
            },
            "Windows Portable Devices (native)");

        string all = TextOf(page);
        StringAssert.Contains(all, "What your Android phone exposes");
        StringAssert.Contains(all, "Nothing is ever deleted from the phone");
        Assert.IsFalse(all.Contains("What your iPhone exposes"),
            "An Android phone on the cable must not be presented under an iPhone heading.");
        Assert.IsFalse(all.Contains("deleted from the iPhone"),
            "The promise that nothing is deleted must name the device that is actually attached.");
    }

    [TestMethod]
    public void AnAppleDeviceKeepsItsOwnWording()
    {
        using var page = new DashboardPage();
        page.SetDevice(
            new DeviceInfo(DeviceStatus.Ready, "Apple iPhone",
                "iPhone media is accessible through native WPD (hash-verified transfers available).")
            {
                Family = DeviceFamily.Apple,
                Model = "iPhone",
                ConnectionType = "USB (WPD)"
            },
            "Windows Portable Devices (native)");

        string all = TextOf(page);
        StringAssert.Contains(all, "What your iPhone exposes");
        StringAssert.Contains(all, "Nothing is ever deleted from the iPhone");
    }

    [TestMethod]
    public void ANotFoundProbeUsesNeutralWording()
    {
        using var page = new DashboardPage();
        page.SetDevice(
            new DeviceInfo(DeviceStatus.NotFound, "",
                "No accessible phone was found through Windows Shell.")
            {
                Family = DeviceFamily.Apple
            },
            "Windows Shell (compatibility)");
        page.SetCategories(null, null, DeviceStatus.NotFound);

        string all = TextOf(page);
        StringAssert.Contains(all, "What your phone exposes");
        StringAssert.Contains(all, "Connect your phone to check");
        Assert.IsFalse(all.Contains("iPhone"),
            "A missing iPhone must not turn the no-device state into an Apple-only claim.");
    }

    private static string TextOf(Control root) => string.Join("\n", Texts(root));

    private static IEnumerable<string> Texts(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (!string.IsNullOrWhiteSpace(child.Text)) yield return child.Text;
            foreach (string text in Texts(child)) yield return text;
        }
    }
}
