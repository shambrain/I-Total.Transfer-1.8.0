using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The diagnostics page is where an operator goes when the phone is plugged in and nothing happens, so
/// it must describe the phone that is actually attached. "Trust state: CHECK IPHONE" beside an Android
/// phone, or advice to turn off iCloud optimisation, sends its owner looking for a prompt their phone
/// will never show — and the real answer (USB mode, or a lock screen) is missing.
/// </summary>
[TestClass]
public sealed class ConnectionDiagnosticsTests
{
    private static DeviceInfo Device(DeviceFamily family, DeviceStatus status = DeviceStatus.Ready) =>
        new(status, "Test phone", "A message from the backend.") { Family = family };

    /// <summary>What this machine really reports when only a remembered Apple handset exists.</summary>
    private static AttachmentReport CachedAppleRecord() => new(
        AttachmentVerdict.CachedRecordsOnly,
        "CACHED RECORD ONLY (1)",
        "Windows remembers an Apple handset but none is answering.",
        [],
        ["Apple iPhone"],
        []);

    private static string[] Names(IReadOnlyList<DiagnosticItem> items) => items.Select(i => i.Name).ToArray();

    private static DiagnosticItem Row(IReadOnlyList<DiagnosticItem> items, string name) =>
        items.First(i => i.Name == name);

    [TestMethod]
    public void AnAndroidDeviceGetsAndroidRowsAndNoAppleDiagnosis()
    {
        IReadOnlyList<DiagnosticItem> items =
            ConnectionDiagnostics.Collect(Device(DeviceFamily.Android), "Windows Portable Devices (native)", null,
                CachedAppleRecord());
        string[] names = Names(items);

        CollectionAssert.Contains(names, "Device family");
        CollectionAssert.Contains(names, "Android USB mode");
        CollectionAssert.Contains(names, "Unlock state");
        CollectionAssert.Contains(names, "Exposed file set");

        foreach (string appleOnly in new[]
                 {
                     "Trust state", "iCloud originals", "Apple software", "Apple USB driver files",
                     "Apple handsets seen over USB"
                 })
        {
            CollectionAssert.DoesNotContain(names, appleOnly,
                $"'{appleOnly}' describes an iPhone; it must not be presented as this phone's diagnosis.");
        }

        Assert.AreEqual("ANDROID", Row(items, "Device family").Status);
        Assert.AreEqual("FILE TRANSFER", Row(items, "Android USB mode").Status);
        Assert.AreEqual("UNLOCKED", Row(items, "Unlock state").Status);
    }

    [TestMethod]
    public void AnUnreadableAndroidDeviceIsToldToCheckItsUsbModeAndLockScreen()
    {
        // This is the state an Android phone presents when it is in charging mode or the screen has
        // locked: attached, but with an empty tree. Both causes look identical to the operator.
        IReadOnlyList<DiagnosticItem> items =
            ConnectionDiagnostics.Collect(Device(DeviceFamily.Android, DeviceStatus.MediaUnavailable),
                "Windows Portable Devices (native)");

        DiagnosticItem mode = Row(items, "Android USB mode");
        Assert.AreEqual("CHECK PHONE", mode.Status);
        StringAssert.Contains(mode.Details, "File transfer");

        DiagnosticItem unlock = Row(items, "Unlock state");
        Assert.AreEqual("CHECK PHONE", unlock.Status);
        StringAssert.Contains(unlock.Details, "lock");
    }

    [TestMethod]
    public void AnAppleDeviceKeepsTheAppleDiagnosis()
    {
        IReadOnlyList<DiagnosticItem> items =
            ConnectionDiagnostics.Collect(Device(DeviceFamily.Apple), "Windows Portable Devices (native)", null,
                CachedAppleRecord());
        string[] names = Names(items);

        CollectionAssert.Contains(names, "Apple handsets seen over USB");
        CollectionAssert.Contains(names, "Trust state");
        CollectionAssert.Contains(names, "iCloud originals");
        CollectionAssert.Contains(names, "Apple software");
        CollectionAssert.DoesNotContain(names, "Android USB mode");
        Assert.AreEqual("APPLE", Row(items, "Device family").Status);
        Assert.AreEqual("ACCESSIBLE", Row(items, "Trust state").Status);
    }

    [TestMethod]
    public void AnUnidentifiedDeviceSaysSoRatherThanGuessingAFamily()
    {
        var info = new DeviceInfo(DeviceStatus.NotFound, "", "No phone is attached.");
        IReadOnlyList<DiagnosticItem> items = ConnectionDiagnostics.Collect(info, null, null, CachedAppleRecord());

        Assert.AreEqual("NOT IDENTIFIED", Row(items, "Device family").Status,
            "Nothing was classified, so nothing may be claimed.");
        // With nothing identified the Apple checks still describe this PC truthfully, and the row that
        // asks "is an Apple handset attached" is the useful one.
        CollectionAssert.Contains(Names(items), "Apple software");
        CollectionAssert.Contains(Names(items), "Apple handsets seen over USB");

        // The advice must not name a prompt the attached phone may never show: an unidentified device
        // could be an Android phone, and telling its owner to "tap Trust" is exactly the wrong errand.
        DiagnosticItem trust = Row(items, "Trust state");
        Assert.AreEqual("CHECK PHONE", trust.Status);
        StringAssert.Contains(trust.Details, "Android");
        StringAssert.Contains(trust.Details, "File transfer");
        CollectionAssert.DoesNotContain(Names(items), "iCloud originals");
    }

    [TestMethod]
    public void EveryRowHasANameAndSomeDetail()
    {
        foreach (DeviceFamily family in new[] { DeviceFamily.Apple, DeviceFamily.Android, DeviceFamily.Unknown })
        {
            IReadOnlyList<DiagnosticItem> items = ConnectionDiagnostics.Collect(Device(family), "backend");
            Assert.IsTrue(items.Count > 3);
            Assert.AreEqual(items.Count, items.Select(i => i.Name).Distinct().Count(),
                "Two rows with the same name make the page ambiguous.");
            foreach (DiagnosticItem item in items)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Status));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Details));
            }
        }
    }
}
