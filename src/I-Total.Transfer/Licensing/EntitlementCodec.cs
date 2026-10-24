using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITotalTransfer.Licensing;

/// <summary>
/// Reads and verifies the two signed documents the commercial model needs: a purchase entitlement and a
/// revocation list. Both are JSON envelopes carrying a base64 payload and an RSA-PSS/SHA-256 signature
/// over the exact payload bytes.
///
/// <para>
/// The verifier parses nothing from the payload until the signature has passed, so an attacker cannot
/// influence a decision (for example by setting <c>Refunded=false</c> or extending <c>NotAfterUtc</c>) by
/// editing the readable half. There is no private key here: <see cref="Sign"/> exists for the issuing
/// side and tests, and the application never calls it.
/// </para>
/// </summary>
public static class EntitlementCodec
{
    public const string EntitlementFormat = "i-total-entitlement/1";
    public const string RevocationFormat = "i-total-revocations/1";

    /// <summary>Envelopes are small; anything larger is refused before it is decoded.</summary>
    public const int MaxDocumentBytes = 64 * 1024;

    private static readonly JsonSerializerOptions EnvelopeJson = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions PayloadJson = new() { WriteIndented = false };

    internal sealed record Envelope(string Format, string Payload, string Signature);

    internal sealed class RevocationPayload
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset IssuedUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<string> LicenseIds { get; set; } = [];
    }

    /// <summary>Produces an entitlement envelope. Only the issuing backend (or a test) calls this.</summary>
    public static string Sign(EntitlementPayload payload, RSA privateKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(privateKey);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJson);
        byte[] signature = privateKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return JsonSerializer.Serialize(
            new Envelope(EntitlementFormat, Convert.ToBase64String(bytes), Convert.ToBase64String(signature)),
            EnvelopeJson);
    }

    /// <summary>Produces a signed revocation list. Only the issuing backend (or a test) calls this.</summary>
    public static string SignRevocations(IEnumerable<string> licenseIds, RSA privateKey, DateTimeOffset issuedUtc)
    {
        ArgumentNullException.ThrowIfNull(licenseIds);
        ArgumentNullException.ThrowIfNull(privateKey);
        var payload = new RevocationPayload
        {
            IssuedUtc = issuedUtc,
            LicenseIds = licenseIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJson);
        byte[] signature = privateKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return JsonSerializer.Serialize(
            new Envelope(RevocationFormat, Convert.ToBase64String(bytes), Convert.ToBase64String(signature)),
            EnvelopeJson);
    }

    /// <summary>
    /// Verifies one entitlement. A missing public key, a blank document, a bad signature, a wrong product
    /// or channel, validity dates and refund state all produce distinct verdicts rather than an
    /// exception: licence problems must never take the transfer engine down with them.
    /// </summary>
    public static EntitlementCheck Verify(
        string? document,
        RSA? publicKey,
        DateTimeOffset now,
        LicenseChannel expectedChannel,
        string product,
        string? expectedSteamAppId = null)
    {
        if (publicKey is null)
            return EntitlementCheck.Fail(EntitlementVerdict.NotConfigured,
                "This build carries no licence public key, so it cannot verify a purchased entitlement. The trial is unaffected.");
        if (string.IsNullOrWhiteSpace(document))
            return EntitlementCheck.Fail(EntitlementVerdict.Malformed, "The licence file is empty.");

        Envelope? envelope;
        byte[] payloadBytes;
        byte[] signature;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(document.Trim(), EnvelopeJson);
            if (envelope is null || !string.Equals(envelope.Format, EntitlementFormat, StringComparison.Ordinal))
                return EntitlementCheck.Fail(EntitlementVerdict.Malformed,
                    "The file is not an I-Total Transfer entitlement (its format marker is missing or unknown).");
            payloadBytes = Convert.FromBase64String(envelope.Payload);
            signature = Convert.FromBase64String(envelope.Signature);
        }
        catch (Exception)
        {
            return EntitlementCheck.Fail(EntitlementVerdict.Malformed,
                "The licence file could not be read as a signed entitlement.");
        }

        if (payloadBytes.Length > MaxDocumentBytes)
            return EntitlementCheck.Fail(EntitlementVerdict.Malformed, "The licence payload is unreasonably large.");

        bool signed;
        try
        {
            signed = publicKey.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (CryptographicException)
        {
            signed = false;
        }
        if (!signed)
            return EntitlementCheck.Fail(EntitlementVerdict.SignatureInvalid,
                "The licence signature does not match this build's key. The file was altered, or it was issued for a different product key.");

        EntitlementPayload? payload;
        try { payload = JsonSerializer.Deserialize<EntitlementPayload>(payloadBytes, PayloadJson); }
        catch (JsonException) { payload = null; }
        if (payload is null)
            return EntitlementCheck.Fail(EntitlementVerdict.Malformed, "The signed payload is not readable.");

        if (!string.Equals(payload.Product, product, StringComparison.OrdinalIgnoreCase))
            return EntitlementCheck.Fail(EntitlementVerdict.WrongProduct,
                $"The licence was issued for \"{Truncate(payload.Product)}\", not \"{product}\".");

        if (!TryChannel(payload.Channel, out LicenseChannel channel) || channel != expectedChannel)
            return EntitlementCheck.Fail(EntitlementVerdict.WrongChannel,
                expectedChannel == LicenseChannel.Steam
                    ? "This is the Steam edition and only a Steam entitlement can activate it."
                    : "This is the direct edition and only a direct entitlement can activate it.");

        if (channel == LicenseChannel.Steam && !string.IsNullOrWhiteSpace(expectedSteamAppId)
            && !string.Equals(payload.SteamAppId, expectedSteamAppId, StringComparison.Ordinal))
            return EntitlementCheck.Fail(EntitlementVerdict.WrongChannel,
                "The Steam entitlement is bound to a different app id than this build's steam_appid.txt.");

        if (payload.NotBeforeUtc is { } from && now < from)
            return EntitlementCheck.Fail(EntitlementVerdict.NotYetValid,
                $"The licence becomes valid on {from.ToLocalTime():yyyy-MM-dd HH:mm}.");

        if (payload.NotAfterUtc is { } until && now > until)
            return new EntitlementCheck(EntitlementVerdict.Expired, payload,
                $"The licence expired on {until.ToLocalTime():yyyy-MM-dd HH:mm}. The trial rules continue to apply.");

        if (payload.Refunded)
            return new EntitlementCheck(EntitlementVerdict.Refunded, payload,
                "The issuer marked this purchase as refunded, so it no longer grants a licence.");

        if (string.IsNullOrWhiteSpace(payload.LicenseId))
            return EntitlementCheck.Fail(EntitlementVerdict.Malformed, "The signed payload carries no licence id.");

        return new EntitlementCheck(EntitlementVerdict.Valid, payload,
            $"Licence {ShortId(payload.LicenseId)} verified offline with this build's public key.");
    }

    /// <summary>
    /// Verifies a signed revocation list and returns the ids it names. Anything malformed or unsigned
    /// yields an empty set: a broken revocation file can never revoke every licence, and it never throws.
    /// </summary>
    public static IReadOnlySet<string> VerifyRevocations(string? document, RSA? publicKey)
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        if (publicKey is null || string.IsNullOrWhiteSpace(document)) return empty;
        try
        {
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(document.Trim(), EnvelopeJson);
            if (envelope is null || !string.Equals(envelope.Format, RevocationFormat, StringComparison.Ordinal))
                return empty;
            byte[] payloadBytes = Convert.FromBase64String(envelope.Payload);
            byte[] signature = Convert.FromBase64String(envelope.Signature);
            if (payloadBytes.Length > MaxDocumentBytes) return empty;
            if (!publicKey.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return empty;
            RevocationPayload? payload = JsonSerializer.Deserialize<RevocationPayload>(payloadBytes, PayloadJson);
            if (payload?.LicenseIds is null) return empty;
            return new HashSet<string>(payload.LicenseIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return empty;
        }
    }

    private static bool TryChannel(string? value, out LicenseChannel channel)
    {
        if (string.Equals(value, "direct", StringComparison.OrdinalIgnoreCase))
        {
            channel = LicenseChannel.Direct;
            return true;
        }
        if (string.Equals(value, "steam", StringComparison.OrdinalIgnoreCase))
        {
            channel = LicenseChannel.Steam;
            return true;
        }
        channel = LicenseChannel.None;
        return false;
    }

    private static string Truncate(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value.Length <= 40 ? value : value[..40] + "…";

    /// <summary>A short, screenshot-safe form of a licence id.</summary>
    public static string ShortId(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value.Length <= 8 ? value : value[..8] + "…";

    /// <summary>UTF-8 bytes of a document, used by the file store and by tests.</summary>
    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
