using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The single-instance guard is the code answer to KI-27: a second copy must not start and contend for
/// the WPD provider. These tests use unique mutex names so they never collide with a running app.
/// </summary>
[TestClass]
public sealed class SingleInstanceGuardTests
{
    private static string UniqueName() => @"Local\itotal-single-instance-test-" + Guid.NewGuid().ToString("N");

    [TestMethod]
    public void ASecondGuardCannotAcquireWhileTheFirstIsHeld()
    {
        string name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        Assert.IsTrue(first.IsFirstInstance, "the first guard must own the name");

        // A Windows mutex is recursive for the owning thread, so the competing launch must be on a
        // different thread - which is what a second process is.
        bool secondAcquired = true;
        var thread = new Thread(() =>
        {
            using var second = new SingleInstanceGuard(name);
            secondAcquired = second.IsFirstInstance;
        });
        thread.Start();
        thread.Join();

        Assert.IsFalse(secondAcquired, "a second copy must not acquire the same name");
    }

    [TestMethod]
    public void ReleasingTheFirstGuardLetsTheNextLaunchStart()
    {
        string name = UniqueName();
        var first = new SingleInstanceGuard(name);
        Assert.IsTrue(first.IsFirstInstance);
        first.Dispose();

        using var second = new SingleInstanceGuard(name);
        Assert.IsTrue(second.IsFirstInstance, "after the first copy exits, a new copy must start");
    }

    [TestMethod]
    public void DifferentNamesDoNotCollide()
    {
        using var first = new SingleInstanceGuard(UniqueName());
        using var second = new SingleInstanceGuard(UniqueName());

        Assert.IsTrue(first.IsFirstInstance);
        Assert.IsTrue(second.IsFirstInstance);
    }

    [TestMethod]
    public void TheDefaultNameIsSessionLocal()
    {
        Assert.IsTrue(SingleInstanceGuard.DefaultName.StartsWith(@"Local\", StringComparison.Ordinal),
            "the guard must be scoped to the signed-in session, not the whole machine");
        using var guard = new SingleInstanceGuard();
        Assert.AreEqual(SingleInstanceGuard.DefaultName, guard.Name);
    }

    [TestMethod]
    public void AnEmptyNameFallsBackToTheDefault()
    {
        using var guard = new SingleInstanceGuard("   ");
        Assert.AreEqual(SingleInstanceGuard.DefaultName, guard.Name);
    }

    [TestMethod]
    public void DisposingTwiceIsSafe()
    {
        var guard = new SingleInstanceGuard(UniqueName());
        guard.Dispose();
        guard.Dispose();
        Assert.IsFalse(guard.IsFirstInstance);
    }
}
