namespace ITotalTransfer.Core;

/// <summary>
/// What an Apple USB record actually is. One physical handset presents several of these at once, and
/// only one of them is the interface Windows exposes media through.
/// </summary>
public enum AppleUsbInterface
{
    /// <summary>Not a handset at all - a keyboard, a trackpad, or anything else sharing Apple's vendor id.</summary>
    Other,

    /// <summary>
    /// The Windows Portable Devices (MTP) interface. This is the one that has to be present for the
    /// camera roll to be reachable, and its record name is the handset's own name ("Apple iPhone").
    /// </summary>
    MediaInterface,

    /// <summary>
    /// A support interface - the USB composite parent, Apple's own USB driver, or the usbmuxd channel.
    /// Present for a handset, but not evidence that media can be read.
    /// </summary>
    SupportInterface,
}

/// <summary>One Apple USB record as Windows stores it: a display name and the service that owns it.</summary>
public sealed record AppleUsbRecord(string Name, string Service);

/// <summary>
/// Reads the meaning out of the raw Apple USB records. Kept pure - and separate from the registry read -
/// so the classification that decides whether an operator is told "no phone" or "a phone is here" can be
/// tested against the real record sets instead of a live bus.
/// </summary>
public static class AppleUsbInventory
{
    /// <summary>
    /// Classifies one record by the driver service bound to it. The service is the only field that
    /// distinguishes the media interface from the pile of support interfaces, and it is stable across
    /// Apple's driver versions where the display names are not.
    /// </summary>
    public static AppleUsbInterface Classify(string? service)
    {
        string value = (service ?? string.Empty).Trim();
        if (value.Equals("WUDFWpdMtp", StringComparison.OrdinalIgnoreCase)) return AppleUsbInterface.MediaInterface;
        if (value.Equals("usbccgp", StringComparison.OrdinalIgnoreCase)) return AppleUsbInterface.SupportInterface;
        if (value.Equals("USBAAPL64", StringComparison.OrdinalIgnoreCase)) return AppleUsbInterface.SupportInterface;
        if (value.StartsWith("WINUSB", StringComparison.OrdinalIgnoreCase)) return AppleUsbInterface.SupportInterface;
        if (value.StartsWith("usbmux", StringComparison.OrdinalIgnoreCase)) return AppleUsbInterface.SupportInterface;
        return AppleUsbInterface.Other;
    }

    /// <summary>
    /// Turns a registry display value into something an operator can read.
    ///
    /// Windows stores many of these as <em>indirect</em> strings - <c>@oem69.inf,%key%;Apple iPhone</c> -
    /// where the readable part is the fallback after the last semicolon. The readable part is used rather
    /// than resolved through the driver store, because the fallback is exactly what Device Manager would
    /// show and it needs no extra API.
    /// </summary>
    public static string DisplayName(string? friendlyName, string? deviceDesc)
    {
        foreach (string? candidate in new[] { friendlyName, deviceDesc })
        {
            string resolved = Unwrap(candidate);
            if (resolved.Length > 0) return resolved;
        }
        return "";
    }

    private static string Unwrap(string? value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return "";
        if (!text.StartsWith('@')) return text;

        // @file.inf,%key%;Readable Name  ->  the readable part.
        int separator = text.LastIndexOf(';');
        if (separator < 0 || separator == text.Length - 1) return "";
        return text[(separator + 1)..].Trim();
    }

    /// <summary>
    /// Splits a record set into the handset names a media interface belongs to and the support-interface
    /// names, each de-duplicated and in the order first seen. Only media-interface records are handsets:
    /// a machine keeps a support record for every Apple cable it has ever seen, so counting those would
    /// report handsets that were never there.
    /// </summary>
    public static (IReadOnlyList<string> Handsets, IReadOnlyList<string> Support) Summarise(
        IEnumerable<AppleUsbRecord>? records)
    {
        var handsets = new List<string>();
        var support = new List<string>();
        foreach (AppleUsbRecord record in records ?? [])
        {
            switch (Classify(record.Service))
            {
                case AppleUsbInterface.MediaInterface: Add(handsets, record.Name); break;
                case AppleUsbInterface.SupportInterface: Add(support, record.Name); break;
            }
        }
        return (handsets, support);
    }

    private static void Add(List<string> names, string? name)
    {
        string value = (name ?? string.Empty).Trim();
        if (value.Length == 0) return;
        if (names.Contains(value, StringComparer.OrdinalIgnoreCase)) return;
        names.Add(value);
    }
}

/// <summary>The two halves of the USB evidence: what is answering now, and what Windows merely remembers.</summary>
public sealed record AttachmentEvidence(
    IReadOnlyList<string> AttachedNames,
    IReadOnlyList<string> CachedHandsetNames,
    IReadOnlyList<string> CachedSupportNames);

