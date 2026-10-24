using System.Reflection;

namespace ITotalTransfer.UI;

/// <summary>
/// Loads the brand artwork that is embedded in the assembly, so the window icon and the header badge
/// resolve identically whether the app runs from a build folder or a single-file publish.
/// </summary>
internal static class AppIcons
{
    private const string IconResource = "ITotalTransfer.app.ico";
    private const string BadgeResource = "ITotalTransfer.app.png";

    /// <summary>The multi-resolution application icon (window chrome, taskbar, Alt+Tab).</summary>
    public static Icon? WindowIcon()
    {
        using Stream? stream = Open(IconResource);
        if (stream is null) return null;
        try { return new Icon(stream, new Size(256, 256)); }
        catch { return null; }
    }

    /// <summary>The rounded-square brand tile shown beside the title in the header.</summary>
    public static Image? Badge()
    {
        using Stream? stream = Open(BadgeResource);
        if (stream is null) return null;
        try
        {
            using var source = Image.FromStream(stream);
            return new Bitmap(source);
        }
        catch { return null; }
    }

    private static Stream? Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
}
