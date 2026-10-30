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
    public async Task ADefaultClientRejectsACertificateFromAnUntrustedRootAndAcceptsItWithCustomRootTrust()
    {
        (X509Certificate2 root, X509Certificate2 leaf) = MakeChain(dns: ["localhost"], ips: ["127.0.0.1"]);
        using (root)
        using (leaf)
        {
            Assert.IsFalse(await HandshakeAsync(leaf, chainPolicy: null),
                "a default client must reject a server certificate whose root it does not trust");

            var policy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag
            };
            policy.CustomTrustStore.Add(root);
            Assert.IsTrue(await HandshakeAsync(leaf, policy),
                "the same certificate must pass normal chain validation when its root is trusted");
        }
    }

    /// <summary>
    /// Runs one real TLS handshake. The client uses the platform chain policy by default; the second
    /// call supplies an explicit <see cref="X509ChainPolicy"/> with the test root in
    /// <see cref="X509ChainPolicy.CustomTrustStore"/>, so normal chain building, validity, EKU and
    /// hostname checks still run. This is a local .NET client, not Safari, Chrome or an Android phone:
    /// it proves the chain-trust distinction, not any device's trust store.
    /// </summary>
    private static async Task<bool> HandshakeAsync(X509Certificate2 certificate, X509ChainPolicy? chainPolicy)
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
            using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            try
            {
                var options = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    CertificateChainPolicy = chainPolicy
                };
                await ssl.AuthenticateAsClientAsync(options);
                return true;
            }
            catch (AuthenticationException) { return false; }
            catch (IOException) { return false; } // the server aborted after the client rejected the chain
        }
        finally
        {
            listener.Stop();
            await server;
            if (chainPolicy is not null && serverError is not null)
                throw new AssertFailedException("server handshake failed: " + serverError);
        }
    }

    /// <summary>
    /// Creates a private test root and a server leaf signed by it. The leaf carries its private key for
    /// Schannel; disposing the certificates removes the temporary key material.
    /// </summary>
    private static (X509Certificate2 Root, X509Certificate2 Leaf) MakeChain(string[] dns, string[] ips)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=I-Total Test Root " + Guid.NewGuid().ToString("N"), rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        using X509Certificate2 rootWithKey = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));
        X509Certificate2 root = X509CertificateLoader.LoadCertificate(rootWithKey.Export(X509ContentType.Cert));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        foreach (string name in dns) san.AddDnsName(name);
        foreach (string ip in ips) san.AddIpAddress(IPAddress.Parse(ip));
        leafRequest.CertificateExtensions.Add(san.Build());
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid(ServerAuth) }, false));
        leafRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafRequest.PublicKey, false));

        using X509Certificate2 leafPublic = leafRequest.Create(
            rootWithKey, now.AddDays(-1), now.AddDays(30), RandomNumberGenerator.GetBytes(8));
        using X509Certificate2 leafWithKey = leafPublic.CopyWithPrivateKey(leafKey);
        byte[] pfx = leafWithKey.Export(X509ContentType.Pfx, "itotal-test");
        X509Certificate2 leaf = X509CertificateLoader.LoadPkcs12(pfx, "itotal-test",
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        return (root, leaf);
    }
}
