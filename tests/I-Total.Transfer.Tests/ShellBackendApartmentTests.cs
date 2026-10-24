using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Shell compatibility backend is the fallback the app lands on when no streaming backend is
/// available, so it has to survive being driven from a thread-pool thread — which is exactly how the
/// dashboard probes it. It used to create <c>Shell.Application</c> there, where "This PC" reports no
/// portable devices at all, so a connected handset was reported as absent. Every call now round-trips
/// through the backend's own STA thread; these tests prove that round trip completes, answers sanely
/// with or without a handset attached, and disposes cleanly.
///
/// <para>
/// Both tests talk to whatever the machine actually has attached, so they are opt-in
/// (<c>ITOTAL_HARDWARE_TESTS=1</c>) and every device call is time-bounded. An unbounded probe in the
/// default run is how a test suite starts hanging on a busy phone instead of reporting a result.
/// </para>
/// </summary>
[TestClass]
public sealed class ShellBackendApartmentTests
{
    private static readonly TimeSpan Allowance = TimeSpan.FromSeconds(90);

    [TestInitialize]
    public void RequireHardware() => Hardware.Require();

    [TestMethod]
    public void ShellBackendAnswersFromAnMtaThreadInsteadOfHanging()
    {
        DeviceInfo info = Hardware.Bounded("MTA shell probe", Allowance, () =>
        {
            try { return new ShellIPhoneDevice().Probe(); }
            catch (Exception ex)
            {
                Assert.Fail($"The Shell probe threw: {ex.Message}");
                throw;
            }
        });

        Assert.IsNotNull(info, "The Shell probe produced no result.");

        // With no handset the honest answer is "not found"; with one it is one of the device states.
        // Either way it must be a real verdict rather than a fabricated success.
        Assert.IsTrue(info.Status is DeviceStatus.NotFound or DeviceStatus.Connected
                or DeviceStatus.MediaUnavailable or DeviceStatus.MediaAvailable
                or DeviceStatus.Ready or DeviceStatus.LockedOrUntrusted,
            $"Unexpected Shell probe status: {info.Status} — {info.Message}");
        Assert.IsFalse(info.Status.IsUsable() && string.IsNullOrWhiteSpace(info.Name),
            "A readable device must be named.");
    }

    [TestMethod]
    public void ShellBackendProbesAndDisposesCleanly()
    {
        DeviceInfo info = Hardware.Bounded("shell probe and dispose", Allowance, () =>
        {
            var backend = new ShellIPhoneDevice();
            try
            {
                DeviceInfo probed = backend.Probe();
                backend.Dispose();
                backend.Dispose(); // releasing twice must be as safe as releasing once
                return probed;
            }
            finally
            {
                backend.Dispose();
            }
        });

        Assert.IsNotNull(info);
    }
}
