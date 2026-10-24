using System.Security.Cryptography;
using ITotalTransfer.Licensing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Steam edition's entitlement path. This proves the signed file and the app-id binding; it does not
/// prove a live Steamworks ownership call, which needs the SDK and a partner account (see KNOWN_ISSUES).
/// </summary>
[TestClass]
public sealed class SteamEntitlementTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-steam", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static EntitlementPayload SteamPayload(string appId) => new()
    {
        Product = "I-Total Transfer",
        Channel = "steam",
        LicenseId = "STEAM-0001",
        IssuedUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        SteamAppId = appId,
        SteamId = "76561198000000000"
    };

    private static LicenseOptions Options(string directory, string publicKey, string appId = "1234560") => new()
    {
        TrialJobs = 3,
        ProductName = "I-Total Transfer",
        Channel = LicenseChannel.Steam,
        SteamEditionDetected = true,
        SteamDirectory = directory,
        SteamAppId = appId,
        PublicKeyPem = publicKey
    };

    [TestMethod]
    public void ADirectoryWithoutSteamAppIdIsNotTheSteamEdition()
    {
        string dir = TempDir();
        try
        {
            using RSA key = RSA.Create(2048);
            var provider = new SteamEntitlementProvider(dir, Options(dir, key.ExportSubjectPublicKeyInfoPem()));

            SteamCheck check = provider.Check();
            Assert.IsFalse(check.Owned);
            StringAssert.Contains(check.Detail, "not the Steam edition");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ASteamInstallWithoutAnEntitlementSaysExactlyWhatIsMissing()
    {
        string dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, SteamEdition.AppIdFile), "1234560");
            using RSA key = RSA.Create(2048);
            var provider = new SteamEntitlementProvider(dir, Options(dir, key.ExportSubjectPublicKeyInfoPem()));

            SteamCheck check = provider.Check();
            Assert.IsFalse(check.Owned);
            StringAssert.Contains(check.Detail, "1234560");
            StringAssert.Contains(check.Detail, SteamEdition.EntitlementFile);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ASignedSteamEntitlementForThisAppIdLicensesTheEdition()
    {
        string dir = TempDir();
        try
        {
            using RSA key = RSA.Create(2048);
            File.WriteAllText(Path.Combine(dir, SteamEdition.AppIdFile), "1234560");
            File.WriteAllText(Path.Combine(dir, SteamEdition.EntitlementFile),
                EntitlementCodec.Sign(SteamPayload("1234560"), key));

            var options = Options(dir, key.ExportSubjectPublicKeyInfoPem());
            var provider = new SteamEntitlementProvider(dir, options);
            Assert.IsTrue(provider.Check().Owned, provider.Check().Detail);

            using var service = new LicenseService(options, new MemoryTrialStore(), new MemoryLicenseStore());
            LicenseSnapshot snapshot = service.Snapshot();
            Assert.AreEqual(LicenseStanding.Licensed, snapshot.Standing, snapshot.Detail);
            Assert.AreEqual(LicenseChannel.Steam, snapshot.Channel);
            Assert.AreEqual("Licensed — Steam", snapshot.Headline);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ASteamEntitlementForAnotherAppIdOrADirectLicenceIsRefused()
    {
        string dir = TempDir();
        try
        {
            using RSA key = RSA.Create(2048);
            File.WriteAllText(Path.Combine(dir, SteamEdition.AppIdFile), "1234560");
            var options = Options(dir, key.ExportSubjectPublicKeyInfoPem());

            File.WriteAllText(Path.Combine(dir, SteamEdition.EntitlementFile),
                EntitlementCodec.Sign(SteamPayload("9999999"), key));
            Assert.IsFalse(new SteamEntitlementProvider(dir, options).Check().Owned);

            EntitlementPayload direct = SteamPayload("1234560");
            direct.Channel = "direct";
            File.WriteAllText(Path.Combine(dir, SteamEdition.EntitlementFile),
                EntitlementCodec.Sign(direct, key));
            Assert.IsFalse(new SteamEntitlementProvider(dir, options).Check().Owned);

            using var service = new LicenseService(options, new MemoryTrialStore(), new MemoryLicenseStore());
            Assert.AreEqual(LicenseStanding.Trial, service.Snapshot().Standing,
                "A refused Steam entitlement must leave the trial running, never crash or half-activate.");
        }
        finally { Directory.Delete(dir, true); }
    }
}
