using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// "Windows can see my iPhone" and "my iPhone is plugged in" are different statements, and on a PC that
/// has ever hosted an Apple device the first stays true forever. Device Manager keeps an "Apple iPhone"
/// entry whose own properties say "Currently, this hardware device is not connected to the computer",
/// and drivers keep loading for it, so an operator looking at Windows is told one thing while the app
/// says another. These tests pin which of those readings the app must report, using the record set a
/// real machine actually stores.
/// </summary>
[TestClass]
public sealed class AttachmentDiagnosisTests
{
    /// <summary>
    /// Verbatim (name, service) pairs from the USB enum key of the machine this was written on - a PC
    /// with an iPhone and an iPad on record and neither of them attached. Two records carry the same
    /// handset name because one handset exposes several USB interfaces; only WUDFWpdMtp is the media one.
    /// </summary>
    private static readonly AppleUsbRecord[] LiveCachedRecords =
    [
        new("Apple Mobile Device USB Composite Device", "usbccgp"),
        new("Apple iPhone", "WUDFWpdMtp"),
        new("Apple iPhone", "WUDFWpdMtp"),
        new("Apple Mobile Device USB Device", "WINUSB"),
        new("Apple Mobile Device USB Driver", "USBAAPL64"),
        new("Apple Mobile Device USB Driver", "USBAAPL64"),
        new("Apple iPad", "WUDFWpdMtp"),
        new("USB Input Device", "HidUsb")
    ];

    [TestMethod]
    public void TheOnlyThingThatCountsAsAHandsetIsTheMediaInterface()
    {
        (IReadOnlyList<string> handsets, IReadOnlyList<string> support) =
            AppleUsbInventory.Summarise(LiveCachedRecords);

        CollectionAssert.AreEqual(new[] { "Apple iPhone", "Apple iPad" }, handsets.ToArray(),
            "Only the MTP records are handsets; a machine keeps a support record for every Apple cable it has seen.");
        CollectionAssert.AreEqual(
            new[] { "Apple Mobile Device USB Composite Device", "Apple Mobile Device USB Device", "Apple Mobile Device USB Driver" },
            support.ToArray(),
            "Support records are reported, de-duplicated, but never as handsets.");
    }

    [TestMethod]
    public void TheServiceDecidesTheInterfaceNotTheDisplayName()
    {
        Assert.AreEqual(AppleUsbInterface.MediaInterface, AppleUsbInventory.Classify("WUDFWpdMtp"));
        Assert.AreEqual(AppleUsbInterface.MediaInterface, AppleUsbInventory.Classify("wudfwpdmtp"));

        Assert.AreEqual(AppleUsbInterface.SupportInterface, AppleUsbInventory.Classify("usbccgp"));
        Assert.AreEqual(AppleUsbInterface.SupportInterface, AppleUsbInventory.Classify("USBAAPL64"));
        Assert.AreEqual(AppleUsbInterface.SupportInterface, AppleUsbInventory.Classify("WINUSB"));
        Assert.AreEqual(AppleUsbInterface.SupportInterface, AppleUsbInventory.Classify("usbmux"));
        Assert.AreEqual(AppleUsbInterface.SupportInterface, AppleUsbInventory.Classify(" USBAAPL64 "),
            "The service value is compared trimmed and case-insensitively.");

        Assert.AreEqual(AppleUsbInterface.Other, AppleUsbInventory.Classify("HidUsb"));
        Assert.AreEqual(AppleUsbInterface.Other, AppleUsbInventory.Classify(""));
        Assert.AreEqual(AppleUsbInterface.Other, AppleUsbInventory.Classify(null));
    }

    [TestMethod]
    public void AMachineThatRemembersHandsetsButHasNoneAttachedIsNotReportedAsConnected()
    {
        (IReadOnlyList<string> handsets, IReadOnlyList<string> support) =
            AppleUsbInventory.Summarise(LiveCachedRecords);

        AttachmentReport report = AttachmentDiagnosis.Decide(new AttachmentEvidence([], handsets, support));

        Assert.AreEqual(AttachmentVerdict.CachedRecordsOnly, report.Verdict);
        Assert.IsFalse(report.IsAttached, "A cached record must never be reported as an attached handset.");
        Assert.AreEqual("CACHED RECORD ONLY (2)", report.Status);
        StringAssert.Contains(report.Headline, "No handset is attached");
        StringAssert.Contains(report.Headline, "Apple iPhone");
        StringAssert.Contains(report.Headline, "Apple iPad");
        StringAssert.Contains(report.Guidance, "not connected to the computer",
            "The guidance must name the Device Manager wording the operator is likely looking at.");
    }

