using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The live transfer view is refreshed on a clock, not per file. These tests drive an injectable clock so the
/// pacing is pinned exactly, rather than by sleeping and hoping.
/// </summary>
[TestClass]
public sealed class ThrottledProgressTests
{
    private sealed class Collector : IProgress<int>
    {
        public List<int> Values { get; } = [];
        public void Report(int value) { lock (Values) Values.Add(value); }
    }

    private sealed class Clock
    {
        public long Now;
        public long Read() => Now;
    }

    [TestMethod]
    public void FastUpdatesAreCoalescedToTheInterval()
    {
        var clock = new Clock();
        var inner = new Collector();
        var throttled = new ThrottledProgress<int>(inner, TimeSpan.FromMilliseconds(250), clock.Read);

        // 1,000 updates in the same millisecond: the receiving end sees one.
        for (int i = 0; i < 1_000; i++) throttled.Report(i);

        Assert.AreEqual(1, inner.Values.Count);
        Assert.AreEqual(0, inner.Values[0]);
        Assert.AreEqual(1, throttled.Forwarded);
        Assert.AreEqual(999, throttled.Dropped);
    }

    [TestMethod]
    public void AnUpdateIsForwardedOnceTheIntervalHasElapsed()
    {
        var clock = new Clock();
        var inner = new Collector();
        var throttled = new ThrottledProgress<int>(inner, TimeSpan.FromMilliseconds(250), clock.Read);

        throttled.Report(1);          // forwarded (first)
        clock.Now = 100; throttled.Report(2);   // dropped
        clock.Now = 249; throttled.Report(3);   // dropped (249 - 0 < 250)
        clock.Now = 250; throttled.Report(4);   // forwarded exactly on the boundary
        clock.Now = 260; throttled.Report(5);   // dropped
        clock.Now = 500; throttled.Report(6);   // forwarded

        CollectionAssert.AreEqual(new[] { 1, 4, 6 }, inner.Values);
        Assert.AreEqual(3, throttled.Forwarded);
        Assert.AreEqual(3, throttled.Dropped);
    }

    [TestMethod]
    public void TheMostRecentValueIsTheOneDelivered()
    {
        // Dropping an update must not deliver a stale value later: whatever is forwarded is what the engine
        // reported at that moment, and the terminal state comes from the run's summary instead.
        var clock = new Clock();
        var inner = new Collector();
        var throttled = new ThrottledProgress<int>(inner, TimeSpan.FromMilliseconds(250), clock.Read);

        throttled.Report(1);
        clock.Now = 300;
        throttled.Report(99);
        Assert.AreEqual(99, inner.Values[^1]);
    }

    [TestMethod]
    public void AZeroIntervalForwardsEverything()
    {
        var clock = new Clock();
        var inner = new Collector();
        var throttled = new ThrottledProgress<int>(inner, TimeSpan.Zero, clock.Read);

        for (int i = 0; i < 5; i++) { clock.Now += 10; throttled.Report(i); }

        Assert.AreEqual(5, inner.Values.Count);
        Assert.AreEqual(0, throttled.Dropped);
    }

    [TestMethod]
    public void ManyThreadsReportingDoNotForwardMoreThanTheRateAllows()
    {
        var clock = new Clock();          // frozen, so only the first report can ever pass
        var inner = new Collector();
        var throttled = new ThrottledProgress<int>(inner, TimeSpan.FromMilliseconds(250), clock.Read);

        Parallel.For(0, 2_000, i => throttled.Report(i));

        Assert.AreEqual(1, inner.Values.Count, "A frozen clock admits exactly one update no matter who reports.");
        Assert.AreEqual(1, throttled.Forwarded);
        Assert.AreEqual(1_999, throttled.Dropped);
    }
}
