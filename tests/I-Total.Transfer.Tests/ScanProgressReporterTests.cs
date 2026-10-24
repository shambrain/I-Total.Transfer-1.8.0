using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// A single iPhone album can hold tens of thousands of objects and the device answers metadata at
/// roughly 27 ms per object (measured: a 26,320-item folder took ~12 minutes to enumerate the first
/// time). A scan must therefore report often enough to look alive and rarely enough not to flood the
/// UI thread, and it must always report when the walk moves to another folder.
/// </summary>
[TestClass]
public sealed class ScanProgressReporterTests
{
    [TestMethod]
    public void TheFirstReportForAFolderAlwaysGoesOut()
    {
        var reporter = new ScanProgressReporter(TimeSpan.FromSeconds(10));
        Assert.IsTrue(reporter.ShouldReport("202608_a", TimeSpan.Zero));
    }

    [TestMethod]
    public void MovingToAnotherFolderAlwaysReportsEvenInsideTheQuietWindow()
    {
        var reporter = new ScanProgressReporter(TimeSpan.FromSeconds(10));
        Assert.IsTrue(reporter.ShouldReport("202608_a", TimeSpan.Zero));
        Assert.IsTrue(reporter.ShouldReport("202608_b", TimeSpan.FromMilliseconds(50)));
        Assert.IsTrue(reporter.ShouldReport("DCIM", TimeSpan.FromMilliseconds(60)));
    }

    [TestMethod]
    public void TheSameFolderIsNotReportedAgainBeforeTheInterval()
    {
        var reporter = new ScanProgressReporter(TimeSpan.FromMilliseconds(250));
        Assert.IsTrue(reporter.ShouldReport("202608_a", TimeSpan.Zero));
        Assert.IsFalse(reporter.ShouldReport("202608_a", TimeSpan.FromMilliseconds(100)));
        Assert.IsFalse(reporter.ShouldReport("202608_a", TimeSpan.FromMilliseconds(249)));
        Assert.IsTrue(reporter.ShouldReport("202608_a", TimeSpan.FromMilliseconds(250)));
    }

    [TestMethod]
    public void ProgressLinesNameTheFolderTheCountTheSizeAndTheRate()
    {
        string line = ScanProgressReporter.Describe("202608_a", 12_480, 5L * 1024 * 1024 * 1024,
            TimeSpan.FromSeconds(60));

        StringAssert.Contains(line, "Scanning 202608_a");
        StringAssert.Contains(line, "12,480 files");
        StringAssert.Contains(line, "GB");
        StringAssert.Contains(line, "208 files/s");
        StringAssert.Contains(line, "01:00 elapsed");
    }

    [TestMethod]
    public void TheRootFolderIsNamedWithTheCanonicalLabel()
    {
        string line = ScanProgressReporter.Describe("", 0, 0, TimeSpan.Zero);
        StringAssert.Contains(line, "Scanning DCIM");
        StringAssert.Contains(line, "0 files");
    }

    [TestMethod]
    public void NoRateIsClaimedBeforeThereIsAnythingToMeasure()
    {
        // A rate computed from a fraction of a second would be noise, not information.
        string line = ScanProgressReporter.Describe("202608_a", 3, 1024, TimeSpan.FromMilliseconds(200));
        Assert.IsFalse(line.Contains("files/s"), line);
    }
}
