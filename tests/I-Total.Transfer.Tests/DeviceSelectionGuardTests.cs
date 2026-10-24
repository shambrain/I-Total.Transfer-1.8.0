using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotal.Transfer.Tests;

/// <summary>
/// One backend, one phone. The guard is what stops a session that began on the iPhone from silently
/// continuing on the Android phone when the iPhone stops answering mid-scan.
/// </summary>
[TestClass]
public sealed class DeviceSelectionGuardTests
{
    [TestMethod]
    public void FirstSelectionBindsTheDevice()
    {
        var guard = new DeviceSelectionGuard();

        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        Assert.IsTrue(guard.IsBound);
        Assert.AreEqual("wpd-apple", guard.DeviceId);
        Assert.AreEqual("Apple iPhone", guard.FriendlyName);
        Assert.AreEqual(DeviceFamily.Apple, guard.Family);
    }

    [TestMethod]
    public void TheSameDeviceCanBeProbedAgain()
    {
        var guard = new DeviceSelectionGuard();
        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        Assert.AreEqual("wpd-apple", guard.DeviceId);
    }

    [TestMethod]
    public void BindingADifferentDeviceIsRefused()
    {
        var guard = new DeviceSelectionGuard();
        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
            () => guard.Bind("mtp-huawei", "HUAWEI Y7 Prime 2018", DeviceFamily.Android));

        StringAssert.Contains(error.Message, "Apple iPhone");
        StringAssert.Contains(error.Message, "will not switch phones automatically");
        Assert.AreEqual("wpd-apple", guard.DeviceId);
    }

    [TestMethod]
    public void IsDifferentReportsTheOtherPhoneWithoutRebinding()
    {
        var guard = new DeviceSelectionGuard();
        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        Assert.IsTrue(guard.IsDifferent("mtp-huawei"));
        Assert.IsFalse(guard.IsDifferent("wpd-apple"));
        Assert.AreEqual("wpd-apple", guard.DeviceId);
    }

    [TestMethod]
    public void AnUnboundGuardDoesNotClaimADeviceIsDifferent()
    {
        var guard = new DeviceSelectionGuard();

        Assert.IsFalse(guard.IsBound);
        Assert.IsFalse(guard.IsDifferent("mtp-huawei"));
    }

    [TestMethod]
    public void TheGoneMessageNamesTheSelectedPhoneAndNeverClaimsTheOtherWasRead()
    {
        var guard = new DeviceSelectionGuard();
        guard.Bind("wpd-apple", "Apple iPhone", DeviceFamily.Apple);

        string message = guard.GoneMessage("HUAWEI Y7 Prime 2018");

        StringAssert.Contains(message, "Apple iPhone");
        StringAssert.Contains(message, "HUAWEI Y7 Prime 2018");
        StringAssert.Contains(message, "was not used");
    }
}
