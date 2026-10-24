using System.Diagnostics;
using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The reported failure was one iPhone album holding 26,320 items, so these tests reproduce that shape
/// deterministically, with no phone involved.
///
/// <para>
/// They guard the two ways a scanner of that size breaks. First, a quadratic step in the per-file path:
/// a scan that is fine on 200 files and unusable on 26,400. Second, an operation that cannot be stopped —
/// a scan must react to cancellation promptly, at whatever size. Timing bounds here are deliberately
/// generous; their job is to catch an accidental <c>O(n²)</c>, not to benchmark the machine. The real
/// device figures are recorded in <c>docs/PERFORMANCE-AUDIT.md</c>.
/// </para>
/// </summary>
[TestClass]
public sealed class LargeLibraryStressTests
{
    /// <summary>One more than the largest album measured on the live handset (26,320).</summary>
    private const int OneBigAlbum = 26_400;

    /// <summary>Counts reports without relying on <see cref="Progress{T}"/>, which needs a message pump.</summary>
    private sealed class RecordingProgress : IProgress<string>
    {
        private readonly List<string> _messages = new();

        public int Count { get { lock (_messages) return _messages.Count; } }

        public string Last { get { lock (_messages) return _messages.Count == 0 ? "" : _messages[^1]; } }

        public void Report(string value) { lock (_messages) _messages.Add(value); }
    }

    private static string TempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-stress", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScanResult Scan(ITransferBackend backend, IProgress<string>? progress, CancellationToken token = default)
    {
        var service = new TransferService(backend, new LogService(TempDirectory()));
        return service.ScanAsync(token, progress).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void ScanningASingleFolderOfTwentySixThousandEntriesCompletesAndReportsProgress()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: OneBigAlbum, bytesPerFile: 64);
        var progress = new RecordingProgress();

        var clock = Stopwatch.StartNew();
        ScanResult scan = Scan(backend, progress);
        clock.Stop();

        Assert.AreEqual(OneBigAlbum, scan.Files.Count,
            "A scanner that drops entries in a huge folder is the failure this workload exists to catch.");
        Assert.AreEqual(1, scan.Files.Select(f => f.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "The workload must really be one directory, as the report described it.");
        Assert.IsTrue(scan.Files.All(f => f.Size == 64));

        Assert.IsTrue(progress.Count >= 20,
            $"A scan of {OneBigAlbum:N0} entries reported only {progress.Count} time(s); with no reporting the "
            + "window looks frozen, which is the defect that was reported.");
        StringAssert.Contains(progress.Last, "Scanning", "Progress must say what it is doing.");

        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(15),
            $"Scanning {OneBigAlbum:N0} in-memory entries took {clock.Elapsed.TotalSeconds:N1}s, which points at a "
            + "quadratic step in the per-file path.");
    }

    [TestMethod]
    public void CancellingAScanOfABigAlbumIsHonouredPromptly()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: OneBigAlbum, bytesPerFile: 64);

        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress();
        var service = new TransferService(backend, new LogService(TempDirectory()));

        // Cancel from inside the first progress report: the backend must notice on its next step rather
        // than finishing the whole album first.
        var cancelOnFirstReport = new CancellingProgress(cancellation);

        var clock = Stopwatch.StartNew();
        try
        {
            service.ScanAsync(cancellation.Token, cancelOnFirstReport).GetAwaiter().GetResult();
            Assert.Fail("A cancelled scan must not report success — a partial library is not a complete one.");
        }
        catch (OperationCanceledException) { /* the only acceptable outcome */ }
        clock.Stop();

        Assert.IsTrue(cancelOnFirstReport.Reported,
            "The scan finished before it ever reported progress, so this test proved nothing about cancellation.");
        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(15),
            $"Cancellation took {clock.Elapsed.TotalSeconds:N1}s; it must not wait for the whole album.");
    }

    private sealed class CancellingProgress : IProgress<string>
    {
        private readonly CancellationTokenSource _source;
        public bool Reported { get; private set; }

        public CancellingProgress(CancellationTokenSource source) => _source = source;

        public void Report(string value)
        {
            Reported = true;
            _source.Cancel();
        }
    }

    [TestMethod]
    public void SummarisingAndFilteringThreeHundredThousandEntriesStaysLinear()
    {
        // The dashboard summary and the transfer plan both walk the whole library, so both must stay
        // linear. 300,000 entries across 300 folders is a deliberately oversized version of the reported
        // workload — the aggregate is the part that would blow up if it were recomputed per file.
        const int folders = 300, perFolder = 1_000;
        var files = new List<MediaFile>(folders * perFolder);
        for (int f = 0; f < folders; f++)
        {
            string folder = $"DCIM\\2026{f:D2}_a";
            for (int i = 0; i < perFolder; i++)
            {
                bool isVideo = i % 50 == 0;
                files.Add(new MediaFile(folder, isVideo ? $"VID_{f:D2}_{i:D4}.MOV" : $"IMG_{f:D2}_{i:D4}.JPG",
                    1_000 + i, isVideo ? ".MOV" : ".JPG"));
            }
        }

        var clock = Stopwatch.StartNew();
        Dictionary<MediaCategory, CategoryStat> summary = MediaSummary.From(files);
        IReadOnlyList<MediaFile> videosOnly = TransferRules.ApplyFilter(files, new FilterSpec(
            TransferRules.ParseExtensionList(".mov"), [], [], ExcludeSidecars: false, MinSize: 0, MaxSize: 0));
        clock.Stop();

        Assert.AreEqual(folders * perFolder, summary.Values.Sum(v => v.Files));
        Assert.AreEqual(folders * (perFolder / 50), summary[MediaCategory.Videos].Files,
            "Video membership is decided by extension, so the count must be exact.");
        Assert.AreEqual(files.Count, summary[MediaCategory.Photos].Files + summary[MediaCategory.Videos].Files,
            "Every entry in this workload is a photo or a video.");
        Assert.AreEqual(summary[MediaCategory.Videos].Files, videosOnly.Count,
            "The filter and the summary must agree about the same library.");
        Assert.AreEqual(summary.Values.Sum(v => v.Bytes), files.Sum(f => f.Size),
            "Total bytes must be the real total, not an estimate.");

        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(30),
            $"Summarising and filtering 300,000 entries took {clock.Elapsed.TotalSeconds:N1}s, which points at a "
            + "quadratic step.");
    }
}
