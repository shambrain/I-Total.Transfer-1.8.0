using Microsoft.Win32;
using ITotalTransfer.Core;

namespace ITotalTransfer.Devices;

/// <summary>
/// Reads the Apple USB records Windows keeps for every handset that has ever been connected, from
/// <c>HKLM\SYSTEM\CurrentControlSet\Enum\USB</c>.
///
/// <para>
/// The registry is read rather than the device list on purpose. A live device list answers "what is
/// answering right now"; it cannot answer "what does Windows still believe is here", which is the
/// question behind the operator's most common confusion - an Apple device sitting in Device Manager
/// while the application reports nothing connected. Both readings are reported, and
/// <see cref="AppleUsbInventory.Summarise"/> decides which of the records are actually handsets.
/// </para>
///
/// <para>Nothing is written and nothing is opened: this is a read of a key any user can read.</para>
/// </summary>
public static class AppleDeviceHistory
{
    private const string UsbEnumKey = @"SYSTEM\CurrentControlSet\Enum\USB";

    /// <summary>Apple's USB vendor id, as it appears in a record's key name.</summary>
    private const string AppleVendorPrefix = "VID_05AC";

    /// <summary>Every Apple USB record Windows holds, as (display name, driver service) pairs.</summary>
    public static IReadOnlyList<AppleUsbRecord> Read()
    {
        var records = new List<AppleUsbRecord>();
        try
        {
            using RegistryKey? usb = Registry.LocalMachine.OpenSubKey(UsbEnumKey);
            if (usb is null) return records;

            foreach (string deviceKeyName in usb.GetSubKeyNames())
            {
                // VID_05AC&PID_12A8 and VID_05AC&PID_12A8&MI_00 are different records for one handset.
                if (!deviceKeyName.StartsWith(AppleVendorPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                using RegistryKey? deviceKey = usb.OpenSubKey(deviceKeyName);
                if (deviceKey is null) continue;

                foreach (string instanceName in deviceKey.GetSubKeyNames())
                {
                    using RegistryKey? instance = deviceKey.OpenSubKey(instanceName);
                    if (instance is null) continue;

                    string name = AppleUsbInventory.DisplayName(
                        instance.GetValue("FriendlyName") as string,
                        instance.GetValue("DeviceDesc") as string);
                    string service = instance.GetValue("Service") as string ?? "";
                    records.Add(new AppleUsbRecord(name, service));
                }
            }
        }
        catch (Exception ex)
        {
            // A locked-down or unusual machine simply reports no records; the live probe still runs.
            System.Diagnostics.Debug.WriteLine($"Apple USB record read failed: {ex.Message}");
        }
        return records;
    }

    /// <summary>
    /// The full evidence for the diagnosis: the names a live probe found attached, plus what this PC
    /// remembers. A read that fails contributes no records rather than failing the diagnosis.
    /// </summary>
    public static AttachmentEvidence Evidence(IEnumerable<string>? attachedNames)
    {
        (IReadOnlyList<string> handsets, IReadOnlyList<string> support) = AppleUsbInventory.Summarise(Read());
        var attached = new List<string>();
        foreach (string? name in attachedNames ?? [])
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (attached.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            attached.Add(name);
        }
        return new AttachmentEvidence(attached, handsets, support);
    }

    /// <summary>
    /// The same evidence, filtered to Apple handsets. An Android phone answers on the very same
    /// portable-device bus, so passing every attached handset through would put "HUAWEI Y7 Prime 2018"
    /// under a row titled "Apple handsets seen over USB" - a family-specific conclusion drawn from a
    /// device of the other family. The family is a property the classifier already established, so it is
    /// used here rather than re-deriving it from the name.
    /// </summary>
    public static AttachmentEvidence Evidence(IEnumerable<DevicePresence>? attached)
        => Evidence((attached ?? []).Where(p => p.Present && p.Family == DeviceFamily.Apple)
                                     .Select(p => p.Name));
}
