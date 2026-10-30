using System.Net;
using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The wireless credential model: a single-use pairing secret, session tokens minted from it, expiry,
/// rotation and revocation. Pure state, so every rule is tested without a socket.
///
/// <para>
/// These tests exist because the first implementation used one token for the ticket and for every
/// upload, forever until it expired. A secret shown in a URL and a QR code must not also be a
/// long-lived write credential.
/// </para>
/// </summary>
[TestClass]
public sealed class PairingStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void RedeemingTheSecretMintsASessionTokenThatIsNotTheSecret()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));

        PairingDecision decision = state.Redeem(secret, "192.168.1.9", Now);

        Assert.AreEqual(PairingOutcome.Paired, decision.Outcome);
        Assert.IsTrue(decision.IsAccepted);
        Assert.IsNotNull(decision.SessionToken);
        Assert.AreNotEqual(secret, decision.SessionToken, "The write credential must not be the displayed secret.");
        Assert.AreEqual(32, decision.SessionToken!.Length);
    }

    [TestMethod]
    public void TheSecretIsSingleUse()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        Assert.AreEqual(PairingOutcome.Paired, state.Redeem(secret, "a", Now).Outcome);

        PairingDecision second = state.Redeem(secret, "b", Now.AddSeconds(1));

        Assert.AreEqual(PairingOutcome.AlreadyUsed, second.Outcome);
        Assert.IsFalse(second.IsAccepted);
        Assert.IsNull(second.SessionToken, "A spent secret must not mint another session.");
    }

    [TestMethod]
    public void AnExpiredSecretIsRefused()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));

        PairingDecision decision = state.Redeem(secret, "a", Now.AddMinutes(6));

        Assert.AreEqual(PairingOutcome.Invalid, decision.Outcome);
        StringAssert.Contains(decision.Detail, "expired");
    }

    [TestMethod]
    public void ASessionTokenIsAcceptedUntilItExpires()
    {
        var state = new PairingState { SessionLifetime = TimeSpan.FromMinutes(30) };
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        string session = state.Redeem(secret, "a", Now).SessionToken!;

        Assert.AreEqual(PairingOutcome.Accepted, state.Authorize(session, "a", Now.AddMinutes(29)).Outcome);
        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(session, "a", Now.AddMinutes(31)).Outcome);
    }

    [TestMethod]
    public void ASecretCanNeverBeUsedAsASessionToken()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));

        // Even before redemption, presenting the secret as a session credential must fail.
        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(secret, "a", Now).Outcome);
        Assert.IsFalse(state.SecretWasUsed, "A failed authorize must not spend the secret.");
        Assert.AreEqual(PairingOutcome.Paired, state.Redeem(secret, "a", Now).Outcome);
    }

    [TestMethod]
    public void RotationReplacesTheTokenAndInvalidatesTheOldOne()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        string session = state.Redeem(secret, "a", Now).SessionToken!;

        string? rotated = state.Rotate(session, Now.AddMinutes(10));

        Assert.IsNotNull(rotated);
        Assert.AreNotEqual(session, rotated);
        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(session, "a", Now.AddMinutes(11)).Outcome,
            "The replaced token must stop working immediately.");
        Assert.AreEqual(PairingOutcome.Accepted, state.Authorize(rotated!, "a", Now.AddMinutes(11)).Outcome);
    }

    [TestMethod]
    public void RotationExtendsTheSessionWithoutWideningTheLifetime()
    {
        var state = new PairingState { SessionLifetime = TimeSpan.FromMinutes(10) };
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        string session = state.Redeem(secret, "a", Now).SessionToken!;

        string rotated = state.Rotate(session, Now.AddMinutes(9))!;

        Assert.AreEqual(PairingOutcome.Accepted, state.Authorize(rotated, "a", Now.AddMinutes(18)).Outcome);
        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(rotated, "a", Now.AddMinutes(20)).Outcome);
    }

    [TestMethod]
    public void RevokeAllInvalidatesEverySessionAndTheSecret()
    {
        var state = new PairingState();
        string secret = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        string session = state.Redeem(secret, "a", Now).SessionToken!;
        state.CreateSecret(Now, TimeSpan.FromMinutes(5));

        state.RevokeAll();

        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(session, "a", Now.AddMinutes(1)).Outcome);
        Assert.AreEqual(0, state.LiveSessionCount(Now.AddMinutes(1)));
        Assert.IsFalse(state.HasLiveSecret(Now.AddMinutes(1)));
    }

    [TestMethod]
    public void RevokingOneSessionLeavesTheOthersAlive()
    {
        var state = new PairingState();
        string first = state.Redeem(state.CreateSecret(Now, TimeSpan.FromMinutes(5)), "a", Now).SessionToken!;
        string second = state.Redeem(state.CreateSecret(Now, TimeSpan.FromMinutes(5)), "b", Now).SessionToken!;

        Assert.IsTrue(state.Revoke(first));

        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize(first, "a", Now.AddMinutes(1)).Outcome);
        Assert.AreEqual(PairingOutcome.Accepted, state.Authorize(second, "b", Now.AddMinutes(1)).Outcome);
        Assert.IsFalse(state.Revoke(first), "Revoking twice reports that nothing changed.");
    }

    [TestMethod]
    public void IssuingANewSecretInvalidatesTheOldOne()
    {
        var state = new PairingState();
        string first = state.CreateSecret(Now, TimeSpan.FromMinutes(5));
        string second = state.CreateSecret(Now.AddSeconds(1), TimeSpan.FromMinutes(5));

        Assert.AreEqual(PairingOutcome.Invalid, state.Redeem(first, "a", Now.AddSeconds(2)).Outcome);
        Assert.AreEqual(PairingOutcome.Paired, state.Redeem(second, "a", Now.AddSeconds(2)).Outcome);
    }

    [TestMethod]
    public void SessionsAreBoundedSoALeakedSecretCannotOpenUnlimitedClients()
    {
        var state = new PairingState();
        for (int i = 0; i < PairingState.MaxSessions + 3; i++)
            state.Redeem(state.CreateSecret(Now, TimeSpan.FromMinutes(5)), "client" + i, Now);

        Assert.AreEqual(PairingState.MaxSessions, state.LiveSessionCount(Now));
    }

    [TestMethod]
    public void MissingAndUnknownCredentialsAreReportedDistinctly()
    {
        var state = new PairingState();

        Assert.AreEqual(PairingOutcome.Missing, state.Authorize(null, "a", Now).Outcome);
        Assert.AreEqual(PairingOutcome.Missing, state.Authorize("   ", "a", Now).Outcome);
        Assert.AreEqual(PairingOutcome.Invalid, state.Authorize("not-a-token", "a", Now).Outcome);
    }

    [TestMethod]
    public void TokensAreRandomAndLowercaseHex()
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 200; i++)
        {
            string token = PairingState.NewToken();
            Assert.AreEqual(32, token.Length);
            Assert.AreEqual(token.ToLowerInvariant(), token);
            Assert.IsTrue(token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
            Assert.IsTrue(tokens.Add(token), "Tokens must not repeat.");
        }
    }
}

