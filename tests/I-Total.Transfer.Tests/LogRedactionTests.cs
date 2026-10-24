using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The redaction contract behind the acceptance item "Logs and exported reports avoid unnecessary private
/// data". A device serial is a hardware identifier: it must not be recoverable from the log file that sits
/// inside the backup folder, nor from the diagnostics bundle that promises in writing not to carry one.
///
/// <para>
/// The opposite failure is tested just as deliberately: a redactor that mangles ordinary log text makes the
/// log useless without making it safer, so the operational lines a support engineer needs must survive
/// byte-for-byte.
/// </para>
/// </summary>
[TestClass]
public sealed class LogRedactionTests
{
    private const string Serial = "FCC4L3VYCW";

    // ------------------------------------------------------------------ what must not survive

    [TestMethod]
    public void TheDeviceSummaryLineKeepsTheLabelAndLosesTheSerial()
    {
        string line = "Apple iPhone - iOS: 27.0.1 - Serial: " + Serial + " - Link: USB (WPD)";

        string redacted = LogService.RedactSensitive(line);

        Assert.IsFalse(redacted.Contains(Serial, StringComparison.Ordinal),
            "A full serial must not be recoverable from a line that ships with the backup: " + redacted);
        StringAssert.Contains(redacted, "Serial:", "The line must still read as a support line.");
        StringAssert.Contains(redacted, "REDACTED");
        StringAssert.Contains(redacted, "USB (WPD)", "Nothing but the identifier may be touched.");
    }

    [TestMethod]
    public void AUdidIsMaskedTheSameWayASerialIs()
    {
        string redacted = LogService.RedactSensitive("AFC device UDID: 00008030-001A2B3C4D5E6F7G");
        Assert.IsFalse(redacted.Contains("00008030-001A2B3C4D5E6F7G", StringComparison.Ordinal), redacted);
        StringAssert.Contains(redacted, "UDID");
    }

    [TestMethod]
    public void AStableIdInsideJsonIsMaskedWithoutBreakingTheJson()
    {
        string json = "{ \"DeviceStableId\": \"00001234567890ABCDEF\", \"BackendName\": \"Windows Portable Devices\" }";

        string redacted = LogService.RedactSensitive(json);

        Assert.IsFalse(redacted.Contains("00001234567890ABCDEF", StringComparison.Ordinal), redacted);
        StringAssert.Contains(redacted, "DeviceStableId", "The key is what makes the value readable as an identifier.");
        StringAssert.Contains(redacted, "Windows Portable Devices");
    }

    [TestMethod]
    public void LongHexDigestsKeepOnlyAShortPrefix()
    {
        string digest = new string('a', 64);
        string redacted = LogService.RedactSensitive("sha256=" + digest);
        Assert.IsFalse(redacted.Contains(digest, StringComparison.Ordinal));
        StringAssert.StartsWith(redacted, "sha256=aaaa…REDACTED");
    }

    [TestMethod]
    public void ASecretAssignmentLosesItsValueAndNotJustItsLabel()
    {
        // The old rule rewrote "password: hunter2" as "password=…REDACTED hunter2": the value was left
        // sitting in the log, which is worse than not redacting at all because it reads as done.
        string redacted = LogService.RedactSensitive("pairing_token: 8f14e45fceea167a and password: hunter2");

        Assert.IsFalse(redacted.Contains("8f14e45fceea167a", StringComparison.Ordinal), redacted);
        Assert.IsFalse(redacted.Contains("hunter2", StringComparison.Ordinal), redacted);
        StringAssert.Contains(redacted, "pairing_token=…REDACTED");
        StringAssert.Contains(redacted, "password=…REDACTED");
    }

    // ------------------------------------------------------------------ what must survive

    [TestMethod]
    public void OrdinaryOperationalTextIsLeftExactlyAsWritten()
    {
        string[] lines =
        [
            "Scan finished: 26,335 files, 106.49 GB, status=Complete, duration=12.1 min, backend=Windows Portable Devices (native).",
            "Copying DCIM/100APPLE/IMG_1234.HEIC (3.4 MB).",
            "A folder named Serial number photos is not an identifier and must not be mistaken for one.",
            "Destination free space: 412.3 GB.",
        ];

        foreach (string line in lines)
            Assert.AreEqual(line, LogService.RedactSensitive(line), "Redaction must not touch ordinary log text.");
    }

    [TestMethod]
    public void AnAbsentIdentifierIsNotMaskedIntoNoise()
    {
        Assert.AreEqual("Serial: unavailable", LogService.RedactSensitive("Serial: unavailable"));
        Assert.AreEqual("UDID: None", LogService.RedactSensitive("UDID: None"));
    }

    [TestMethod]
    public void AnEmptyMessageIsReturnedUnchanged()
    {
        Assert.AreEqual("", LogService.RedactSensitive(""));
        Assert.IsNull(LogService.RedactSensitive(null!));
    }

    // ------------------------------------------------------------------ the file the operator ends up with

    [TestMethod]
    public void ALogFileWrittenWithRedactionOnNeverHoldsTheSerial()
    {
        string folder = Path.Combine(Path.GetTempPath(), "itotal-redact", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var log = new LogService(folder, redact: true, minimumLevel: "INFO"))
            {
                log.Write("Device: Apple iPhone - iOS: 27.0.1 - Serial: " + Serial + " - Link: USB (WPD)");
                log.Write("Connection check: Ready (Windows Portable Devices (native))", "OK");
            }

            string text = File.ReadAllText(Path.Combine(folder, "I-total-transfer.log"));

            Assert.IsFalse(text.Contains(Serial, StringComparison.Ordinal),
                "The serial must never reach the log file that ships inside the destination folder.");
            StringAssert.Contains(text, "Serial:", "The line still has to be usable by support.");
            StringAssert.Contains(text, "Connection check", "The rest of the log is untouched.");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
        }
    }
}
