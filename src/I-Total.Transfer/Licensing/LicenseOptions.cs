using System.Security.Cryptography;

namespace ITotalTransfer.Licensing;

/// <summary>
/// Where the accepted entitlement lives. A copy is kept under <c>%APPDATA%</c> so the purchased file can be
/// moved, renamed or deleted from the download folder without losing the licence.
/// </summary>
public interface ILicenseFileStore
{
    string Location { get; }
    string RevocationLocation { get; }
    string? Read();
    string? ReadRevocations();
    void Write(string document);
    void Delete();
    void WriteRevocations(string document);
}

/// <summary>File-based entitlement store, atomic on save and tolerant of a missing or unreadable file.</summary>
public sealed class FileLicenseStore : ILicenseFileStore
{
    public string Location { get; }
    public string RevocationLocation { get; }

    public FileLicenseStore(string? directory = null)
    {
        string root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "I-Total Transfer");
        Location = Path.Combine(root, "licence.lic");
        RevocationLocation = Path.Combine(root, "revocations.lic");
    }

    public string? Read() => ReadOrNull(Location);
    public string? ReadRevocations() => ReadOrNull(RevocationLocation);

    public void Write(string document) => WriteAtomic(Location, document);
    public void WriteRevocations(string document) => WriteAtomic(RevocationLocation, document);

    public void Delete()
    {
        try { if (File.Exists(Location)) File.Delete(Location); }
        catch (Exception) { /* Left for the operator to remove. */ }
    }

    private static string? ReadOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (Exception) { return null; }
    }

    private static void WriteAtomic(string path, string document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, document);
        if (!File.Exists(path)) { File.Move(temporary, path); return; }
        for (int attempt = 1; ; attempt++)
        {
            try { File.Replace(temporary, path, null, true); return; }
            catch (IOException) when (attempt < 3) { Thread.Sleep(50 * attempt); }
        }
    }
}

/// <summary>In-memory entitlement store used by tests and by a build that cannot write to disk.</summary>
public sealed class MemoryLicenseStore : ILicenseFileStore
{
    public string Location => "memory://licence";
    public string RevocationLocation => "memory://revocations";
    public string? Document { get; private set; }
    public string? Revocations { get; private set; }
    public bool FailOnWrite { get; set; }

    public string? Read() => Document;
    public string? ReadRevocations() => Revocations;

    public void Write(string document)
    {
        if (FailOnWrite) throw new IOException("The licence store is read-only for this test.");
        Document = document;
    }

    public void Delete() => Document = null;

    public void WriteRevocations(string document)
    {
        if (FailOnWrite) throw new IOException("The licence store is read-only for this test.");
        Revocations = document;
    }
}

/// <summary>
/// Build-time commercial settings. None of these values is secret: the private signing key belongs to the
/// issuing backend and is never part of the desktop application.
/// </summary>
public sealed class LicenseOptions
{
    public const int DefaultTrialJobs = 3;

    /// <summary>How many meaningful jobs the free trial allows. Set at build time (<c>TrialJobLimit</c>).</summary>
    public int TrialJobs { get; init; } = DefaultTrialJobs;

    public string ProductName { get; init; } = "I-Total Transfer";

    /// <summary>Which channel this build accepts. A Steam build refuses a direct entitlement and vice versa.</summary>
    public LicenseChannel Channel { get; init; } = LicenseChannel.Direct;

    public string? SteamAppId { get; init; }

    /// <summary>Directory holding <c>steam_appid.txt</c> and the Steam entitlement, when this is a Steam build.</summary>
    public string? SteamDirectory { get; init; }

    /// <summary>The RSA public key (PEM) embedded at build time, or null when the build carries none.</summary>
    public string? PublicKeyPem { get; init; }

    /// <summary>Purchase page opened by the Buy button. Empty means "not configured in this build".</summary>
    public string PurchaseUrl { get; init; } = "";

    /// <summary>True when this executable ships inside the Steam edition.</summary>
    public bool SteamEditionDetected { get; init; }

