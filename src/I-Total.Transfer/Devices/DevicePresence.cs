using System.Runtime.InteropServices;
using ITotalTransfer.Core;
using ITotalTransfer.Devices.Wpd;

namespace ITotalTransfer.Devices;

/// <summary>The result of a cheap "is a handset on the bus?" check.</summary>
public sealed record DevicePresence(bool Present, string Name, string Transport, string Detail)
{
    /// <summary>Which family the attached device was classified as; Unknown when nothing was found.</summary>
    public DeviceFamily Family { get; init; } = DeviceFamily.Unknown;

    public static readonly DevicePresence Absent = new(false, "", "", "");
}

/// <summary>
/// Answers "is a handset attached?" without opening a content session.
/// A full media probe walks the device's object tree and can take seconds on MTP; running that on a
/// short timer would fight with Explorer, with Apple's services and with a transfer in flight. This
/// check only reads the device list and the device/shell friendly names, so it is safe to poll.
///
/// <para>
/// The same family classifier the media backend uses decides what counts, so an Android phone is
/// "attached and answering" here exactly as an iPhone is. Keeping a second, Apple-only test in this
/// class is what made an attached Android phone read as no device at all.
/// </para>
/// </summary>
internal static class DevicePresenceMonitor
{
    /// <summary>WPD requires an MTA apartment; this monitor gets its own serialized one.</summary>
    private static readonly ComApartment Apartment = new();

    public static DevicePresence Check()
    {
        foreach (DevicePresence presence in CheckAll()) return presence;
        return DevicePresence.Absent;
    }

    /// <summary>
    /// Every handset currently answering, not only the first. With an iPhone and an Android phone on the
    /// desk at the same time "the first device WPD lists" is an arbitrary pick, so anything that draws a
    /// family-specific conclusion - the Apple USB diagnosis above all - must filter by family instead of
    /// trusting whichever device happened to come back first.
    /// </summary>
    public static IReadOnlyList<DevicePresence> CheckAll()
    {
        IReadOnlyList<DevicePresence> found = Apartment.Invoke(CollectPortableDevices);
        return found.Count > 0 ? found : OnStaThread(CollectShell);
    }

    /// <summary>Shell.Application lives in an STA; it must not be created on the MTA worker.</summary>
    private static IReadOnlyList<DevicePresence> OnStaThread(Func<IReadOnlyList<DevicePresence>> work)
    {
        IReadOnlyList<DevicePresence> result = [];
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch { /* a failed shell probe simply means "not present". */ }
        })
        {
            IsBackground = true,
            Name = "DevicePresence-STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5));
        return result;
    }

    private static IReadOnlyList<DevicePresence> CollectPortableDevices()
    {
        var found = new List<DevicePresence>();
        try
        {
            IPortableDeviceManager manager = WpdFactory.CreateManager();
            uint count = 0;
            manager.GetDevices(IntPtr.Zero, ref count);
            if (count == 0) return found;

            IntPtr buffer = Marshal.AllocCoTaskMem(IntPtr.Size * (int)count);
            try
            {
                manager.GetDevices(buffer, ref count);
                for (int i = 0; i < count; i++)
                {
                    IntPtr p = Marshal.ReadIntPtr(buffer, i * IntPtr.Size);
                    if (p == IntPtr.Zero) continue;
                    string? id = Marshal.PtrToStringUni(p);
                    if (string.IsNullOrEmpty(id)) continue;

                    string friendly = ReadString(manager, id, friendly: true);
                    string manufacturer = ReadString(manager, id, friendly: false);
                    DeviceFamilyVerdict verdict =
                        DeviceFamilies.Classify(new PortableDeviceIdentity(friendly, manufacturer, null));
                    if (verdict.Family == DeviceFamily.Unknown) continue;
                    found.Add(new DevicePresence(true,
                        string.IsNullOrWhiteSpace(friendly) ? DeviceFamilies.Describe(verdict.Family) : friendly,
                        "USB · Windows Portable Devices", "The handset answers on the portable-device bus.")
                    {
                        Family = verdict.Family
                    });
                }
            }
            finally { Marshal.FreeCoTaskMem(buffer); }
        }
        catch { /* WPD is optional; the shell check still runs. */ }
        return found;
    }

    private static IReadOnlyList<DevicePresence> CollectShell()
    {
        var found = new List<DevicePresence>();
        dynamic? shell = null;
        dynamic? computer = null;
        dynamic? devices = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true)!)!;
            computer = shell.NameSpace(17); // ssfDRIVES — "This PC"
            devices = computer?.Items();
            if (devices is null) return found;
            int count = Convert.ToInt32(devices.Count);
            for (int i = 0; i < count; i++)
            {
                dynamic? item = null;
                try
                {
                    item = devices.Item(i);
                    // A drive is never a handset. Without this, a volume whose label happened to contain
                    // a maker's name would be reported as an attached phone.
                    bool isDrive = false;
                    try { isDrive = Convert.ToBoolean(item.IsFileSystem); } catch { }
                    if (isDrive) continue;

                    string name = Convert.ToString(item.Name) ?? "";
                    DeviceFamilyVerdict verdict = DeviceFamilies.Classify(new PortableDeviceIdentity(name, null, null));
                    if (verdict.Family != DeviceFamily.Unknown)
                        found.Add(new DevicePresence(true, name, "USB · Windows Shell", "The handset is listed under This PC.")
                        {
                            Family = verdict.Family
                        });
                }
                finally { Release(item); }
            }
        }
        catch { /* a machine without the shell COM object simply reports "not present". */ }
        finally { Release(devices); Release(computer); Release(shell); }
        return found;
    }

    private static string ReadString(IPortableDeviceManager manager, string id, bool friendly)
    {
        uint length = 0;
        try
        {
            if (friendly) manager.GetDeviceFriendlyName(id, IntPtr.Zero, ref length);
            else manager.GetDeviceManufacturer(id, IntPtr.Zero, ref length);
        }
        catch { length = 0; }
        if (length == 0) length = 260;
        IntPtr buffer = Marshal.AllocCoTaskMem((int)length * 2 + 2);
        try
        {
            uint actual = length;
            try
            {
                if (friendly) manager.GetDeviceFriendlyName(id, buffer, ref actual);
                else manager.GetDeviceManufacturer(id, buffer, ref actual);
            }
            catch { return ""; }
            return Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally { Marshal.FreeCoTaskMem(buffer); }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } }
    }
}
