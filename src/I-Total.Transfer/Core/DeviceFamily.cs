namespace ITotalTransfer.Core;

/// <summary>
/// Which family of device a directly-attached portable device is, as far as the media transport can
/// tell.
///
/// <para>
/// This decides <em>policy</em>, never a claim about the device. An iPhone and an Android phone reach
/// Windows through the same transport (MTP, surfaced as Windows Portable Devices) but expose
/// fundamentally different storage, and walking one as if it were the other is exactly how a backup
/// ends up quietly incomplete: an Android phone has a <c>DCIM</c> folder too, so an iPhone-shaped walk
/// of an Android phone would find the camera roll, report a complete scan, and leave Music, Movies,
/// Downloads, Documents and every other user folder behind without saying so.
/// </para>
/// </summary>
public enum DeviceFamily
{
    /// <summary>
    /// Nothing identified it as a phone this app supports. Deliberately kept distinct from the two
    /// known families so the interface can say <em>what</em> was found instead of pretending nothing
    /// was attached.
    /// </summary>
    Unknown = 0,

    /// <summary>An iPhone or iPad: one camera-roll container, read-only through Apple's driver.</summary>
    Apple = 1,

    /// <summary>An Android phone or tablet: whole storage volumes of user-visible files.</summary>
    Android = 2
}

/// <summary>
/// The identity of one Windows Portable Device, as far as the transport can read it.
///
/// <para>
/// <c>WPD_DEVICE_TYPE</c> is deliberately <em>not</em> part of this, and that is a researched
/// decision rather than an omission. Microsoft's own reference for the type says applications
/// "may use these values to determine the generic visual appearance of the device" and then states
/// outright: "WPD applications must use the capabilities of the portable device to determine
/// functionally, not the WPD_DEVICE_TYPE value." Classification here decides storage policy, which is
/// a functional question, so it uses what the device calls itself and then what its storage actually
/// looks like (see <see cref="PortableStorage"/>) rather than a field documented as being about
/// appearance.
/// </para>
///
/// <para>
/// What is used instead is what the device calls itself — friendly name, manufacturer and model — and
/// then, above all, what its storage actually looks like (see <see cref="PortableStorage"/>).
/// </para>
/// </summary>
public sealed record PortableDeviceIdentity(string? FriendlyName, string? Manufacturer, string? Model);

/// <summary>
/// The family decided for one device together with the reason it was decided, so a log line or a
/// support bundle can be checked against the machine rather than trusted.
/// </summary>
public sealed record DeviceFamilyVerdict(DeviceFamily Family, string Basis);

/// <summary>
/// Classifies an attached portable device into the family whose storage rules apply to it.
///
/// <para>
/// Both directions of a mistake are not equally bad, and the rules are ordered accordingly. Calling an
/// Android device Apple makes the scan <em>under</em>-scope the phone while still reporting a complete
/// scan, which is the failure this exists to prevent; calling an Apple device Android merely makes it
/// look in more places than a camera roll. So the Apple match is strict (the device must actually say
/// iPhone, iPad, iPod or Apple) and the Android match, being the safe direction, is allowed to be
/// generous.
/// </para>
/// </summary>
public static class DeviceFamilies
{
    /// <summary>The tokens Apple puts in the names its own devices report.</summary>
    private static readonly string[] AppleTokens = ["iPhone", "iPad", "iPod"];

    /// <summary>
    /// Manufacturers, and the device names they give their phones, matched against one whole word of
    /// the reported name, model or manufacturer rather than as a substring.
    ///
    /// <para>
    /// Word matching, not substring matching, is what keeps a device from matching on a longer word
    /// that merely contains a maker's name — and a few of these makers (Samsung, Sony, Sharp, TCL,
    /// Asus, Lenovo, LG) build things that are not phones at all. That is tolerable here precisely
    /// because the Android policy over-scans: a misidentified camera would be read as generic MTP
    /// storage, which exposes the same files, rather than as a camera roll, which would hide them.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> AndroidMakers = new(StringComparer.OrdinalIgnoreCase)
    {
        // Device names: a portable device called one of these is that maker's Android hardware, and in
        // practice the name is often the only identity a phone hands over.
        "ANDROID", "PIXEL", "GALAXY", "NEXUS",
        "GOOGLE", "SAMSUNG", "XIAOMI", "REDMI", "POCO", "ONEPLUS", "OPPO",
        "REALME", "VIVO", "IQOO", "MOTOROLA", "LENOVO", "NOKIA", "HMD", "SONY", "HTC", "LG",
        "ASUS", "ASUSTEK", "ZTE", "HONOR", "HUAWEI", "TCL", "ALCATEL", "NOTHING", "FAIRPHONE",
        "INFINIX", "TECNO", "ITEL", "LAVA", "MICROMAX", "MEIZU", "SHARP", "KYOCERA", "ESSENTIAL",
        "BLACKBERRY", "BLU", "WIKO", "UMIDIGI", "DOOGEE", "OUKITEL", "CUBOT",
        // Android system-on-chip vendors: a device with no brand of its own commonly reports one of
        // these as its manufacturer.
        "MTK", "MEDIATEK", "UNISOC", "SPREADTRUM"
    };

