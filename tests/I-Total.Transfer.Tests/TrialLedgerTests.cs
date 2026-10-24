using ITotalTransfer.Licensing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace ITotalTransfer.Tests;

/// <summary>
/// Persistence of the trial counter. The point of two stores is that a single deletion is not a reset, so
/// the merge and the corrupt-file behaviour are tested directly rather than assumed.
/// </summary>
[TestClass]
public sealed class TrialLedgerTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-trial", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [TestMethod]
    public void TheFileLedgerRoundTripsEveryField()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "trial.json");
            var store = new FileTrialStore(path);
            var state = new TrialLedgerState
            {
                JobsUsed = 2,
                ConsumedJobs = ["aaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbb"],
                FirstSeenUtc = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                LastSeenUtc = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero),
                LastJobUtc = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero)
            };

            store.Save(state);
            TrialLedgerState? loaded = store.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.JobsUsed);
            CollectionAssert.AreEquivalent(state.ConsumedJobs, loaded.ConsumedJobs);
            Assert.AreEqual(state.FirstSeenUtc, loaded.FirstSeenUtc);
            Assert.AreEqual(state.LastJobUtc, loaded.LastJobUtc);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ACorruptFileIsQuarantinedAndTheOtherStoreStillCounts()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "trial.json");
            File.WriteAllText(path, "{ this is not json");

            var fileStore = new FileTrialStore(path);
            Assert.IsNull(fileStore.Load(), "A corrupt counter must not be read as a fresh trial.");
            Assert.IsTrue(Directory.GetFiles(dir, "trial.json.corrupt-*").Length == 1,
                "The unreadable counter is preserved for diagnosis, not silently overwritten.");

            // The registry-side copy is the one that decides, so the trial keeps its two used jobs.
            var survivor = new MemoryTrialStore(new TrialLedgerState { JobsUsed = 2 });
            TrialLedgerState? merged = new CompositeTrialStore(fileStore, survivor).Load();
            Assert.AreEqual(2, merged!.JobsUsed);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void TheMergeTakesTheHigherCountAndTheUnionOfJobs()
    {
        var a = new TrialLedgerState
        {
            JobsUsed = 2,
            ConsumedJobs = ["one", "two"],
            FirstSeenUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            LastSeenUtc = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero)
        };
        var b = new TrialLedgerState
        {
            JobsUsed = 3,
            ConsumedJobs = ["two", "three"],
            FirstSeenUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            LastSeenUtc = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero)
        };

        a.MergeFrom(b);
        Assert.AreEqual(3, a.JobsUsed);
        CollectionAssert.AreEquivalent(new[] { "one", "two", "three" }, a.ConsumedJobs);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), a.FirstSeenUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), a.LastSeenUtc);
    }

    [TestMethod]
    public void NegativeCountsAndDuplicatesAreRepairedInsteadOfTrusted()
    {
        var negative = new TrialLedgerState { JobsUsed = -5 }.Normalized();
        Assert.AreEqual(0, negative.JobsUsed);

        var duplicates = new TrialLedgerState
        {
            JobsUsed = 0,
            ConsumedJobs = ["duplicate", "duplicate", "", null!]
        }.Normalized();

        CollectionAssert.AreEquivalent(new[] { "duplicate" }, duplicates.ConsumedJobs);
        Assert.AreEqual(1, duplicates.JobsUsed,
            "The count is lifted to the number of recorded jobs so the display cannot disagree with the ledger.");

        var ahead = new TrialLedgerState { JobsUsed = 0, ConsumedJobs = ["a", "b"] }.Normalized();
        Assert.AreEqual(2, ahead.JobsUsed, "The count may never be lower than the recorded jobs.");
    }

    [TestMethod]
    public void TheRegistryLedgerRoundTripsAndIsRemovedAfterwards()
    {
        string subKey = @"Software\I-Total Transfer\Tests\Trial-" + Guid.NewGuid().ToString("N");
        try
        {
            var store = new RegistryTrialStore(subKey);
            Assert.IsNull(store.Load());

            store.Save(new TrialLedgerState
            {
                JobsUsed = 2,
                ConsumedJobs = ["cafebabe0000000000000000"],
                FirstSeenUtc = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero),
                LastSeenUtc = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero)
            });

            TrialLedgerState? loaded = store.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.JobsUsed);
            CollectionAssert.AreEquivalent(new[] { "cafebabe0000000000000000" }, loaded.ConsumedJobs);
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
