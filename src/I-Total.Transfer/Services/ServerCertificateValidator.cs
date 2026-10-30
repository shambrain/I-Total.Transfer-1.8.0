using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ITotalTransfer.Services;

/// <summary>
/// Decides whether a certificate may be used as the receiver's TLS server certificate for one bind
/// address. This is deliberately a structured check, not a substring search over the subject or the
/// formatted SAN text: an address must match an actual SAN entry exactly (or a single-label wildcard
/// DNS pattern), and the certificate must be currently valid, carry a private key, and be suitable for
/// TLS server authentication.
///
/// <para>
/// This class only decides <em>server-side selection</em>. Whether a phone trusts the certificate chain
/// is a separate question answered by the phone's trust store; a self-signed certificate selected here
/// is still rejected by Safari or Chrome until its CA (or the certificate itself) is trusted on that
/// device.
/// </para>
/// </summary>
public static class ServerCertificateValidator
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private static readonly Asn1Tag DnsNameTag = new(TagClass.ContextSpecific, 2);
    private static readonly Asn1Tag IpAddressTag = new(TagClass.ContextSpecific, 7);

    /// <summary>
    /// True when the certificate is currently valid, has a private key, is suitable for TLS server
    /// authentication (when EKU/key-usage are constrained), and names the address in a SAN entry.
    /// </summary>
    public static bool IsUsableForServer(X509Certificate2 certificate, string address, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (string.IsNullOrWhiteSpace(address)) return false;
        if (!certificate.HasPrivateKey) return false;

        DateTimeOffset notBefore = certificate.NotBefore.ToUniversalTime();
        DateTimeOffset notAfter = certificate.NotAfter.ToUniversalTime();
        if (now < notBefore || now > notAfter) return false;

        if (!HasServerAuthenticationEku(certificate)) return false;
        if (!HasSuitableKeyUsage(certificate)) return false;
        return NamesAddress(certificate, address);
    }

    /// <summary>
    /// Exact identity check. An IP bind address requires an exact IP SAN; a DNS bind address requires an
    /// exact DNS SAN or a leftmost single-label wildcard. The subject CN is not used: when a SAN exists
    /// it is authoritative, and a CN-only certificate is refused rather than guessed at.
    /// </summary>
    public static bool NamesAddress(X509Certificate2 certificate, string address)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (string.IsNullOrWhiteSpace(address)) return false;

        if (IPAddress.TryParse(address, out IPAddress? ip))
            return GetIpAddresses(certificate).Any(candidate => candidate.Equals(ip));

        return GetDnsNames(certificate).Any(name => DnsNameMatches(name, address));
    }

    /// <summary>Every IP address in the certificate's SAN extension.</summary>
    public static IReadOnlyList<IPAddress> GetIpAddresses(X509Certificate2 certificate) =>
        ReadNames(certificate).Where(name => name.Ip is not null).Select(name => name.Ip!).ToArray();

    /// <summary>Every DNS name in the certificate's SAN extension.</summary>
    public static IReadOnlyList<string> GetDnsNames(X509Certificate2 certificate) =>
        ReadNames(certificate).Where(name => name.Dns is not null).Select(name => name.Dns!).ToArray();

    /// <summary>
    /// RFC 6125-style hostname matching for the subset a local receiver needs: exact match or a
    /// leftmost <c>*.</c> wildcard that covers exactly one label. A wildcard does not match the apex and
    /// does not span a dot.
    /// </summary>
    public static bool DnsNameMatches(string pattern, string host)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(host)) return false;
        string expected = pattern.TrimEnd('.').ToLowerInvariant();
        string actual = host.TrimEnd('.').ToLowerInvariant();
        if (expected.Length == 0 || actual.Length == 0) return false;

        int star = expected.IndexOf('*');
        if (star < 0) return string.Equals(expected, actual, StringComparison.Ordinal);

        // Only a single leftmost "*." label is accepted; embedded or partial wildcards are refused.
        if (star != 0 || expected.Length < 3 || expected[1] != '.') return false;
        string suffix = expected[2..];
        if (suffix.Length == 0 || suffix.Contains('*') || !suffix.Contains('.')) return false;

        int dot = actual.IndexOf('.');
        if (dot <= 0) return false;
        // The wildcard replaces exactly one label: "a.example.com" matches "*.example.com",
        // "a.b.example.com" and the apex "example.com" do not.
        return string.Equals(actual[(dot + 1)..], suffix, StringComparison.Ordinal);
    }

    private static bool HasServerAuthenticationEku(X509Certificate2 certificate)
    {
        X509EnhancedKeyUsageExtension? eku = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();
        if (eku is null) return true; // unconstrained
        foreach (Oid usage in eku.EnhancedKeyUsages)
            if (string.Equals(usage.Value, ServerAuthenticationOid, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool HasSuitableKeyUsage(X509Certificate2 certificate)
    {
        X509KeyUsageExtension? usage = certificate.Extensions
            .OfType<X509KeyUsageExtension>()
            .FirstOrDefault();
        if (usage is null) return true; // unconstrained
        const X509KeyUsageFlags required = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
        return (usage.KeyUsages & required) != 0;
    }

    private readonly record struct GeneralName(string? Dns, IPAddress? Ip);

    private static List<GeneralName> ReadNames(X509Certificate2 certificate)
    {
        var names = new List<GeneralName>();
        X509Extension? extension = certificate.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(candidate => candidate.Oid?.Value == SubjectAlternativeNameOid);
        if (extension is null) return names;

        try
        {
            var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
            AsnReader sequence = reader.ReadSequence();
            while (sequence.HasData)
            {
                Asn1Tag tag = sequence.PeekTag();
                if (tag.TagClass != TagClass.ContextSpecific)
                {
                    sequence.ReadEncodedValue();
                    continue;
                }

                if (tag.TagValue == DnsNameTag.TagValue)
                {
                    string dns = sequence.ReadCharacterString(UniversalTagNumber.IA5String, DnsNameTag);
                    if (!string.IsNullOrWhiteSpace(dns)) names.Add(new GeneralName(dns, null));
                }
                else if (tag.TagValue == IpAddressTag.TagValue)
                {
                    byte[] bytes = sequence.ReadOctetString(IpAddressTag);
                    if (bytes.Length is 4 or 16) names.Add(new GeneralName(null, new IPAddress(bytes)));
                }
                else
                {
                    sequence.ReadEncodedValue();
                }
            }
        }
        catch (AsnContentException) { }
        catch (CryptographicException) { }
        return names;
    }
}
