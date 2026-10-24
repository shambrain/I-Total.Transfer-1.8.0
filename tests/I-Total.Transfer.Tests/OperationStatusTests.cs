using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The operation band's words are read at a glance after a long run. "Stopped" beside a summary that
/// copied every planned file is the kind of label that sends an operator looking for a cancellation that
/// never happened, so the headings and the busy rule are pinned here.
/// </summary>
[TestClass]
public sealed class OperationStatusTests
{
    [TestMethod]
    public void AFinishedPassWithUnverifiableFilesNeedsAttentionRatherThanStopped()
    {
        var status = new OperationStatus(
            OperationPhase.Incomplete,
            "COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP",
            Completed: 520,
            Total: 520,
            Indeterminate: false);

        Assert.AreEqual("Needs attention", status.Heading);
        Assert.IsFalse(status.IsBusy, "Nothing is moving, so the band must not animate as a running job.");
        Assert.IsTrue(status.HasMeasuredTotal);
        Assert.AreEqual(1000, status.Permille, "Every planned file was processed.");
    }

    [TestMethod]
    public void ARealFailureSaysFailedRatherThanStopped()
    {
        var status = new OperationStatus(OperationPhase.Failed, "Transfer failed: device disappeared.");
        Assert.AreEqual("Failed", status.Heading);
        Assert.IsFalse(status.IsBusy);
        Assert.IsFalse(status.HasMeasuredTotal);
    }

    [TestMethod]
    public void BusyPhasesStayBusy()
    {
        Assert.IsTrue(new OperationStatus(OperationPhase.Transferring, "copying").IsBusy);
        Assert.IsTrue(new OperationStatus(OperationPhase.Scanning, "scanning").IsBusy);
        Assert.IsTrue(new OperationStatus(OperationPhase.Paused, "paused").IsBusy);
        Assert.IsFalse(new OperationStatus(OperationPhase.Idle, "idle").IsBusy);
    }
}