/// <summary>How the USB evidence adds up.</summary>
public enum AttachmentVerdict
{
    /// <summary>No Apple handset has ever been seen over USB on this machine.</summary>
    NoRecords,

    /// <summary>Windows holds Apple handset records, but no handset is answering right now.</summary>
    CachedRecordsOnly,

    /// <summary>A handset is attached and answering.</summary>
    Attached,
}

/// <summary>
/// The operator-facing reading of the USB evidence, with the two facts that are most often confused kept
/// apart: <em>Windows remembers this handset</em> and <em>this handset is here</em>.
///
/// <para>
/// A PC accumulates a USB record for every Apple device ever plugged into it. Device Manager keeps
/// showing those records - "Currently, this hardware device is not connected to the computer" - and the
/// Apple services keep listing them, so "my PC can see my iPhone" is routinely true of a phone that is
/// not on the desk. Reporting that as <em>connected</em> leads to a scan that can never find a file;
/// reporting it as <em>nothing is plugged in</em> leaves the operator staring at an "Apple iPhone" entry
/// in Device Manager and wondering which of the two is lying. This names both, so neither has to be
/// guessed at - and it is why the check exists at all rather than a bare device list.
/// </para>
/// </summary>
public sealed record AttachmentReport(
    AttachmentVerdict Verdict,
    string Headline,
    string Guidance,
    IReadOnlyList<string> AttachedNames,
    IReadOnlyList<string> CachedHandsetNames,
    IReadOnlyList<string> CachedSupportNames)
{
    public bool IsAttached => Verdict == AttachmentVerdict.Attached;

    /// <summary>How many distinct handsets Windows remembers but is not talking to.</summary>
    public int CachedHandsetCount => CachedHandsetNames.Count;

    /// <summary>The short status word for a diagnostics row.</summary>
    public string Status => Verdict switch
    {
        AttachmentVerdict.Attached => "ATTACHED",
        AttachmentVerdict.CachedRecordsOnly => $"CACHED RECORD ONLY ({CachedHandsetCount:N0})",
        _ => "NONE",
    };
}

public static class AttachmentDiagnosis
{
    /// <summary>
    /// Reads the evidence. When a handset is attached and Windows also holds older records for it, the
    /// attached reading always wins - a stale record must never downgrade a live device, which is the
    /// mistake this whole type exists to prevent.
    /// </summary>
    public static AttachmentReport Decide(AttachmentEvidence? evidence)
        => Decide(evidence?.AttachedNames, evidence?.CachedHandsetNames, evidence?.CachedSupportNames);

    public static AttachmentReport Decide(
        IReadOnlyList<string>? attachedNames,
        IReadOnlyList<string>? cachedHandsetNames,
        IReadOnlyList<string>? cachedSupportNames)
    {
        IReadOnlyList<string> attached = Clean(attachedNames);
        IReadOnlyList<string> cached = Clean(cachedHandsetNames);
        IReadOnlyList<string> support = Clean(cachedSupportNames);

        if (attached.Count > 0)
        {
            return new AttachmentReport(
                AttachmentVerdict.Attached,
                $"Attached and answering: {Join(attached)}.",
                "If its media is not readable, unlock the handset and tap Trust, then probe again. A cable "
                    + "that charges but carries no data, or an unpowered hub, is the next thing to rule out.",
                attached, cached, support);
        }

        if (cached.Count > 0)
        {
            string records = cached.Count == 1 ? "record" : "records";
            return new AttachmentReport(
                AttachmentVerdict.CachedRecordsOnly,
                $"No handset is attached. Windows still holds {cached.Count:N0} cached Apple handset "
                    + $"{records}: {Join(cached)}.",
                "Cached records are leftovers from earlier connections - Device Manager shows them as "
                    + "\"Currently, this hardware device is not connected to the computer\" - and they cannot "
                    + "supply a single file. They explain an Apple device appearing in Device Manager while "
                    + "this app reports no handset. Connect the phone, unlock it, and tap Trust on the prompt.",
                attached, cached, support);
        }

        return new AttachmentReport(
            AttachmentVerdict.NoRecords,
            "No Apple handset has ever been seen over USB on this PC.",
            "Install Apple Devices (or Apple Mobile Device Support) so the USB driver is present, use a known "
                + "data-capable cable, connect straight to the PC rather than through a hub, and accept any "
                + "driver prompt that appears the first time.",
            attached, cached, support);
    }

    /// <summary>Trims, drops blanks and de-duplicates, keeping the order devices were reported in.</summary>
    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? names)
    {
        var result = new List<string>();
        foreach (string? name in names ?? [])
        {
            string value = (name ?? string.Empty).Trim();
            if (value.Length == 0) continue;
            if (result.Contains(value, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(value);
        }
        return result;
    }

    private static string Join(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Concat(names) : string.Join(", ", names);
}