    [TestMethod]
    public void AnAttachedHandsetIsNeverDowngradedByWhatWindowsRemembers()
    {
        AttachmentReport report = AttachmentDiagnosis.Decide(
            ["Apple iPhone"], ["Apple iPad"], ["Apple Mobile Device USB Driver"]);

        Assert.AreEqual(AttachmentVerdict.Attached, report.Verdict);
        Assert.AreEqual("ATTACHED", report.Status);
        StringAssert.Contains(report.Headline, "Apple iPhone");
        StringAssert.Contains(report.Guidance, "Trust");
        CollectionAssert.AreEqual(new[] { "Apple iPad" }, report.CachedHandsetNames.ToArray(),
            "The older records are still reported - they are just not allowed to change the verdict.");
    }

    [TestMethod]
    public void AMachineThatHasNeverSeenAnAppleDeviceSaysSoAndSaysWhatToFix()
    {
        AttachmentReport report = AttachmentDiagnosis.Decide(
            new AttachmentEvidence([], [], []));

        Assert.AreEqual(AttachmentVerdict.NoRecords, report.Verdict);
        Assert.AreEqual("NONE", report.Status);
        StringAssert.Contains(report.Headline, "has ever been seen");
        StringAssert.Contains(report.Guidance, "cable");
    }

    [TestMethod]
    public void MissingEvidenceIsTreatedAsNothingSeenRatherThanThrowing()
    {
        Assert.AreEqual(AttachmentVerdict.NoRecords, AttachmentDiagnosis.Decide((AttachmentEvidence?)null).Verdict);
        Assert.AreEqual(AttachmentVerdict.NoRecords, AttachmentDiagnosis.Decide(null, null, null).Verdict);
        Assert.AreEqual(AttachmentVerdict.NoRecords,
            AttachmentDiagnosis.Decide(new AttachmentEvidence([], [], [])).Verdict);
    }

    [TestMethod]
    public void BlankAndDuplicatedNamesAreCollapsed()
    {
        AttachmentReport report = AttachmentDiagnosis.Decide(
            ["", "   "], ["Apple iPhone", "apple iphone", " Apple iPhone "], []);

        Assert.AreEqual(AttachmentVerdict.CachedRecordsOnly, report.Verdict);
        Assert.AreEqual(1, report.CachedHandsetCount);
        Assert.AreEqual("CACHED RECORD ONLY (1)", report.Status);
        StringAssert.Contains(report.Headline, "1 cached Apple handset record:");
    }

    [TestMethod]
    public void OneCachedHandsetReadsAsASingleRecordNotRecords()
    {
        AttachmentReport report = AttachmentDiagnosis.Decide([], ["Apple iPhone"], []);

        StringAssert.Contains(report.Headline, "1 cached Apple handset record:");
        Assert.IsFalse(report.Headline.Contains("records:"),
            "Singular and plural are both shown to an operator; they must agree with the count.");
    }

    [TestMethod]
    public void WindowsStoresIndirectStringsAndTheReadableHalfIsUsed()
    {
        Assert.AreEqual("Apple Mobile Device USB Composite Device",
            AppleUsbInventory.DisplayName(
                @"@oem69.inf,%iPhone.AppleUSB.DeviceDesc%;Apple Mobile Device USB Composite Device", null));

        Assert.AreEqual("Apple iPhone", AppleUsbInventory.DisplayName("Apple iPhone", null),
            "A plain name is already readable.");

        Assert.AreEqual("Apple iPad", AppleUsbInventory.DisplayName(null, "Apple iPad"),
            "DeviceDesc is the fallback when FriendlyName is absent.");

        Assert.AreEqual("Apple iPhone",
            AppleUsbInventory.DisplayName(@"@oem69.inf,%key%;Apple iPhone", "Apple iPad"),
            "FriendlyName wins when both are present.");

        Assert.AreEqual("", AppleUsbInventory.DisplayName(@"@oem69.inf,%iPhone.AppleUSB.DeviceDesc%", null),
            "An indirect string with no readable part is not shown to anyone.");
        Assert.AreEqual("", AppleUsbInventory.DisplayName("", ""));
        Assert.AreEqual("", AppleUsbInventory.DisplayName(null, null));
    }

