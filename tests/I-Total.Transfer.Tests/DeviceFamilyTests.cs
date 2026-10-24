using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Which storage rules apply to an attached device is decided from what the device calls itself, and
/// the mistake is not symmetric: reading an Android phone as an iPhone would walk only its camera roll
/// and still report a complete scan, while reading an iPhone as generic storage only looks in more
/// places than a camera roll. The tests therefore pin both the Apple rules and the reason the Android
/// match is allowed to be generous.
/// </summary>
[TestClass]
public sealed class DeviceFamilyTests
{
    private static DeviceFamilyVerdict Classify(string? name, string? manufacturer, string? model = null) =>
        DeviceFamilies.Classify(new PortableDeviceIdentity(name, manufacturer, model));

    [TestMethod]
    public void ApplesOwnDeviceNamesAreApple()
    {
        foreach (string name in new[] { "Apple iPhone", "iPhone", "iPad", "John's iPad Pro", "iPod touch" })
            Assert.AreEqual(DeviceFamily.Apple, Classify(name, null).Family, $"{name} is an Apple device.");
    }

    [TestMethod]
    public void AnAppleManufacturerIsAppleEvenWhenTheNameSaysNothing()
    {
        DeviceFamilyVerdict verdict = Classify("Portable Device", "Apple Inc.");
        Assert.AreEqual(DeviceFamily.Apple, verdict.Family);
        StringAssert.Contains(verdict.Basis, "Apple");

        // A driver build that names the handset only "Apple Device" was accepted before Android support
        // existed, so it must still be: narrowing the match would make a readable phone invisible.
        Assert.AreEqual(DeviceFamily.Apple, Classify("Apple Device", null).Family);
    }

    [TestMethod]
    public void ADeviceThatSaysItIsAndroidIsAndroid()
    {
        Assert.AreEqual(DeviceFamily.Android, Classify("Android", null).Family);
        Assert.AreEqual(DeviceFamily.Android, Classify("SAMSUNG Android", null).Family);
        Assert.AreEqual(DeviceFamily.Android, Classify(null, null, "Android SDK built for x86").Family,
            "A device that reports its model as Android is Android whatever its name says.");

        // The name is often the only identity a phone hands over: Windows Portable Devices reads these
        // straight from the handset, and a Pixel reports itself by name with no manufacturer at all.
        Assert.AreEqual(DeviceFamily.Android, Classify("Pixel 7", null).Family);
        Assert.AreEqual(DeviceFamily.Android, Classify("Galaxy S24", null).Family);
    }

    [TestMethod]
    public void AManufacturerIsMatchedAsAWholeWordNotAsASubstring()
    {
        Assert.AreEqual(DeviceFamily.Android, Classify("Galaxy S24", "SAMSUNG ELECTRONICS CO., LTD.").Family,
            "The manufacturer string a phone really reports is a sentence, not a single word.");
        Assert.AreEqual(DeviceFamily.Android, Classify("Phone", "HMD Global").Family);
        Assert.AreEqual(DeviceFamily.Android, Classify("Phone", "lg electronics").Family);
        Assert.AreEqual(DeviceFamily.Android, Classify("Phone", "Xiaomi Communications Co Ltd").Family);

        // Substring matching would have these wrong: neither word is an Android maker.
        Assert.AreEqual(DeviceFamily.Unknown, Classify("Phone", "Volkswagen").Family,
            "A whole-word match must not fire on a longer word that merely contains a maker's name.");
        Assert.AreEqual(DeviceFamily.Unknown, Classify("Phone", "Microtek").Family,
            "MTK is a maker word; Microtek is not MTK.");
    }

    [TestMethod]
    public void AppleIsDecidedBeforeAnyManufacturerListIsConsulted()
    {
        // A brand that makes Android devices and also appears on a device that calls itself an iPhone:
        // the device's own name wins, because the name is the device talking about itself.
        DeviceFamilyVerdict verdict = Classify("Apple iPhone", "Samsung");
        Assert.AreEqual(DeviceFamily.Apple, verdict.Family);
        StringAssert.Contains(verdict.Basis, "iPhone");
    }

    [TestMethod]
    public void ADeviceThatIdentifiesAsNeitherFamilyIsUnknownAndSaysWhy()
    {
        DeviceFamilyVerdict verdict = Classify("F2088", "Shenzhen Something Ltd");
        Assert.AreEqual(DeviceFamily.Unknown, verdict.Family);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verdict.Basis));

        DeviceFamilyVerdict empty = Classify(null, null);
        Assert.AreEqual(DeviceFamily.Unknown, empty.Family);
        Assert.IsFalse(string.IsNullOrWhiteSpace(empty.Basis));
    }

    [TestMethod]
    public void CaseAndSurroundingSpaceDoNotChangeTheAnswer()
    {
        Assert.AreEqual(DeviceFamily.Apple, Classify("  APPLE IPHONE  ", "").Family);
        Assert.AreEqual(DeviceFamily.Android, Classify("Phone", " google ").Family);
    }

    [TestMethod]
    public void UnknownIsTheDefaultSoAnUnprobedDeviceNeverClaimsAFamily()
    {
        Assert.AreEqual(DeviceFamily.Unknown, default(DeviceFamily));
        Assert.AreEqual(DeviceFamily.Unknown, new DeviceInfo(DeviceStatus.NotFound, "", "").Family,
            "A device that was never classified must not present itself as an iPhone or an Android phone.");
    }

    [TestMethod]
    public void TheFamiliesAreDescribedInPlainWords()
    {
        Assert.AreEqual("iPhone or iPad", DeviceFamilies.Describe(DeviceFamily.Apple));
        Assert.AreEqual("Android device", DeviceFamilies.Describe(DeviceFamily.Android));
        StringAssert.Contains(DeviceFamilies.Describe(DeviceFamily.Unknown), "portable device");
    }

    [TestMethod]
    public void TheBothPhonesPreferenceReordersAndNeverRemovesAFamily()
    {
        // With an iPhone and an Android phone on the same desk, the preference decides which one is read.
        // It must only reorder: an operator who prefers the iPhone still gets the Android phone when that
        // is the only one plugged in, because refusing to read the phone that is present would be worse.
        CollectionAssert.AreEqual(
            new[] { DeviceFamily.Apple, DeviceFamily.Android },
            DeviceFamilies.SelectionOrder(HandsetPreference.AppleFirst).ToArray());
        CollectionAssert.AreEqual(
            new[] { DeviceFamily.Android, DeviceFamily.Apple },
            DeviceFamilies.SelectionOrder(HandsetPreference.AndroidFirst).ToArray());

        foreach (HandsetPreference preference in Enum.GetValues<HandsetPreference>())
        {
            IReadOnlyList<DeviceFamily> order = DeviceFamilies.SelectionOrder(preference);
            Assert.AreEqual(2, order.Count, "A preference must never make a family unreachable.");
            CollectionAssert.AreEquivalent(
                new[] { DeviceFamily.Apple, DeviceFamily.Android }, order.ToArray());
        }

        Assert.AreEqual(HandsetPreference.AppleFirst, default(HandsetPreference),
            "Before Android support existed an iPhone was read whenever one was attached, and a default " +
            "enum value must not change that.");
    }

    [TestMethod]
    public void ClassificationRejectsAMissingIdentity()
    {
        Assert.ThrowsException<ArgumentNullException>(() => DeviceFamilies.Classify(null!));
    }
}