    /// <summary>Reads the settings embedded in this build. Called once, at startup.</summary>
    public static LicenseOptions ForThisBuild()
    {
        bool steam = SteamEdition.IsSteamDistribution();
        return new LicenseOptions
        {
            TrialJobs = ReadTrialLimit(),
            Channel = steam ? LicenseChannel.Steam : LicenseChannel.Direct,
            SteamEditionDetected = steam,
            SteamAppId = steam ? SteamEdition.AppId() : null,
            PublicKeyPem = ReadEmbeddedPublicKey(),
            PurchaseUrl = ReadMetadata("PurchaseUrl")
        };
    }

    /// <summary>
    /// Creates the verifier from the configured public key. Returns null when the build has no key, which
    /// makes direct activation fail closed with <see cref="EntitlementVerdict.NotConfigured"/> rather than
    /// accepting an unsigned file.
    /// </summary>
    public RSA? CreateVerifier()
    {
        if (string.IsNullOrWhiteSpace(PublicKeyPem)) return null;
        try
        {
            RSA rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            return rsa;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int ReadTrialLimit()
    {
        string? raw = ReadMetadata("TrialJobLimit");
        return int.TryParse(raw, out int value) ? Math.Clamp(value, 0, 1000) : DefaultTrialJobs;
    }

    private static string ReadEmbeddedPublicKey()
    {
        try
        {
            using Stream? stream = typeof(LicenseOptions).Assembly
                .GetManifestResourceStream("ITotalTransfer.Licensing.public-key.pem");
            if (stream is null) return "";
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string ReadMetadata(string key) =>
        typeof(LicenseOptions).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal))?.Value ?? "";
}

/// <summary>How the Steam edition is recognised and where its signed entitlement lives.</summary>
public static class SteamEdition
{
    public const string AppIdFile = "steam_appid.txt";
    public const string EntitlementFile = "steam_entitlement.lic";

    public static string InstallDirectory => AppContext.BaseDirectory;

    /// <summary>A Steam install ships <c>steam_appid.txt</c> beside the executable.</summary>
    public static bool IsSteamDistribution(string? directory = null) =>
        File.Exists(Path.Combine(directory ?? InstallDirectory, AppIdFile));

    /// <summary>The app id declared by the Steam install, or null when there is none or it is unreadable.</summary>
    public static string? AppId(string? directory = null)
    {
        try
        {
            string path = Path.Combine(directory ?? InstallDirectory, AppIdFile);
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>The outcome of asking the Steam edition for its entitlement.</summary>
public sealed record SteamCheck(bool Owned, string Detail, EntitlementCheck? Entitlement);

/// <summary>
/// Verifies the signed entitlement shipped with the Steam edition, bound to the app id in
/// <c>steam_appid.txt</c>. The entitlement is issued by the same backend as a direct purchase, so the two
/// channels share one verifier and one revocation mechanism.
///
/// <para>
/// <b>Not implemented here:</b> live Steamworks ownership calls. That needs the Steamworks SDK and a
/// Steamworks partner account, neither of which is in this workspace. This provider therefore proves a
/// signed entitlement, not a live Steam client check; see <c>docs/KNOWN_ISSUES.md</c>.
/// </para>
/// </summary>
public sealed class SteamEntitlementProvider
{
    private readonly string _directory;
    private readonly LicenseOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    public SteamEntitlementProvider(string? directory, LicenseOptions options, Func<DateTimeOffset>? clock = null)
    {
        _directory = directory ?? SteamEdition.InstallDirectory;
        _options = options;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public SteamCheck Check()
    {
        if (!SteamEdition.IsSteamDistribution(_directory))
            return new SteamCheck(false,
                "This is not the Steam edition: no steam_appid.txt was found beside the executable.", null);

        string appId = SteamEdition.AppId(_directory) ?? "";
        string path = Path.Combine(_directory, SteamEdition.EntitlementFile);
        if (!File.Exists(path))
            return new SteamCheck(false,
                $"The Steam edition was detected (app {appId}), but no Steam entitlement is present at {path}.",
                null);

        string document;
        try { document = File.ReadAllText(path); }
        catch (Exception ex)
        {
            return new SteamCheck(false, "The Steam entitlement could not be read: " + ex.Message, null);
        }

        EntitlementCheck check = EntitlementCodec.Verify(
            document, _options.CreateVerifier(), _clock(), LicenseChannel.Steam, _options.ProductName, appId);
        return new SteamCheck(check.IsValid, check.Detail, check);
    }
}
