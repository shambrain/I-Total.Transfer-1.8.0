using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Server-side certificate selection is an exact identity check, not a substring search. These tests
/// build real certificates with SAN/EKU/key-usage extensions and exercise the selector directly.
///
/// <para>
/// The last test is deliberately about the client side: a self-signed certificate that the receiver
/// may select is still rejected by a default TLS client until its chain is trusted. That is the same
/// boundary Safari and Chrome apply, and it is why the phones need the certificate or its CA installed.
/// </para>
/// </summary>
[TestClass]
public sealed class ServerCertificateValidatorTests
{
    private const string ServerAuth = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuth = "1.3.6.1.5.5.7.3.2";

    private static readonly List<RSA> Keys = [];

    private static X509Certificate2 Make(
        string[]? dns = null,
        string[]? ips = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        string? eku = ServerAuth,
        X509KeyUsageFlags? keyUsage = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
        bool withPrivateKey = true,
        string subject = "CN=receiver.test")
    {
        RSA rsa = RSA.Create(2048);
        // Keep the key alive for the test process: Schannel cannot use an ephemeral key, and a
        // self-signed certificate's private key is backed by this RSA instance.
        Keys.Add(rsa);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        foreach (string name in dns ?? []) san.AddDnsName(name);
        foreach (string ip in ips ?? []) san.AddIpAddress(IPAddress.Parse(ip));
        if ((dns?.Length ?? 0) + (ips?.Length ?? 0) > 0) request.CertificateExtensions.Add(san.Build());
        if (eku is not null)
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(eku) }, false));
        if (keyUsage is not null)
            request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage.Value, false));

        X509Certificate2 created = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(30));
        if (!withPrivateKey)
        {
            using (created)
                return X509CertificateLoader.LoadCertificate(created.Export(X509ContentType.Cert));
        }

        // Schannel refuses ephemeral CNG keys. Import the self-signed key into the user profile; without
        // PersistKeySet the key is removed when the certificate is disposed.
        byte[] pfx = created.Export(X509ContentType.Pfx, "itotal-test");
        created.Dispose();
        return X509CertificateLoader.LoadPkcs12(pfx, "itotal-test",
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private static bool Usable(X509Certificate2 certificate, string address) =>
        ServerCertificateValidator.IsUsableForServer(certificate, address, DateTimeOffset.UtcNow);

    [TestMethod]
    public void AnExactIpv4SanIsAcceptedAndSimilarAddressesAreNot()
    {
        using X509Certificate2 certificate = Make(ips: ["192.168.1.5"]);

        Assert.IsTrue(Usable(certificate, "192.168.1.5"));
        Assert.IsFalse(Usable(certificate, "192.168.1.50"));
        Assert.IsFalse(Usable(certificate, "192.168.1.5.1"));
        Assert.IsFalse(Usable(certificate, "10.0.0.5"));
    }

    [TestMethod]
    public void AnExactIpv6SanIsAcceptedAndSimilarAddressesAreNot()
    {
        using X509Certificate2 certificate = Make(ips: ["2001:db8::1"]);

        Assert.IsTrue(Usable(certificate, "2001:db8::1"));
        Assert.IsFalse(Usable(certificate, "2001:db8::2"));
        Assert.IsFalse(Usable(certificate, "2001:db8::10"));
    }

    [TestMethod]
    public void AnExactDnsSanIsAcceptedAndSubstringsAreNot()
    {
        using X509Certificate2 certificate = Make(dns: ["phone.local"]);

        Assert.IsTrue(Usable(certificate, "phone.local"));
        Assert.IsTrue(Usable(certificate, "PHONE.LOCAL"));
        Assert.IsTrue(Usable(certificate, "phone.local."));
        Assert.IsFalse(Usable(certificate, "notphone.local"));
        Assert.IsFalse(Usable(certificate, "phone.local.evil"));
        Assert.IsFalse(Usable(certificate, "phone"));
    }

    [TestMethod]
    public void ASubjectCnAloneIsNotANameMatch()
    {
        using X509Certificate2 certificate = Make(subject: "CN=192.168.1.5", ips: null, dns: null);

        Assert.IsFalse(ServerCertificateValidator.NamesAddress(certificate, "192.168.1.5"));
        Assert.IsFalse(Usable(certificate, "192.168.1.5"));
    }

    [TestMethod]
    public void ALeftmostWildcardMatchesExactlyOneLabel()
    {
        Assert.IsTrue(ServerCertificateValidator.DnsNameMatches("*.example.com", "a.example.com"));
        Assert.IsFalse(ServerCertificateValidator.DnsNameMatches("*.example.com", "example.com"));
        Assert.IsFalse(ServerCertificateValidator.DnsNameMatches("*.example.com", "a.b.example.com"));
        Assert.IsFalse(ServerCertificateValidator.DnsNameMatches("a*.example.com", "ab.example.com"));
        Assert.IsFalse(ServerCertificateValidator.DnsNameMatches("*.com", "example.com"));
        Assert.IsFalse(ServerCertificateValidator.DnsNameMatches("*", "example.com"));
    }

    [TestMethod]
    public void AWildcardSanIsAcceptedForOneLabelOnly()
    {
        using X509Certificate2 certificate = Make(dns: ["*.example.com"]);

        Assert.IsTrue(Usable(certificate, "phone.example.com"));
        Assert.IsFalse(Usable(certificate, "example.com"));
        Assert.IsFalse(Usable(certificate, "phone.eu.example.com"));
    }

    [TestMethod]
    public void ACertificateWithAClientAuthEkuIsRefused()
    {
        using X509Certificate2 certificate = Make(ips: ["192.168.1.5"], eku: ClientAuth);

        Assert.IsTrue(ServerCertificateValidator.NamesAddress(certificate, "192.168.1.5"));
        Assert.IsFalse(Usable(certificate, "192.168.1.5"), "a client-auth-only certificate is not a server certificate");
    }

    [TestMethod]
    public void ACertificateWithoutEkuIsAccepted()
    {
        using X509Certificate2 certificate = Make(ips: ["192.168.1.5"], eku: null);
        Assert.IsTrue(Usable(certificate, "192.168.1.5"));
    }

    [TestMethod]
    public void KeyUsageMustPermitTlsServerUse()
    {
        using X509Certificate2 agreementOnly = Make(ips: ["192.168.1.5"], keyUsage: X509KeyUsageFlags.KeyAgreement);
        using X509Certificate2 signatureOnly = Make(ips: ["192.168.1.5"], keyUsage: X509KeyUsageFlags.DigitalSignature);
        using X509Certificate2 encipherOnly = Make(ips: ["192.168.1.5"], keyUsage: X509KeyUsageFlags.KeyEncipherment);

        Assert.IsFalse(Usable(agreementOnly, "192.168.1.5"));
        Assert.IsTrue(Usable(signatureOnly, "192.168.1.5"));
        Assert.IsTrue(Usable(encipherOnly, "192.168.1.5"));
    }

    [TestMethod]
    public void AnExpiredCertificateIsRefused()
    {
        using X509Certificate2 certificate = Make(
            ips: ["192.168.1.5"],
            notBefore: DateTimeOffset.UtcNow.AddDays(-30),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        Assert.IsFalse(Usable(certificate, "192.168.1.5"));
    }

    [TestMethod]
    public void ANonYetValidCertificateIsRefused()
    {
        using X509Certificate2 certificate = Make(
            ips: ["192.168.1.5"],
            notBefore: DateTimeOffset.UtcNow.AddDays(1),
            notAfter: DateTimeOffset.UtcNow.AddDays(30));

        Assert.IsFalse(Usable(certificate, "192.168.1.5"));
    }

    [TestMethod]
    public void ACertificateWithoutAPrivateKeyIsRefused()
    {
        using X509Certificate2 certificate = Make(ips: ["192.168.1.5"], withPrivateKey: false);

        Assert.IsFalse(certificate.HasPrivateKey);
        Assert.IsFalse(Usable(certificate, "192.168.1.5"));
    }

    [TestMethod]
    public void ACertificateWithoutASanIsRefused()
    {
        using X509Certificate2 certificate = Make(dns: null, ips: null);
        Assert.IsFalse(Usable(certificate, "localhost"));
        Assert.IsFalse(Usable(certificate, "127.0.0.1"));
    }

    [TestMethod]
    public async Task ADefaultClientRejectsASelfSignedServerCertificateUntilItIsTrusted()
    {
        using X509Certificate2 certificate = Make(dns: ["localhost"], ips: ["127.0.0.1"]);

        Assert.IsFalse(await HandshakeAsync(certificate, trustAny: false),
            "a default client must reject a self-signed certificate it does not trust");
        Assert.IsTrue(await HandshakeAsync(certificate, trustAny: true),
            "the same certificate must work once the client trusts it");
    }

    /// <summary>
    /// Runs one real TLS handshake. The client uses the platform chain policy by default, which is the
    /// same trust decision a browser makes; the permissive callback is the "certificate installed on the
    /// phone" case. This is a local .NET client, not Safari or Chrome.
    /// </summary>
    private static async Task<bool> HandshakeAsync(X509Certificate2 certificate, bool trustAny)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Exception? serverError = null;
        Task server = Task.Run(async () =>
        {
            try
            {
                using TcpClient accepted = await listener.AcceptTcpClientAsync();
                using var ssl = new SslStream(accepted.GetStream(), leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(certificate);
            }
            catch (Exception ex) { serverError = ex; }
        });

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var ssl = trustAny
                ? new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
                    (_, _, _, _) => true)
                : new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            try
            {
                await ssl.AuthenticateAsClientAsync("localhost");
                return true;
            }
            catch (Exception ex) when (trustAny) { throw new AssertFailedException("trusted handshake failed: " + ex); }
            catch (AuthenticationException) { return false; }
            catch (IOException) { return false; } // the server aborted after the client rejected the chain
        }
        finally
        {
            listener.Stop();
            await server;
            if (trustAny && serverError is not null)
                throw new AssertFailedException("server handshake failed: " + serverError);
        }
    }
}
