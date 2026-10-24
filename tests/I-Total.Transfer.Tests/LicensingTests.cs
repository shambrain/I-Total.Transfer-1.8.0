using System.Security.Cryptography;
using ITotalTransfer.Licensing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The commercial contract, tested without a store or a payment provider.
///
/// <para>
/// The rules the product promises are narrow and testable: the trial is measured in meaningful completed
/// jobs, every kind of unsuccessful run is free, a repeated report is free, the counter survives a restart
/// and the loss of one of its two stores, a licence is accepted only if its signature verifies, and no
/// licence problem can stop an unfinished session from being continued.
/// </para>
/// </summary>
[TestClass]
public sealed class LicensingTests
{
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Read() => Now;
    }

    private static LicenseService Service(
        ITrialStore trial,
        ILicenseFileStore? files = null,
        string? publicKeyPem = null,
        int limit = 3,
        Func<DateTimeOffset>? clock = null,
        SteamEntitlementProvider? steam = null)
        => new(new LicenseOptions { TrialJobs = limit, PublicKeyPem = publicKeyPem }, trial, files ?? new MemoryLicenseStore(),
            clock, steam);

    private static JobFacts Job(
        string id, bool completed = true, int copied = 1, long bytes = 4096, int failed = 0, bool cancelled = false) =>
        new(id, LicenseJobKind.DeviceToWindows, completed, copied, bytes, failed, cancelled);

    private static EntitlementPayload Payload(string id = "LIC-2026-0001", string product = "I-Total Transfer",
        string channel = "direct", DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null,
        bool refunded = false, string? appId = null) => new()
        {
            Product = product,
            Channel = channel,
            LicenseId = id,
            IssuedUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            NotBeforeUtc = notBefore,
            NotAfterUtc = notAfter,
            Refunded = refunded,
            SteamAppId = appId
        };

    [TestMethod]
    public void ATrialStartsWithThreeJobsAndEveryKindOfUnsuccessfulRunIsFree()
    {
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial);

        LicenseSnapshot start = service.Snapshot();
        Assert.AreEqual(LicenseStanding.Trial, start.Standing);
        Assert.AreEqual(3, start.JobLimit);
        Assert.AreEqual(3, start.JobsRemaining);

        // A failure, a cancellation, an empty plan and a run where everything was already present are all
        // free. Charging for them would make the trial limit a limit on attempts, which it is not.
        Assert.IsFalse(service.RecordJob(Job("failed", completed: false, copied: 0, bytes: 0, failed: 1)).Counted);
        Assert.IsFalse(service.RecordJob(Job("cancelled", copied: 4, cancelled: true)).Counted);
        Assert.IsFalse(service.RecordJob(Job("empty", copied: 0, bytes: 0)).Counted);
        Assert.IsFalse(service.RecordJob(Job("already-present", copied: 0, bytes: 0)).Counted);
        Assert.IsFalse(service.RecordJob(Job("partial", completed: false, copied: 3, failed: 1)).Counted);

        LicenseSnapshot after = service.Snapshot();
        Assert.AreEqual(0, after.JobsUsed, "None of those runs delivered a completed transfer.");
        Assert.AreEqual(3, after.JobsRemaining);
    }

    [TestMethod]
    public void ThreeSuccessfulJobsExhaustTheTrialAndOnlyAResumeIsThenAllowed()
    {
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial);

        for (int i = 1; i <= 3; i++)
        {
            JobRecordResult result = service.RecordJob(Job("job-" + i));
            Assert.IsTrue(result.Counted, "Job " + i + " should have used a trial job.");
            Assert.AreEqual(3 - i, result.Snapshot.JobsRemaining);
        }

        Assert.AreEqual(LicenseStanding.TrialExhausted, service.Snapshot().Standing);
        LicenseDecision fresh = service.TryStartJob(resuming: false);
        Assert.IsFalse(fresh.Allowed);
        StringAssert.Contains(fresh.Message, "Activate a licence");

        // An unfinished session is work in progress, not a new job. Licence state must never strand it.
        LicenseDecision resume = service.TryStartJob(resuming: true);
        Assert.IsTrue(resume.Allowed);

        // The fourth completion adds no further use and does not throw.
        JobRecordResult extra = service.RecordJob(Job("job-4"));
        Assert.IsFalse(extra.Counted);
        Assert.AreEqual(3, extra.Snapshot.JobsUsed);
    }

    [TestMethod]
    public void ReportingTheSameFinishedJobTwiceCostsOneTrialJob()
    {
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial);

        JobFacts job = Job("same-run-id");
        Assert.IsTrue(service.RecordJob(job).Counted);
        JobRecordResult second = service.RecordJob(job);

        Assert.IsFalse(second.Counted);
        Assert.IsTrue(second.AlreadyRecorded);
        Assert.AreEqual(1, second.Snapshot.JobsUsed);
    }

    [TestMethod]
    public void TheCounterSurvivesARestartAndTheLossOfOneOfItsTwoStores()
    {
        var first = new MemoryTrialStore();
        var second = new MemoryTrialStore();
        var stores = new CompositeTrialStore(first, second);

        using (LicenseService before = Service(stores))
        {
            Assert.IsTrue(before.RecordJob(Job("a")).Counted);
            Assert.IsTrue(before.RecordJob(Job("b")).Counted);
        }

        using (LicenseService after = Service(new CompositeTrialStore(first, second)))
            Assert.AreEqual(2, after.Snapshot().JobsUsed, "A restart must not reset the counter.");

        // Deleting the file store is the obvious way to try for a fresh trial; the registry copy must win.
        using (LicenseService afterDeletingOne = Service(new CompositeTrialStore(new MemoryTrialStore(), second)))
        {
            Assert.AreEqual(2, afterDeletingOne.Snapshot().JobsUsed);
            Assert.AreEqual(1, afterDeletingOne.Snapshot().JobsRemaining);
        }
    }

    [TestMethod]
    public void AReadOnlyLedgerStillLetsTheJobFinishAndSaysTheCountIsOnlyInMemory()
    {
        var trial = new MemoryTrialStore { FailOnSave = true };
        using LicenseService service = Service(trial);

        JobRecordResult result = service.RecordJob(Job("read-only-store"));

        Assert.IsTrue(result.Counted, "A store failure must not turn a successful transfer into a licence failure.");
        Assert.AreEqual(1, result.Snapshot.JobsUsed);
        StringAssert.Contains(result.Snapshot.Detail, "could not be written");
    }

    [TestMethod]
    public void MovingTheClockBackCannotResetTheCounter()
    {
        var trial = new MemoryTrialStore();
        var clock = new Clock();
        using (LicenseService first = Service(trial, clock: clock.Read))
            Assert.IsTrue(first.RecordJob(Job("before-rollback")).Counted);

        clock.Now = clock.Now.AddDays(-30);
        using LicenseService second = Service(trial, clock: clock.Read);
        LicenseSnapshot snapshot = second.Snapshot();

        Assert.AreEqual(1, snapshot.JobsUsed, "A clock change must not look like a new machine.");
        StringAssert.Contains(snapshot.Detail, "system clock");
    }

    [TestMethod]
    public void AnEntitlementSignedByTheIssuerLiftsTheLimitImmediately()
    {
        using RSA key = RSA.Create(2048);
        string publicKey = key.ExportSubjectPublicKeyInfoPem();
        var trial = new MemoryTrialStore();
        var files = new MemoryLicenseStore();
        using LicenseService service = Service(trial, files, publicKey);

        for (int i = 0; i < 3; i++) service.RecordJob(Job("trial-" + i));
        Assert.IsFalse(service.TryStartJob(resuming: false).Allowed);

        string document = EntitlementCodec.Sign(Payload(), key);
        LicenseActivationResult result = service.ActivateText(document, "test");

        Assert.IsTrue(result.Accepted, result.Message);
        Assert.AreEqual(LicenseStanding.Licensed, result.Snapshot.Standing);
        Assert.AreEqual(LicenseChannel.Direct, result.Snapshot.Channel);
        Assert.IsTrue(service.TryStartJob(resuming: false).Allowed);
        Assert.AreEqual("LIC-2026…", result.Snapshot.LicenseIdShort);

        // A licensed job is not trial-metered, and the accepted file is stored for the next start.
        Assert.IsFalse(service.RecordJob(Job("licensed-job")).Counted);
        Assert.IsNotNull(files.Document);

        using LicenseService restarted = Service(trial, files, publicKey);
        Assert.AreEqual(LicenseStanding.Licensed, restarted.Snapshot().Standing);
    }

    [TestMethod]
    public void ATamperedEntitlementIsRejectedAndLeavesTheTrialExactlyAsItWas()
    {
        using RSA key = RSA.Create(2048);
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial, publicKeyPem: key.ExportSubjectPublicKeyInfoPem());

        service.RecordJob(Job("one"));
        int usedBefore = service.Snapshot().JobsUsed;

        string document = EntitlementCodec.Sign(Payload(), key);
        // Flip one character of the signature: the readable payload stays valid-looking, which is exactly
        // the attack the signature exists to stop.
        int index = document.IndexOf("\"Signature\": \"", StringComparison.Ordinal) + 15;
        char replacement = document[index] == 'A' ? 'B' : 'A';
        string tampered = document[..index] + replacement + document[(index + 1)..];

        LicenseActivationResult result = service.ActivateText(tampered, "test");

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(EntitlementVerdict.SignatureInvalid, result.Verdict);
        Assert.AreEqual(LicenseStanding.Trial, result.Snapshot.Standing);
        Assert.AreEqual(usedBefore, result.Snapshot.JobsUsed);
    }

    [TestMethod]
    public void AnEntitlementForAnotherProductOtherChannelOrAnotherIssuerKeyIsRejected()
    {
        using RSA issuer = RSA.Create(2048);
        using RSA otherIssuer = RSA.Create(2048);
        string issuerPublic = issuer.ExportSubjectPublicKeyInfoPem();

        using (LicenseService service = Service(new MemoryTrialStore(), publicKeyPem: issuerPublic))
        {
            Assert.AreEqual(EntitlementVerdict.WrongProduct,
                service.ActivateText(EntitlementCodec.Sign(Payload(product: "Some Other App"), issuer), "test").Verdict);
            Assert.AreEqual(EntitlementVerdict.WrongChannel,
                service.ActivateText(EntitlementCodec.Sign(Payload(channel: "steam"), issuer), "test").Verdict);
            Assert.AreEqual(EntitlementVerdict.SignatureInvalid,
                service.ActivateText(EntitlementCodec.Sign(Payload(), otherIssuer), "test").Verdict);
        }
    }

    [TestMethod]
    public void AnExpiredEntitlementNamesItsDateAndLeavesTheTrialRulesInForce()
    {
        using RSA key = RSA.Create(2048);
        var clock = new Clock();
        using LicenseService service = Service(new MemoryTrialStore(), publicKeyPem: key.ExportSubjectPublicKeyInfoPem(),
            clock: clock.Read);

        string expired = EntitlementCodec.Sign(Payload(notAfter: clock.Now.AddDays(-1)), key);
        LicenseActivationResult result = service.ActivateText(expired, "test");

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(EntitlementVerdict.Expired, result.Verdict);
        Assert.AreEqual(LicenseStanding.Trial, result.Snapshot.Standing);
        StringAssert.Contains(result.Message, "expired");

        // A not-yet-valid licence is refused too, rather than activating early.
        string future = EntitlementCodec.Sign(Payload(notBefore: clock.Now.AddDays(1)), key);
        Assert.AreEqual(EntitlementVerdict.NotYetValid, service.ActivateText(future, "test").Verdict);
    }

    [TestMethod]
    public void ARefundedEntitlementIsRefused()
    {
        using RSA key = RSA.Create(2048);
        using LicenseService service = Service(new MemoryTrialStore(), publicKeyPem: key.ExportSubjectPublicKeyInfoPem());

        LicenseActivationResult result = service.ActivateText(
            EntitlementCodec.Sign(Payload(refunded: true), key), "test");

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(EntitlementVerdict.Refunded, result.Verdict);
        Assert.AreEqual(LicenseStanding.Trial, result.Snapshot.Standing);
    }

    [TestMethod]
    public void ASignedRevocationListRevokesAnAcceptedLicenceAndAnUnsignedOneChangesNothing()
    {
        using RSA key = RSA.Create(2048);
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial, publicKeyPem: key.ExportSubjectPublicKeyInfoPem());
        for (int i = 0; i < 3; i++) service.RecordJob(Job("trial-" + i));

        Assert.IsTrue(service.ActivateText(EntitlementCodec.Sign(Payload("LIC-REVOKE-1"), key), "test").Accepted);
        Assert.AreEqual(LicenseStanding.Licensed, service.Snapshot().Standing);

        // An unsigned "revocation" naming every licence must be ignored, not obeyed.
        const string forged = "{ \"Format\": \"i-total-revocations/1\", \"Payload\": \"e30=\", \"Signature\": \"AAAA\" }";
        Assert.AreEqual(0, service.ApplyRevocations(forged));
        Assert.AreEqual(LicenseStanding.Licensed, service.Snapshot().Standing);

        string revocations = EntitlementCodec.SignRevocations(["LIC-REVOKE-1"], key, service.Snapshot().CheckedUtc ?? DateTimeOffset.UtcNow);
        Assert.AreEqual(1, service.ApplyRevocations(revocations));

        LicenseSnapshot after = service.Snapshot();
        Assert.AreEqual(LicenseStanding.TrialExhausted, after.Standing);
        StringAssert.Contains(after.Detail, "revoked");
        Assert.IsFalse(service.TryStartJob(resuming: false).Allowed);
    }

    [TestMethod]
    public void ABuildWithoutAPublicKeyFailsClosedAndSaysWhy()
    {
        using LicenseService service = Service(new MemoryTrialStore());

        Assert.IsFalse(service.CanVerifyEntitlements);
        LicenseActivationResult result = service.ActivateText("{}", "test");

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(EntitlementVerdict.NotConfigured, result.Verdict);
        Assert.AreEqual(LicenseStanding.Trial, result.Snapshot.Standing);
        StringAssert.Contains(result.Message, "public key");
    }

    [TestMethod]
    public void OneWirelessSenderSessionCostsOneJobNoMatterHowManyFilesItSends()
    {
        var trial = new MemoryTrialStore();
        using LicenseService service = Service(trial, limit: 1);

        Assert.IsTrue(service.TryStartWirelessUpload("session-token-a").Allowed);
        JobRecordResult first = service.RecordWirelessReceipt("session-token-a", "DCIM/a.heic", 1000);
        Assert.IsTrue(first.Counted);
        Assert.AreEqual(LicenseStanding.TrialExhausted, first.Snapshot.Standing);

        // The same session may keep sending: it has already paid for itself.
        Assert.IsTrue(service.TryStartWirelessUpload("session-token-a").Allowed);
        JobRecordResult second = service.RecordWirelessReceipt("session-token-a", "DCIM/b.heic", 2000);
        Assert.IsFalse(second.Counted);
        Assert.IsTrue(second.AlreadyRecorded);

        // A different sender needs the trial to have a job left.
        Assert.IsFalse(service.TryStartWirelessUpload("session-token-b").Allowed);
        Assert.AreEqual(1, service.Snapshot().JobsUsed);
    }

    [TestMethod]
    public void TheShippedBuildDefaultsToThreeTrialJobs()
    {
        Assert.AreEqual(LicenseOptions.DefaultTrialJobs, LicenseOptions.ForThisBuild().TrialJobs);
    }
}