    /// <summary>Classifies one device and explains the decision.</summary>
    public static DeviceFamilyVerdict Classify(PortableDeviceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        string name = Clean(identity.FriendlyName);
        string maker = Clean(identity.Manufacturer);
        string model = Clean(identity.Model);

        // Apple first, and strictly: the name must contain one of Apple's own device words, or the
        // manufacturer must be Apple. No OEM list can reach this branch.
        foreach (string token in AppleTokens)
        {
            if (Contains(name, token)) return new DeviceFamilyVerdict(DeviceFamily.Apple, $"its name '{name}' says {token}");
            if (Contains(model, token)) return new DeviceFamilyVerdict(DeviceFamily.Apple, $"its model '{model}' says {token}");
        }

        if (Contains(maker, "Apple")) return new DeviceFamilyVerdict(DeviceFamily.Apple, "the manufacturer is Apple");

        // Some driver builds name the handset "Apple Device" without an iPhone/iPad word in it, and the
        // app has always accepted that; dropping it would make a device it used to read invisible.
        if (Contains(name, "Apple")) return new DeviceFamilyVerdict(DeviceFamily.Apple, $"its name '{name}' says Apple");

        // Android: the safe direction to be generous in. A device is allowed to give itself away by its
        // name, its model or its manufacturer, because being wrong this way only widens the search.
        if (Contains(name, "Android")) return new DeviceFamilyVerdict(DeviceFamily.Android, $"its name '{name}' says Android");
        if (Contains(model, "Android")) return new DeviceFamilyVerdict(DeviceFamily.Android, $"its model '{model}' says Android");

        if (HasMakerWord(name, out string inName))
            return new DeviceFamilyVerdict(DeviceFamily.Android, $"its name '{name}' is an Android device name ({inName})");

        if (HasMakerWord(model, out string inModel))
            return new DeviceFamilyVerdict(DeviceFamily.Android, $"its model '{model}' is an Android device name ({inModel})");

        if (maker.Length > 0 && HasMakerWord(maker, out string inMaker))
            return new DeviceFamilyVerdict(DeviceFamily.Android, $"the manufacturer '{maker}' ({inMaker}) makes Android devices");

        return new DeviceFamilyVerdict(DeviceFamily.Unknown,
            name.Length > 0 ? $"nothing about '{name}' identifies it as an iPhone, iPad or Android device"
                            : "the device reported no name that identifies its family");
    }

    /// <summary>A short, plain description of the family for messages and reports.</summary>
    public static string Describe(DeviceFamily family) => family switch
    {
        DeviceFamily.Apple => "iPhone or iPad",
        DeviceFamily.Android => "Android device",
        _ => "portable device"
    };

    /// <summary>
    /// The order families are looked for in when more than one handset is attached. The preference only
    /// reorders the two families; it never removes one, so an operator who preferred the iPhone is still
    /// offered the Android phone when that is the only one plugged in.
    /// </summary>
    public static IReadOnlyList<DeviceFamily> SelectionOrder(HandsetPreference preference) =>
        preference == HandsetPreference.AndroidFirst
            ? [DeviceFamily.Android, DeviceFamily.Apple]
            : [DeviceFamily.Apple, DeviceFamily.Android];

    /// <summary>True when at least one whole word of the manufacturer is a known Android maker.</summary>
    private static bool HasMakerWord(string manufacturer, out string matched)
    {
        foreach (string word in manufacturer.Split([' ', ',', '.', '-', '_', '(', ')', '/', '\\', '&', '+', '\t'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (AndroidMakers.Contains(word)) { matched = word; return true; }
        }
        matched = string.Empty;
        return false;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static bool Contains(string value, string token) =>
        value.Length > 0 && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
