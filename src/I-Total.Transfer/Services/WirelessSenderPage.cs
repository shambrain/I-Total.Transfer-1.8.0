using System.Reflection;
using System.Text;

namespace ITotalTransfer.Services;

/// <summary>
/// The browser sender served by <see cref="WirelessReceiver"/>. It is a real, self-contained sender
/// path for iOS Safari and Android Chrome: the operator selects files explicitly, pairs with the
/// single-use code, and each file is hashed on the device before upload. It is not an app-store
/// companion and it cannot enumerate a phone's photo library - those limits are stated on the page.
///
/// <para>
/// The HTML and JavaScript are embedded resources rather than strings in this class, so the same files
/// can be exercised directly by the Node tests in <c>tests/sender</c>.
/// </para>
/// </summary>
public static class WirelessSenderPage
{
    public const string HtmlResource = "ITotalTransfer.Wireless.sender.html";
    public const string ScriptResource = "ITotalTransfer.Wireless.sender.js";

    public static string Html { get; } = Load(HtmlResource);
    public static string JavaScript { get; } = Load(ScriptResource);

    private static string Load(string name)
    {
        Assembly assembly = typeof(WirelessSenderPage).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
            throw new InvalidOperationException($"Embedded sender resource '{name}' is missing from the assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
