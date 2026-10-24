using System.Net;
using System.Net.Sockets;

namespace ITotalTransfer.Core;

/// <summary>
/// The address rules a LAN receiver must obey. A media receiver that binds to <c>0.0.0.0</c> is
/// reachable from whatever network the machine is attached to — a café, a hotel, a corporate VPN —
/// and the plan for this feature says plainly: bind to the selected private interface, never a
/// wildcard, never a public address.
///
/// <para>
/// These rules are pure so they can be tested without opening a socket, and they are deliberately
/// strict: loopback and the three IPv4 private ranges only. A carrier-grade NAT address
/// (100.64.0.0/10) is <em>not</em> accepted, because it is routinely routable between subscribers.
/// </para>
/// </summary>
public static class PrivateNetwork
{
    /// <summary>True for 127.0.0.0/8.</summary>
    public static bool IsLoopback(IPAddress address) => IPAddress.IsLoopback(address);

    /// <summary>
    /// True for the private IPv4 ranges a home or office LAN actually uses: 10/8, 172.16/12 and
    /// 192.168/16. IPv6 is not accepted here because the receiver only ever binds an IPv4 interface.
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>Parses and classifies one address; false when it is not a usable literal.</summary>
    public static bool IsAllowedBindAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        string value = address.Trim();
        // "localhost" is the loopback spelling the tests and the UI use.
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(value, out IPAddress? parsed) && (IsLoopback(parsed) || IsPrivate(parsed));
    }

    /// <summary>
    /// A human-readable reason an address was refused, or null when it is acceptable. The UI shows
    /// this verbatim, so it names the rule rather than saying "invalid".
    /// </summary>
    public static string? RejectionReason(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Choose the private network address to listen on.";
        if (IsAllowedBindAddress(address)) return null;

        string value = address.Trim();
        if (value is "0.0.0.0" or "*" or "+")
            return "Refused: listening on every interface would expose the receiver to any network this PC joins.";
        if (IPAddress.TryParse(value, out IPAddress? parsed) && IsLoopback(parsed) == false && IsPrivate(parsed) == false)
            return $"Refused: {value} is not a private address. Bind the receiver to a 10.x, 172.16-31.x or 192.168.x interface.";
        return $"Refused: '{value}' is not a usable IPv4 address on a private interface.";
    }

    /// <summary>True when the address belongs to an interface that is currently up.</summary>
    public static bool IsAssignedToThisMachine(string address)
    {
        if (!IPAddress.TryParse(address, out IPAddress? target)) return false;
        if (IsLoopback(target)) return true;
        foreach (System.Net.NetworkInformation.NetworkInterface nic in
                 System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            foreach (System.Net.NetworkInformation.UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                if (info.Address.Equals(target)) return true;
        }
        return false;
    }
}
