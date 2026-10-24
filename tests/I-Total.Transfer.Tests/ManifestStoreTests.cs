using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class ManifestStoreTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-manifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScanResult Scan(params MediaFile[] files) =>
        new("iPhone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, files, []);

    private static ScanResult ScanWithIdentity(string deviceName, string? stableId, params MediaFile[] files) =>
        new(deviceName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, files, [])
        {
            StableId = stableId
        };

    [TestMethod]
    public void FreshDestinationCreatesNewManifest()
    {
        string dir = TempDir();
        try
        {
            var store = new ManifestStore(dir, new LogService(dir));
            SessionManifest manifest = store.LoadOrCreate(dir, "iPhone", null, "WPD");
            Assert.AreEqual(ManifestStore.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.AreEqual("iPhone", manifest.DeviceName);
            Assert.AreEqual(0, manifest.Entries.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void MergeScanAndReloadPreservesEntries()
    {
        string dir = TempDir();
        try
        {
            var store = new ManifestStore(dir, new LogService(dir));
            store.LoadOrCreate(dir, "iPhone", null, "WPD");
            store.MergeScan(Scan(new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic"),
                                 new MediaFile("DCIM", "IMG_2.MOV", 20, ".mov")));
            Assert.AreEqual(2, store.Manifest.Entries.Count);
            Assert.AreEqual("Complete", store.Manifest.SourceEnumerationStatus,
                "The record must remember whether the plan came from a complete or partial walk.");

            var reopened = new ManifestStore(dir, new LogService(dir));
            SessionManifest loaded = reopened.LoadOrCreate(dir, "iPhone", null, "WPD");
            Assert.AreEqual(2, loaded.Entries.Count);
            Assert.IsTrue(loaded.Entries.Any(e => e.RelativePath.EndsWith("IMG_1.HEIC", StringComparison.OrdinalIgnoreCase)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void CorruptManifestIsPreservedAsBackupAndNewManifestCreated()
    {
        string dir = TempDir();
        try
        {
            string manifestPath = Path.Combine(dir, ".i-total", "session.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, "{ corrupt manifest ");

            var store = new ManifestStore(dir, new LogService(dir));
            SessionManifest manifest = store.LoadOrCreate(dir, "iPhone", null, "WPD");
            Assert.AreEqual(0, manifest.Entries.Count);
            Assert.IsTrue(File.Exists(manifestPath + ".bak"), "The unreadable manifest should be preserved as .bak.");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ManifestForDifferentDestinationIsNotReused()
    {
        string dir = TempDir();
        try
        {
            string manifestPath = Path.Combine(dir, ".i-total", "session.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath,
                "{\"SchemaVersion\":2,\"Destination\":\"C:\\\\SomeOther\\\\Backup\",\"Entries\":[{\"RelativePath\":\"DCIM\\\\x.heic\"}]}");

            var store = new ManifestStore(dir, new LogService(dir));
            SessionManifest loaded = store.LoadOrCreate(dir, "iPhone", null, "WPD");
            Assert.AreEqual(0, loaded.Entries.Count, "A manifest for a different destination must not be adopted.");
            Assert.IsTrue(File.Exists(manifestPath + ".bak"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void SameHardwareIdentityResumesTheExistingSession()
    {
        string dir = TempDir();
        try
        {
            var first = new ManifestStore(dir, new LogService(dir));
            first.LoadOrCreate(dir, "Apple iPhone", "phone-a", "WPD");
            first.MergeScan(ScanWithIdentity("Apple iPhone", "phone-a",
                new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));

            var reopened = new ManifestStore(dir, new LogService(dir));
            SessionManifest loaded = reopened.LoadOrCreate(dir, "Apple iPhone", "phone-a", "WPD");

            Assert.AreEqual(1, loaded.Entries.Count);
            Assert.AreEqual("phone-a", loaded.DeviceStableId);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void DifferentHardwareIdentityIsRefusedBeforeTheManifestIsTrusted()
    {
        string dir = TempDir();
        try
        {
            var first = new ManifestStore(dir, new LogService(dir));
            first.LoadOrCreate(dir, "Apple iPhone", "phone-a", "WPD");
            first.MergeScan(ScanWithIdentity("Apple iPhone", "phone-a",
                new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));
            string manifestPath = Path.Combine(dir, ".i-total", "session.json");
            string before = File.ReadAllText(manifestPath);

            var second = new ManifestStore(dir, new LogService(dir));
            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => second.LoadOrCreate(dir, "Apple iPhone", "phone-b", "WPD"));

            StringAssert.Contains(error.Message, "different phone");
            StringAssert.Contains(error.Message, "No files were changed");
            Assert.AreEqual(before, File.ReadAllText(manifestPath),
                "A refused source identity must leave the existing session record untouched.");
            Assert.IsFalse(File.Exists(manifestPath + ".bak"),
                "The record is not archived when it is refused; it still belongs to the original phone.");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void LegacyManifestAdoptsAHardwareIdentityOnlyWhenTheDeviceNameMatches()
    {
        string dir = TempDir();
        try
        {
            var first = new ManifestStore(dir, new LogService(dir));
            first.LoadOrCreate(dir, "Apple iPhone", null, "WPD");
            first.MergeScan(ScanWithIdentity("Apple iPhone", null,
                new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));

            var reopened = new ManifestStore(dir, new LogService(dir));
            SessionManifest loaded = reopened.LoadOrCreate(dir, "Apple iPhone", "phone-a", "WPD");

            Assert.AreEqual("phone-a", loaded.DeviceStableId,
                "A pre-identity manifest may be upgraded once the name agrees.");
            Assert.AreEqual(1, loaded.Entries.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void LegacyManifestWithADifferentDeviceNameIsRefused()
    {
        string dir = TempDir();
        try
        {
            var first = new ManifestStore(dir, new LogService(dir));
            first.LoadOrCreate(dir, "Apple iPhone", null, "WPD");
            first.MergeScan(ScanWithIdentity("Apple iPhone", null,
                new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));

            var second = new ManifestStore(dir, new LogService(dir));
            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => second.LoadOrCreate(dir, "HUAWEI Y7 Prime 2018", "phone-b", "WPD"));

            StringAssert.Contains(error.Message, "Choose a new destination folder");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AManifestWithAHardwareIdentityIsNotResumedWithoutOne()
    {
        string dir = TempDir();
        try
        {
            var first = new ManifestStore(dir, new LogService(dir));
            first.LoadOrCreate(dir, "Apple iPhone", "phone-a", "WPD");
            first.MergeScan(ScanWithIdentity("Apple iPhone", "phone-a",
                new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));

            var shell = new ManifestStore(dir, new LogService(dir));
            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => shell.LoadOrCreate(dir, "Apple iPhone", null, "Windows Shell"));

            StringAssert.Contains(error.Message, "did not provide a hardware identity");
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// The manifest is the recovery plan, so losing a write to a momentarily locked file is not just a
    /// logging nuisance: it ages the plan the Overview reads and a continuation rebuilds from. A locked
    /// record must not throw, must not damage the record already on disk, and must catch up once the lock
    /// is gone.
    /// </summary>
    [TestMethod]
    public void ALockedManifestDoesNotThrowAndDoesNotLoseTheRecordItAlreadyHas()
    {
        string dir = TempDir();
        try
        {
            var store = new ManifestStore(dir, new LogService(dir));
            store.LoadOrCreate(dir, "iPhone", null, "WPD");
            store.MergeScan(Scan(new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic")));

            string manifestPath = Path.Combine(dir, ".i-total", "session.json");
            string good = File.ReadAllText(manifestPath);
            Assert.IsTrue(good.Contains("IMG_1.HEIC", StringComparison.OrdinalIgnoreCase));

            // Hold the record open, exclusively, across the whole retry window.
            using (new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                store.MergeScan(Scan(new MediaFile("DCIM", "IMG_1.HEIC", 10, ".heic"),
                                     new MediaFile("DCIM", "IMG_2.MOV", 20, ".mov")));
            }

            Assert.AreEqual(good, File.ReadAllText(manifestPath),
                "A refused write must leave the previous record exactly as it was, never half-written.");

            store.Flush();
            Assert.IsTrue(File.ReadAllText(manifestPath).Contains("IMG_2.MOV", StringComparison.OrdinalIgnoreCase),
                "Once the lock is gone the record has to catch up.");
        }
        finally { Directory.Delete(dir, true); }
    }
}