/// <summary>The address rules the receiver enforces before it binds a socket.</summary>
[TestClass]
public sealed class PrivateNetworkTests
{
    [TestMethod]
    [DataRow("10.0.0.1")]
    [DataRow("10.255.255.254")]
    [DataRow("172.16.0.1")]
    [DataRow("172.31.255.254")]
    [DataRow("192.168.0.1")]
    [DataRow("192.168.1.24")]
    [DataRow("127.0.0.1")]
    public void PrivateAndLoopbackAddressesAreAllowed(string address) =>
        Assert.IsTrue(PrivateNetwork.IsAllowedBindAddress(address), address + " should be allowed.");

    [TestMethod]
    [DataRow("172.15.0.1")]
    [DataRow("172.32.0.1")]
    [DataRow("8.8.8.8")]
    [DataRow("100.64.0.1")]
    [DataRow("169.254.10.10")]
    [DataRow("0.0.0.0")]
    [DataRow("*")]
    [DataRow("+")]
    [DataRow("")]
    public void PublicWildcardAndCarrierGradeAddressesAreRefused(string address) =>
        Assert.IsFalse(PrivateNetwork.IsAllowedBindAddress(address), address + " should be refused.");

    [TestMethod]
    public void TheWildcardIsRefusedWithAnExplanationRatherThanABlankError()
    {
        string? reason = PrivateNetwork.RejectionReason("0.0.0.0");

        Assert.IsNotNull(reason);
        StringAssert.Contains(reason, "every interface");
    }

