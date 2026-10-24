using System.Text.Json;
using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The destination folder's own session record is the only evidence an interrupted job leaves behind, so
/// what it is allowed to claim is pinned here. These tests use no device and no engine: they fix the
/// reading, the counting and the refusal rules of <see cref="SessionRecovery"/>.
/// </summary>
[TestClass]
public sealed class SessionRecoveryTests
{
    private static string TempDestination()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string destination, SessionManifest manifest)
    {
        string folder = Path.Combine(destination, ".i-total");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "session.json"), JsonSerializer.Serialize(manifest));
    }

    private static SessionManifest Manifest(string destination, string? status, params TransferEntry[] entries) => new()
    {
        Destination = destination,
        DeviceName = "Apple iPhone",
        BackendName = "Windows Portable Devices (native)",
        LastSessionStatus = status,
        Entries = [.. entries]
    };

    private static TransferEntry Entry(string relativePath, long bytes, TransferState state) =>
        new() { RelativePath = relativePath, ExpectedBytes = bytes, State = state };

    [TestMethod]
    public void AFolderWithNoRecordHasNothingToContinue()
    {
        string destination = TempDestination();
        try
        {
            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsFalse(state.HasSession);
            Assert.IsNull(state.Problem, "An ordinary folder is not a problem; it simply holds no session.");
            Assert.IsFalse(state.CanResume);
            Assert.IsTrue(state.Describe().Contains("No session record", StringComparison.Ordinal));
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void EveryEntryStateIsCountedAndNothingIsInvented()
    {
        string destination = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(destination, "RUNNING",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\IMG_0002.JPG", 200, TransferState.SizeVerified),
                Entry("DCIM\\IMG_0003.JPG", 300, TransferState.Skipped),
                Entry("DCIM\\IMG_0004.JPG", 400, TransferState.CopiedUnverified),
                Entry("DCIM\\IMG_0005.JPG", 500, TransferState.Failed),
                Entry("DCIM\\IMG_0006.JPG", 600, TransferState.Inaccessible),
                Entry("DCIM\\IMG_0007.JPG", 700, TransferState.Queued),
                Entry("DCIM\\IMG_0008.JPG", 800, TransferState.Copying)));

            RecoveryState state = SessionRecovery.Inspect(destination);

            Assert.IsTrue(state.HasSession);
            Assert.AreEqual(8, state.Total);
            Assert.AreEqual(2, state.Verified);
            Assert.AreEqual(1, state.Skipped);
            Assert.AreEqual(1, state.CopiedUnverified);
            Assert.AreEqual(1, state.Failed);
            Assert.AreEqual(1, state.Inaccessible);
            Assert.AreEqual(2, state.Pending, "A file interrupted mid-flight is work the session never finished.");
            Assert.AreEqual(300, state.VerifiedBytes);
            Assert.AreEqual(400 + 500 + 600 + 700 + 800, state.OutstandingBytes);
            Assert.AreEqual(5, state.Outstanding);
            Assert.IsTrue(state.CanResume);
            Assert.IsFalse(state.Finished);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void TheCompactBarNamesFilteredFilesAsSkippedNotPlanned()
    {
        // A filtered run writes every scanned file into the session so recovery can tell what was
        // deliberately left out. That must not make the bar claim those files were planned: 4,106
        // "planned" beside 412 "copied" reads as 3,500 lost files.
        string destination = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(destination,
                "COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\IMG_0002.JPG", 200, TransferState.CopiedUnverified),
                Entry("DCIM\\IMG_0003.JPG", 300, TransferState.Skipped),
                Entry("DCIM\\IMG_0004.JPG", 400, TransferState.Skipped)));

            RecoveryState state = SessionRecovery.Inspect(destination);
            string compact = state.Compact();

            StringAssert.Contains(compact, "2 planned");
            StringAssert.Contains(compact, "2 skipped by filter");
            Assert.IsFalse(compact.Contains("4 planned", StringComparison.Ordinal),
                "Files rejected by the filter were never planned for copying.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void AFinishedSessionIsNotOfferedForResume()
    {
        string destination = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(destination, "HASH-VERIFIED COMPLETE (source-to-destination)",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\IMG_0002.JPG", 200, TransferState.Skipped)));

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsTrue(state.Finished);
            Assert.IsFalse(state.CanResume);
            Assert.AreEqual(0, state.Outstanding);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ASessionThatCouldNotBeDeclaredCompleteIsStillOffered()
    {
        string destination = TempDestination();
        try
        {
            // The engine's own "finished a pass, but it is not a verified complete backup" wording must not
            // be read as success: it contains the word COMPLETE and still describes unfinished work.
            WriteManifest(destination, Manifest(destination, "INCOMPLETE — NOT A VERIFIED COMPLETE BACKUP",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\IMG_0002.JPG", 200, TransferState.Failed)));

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsFalse(state.Finished);
            Assert.IsTrue(state.CanResume);
            Assert.AreEqual(1, state.Outstanding);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ARunThatStoppedWithoutASummaryIsReportedAsInterrupted()
    {
        string destination = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(destination, null,
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\IMG_0002.JPG", 200, TransferState.Queued)));

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.AreEqual(RecoveryState.StatusUnknown, state.Status);
            Assert.IsTrue(state.WhyStopped.Contains("never recorded", StringComparison.Ordinal),
                "An unrecorded outcome must be described as unrecorded, not guessed at.");
            Assert.IsTrue(state.CanResume);
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ARunningSessionIsDescribedAsInterrupted()
    {
        string destination = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(destination, "RUNNING",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.Queued)));
            RecoveryState state = SessionRecovery.Inspect(destination);
            StringAssert.Contains(state.Headline, "interrupted");
            StringAssert.Contains(state.WhyStopped, "stopped while files were still moving");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ARecordBelongingToAnotherFolderIsRefused()
    {
        string destination = TempDestination();
        string other = TempDestination();
        try
        {
            WriteManifest(destination, Manifest(other, "RUNNING", Entry("DCIM\\IMG_0001.JPG", 100, TransferState.Queued)));
            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsFalse(state.HasSession);
            Assert.IsFalse(state.CanResume);
            Assert.IsNotNull(state.Problem);
            StringAssert.Contains(state.Problem!, "different destination");
        }
        finally { Directory.Delete(destination, true); Directory.Delete(other, true); }
    }

    [TestMethod]
    public void ADamagedRecordIsReportedAndLeftInPlace()
    {
        string destination = TempDestination();
        try
        {
            string folder = Path.Combine(destination, ".i-total");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "session.json");
            File.WriteAllText(path, "{ this is not a manifest");

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsFalse(state.HasSession);
            Assert.IsFalse(state.CanResume);
            Assert.IsNotNull(state.Problem);
            Assert.IsTrue(File.Exists(path), "A damaged record is evidence; it is reported, never deleted.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ARecordFromANewerSchemaIsRefusedRatherThanMisread()
    {
        string destination = TempDestination();
        try
        {
            SessionManifest manifest = Manifest(destination, "RUNNING", Entry("DCIM\\IMG_0001.JPG", 100, TransferState.Queued));
            manifest.SchemaVersion = 99;
            WriteManifest(destination, manifest);

            RecoveryState state = SessionRecovery.Inspect(destination);
            Assert.IsFalse(state.HasSession);
            Assert.IsNotNull(state.Problem);
            StringAssert.Contains(state.Problem!, "schema");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void TheResumePlanCarriesOnlyOutstandingEntriesAndTheirOwnFacts()
    {
        string destination = TempDestination();
        try
        {
            var modified = new DateTimeOffset(2026, 8, 1, 12, 30, 0, TimeSpan.Zero);
            SessionManifest manifest = Manifest(destination, "RUNNING",
                Entry("DCIM\\100APPLE\\IMG_0001.JPG", 100, TransferState.IntegrityVerified),
                Entry("DCIM\\100APPLE\\IMG_0002.JPG", 200, TransferState.Skipped),
                Entry("DCIM\\100APPLE\\IMG_0003.JPG", 300, TransferState.Queued),
                Entry("DCIM\\100APPLE\\VID_0004.MOV", 400, TransferState.Failed));
            manifest.Entries[2].SourceModifiedUtc = modified;

            ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out int unsafeSkipped);

            Assert.AreEqual(0, unsafeSkipped);
            Assert.AreEqual(2, plan.Files.Count, "Only the unfinished entries are work; verified ones are not re-fetched.");
            Assert.IsTrue(plan.Files.All(f => f.RelativePath is "DCIM\\100APPLE\\IMG_0003.JPG" or "DCIM\\100APPLE\\VID_0004.MOV"));
            Assert.AreEqual("DCIM\\100APPLE", plan.Files[0].Folder);
            Assert.AreEqual("IMG_0003.JPG", plan.Files[0].Name);
            Assert.AreEqual(300, plan.Files[0].Size);
            Assert.AreEqual(".jpg", plan.Files[0].Extension);
            Assert.AreEqual(modified, plan.Files[0].ModifiedUtc, "A resumed file keeps the source timestamp the session recorded.");
            Assert.AreEqual(".mov", plan.Files[1].Extension);
            Assert.IsTrue(plan.Files.All(f => f.SourceObjectId is null),
                "Object identifiers do not survive a reconnect; the backend must resolve each file by path.");
            Assert.IsTrue(plan.IsComplete, "A plan rebuilt from a finished record is not a partial enumeration.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void TheResumePlanRefusesPathsTheEngineWouldReject()
    {
        string destination = TempDestination();
        try
        {
            SessionManifest manifest = Manifest(destination, "RUNNING",
                Entry("DCIM\\IMG_0001.JPG", 100, TransferState.Queued),
                Entry("..\\..\\escape.txt", 10, TransferState.Queued),
                Entry("C:\\Windows\\system32\\evil.dll", 10, TransferState.Queued),
                Entry("no-folder.txt", 10, TransferState.Queued));

            ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out int unsafeSkipped);

            Assert.AreEqual(1, plan.Files.Count);
            Assert.AreEqual(3, unsafeSkipped, "Paths the engine would reject are counted, never passed on as work.");
        }
        finally { Directory.Delete(destination, true); }
    }

    [TestMethod]
    public void ARebuiltPlanCarriesTheRecordedHardwareIdentity()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED",
            new TransferEntry { RelativePath = @"DCIM\IMG_1.HEIC", ExpectedBytes = 10, State = TransferState.Queued });
        manifest.DeviceStableId = "phone-a";

        ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out _);

        Assert.AreEqual("phone-a", plan.StableId,
            "The engine compares the plan's source identity with the record before fetching anything.");
    }

    [TestMethod]
    public void ARebuiltPlanKeepsAnIncompleteEnumerationIncomplete()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED",
            new TransferEntry { RelativePath = @"DCIM\IMG_1.HEIC", ExpectedBytes = 10, State = TransferState.Queued });
        manifest.SourceEnumerationStatus = nameof(EnumerationStatus.Incomplete);

        ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out _);

        Assert.AreEqual(EnumerationStatus.Incomplete, plan.Status,
            "A cancel must not erase the fact that the source listing was partial.");
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("remains partial", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnUnrecognisedEnumerationStatusIsTreatedAsPartial()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED",
            new TransferEntry { RelativePath = @"DCIM\IMG_1.HEIC", ExpectedBytes = 10, State = TransferState.Queued });
        manifest.SourceEnumerationStatus = "something-this-build-does-not-know";

        ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out _);

        Assert.AreEqual(EnumerationStatus.Incomplete, plan.Status,
            "Damaged status text must never be interpreted as a complete enumeration.");
    }

    [TestMethod]
    public void ALegacyPlanWithoutAnEnumerationStatusIsTreatedAsComplete()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED",
            new TransferEntry { RelativePath = @"DCIM\IMG_1.HEIC", ExpectedBytes = 10, State = TransferState.Queued });
        manifest.SourceEnumerationStatus = null;

        ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out _);

        Assert.AreEqual(EnumerationStatus.Complete, plan.Status,
            "A record written before the status field existed has no evidence of a partial walk.");
    }

    [TestMethod]
    public void ALiveProbeMustMatchTheRecordedHardwareIdentity()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED");
        manifest.DeviceStableId = "phone-a";

        var original = new DeviceInfo(DeviceStatus.Ready, "Apple iPhone", "Ready") { StableId = "phone-a" };
        var different = new DeviceInfo(DeviceStatus.Ready, "Apple iPhone", "Ready") { StableId = "phone-b" };
        var unidentified = new DeviceInfo(DeviceStatus.Ready, "Apple iPhone", "Ready");

        Assert.IsTrue(SessionRecovery.MatchesCurrentDevice(manifest, original, out string? problem),
            "The same hardware identity may continue the session.");
        Assert.IsNull(problem);

        Assert.IsFalse(SessionRecovery.MatchesCurrentDevice(manifest, different, out problem));
        StringAssert.Contains(problem, "different hardware identity");
        StringAssert.Contains(problem, "Nothing has been copied");

        Assert.IsFalse(SessionRecovery.MatchesCurrentDevice(manifest, unidentified, out problem));
        StringAssert.Contains(problem, "did not provide a hardware identity");
    }

    [TestMethod]
    public void ALegacyRecordIsMatchedByNameAndCanAdoptTheLiveIdentity()
    {
        SessionManifest manifest = Manifest(@"C:\backup", "CANCELLED");
        var sameName = new DeviceInfo(DeviceStatus.Ready, "Apple iPhone", "Ready") { StableId = "phone-a" };
        var otherName = new DeviceInfo(DeviceStatus.Ready, "HUAWEI Y7 Prime 2018", "Ready") { StableId = "phone-b" };

        Assert.IsTrue(SessionRecovery.MatchesCurrentDevice(manifest, sameName, out string? problem));
        Assert.IsNull(problem);

        Assert.IsFalse(SessionRecovery.MatchesCurrentDevice(manifest, otherName, out problem));
        StringAssert.Contains(problem, "Choose a new destination folder");
    }

    [TestMethod]
    public void TheFingerprintFollowsTheSessionNotTheMachine()
    {
        RecoveryState before = new()
        {
            HasSession = true,
            Destination = @"C:\backup",
            UpdatedUtc = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            Total = 10,
            Pending = 4,
            Status = "CANCELLED"
        };
        RecoveryState progressed = before with { UpdatedUtc = new DateTimeOffset(2026, 8, 1, 11, 0, 0, TimeSpan.Zero), Pending = 2 };
        RecoveryState same = before with { Status = "ABORTED" };

        Assert.AreEqual(before.Fingerprint, same.Fingerprint, "Only the session's own shape decides dismissal, not its wording.");
        Assert.AreNotEqual(before.Fingerprint, progressed.Fingerprint,
            "Progress must bring the offer back: a dismissal may never hide a changed session.");
    }

    [TestMethod]
    public void TheDescriptionNamesWhatIsDoneAndWhatIsNot()
    {
        RecoveryState state = new()
        {
            HasSession = true,
            Destination = @"C:\backup",
            DeviceName = "Apple iPhone",
            Total = 12,
            Verified = 5,
            Pending = 6,
            Failed = 1,
            UpdatedUtc = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)
        };
        string text = state.Describe();
        StringAssert.Contains(text, "5 copied and verified");
        StringAssert.Contains(text, "6 never attempted");
        StringAssert.Contains(text, "1 failed");
        StringAssert.Contains(text, "12 file(s)");
    }

    /// <summary>
    /// The bar's message is a fixed height, so the model has to offer a summary short enough to be read
    /// in full there. It must still separate what is done from what is not, and name the problem counts.
    /// </summary>
    [TestMethod]
    public void TheCompactSummaryFitsABarAndStillSeparatesDoneFromUndone()
    {
        RecoveryState state = new()
        {
            HasSession = true,
            Destination = @"C:\backup",
            Total = 26_335,
            Verified = 1_203,
            Pending = 25_116,
            Failed = 12,
            Inaccessible = 4
        };
        string text = state.Compact();
        StringAssert.Contains(text, "26,335 planned");
        StringAssert.Contains(text, "1,203 copied and verified");
        StringAssert.Contains(text, "25,132 outstanding");
        StringAssert.Contains(text, "(12 failed, 4 unreadable");
        Assert.IsTrue(text.Length < 150, $"The bar cannot read a summary of {text.Length} characters: {text}");

        RecoveryState clean = state with { Failed = 0, Inaccessible = 0, Pending = 25_132 };
        Assert.IsFalse(clean.Compact().Contains('('), "A session with no problems must not carry an empty problem note.");
    }
}
