using System.Security.Cryptography;
using ITotalTransfer.Licensing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The signed-document rules on their own: envelope format, signature coverage, validity dates and the
/// revocation list. Licence parsing runs on user-supplied files, so every malformed input must become a
/// verdict rather than an exception.
/// </summary>
[TestClass]
public sealed class EntitlementCodecTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static EntitlementPayload Payload(DateTimeOffset? notAfter = null, DateTimeOffset? notBefore = null) => new()
    {
        Product = "I-Total Transfer",
        Channel = "direct",
        LicenseId = "LIC-ROUNDTRIP-1",
        IssuedUtc = Now.AddDays(-1),
        NotAfterUtc = notAfter,
        NotBeforeUtc = notBefore
    };

    [TestMethod]
    public void ASignedPayloadRoundTripsAndTheSignatureCoversTheReadableHalf()
    {
        using RSA key = RSA.Create(2048);
        string document = EntitlementCodec.Sign(Payload(), key);

        EntitlementCheck check = EntitlementCodec.Verify(document, key, Now, LicenseChannel.Direct, "I-Total Transfer");
        Assert.AreEqual(EntitlementVerdict.Valid, check.Verdict, check.Detail);
        Assert.AreEqual("LIC-ROUNDTRIP-1", check.Payload!.LicenseId);

        // Swap the payload for a differently-worded grant: the signature no longer matches the bytes, so it
        // must be refused even though every field looks plausible.
        using RSA attacker = RSA.Create(2048);
        EntitlementPayload attackerPayload = Payload();
        attackerPayload.LicenseId = "ATTACKER-1";
        string otherDocument = EntitlementCodec.Sign(attackerPayload, attacker);
        string otherPayload = Extract(otherDocument, "Payload");
        string edited = Replace(document, "Payload", otherPayload);
        Assert.AreNotEqual(document, edited, "The test must actually change the payload.");
        Assert.AreEqual(EntitlementVerdict.SignatureInvalid,
            EntitlementCodec.Verify(edited, key, Now, LicenseChannel.Direct, "I-Total Transfer").Verdict);
    }

    [TestMethod]
    public void ValidityDatesAreHonouredIncludingBothBoundaries()
    {
        using RSA key = RSA.Create(2048);

        // Exactly at NotBefore and exactly at NotAfter are both still valid; one tick outside is not.
        EntitlementCheck atStart = EntitlementCodec.Verify(
            EntitlementCodec.Sign(Payload(notBefore: Now), key), key, Now, LicenseChannel.Direct, "I-Total Transfer");
        Assert.AreEqual(EntitlementVerdict.Valid, atStart.Verdict);

        EntitlementCheck atEnd = EntitlementCodec.Verify(
            EntitlementCodec.Sign(Payload(notAfter: Now), key), key, Now, LicenseChannel.Direct, "I-Total Transfer");
        Assert.AreEqual(EntitlementVerdict.Valid, atEnd.Verdict);

        EntitlementCheck justExpired = EntitlementCodec.Verify(
            EntitlementCodec.Sign(Payload(notAfter: Now.AddTicks(-1)), key), key, Now,
            LicenseChannel.Direct, "I-Total Transfer");
        Assert.AreEqual(EntitlementVerdict.Expired, justExpired.Verdict);

        EntitlementCheck justEarly = EntitlementCodec.Verify(
            EntitlementCodec.Sign(Payload(notBefore: Now.AddTicks(1)), key), key, Now,
            LicenseChannel.Direct, "I-Total Transfer");
        Assert.AreEqual(EntitlementVerdict.NotYetValid, justEarly.Verdict);
    }

    [TestMethod]
    public void EveryMalformedInputIsAVerdictRatherThanAnException()
    {
        using RSA key = RSA.Create(2048);
        string[] rubbish =
        [
            "", "   ", "not json", "{}", "[]", "null",
            "{ \"Format\": \"i-total-entitlement/1\", \"Payload\": \"!!!\", \"Signature\": \"AAAA\" }",
            "{ \"Format\": \"something-else/9\", \"Payload\": \"e30=\", \"Signature\": \"AAAA\" }",
            "{ \"Format\": \"i-total-entitlement/1\", \"Payload\": \"\", \"Signature\": \"\" }",
            new string('x', EntitlementCodec.MaxDocumentBytes + 1)
        ];

        foreach (string text in rubbish)
        {
            EntitlementCheck check = EntitlementCodec.Verify(text, key, Now, LicenseChannel.Direct, "I-Total Transfer");
            Assert.AreNotEqual(EntitlementVerdict.Valid, check.Verdict, "Accepted rubbish: " + text);
        }
    }

    [TestMethod]
    public void ARevocationListOnlyCountsWhenTheIssuerSignedIt()
    {
        using RSA issuer = RSA.Create(2048);
        using RSA attacker = RSA.Create(2048);
        string signed = EntitlementCodec.SignRevocations(["LIC-A", "LIC-B"], issuer, Now);

        IReadOnlySet<string> accepted = EntitlementCodec.VerifyRevocations(signed, issuer);
        CollectionAssert.AreEquivalent(new[] { "LIC-A", "LIC-B" }, accepted.ToArray());

        Assert.AreEqual(0, EntitlementCodec.VerifyRevocations(signed, attacker).Count);
        Assert.AreEqual(0, EntitlementCodec.VerifyRevocations("{}", issuer).Count);
        Assert.AreEqual(0, EntitlementCodec.VerifyRevocations(null, issuer).Count);

        // Editing the payload of a genuine list invalidates its signature.
        string forged = Replace(signed, "Payload",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"LicenseIds\":[\"LIC-A\"]}")));
        Assert.AreEqual(0, EntitlementCodec.VerifyRevocations(forged, issuer).Count);
    }

    [TestMethod]
    public void AShortIdIsSafeToShowInAScreenshot()
    {
        Assert.AreEqual("(none)", EntitlementCodec.ShortId(""));
        Assert.AreEqual("ABC", EntitlementCodec.ShortId("ABC"));
        Assert.AreEqual("ABCDEFGH…", EntitlementCodec.ShortId("ABCDEFGHIJKLMNOP"));
    }

    private static string Extract(string document, string property)
    {
        using var json = System.Text.Json.JsonDocument.Parse(document);
        return json.RootElement.GetProperty(property).GetString() ?? "";
    }

    private static string Replace(string document, string property, string value)
    {
        using var json = System.Text.Json.JsonDocument.Parse(document);
        string oldValue = json.RootElement.GetProperty(property).GetString() ?? "";
        return document.Replace("\"" + property + "\": \"" + oldValue + "\"", "\"" + property + "\": \"" + value + "\"");
    }
}