    [TestMethod]
    public void APublicAddressIsNamedInTheRefusal()
    {
        string? reason = PrivateNetwork.RejectionReason("203.0.113.9");

        Assert.IsNotNull(reason);
        StringAssert.Contains(reason, "203.0.113.9");
        StringAssert.Contains(reason, "not a private address");
    }

    [TestMethod]
    public void AnAllowedAddressHasNoRejectionReason()
    {
        Assert.IsNull(PrivateNetwork.RejectionReason("192.168.1.24"));
        Assert.IsNull(PrivateNetwork.RejectionReason("localhost"));
    }

    [TestMethod]
    public void LocalhostIsClassifiedAsLoopbackNotPrivate()
    {
        Assert.IsTrue(PrivateNetwork.IsAllowedBindAddress("localhost"));
        Assert.IsFalse(PrivateNetwork.IsPrivate(IPAddress.Parse("127.0.0.1")));
        Assert.IsTrue(PrivateNetwork.IsLoopback(IPAddress.Parse("127.0.0.1")));
    }

    [TestMethod]
    public void TheMachineKnowsItsOwnLoopbackAddress()
    {
        Assert.IsTrue(PrivateNetwork.IsAssignedToThisMachine("127.0.0.1"));
        Assert.IsFalse(PrivateNetwork.IsAssignedToThisMachine("203.0.113.9"));
    }
}

