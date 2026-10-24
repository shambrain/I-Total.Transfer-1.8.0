using System.Diagnostics;
using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The second-pass check: two enumerations of one device are compared by relative path and size, and a
/// listing that moved between them must not be accepted as a transfer plan.
/// </summary>
[TestClass]
public sealed class ScanStabilityTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private static ScanResult Scan(params (string Folder, string Name, long Size)[] files)
    {
        var list = files.Select(f => new MediaFile(f.Folder, f.Name, f.Size, Path.GetExtension(f.Name))).ToList();
        return new ScanResult("Apple iPhone", Now.AddMinutes(-1), Now, list, []);
    }

    [TestMethod]
    public void AnIdenticalListingHasNoChanges()
    {
        ScanResult first = Scan(("DCIM", "IMG_0001.HEIC", 100), ("DCIM", "IMG_0002.HEIC", 200));
        ScanResult second = Scan(("DCIM", "IMG_0001.HEIC", 100), ("DCIM", "IMG_0002.HEIC", 200));

        Assert.AreEqual(0, ScanStability.ChangedPaths(first, second).Count);
        Assert.IsTrue(ScanStability.Agree(first, second));
    }

    [TestMethod]
    public void AddedResizedAndRemovedPathsAreAllReported()
    {
        ScanResult first = Scan(
            ("DCIM", "IMG_0001.HEIC", 100),   // unchanged
            ("DCIM", "IMG_0002.HEIC", 200),   // size changed
            ("DCIM", "IMG_0003.HEIC", 300));  // removed
        ScanResult second = Scan(
            ("DCIM", "IMG_0001.HEIC", 100),
            ("DCIM", "IMG_0002.HEIC", 999),
            ("DCIM", "IMG_0004.HEIC", 400));  // added

        IReadOnlyList<string> changed = ScanStability.ChangedPaths(first, second);

        Assert.AreEqual(3, changed.Count, string.Join(", ", changed));
        Assert.IsTrue(changed.Any(p => p.EndsWith("IMG_0002.HEIC", StringComparison.Ordinal)), "A resized file is named.");
        Assert.IsTrue(changed.Any(p => p.EndsWith("IMG_0003.HEIC", StringComparison.Ordinal)), "A removed file is named.");
        Assert.IsTrue(changed.Any(p => p.EndsWith("IMG_0004.HEIC", StringComparison.Ordinal)), "An added file is named.");
        Assert.IsFalse(changed.Any(p => p.EndsWith("IMG_0001.HEIC", StringComparison.Ordinal)), "An unchanged file is not named.");
        Assert.IsFalse(ScanStability.Agree(first, second));
    }

    [TestMethod]
    public void MatchingIgnoresCaseTheWayWindowsDoes()
    {
        ScanResult first = Scan(("DCIM/100APPLE", "IMG_0001.HEIC", 100));
        ScanResult second = Scan(("dcim/100apple", "img_0001.heic", 100));

        Assert.IsTrue(ScanStability.Agree(first, second),
            "A case-only difference is not a change on a case-insensitive filesystem.");
    }

    [TestMethod]
    public void TheReportIsCappedSoADialogCannotBeFlooded()
    {
        var before = Enumerable.Range(0, 5_000).Select(i => ("DCIM", $"IMG_{i:D5}.HEIC", 100L)).ToArray();
        var after = Enumerable.Range(1_000_000, 5_000).Select(i => ("DCIM", $"IMG_{i:D5}.HEIC", 100L)).ToArray();

        IReadOnlyList<string> changed = ScanStability.ChangedPaths(Scan(before), Scan(after));

        Assert.AreEqual(20, changed.Count, "Only a handful of paths are quoted, however large the change.");
    }

    [TestMethod]
    public void ADuplicatePathInAPartialScanDoesNotThrow()
    {
        // A scan with duplicate destination paths is classified incomplete and warned about, but it is still
        // a scan the operator can be holding when the second pass runs. The first implementation built a
        // dictionary from it and threw, turning a warning into "Scan failed".
        ScanResult partial = Scan(
            ("DCIM", "IMG_0001.HEIC", 100),
            ("DCIM", "IMG_0001.HEIC", 100));

        IReadOnlyList<string> changed = ScanStability.ChangedPaths(partial, partial);

        Assert.AreEqual(0, changed.Count);
    }

    [TestMethod]
    [Timeout(120_000)]
    public void ALargeCameraRollIsComparedInLinearTime()
    {
        // 26,000 paths is one real camera roll. The quadratic implementation this replaced needed hundreds of
        // millions of string comparisons here (~26,000 x 26,000) on the UI thread; the linear one is a
        // dictionary lookup per path. The bound is deliberately loose so a slow build machine cannot fail it
        // - it exists to catch the return of a nested scan, which overshoots it by seconds.
        (string, string, long)[] files = Enumerable.Range(0, 26_000)
            .Select(i => ("DCIM/100APPLE", $"IMG_{i:D5}.HEIC", 1024L + i)).ToArray();
        ScanResult first = Scan(files);
        ScanResult second = Scan(files);

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<string> changed = ScanStability.ChangedPaths(first, second);
        stopwatch.Stop();

        Assert.AreEqual(0, changed.Count);
        TestContext.WriteLine($"STABILITY-BENCH 26,000 paths compared in {stopwatch.Elapsed.TotalMilliseconds:N1} ms");
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 5,
            $"Comparing 26,000 paths took {stopwatch.Elapsed.TotalSeconds:N2} s; the comparison looks quadratic again.");
    }
}