    /// <summary>
    /// The live read. It cannot assert a count - that would depend on whose PC runs it - so it asserts
    /// the things that must hold anywhere: no throw, only display names, and never a raw instance key
    /// (which is where Windows stores the handset's serial number).
    /// </summary>
    [TestMethod]
    public void OnlyAppleHandsetsCountAsAppleHandsets()
    {
        // Both families really are on the bus at once on this machine, and an Android phone answers on
        // exactly the same portable-device bus an iPhone does. Passing every attached handset into an
        // Apple-specific row put "HUAWEI Y7 Prime 2018" under "Apple handsets seen over USB" - a
        // family-specific conclusion drawn from the other family's device.
        var attached = new[]
        {
            new DevicePresence(true, "HUAWEI Y7 Prime 2018", "USB · Windows Portable Devices", "")
                { Family = DeviceFamily.Android },
            new DevicePresence(true, "Apple iPhone", "USB · Windows Portable Devices", "")
                { Family = DeviceFamily.Apple },
        };

        AttachmentEvidence evidence = AppleDeviceHistory.Evidence(attached);

        CollectionAssert.AreEqual(new[] { "Apple iPhone" }, evidence.AttachedNames.ToArray());
        AttachmentReport report = AttachmentDiagnosis.Decide(evidence);
        Assert.AreEqual(AttachmentVerdict.Attached, report.Verdict);
        Assert.AreEqual("Attached and answering: Apple iPhone.", report.Headline);
    }

    [TestMethod]
    public void AnAndroidPhoneAloneDoesNotMakeAnAppleHandsetAttached()
    {
        var attached = new[]
        {
            new DevicePresence(true, "HUAWEI Y7 Prime 2018", "USB · Windows Portable Devices", "")
                { Family = DeviceFamily.Android },
        };

        AttachmentEvidence evidence = AppleDeviceHistory.Evidence(attached);
        Assert.AreEqual(0, evidence.AttachedNames.Count,
            "An Android phone is not evidence that an Apple handset is answering.");

        AttachmentReport report = AttachmentDiagnosis.Decide(evidence);
        Assert.AreNotEqual(AttachmentVerdict.Attached, report.Verdict,
            "Windows may well remember Apple handsets; what it must not say is that one is attached.");
    }

    [TestMethod]
    public void AnUnidentifiedAttachedDeviceIsNotClaimedAsAnAppleHandset()
    {
        var attached = new[]
        {
            new DevicePresence(true, "Some Camera", "USB · Windows Shell", "") { Family = DeviceFamily.Unknown },
            new DevicePresence(false, "", "", ""),
        };

        Assert.AreEqual(0, AppleDeviceHistory.Evidence(attached).AttachedNames.Count);
    }

    [TestMethod]
    public void TheLiveRegistryReadIsSafeAndNeverLeaksAnInstanceKey()
    {
        AttachmentEvidence evidence = AppleDeviceHistory.Evidence(attachedNames: null);

        Assert.IsNotNull(evidence);
        Assert.IsNotNull(evidence.AttachedNames);
        Assert.IsNotNull(evidence.CachedHandsetNames);
        Assert.IsNotNull(evidence.CachedSupportNames);
        foreach (string name in evidence.CachedHandsetNames.Concat(evidence.CachedSupportNames))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), "A blank record name is not worth reporting.");
            Assert.IsFalse(name.Contains('\\'), $"'{name}' looks like a registry path, not a display name.");
            Assert.IsFalse(name.Contains('&'), $"'{name}' looks like a device-instance key, not a display name.");
        }

        // Recorded in the test output so a machine's real inventory is visible when it matters.
        Console.WriteLine(
            $"cached handsets={evidence.CachedHandsetNames.Count}; " +
            $"support={evidence.CachedSupportNames.Count}; " +
            $"hands={(evidence.CachedHandsetNames.Count == 0 ? "none" : string.Join(", ", evidence.CachedHandsetNames))}");
    }
}