/// <summary>The approval gate between a paired sender and the operator's disk.</summary>
[TestClass]
public sealed class ApprovalInboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static IncomingOffer Offer(string path, long bytes = 1024) =>
        new(path, bytes, null, "192.168.1.9", Now);

    [TestMethod]
    public void AnOfferWaitsForTheOperatorByDefault()
    {
        var inbox = new ApprovalInbox();

        OfferRecord record = inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        Assert.AreEqual(OfferState.Pending, record.State);
        Assert.AreEqual(1, inbox.Pending.Count);
        Assert.IsFalse(inbox.IsApproved("DCIM/IMG_1.HEIC"), "Nothing may be committed before approval.");
    }

    [TestMethod]
    public void ApprovingMakesTheOfferCommittable()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        OfferRecord? record = inbox.Approve("DCIM/IMG_1.HEIC", Now.AddSeconds(5));

        Assert.IsNotNull(record);
        Assert.AreEqual(OfferState.Approved, record!.State);
        Assert.IsTrue(inbox.IsApproved("DCIM/IMG_1.HEIC"));
        Assert.AreEqual(0, inbox.Pending.Count);
    }

    [TestMethod]
    public void ARefusedPathStaysRefusedForTheSession()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);
        inbox.Refuse("DCIM/IMG_1.HEIC", Now.AddSeconds(1));

        OfferRecord retry = inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now.AddSeconds(2));

        Assert.AreEqual(OfferState.Refused, retry.State,
            "A sender must not out-wait a refusal by retrying the same path.");
        Assert.IsFalse(inbox.IsApproved("DCIM/IMG_1.HEIC"));
    }

    [TestMethod]
    public void AutoApproveIsAnExplicitPreferenceNotASilentDefault()
    {
        var inbox = new ApprovalInbox { AutoApprove = true };

        OfferRecord record = inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        Assert.AreEqual(OfferState.Approved, record.State);
        StringAssert.Contains(record.Reason, "preference");
        Assert.AreEqual(0, inbox.Pending.Count);
    }

    [TestMethod]
    public void PendingOffersExpire()
    {
        var inbox = new ApprovalInbox { OfferLifetime = TimeSpan.FromMinutes(5) };
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        int expired = inbox.Expire(Now.AddMinutes(6));

        Assert.AreEqual(1, expired);
        Assert.AreEqual(OfferState.Expired, inbox.Offers[0].State);
        Assert.IsFalse(inbox.IsApproved("DCIM/IMG_1.HEIC"));
    }

    [TestMethod]
    public void ApproveAllAndRefuseAllCoverEveryPendingOffer()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/A.HEIC"), Now);
        inbox.Offer(Offer("DCIM/B.HEIC"), Now);

        Assert.AreEqual(2, inbox.ApproveAll(Now));
        Assert.IsTrue(inbox.IsApproved("DCIM/A.HEIC"));
        Assert.IsTrue(inbox.IsApproved("DCIM/B.HEIC"));

        var second = new ApprovalInbox();
        second.Offer(Offer("DCIM/C.HEIC"), Now);
        second.Offer(Offer("DCIM/D.HEIC"), Now);
        Assert.AreEqual(2, second.RefuseAll(Now));
        Assert.AreEqual(0, second.Pending.Count);
        Assert.IsFalse(second.IsApproved("DCIM/C.HEIC"));
    }

    [TestMethod]
    public void PathMatchingIsCaseInsensitiveBecauseWindowsIs()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        inbox.Approve("dcim/img_1.heic", Now);

        Assert.IsTrue(inbox.IsApproved("DCIM/IMG_1.HEIC"));
    }

    [TestMethod]
    public void DecidingAnUnknownPathChangesNothing()
    {
        var inbox = new ApprovalInbox();

        Assert.IsNull(inbox.Approve("DCIM/absent.HEIC", Now));
        Assert.IsNull(inbox.Refuse("DCIM/absent.HEIC", Now));
        Assert.AreEqual(0, inbox.Offers.Count);
    }

    [TestMethod]
    public void CancellingAPendingOfferRemovesItFromThePendingList()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);

        OfferRecord? cancelled = inbox.Cancel("DCIM/IMG_1.HEIC", Now.AddSeconds(1), "sender disconnected");

        Assert.IsNotNull(cancelled);
        Assert.AreEqual(OfferState.Cancelled, cancelled!.State);
        Assert.AreEqual(0, inbox.Pending.Count);
        Assert.IsFalse(inbox.IsApproved("DCIM/IMG_1.HEIC"));
    }

    [TestMethod]
    public void ALateApprovalCannotReviveACancelledOffer()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);
        inbox.Cancel("DCIM/IMG_1.HEIC", Now.AddSeconds(1), "sender disconnected");

        OfferRecord? late = inbox.Approve("DCIM/IMG_1.HEIC", Now.AddSeconds(2));

        Assert.IsNotNull(late);
        Assert.AreEqual(OfferState.Cancelled, late!.State);
        Assert.IsFalse(inbox.IsApproved("DCIM/IMG_1.HEIC"));
    }

    [TestMethod]
    public void ACancelledPathCanBeOfferedAgain()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);
        inbox.Cancel("DCIM/IMG_1.HEIC", Now.AddSeconds(1), "sender disconnected");

        OfferRecord retry = inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now.AddSeconds(2));

        Assert.AreEqual(OfferState.Pending, retry.State,
            "a cancellation is not a refusal; the sender may retry the same path");
        Assert.AreEqual(1, inbox.Pending.Count);
    }

    [TestMethod]
    public void CancellingAnUnknownOrDecidedOfferChangesNothing()
    {
        var inbox = new ApprovalInbox();
        Assert.IsNull(inbox.Cancel("DCIM/absent.HEIC", Now, "sender disconnected"));

        inbox.Offer(Offer("DCIM/IMG_1.HEIC"), Now);
        inbox.Approve("DCIM/IMG_1.HEIC", Now.AddSeconds(1));
        OfferRecord? afterApproval = inbox.Cancel("DCIM/IMG_1.HEIC", Now.AddSeconds(2), "sender disconnected");

        Assert.IsNotNull(afterApproval);
        Assert.AreEqual(OfferState.Approved, afterApproval!.State,
            "a decision that already committed must not be rewritten as cancelled");
    }

    [TestMethod]
    public void ClearingForgetsEveryDecision()
    {
        var inbox = new ApprovalInbox();
        inbox.Offer(Offer("DCIM/A.HEIC"), Now);
        inbox.Refuse("DCIM/A.HEIC", Now);

        inbox.Clear();

        Assert.AreEqual(0, inbox.Offers.Count);
        Assert.AreEqual(OfferState.Pending, inbox.Offer(Offer("DCIM/A.HEIC"), Now).State,
            "A stopped receiver must not remember a refusal forever.");
    }
}
