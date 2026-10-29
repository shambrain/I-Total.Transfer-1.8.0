using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The rename policy copies an incoming file beside a colliding name instead of overwriting it. The
/// engine must then verify the bytes it actually stored, not the untouched file that caused the
/// collision, and a resume must not add another copy every time it runs.
/// </summary>
[TestClass]
public sealed class RenameConflictTests
{
    private static string TempDestination()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-rename-flow", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static TransferOptions RenameOptions() =>
        TransferOptions.FromSettings(AppSettings.Defaults()) with { ConflictPolicy = ConflictPolicy.RenameIncoming };

    private static async Task<(TransferService Service, ScanResult Scan)> PrepareAsync(MockBackend backend, string destination)
    {
        var service = new TransferService(backend, new LogService(destination), RenameOptions());
        ScanResult scan = await service.ScanAsync(CancellationToken.None);
        return (service, scan);
    }

    [TestMethod]
    public async Task ARenamedCopyIsStoredUnderAFreeNameAndTheOriginalIsPreserved()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            string original = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            byte[] collision = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
            File.WriteAllBytes(original, collision);

            var (service, scan) = await PrepareAsync(backend, destination);
            TransferSummary summary = await service.CopyAsync(scan, destination, 1,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.AreEqual(1, summary.Copied);
            CollectionAssert.AreEqual(collision, File.ReadAllBytes(original), "the colliding file is never overwritten");

            string renamed = Path.Combine(destination, "DCIM", "IMG_0000 (1).bin");
            Assert.IsTrue(File.Exists(renamed), "the incoming copy is stored beside the collision");
            Assert.AreEqual(1024, new FileInfo(renamed).Length);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            TransferEntry entry = manifest.Entries.Single();
            Assert.AreEqual("DCIM\\IMG_0000 (1).bin", entry.CommittedPath,
                "the manifest must record where the bytes actually landed");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task VerificationChecksTheStoredFileRatherThanTheCollision()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            string original = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            File.WriteAllBytes(original, [9, 9, 9]);

            var (service, scan) = await PrepareAsync(backend, destination);
            await service.CopyAsync(scan, destination, 1, new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            VerificationResult result = await service.VerifyAsync(scan, destination,
                new Progress<TransferProgress>(_ => { }), CancellationToken.None);

            Assert.IsTrue(result.Completed, string.Join(" | ", result.Issues));
            Assert.AreEqual(1, result.Matching);
            Assert.AreEqual(0, result.WrongSize);
            Assert.AreEqual(0, result.WrongHash);
            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("STORED AS", StringComparison.Ordinal)),
                "the report must say the copy lives under a different name");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public async Task ARerunDoesNotAddASecondRenamedCopy()
    {
        using var backend = new MockBackend(canHashSourceBytes: true, fileCount: 1);
        string destination = TempDestination();
        try
        {
            string original = Path.Combine(destination, "DCIM", "IMG_0000.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            File.WriteAllBytes(original, [7, 7, 7, 7]);

            var (service, scan) = await PrepareAsync(backend, destination);
            var progress = new Progress<TransferProgress>(_ => { });
            TransferSummary first = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);
            Assert.AreEqual(1, first.Copied);

            var manifest = System.Text.Json.JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(Path.Combine(destination, ".i-total", "session.json")));
            Assert.IsNotNull(manifest);
            TransferEntry firstEntry = manifest.Entries.Single();
            Assert.IsFalse(string.IsNullOrWhiteSpace(firstEntry.CommittedPath), "committed path missing after first run");
            Assert.IsFalse(string.IsNullOrWhiteSpace(firstEntry.DestinationSha256), "digest missing after first run");
            string committedFull = Path.Combine(destination, firstEntry.CommittedPath!);
            Assert.IsTrue(File.Exists(committedFull), "committed file missing: " + committedFull);
            string committedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(committedFull)));
            Assert.AreEqual(firstEntry.DestinationSha256, committedHash, "the recorded digest does not match the stored file");

            TransferSummary second = await service.CopyAsync(scan, destination, 1, progress, CancellationToken.None);
            Assert.AreEqual(0, second.Copied, "the recorded renamed copy is reused");
            Assert.AreEqual(1, second.AlreadyPresent);
            Assert.IsFalse(File.Exists(Path.Combine(destination, "DCIM", "IMG_0000 (2).bin")),
                "no second renamed copy is created");
            Assert.AreEqual(2, Directory.GetFiles(Path.Combine(destination, "DCIM"), "IMG_0000*.bin").Length,
                "the collision and the one renamed copy are the only files");
        }
        finally { Directory.Delete(destination, true); }
    }
}
