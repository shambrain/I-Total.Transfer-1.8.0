using System.IO.Compression;
using System.Text;
using ITotalTransfer.Core;
using ITotalTransfer.Licensing;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The diagnostics bundle is what an operator sends when the app misbehaves, so the one thing it must
/// never do is fail to exist. These tests hold the log open exactly the way the running application does
/// while the bundle is built, which is the situation the button is actually pressed in.
/// </summary>
[TestClass]
public sealed class DiagnosticsBundleTests
{
    private string _root = "";

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "itotal-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private static string ReadEntry(string zipPath, string entryName)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry? entry = zip.GetEntry(entryName);
        Assert.IsNotNull(entry, $"The bundle has no '{entryName}'.");
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [TestMethod]
    public void ABundleIsProducedWhileTheApplicationIsStillWritingItsLog()
    {
        string logPath = Path.Combine(_root, "I-total-transfer.log");
        File.WriteAllText(logPath,
            "[2026-10-19 13:42:09.688 +03:00] [INFO] Welcome to I-Total Transfer." + Environment.NewLine +
            "[2026-10-19 13:42:09.984 +03:00] [WARN] Connection check: NotFound - no accessible Apple mobile device." + Environment.NewLine,
            new UTF8Encoding(false));

        // Exactly how LogService.OpenWriter holds it for the lifetime of the app.
        using var live = new FileStream(logPath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        string zip;
        try
        {
            zip = DiagnosticsBundle.Create(_root, AppSettings.Defaults(), null, null, logPath, ["Attachment verdict: NONE"]);
        }
        catch (IOException ex)
        {
            Assert.Fail("The export threw while the log was open, so the operator gets no bundle at all: " + ex.Message);
            throw;
        }

        Assert.IsTrue(File.Exists(zip), "No bundle was written.");
        StringAssert.Contains(ReadEntry(zip, "logs-tail.txt"), "Welcome to I-Total Transfer",
            "The log tail is the most useful part of a bundle and must be captured while the app runs.");
        StringAssert.Contains(ReadEntry(zip, "logs-tail.txt"), "Connection check",
            "The whole log, not just its first line, travels.");
    }

    [TestMethod]
    public void ALockedLogCostsItsOwnFileAndNothingElse()
    {
        string logPath = Path.Combine(_root, "I-total-transfer.log");
        File.WriteAllText(logPath, "held", new UTF8Encoding(false));
        using var exclusive = new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        string zip = DiagnosticsBundle.Create(_root, AppSettings.Defaults(), null, null, logPath, ["Scanned files: 0"]);

        StringAssert.Contains(ReadEntry(zip, "logs-tail.txt"), "Log tail unavailable");
        StringAssert.Contains(ReadEntry(zip, "context.txt"), "Scanned files: 0",
            "One unreadable file must not cost the evidence that was readable.");
        StringAssert.Contains(ReadEntry(zip, "README.txt"), "diagnostics bundle");
        Assert.IsFalse(string.IsNullOrWhiteSpace(ReadEntry(zip, "environment.txt")));
    }

    [TestMethod]
    public void TheBundleCarriesTheUsbEvidenceAndRedactsWhatMustNotLeaveTheMachine()
    {
        string logPath = Path.Combine(_root, "I-total-transfer.log");
        File.WriteAllText(logPath, "[INFO] Device serial: FCC4L3VYCW1 attached." + Environment.NewLine, new UTF8Encoding(false));

        string[] extra =
        [
            "Attachment verdict: CACHED RECORD ONLY (2)",
            "Apple handsets attached: none",
            "Apple handset records cached by Windows: Apple iPhone, Apple iPad",
            "Attachment diagnosis: No handset is attached. Windows still holds 2 cached Apple handset records: Apple iPhone, Apple iPad."
        ];
        string zip = DiagnosticsBundle.Create(_root, AppSettings.Defaults(), null, null, logPath, extra);

        string context = ReadEntry(zip, "context.txt");
        StringAssert.Contains(context, "CACHED RECORD ONLY (2)");
        StringAssert.Contains(context, "Apple iPhone, Apple iPad",
            "The device names are the whole point of the evidence and must survive redaction verbatim.");
        StringAssert.Contains(context, "No handset is attached.");

        string log = ReadEntry(zip, "logs-tail.txt");
        Assert.IsFalse(log.Contains("FCC4L3VYCW1"), "A serial number must never leave the machine in a bundle.");
        StringAssert.Contains(log, "FCC4");
        StringAssert.Contains(log, "REDACTED");
    }

    [TestMethod]
    public void NoLogPathMeansNoLogFileRatherThanAnEmptyOne()
    {
        string zip = DiagnosticsBundle.Create(_root, AppSettings.Defaults(), null, null, null, ["Scanned files: 0"]);

        using ZipArchive archive = ZipFile.OpenRead(zip);
        Assert.IsNull(archive.GetEntry("logs-tail.txt"));
        Assert.IsNotNull(archive.GetEntry("context.txt"));
        StringAssert.Contains(ReadEntry(zip, "licence.txt"), "not available",
            "A bundle built without a licence service still says so instead of omitting the file.");
    }

    /// <summary>
    /// The counter is the one commercial fact support needs, and the purchase identifier and ledger
    /// fingerprints are the two that must never travel. This pins the boundary in both directions.
    /// </summary>
    [TestMethod]
    public void TheBundleCarriesTheTrialCounterButNotThePurchaseIdOrLedgerFingerprints()
    {
        var snapshot = new LicenseSnapshot(
            LicenseStanding.Trial, LicenseChannel.None,
            JobsUsed: 2, JobLimit: 3,
            Headline: "Trial - 1 of 3 transfer jobs left",
            Detail: "A job is counted only when a transfer writes at least one file and finishes with no failures.",
            LicenseId: "PURCHASE-ABCDEF1234567890");

        string zip = DiagnosticsBundle.Create(_root, AppSettings.Defaults(), null, null, null,
            ["Scanned files: 0"], snapshot);

        string licence = ReadEntry(zip, "licence.txt");
        StringAssert.Contains(licence, "Standing        : Trial");
        StringAssert.Contains(licence, "Jobs used       : 2");
        StringAssert.Contains(licence, "Job limit       : 3");
        StringAssert.Contains(licence, "Jobs remaining  : 1");
        StringAssert.Contains(licence, "New job allowed : True");
        Assert.IsFalse(licence.Contains("PURCHASE-ABCDEF"),
            "A purchase identifier must not leave the machine in a support bundle.");
        Assert.IsFalse(licence.Contains("ABCDEF1234567890"));

        using ZipArchive archive = ZipFile.OpenRead(zip);
        Assert.IsNull(archive.GetEntry("trial.json"),
            "The ledger itself carries durable per-job fingerprints; the bundle gets the counter, not the ledger.");
    }
}
