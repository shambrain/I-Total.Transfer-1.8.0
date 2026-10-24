using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ITotalTransfer.UI;

/// <summary>
/// The Fluent Design integration layer: Windows 11/10 window chrome (immersive dark title bar,
/// rounded corners, caption tinting), the Fluent typography scale, and the Segoe icon glyph set.
/// Everything degrades gracefully: on a build that does not support an attribute the call simply
/// returns a failure HRESULT and the app keeps its default chrome, so Windows 10 stays supported.
/// </summary>
internal static class Fluent
{
    // ------------------------------------------------------------------ typography
    private static readonly Lazy<FontFamily> UiRegular = new(() => ResolveFamily(
        "Segoe UI Variable Text", "Segoe UI Variable Display", "Segoe UI"));

    private static readonly Lazy<FontFamily> UiDisplay = new(() => ResolveFamily(
        "Segoe UI Variable Display", "Segoe UI Variable Text", "Segoe UI"));

    private static readonly Lazy<FontFamily> Icon = new(() => ResolveFamily(
        "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol"));

    private static FontFamily ResolveFamily(params string[] candidates)
    {
        foreach (string name in candidates)
        {
            try
            {
                var family = new FontFamily(name);
                if (family.Name.Length > 0) return family;
            }
            catch (ArgumentException) { /* not installed — try the next candidate */ }
        }
        return FontFamily.GenericSansSerif;
    }

    // Fonts are immutable and notoriously easy to leak, so every family/size/style triple is created
    // once and shared. Controls never own these instances, so applying a theme repeatedly is
    // allocation-free instead of leaking a Font per control per theme switch.
    private static readonly Dictionary<(string, float, FontStyle), Font> FontCache = [];
    private static readonly object FontLock = new();

    private static Font Get(string family, float size, FontStyle style)
    {
        float safeSize = Math.Max(1f, size);
        var key = (family, safeSize, style);
        lock (FontLock)
        {
            if (FontCache.TryGetValue(key, out Font? cached)) return cached;
            Font created = new(family, safeSize, style);
            FontCache[key] = created;
            return created;
        }
    }

    /// <summary>The Fluent body font at the given size.</summary>
    public static Font Text(float size = 9f, FontStyle style = FontStyle.Regular) =>
        Get(UiRegular.Value.Name, size, style);

    /// <summary>The Fluent display (large-title) font.</summary>
    public static Font Display(float size = 20f, FontStyle style = FontStyle.Bold) =>
        Get(UiDisplay.Value.Name, size, style);

    /// <summary>The icon font used for navigation glyphs and status marks.</summary>
    public static Font Glyph(float size = 11f) => Get(Icon.Value.Name, size, FontStyle.Regular);

    // ------------------------------------------------------------------ window chrome
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private const int DwmcpRound = 2;
    private const int DwmcpRoundSmall = 3;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Windows 11 (build 22000) or newer.</summary>
    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Windows 11 22H2 (build 22621) or newer, where the modern DWM attributes landed.</summary>
    public static bool HasModernDwm => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>
    /// Applies the Fluent window chrome matching <paramref name="theme"/>: an immersive dark caption,
    /// rounded corners and a caption/border tinted to the theme's header surface. Safe to call again
    /// after a live theme change.
    /// </summary>
    public static void ApplyWindowChrome(Form form, Theme theme)
    {
        if (!form.IsHandleCreated) return;
        IntPtr hwnd = form.Handle;
        int dark = theme.IsDark ? 1 : 0;

        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));

        if (!IsWindows11) return; // rounded corners and caption tinting are Windows 11 only.

        int corner = DwmcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        int caption = ToColorRef(theme.CaptionColor);
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));

        int text = ToColorRef(theme.Text);
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));

        int border = ToColorRef(theme.Border);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }

    /// <summary>Rounds the child-window corners of a popup-style surface (Windows 11 only).</summary>
    public static void ApplySmallCorners(Control control)
    {
        if (!IsWindows11 || !control.IsHandleCreated) return;
        int corner = DwmcpRoundSmall;
        DwmSetWindowAttribute(control.Handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /// <summary>True when the family can render the given glyph (used to fall back to text labels).</summary>
    public static bool HasIconFont
    {
        get
        {
            try { return Icon.Value.Name is "Segoe Fluent Icons" or "Segoe MDL2 Assets"; }
            catch { return false; }
        }
    }
}

/// <summary>
/// Segoe Fluent Icons / MDL2 Assets code points. Every glyph used here also exists in the
/// Windows 10 "Segoe MDL2 Assets" font, so the same UI renders on Windows 10 and Windows 11.
/// </summary>
internal static class Glyphs
{
    public const string Home = "\uE80F";
    public const string Download = "\uE896";
    public const string Sync = "\uE895";
    public const string Photo = "\uE8B9";
    public const string Video = "\uE714";
    public const string Audio = "\uE8D6";
    public const string Folder = "\uE8B7";
    public const string Wifi = "\uE701";
    public const string History = "\uE81C";
    public const string Settings = "\uE713";
    public const string Repair = "\uE90F";
    public const string Check = "\uE73E";
    public const string Warning = "\uE7BA";
    public const string Info = "\uE946";
    public const string Error = "\uEA39";
    public const string Play = "\uE768";
    public const string Pause = "\uE769";
    public const string Stop = "\uE71A";
    public const string Search = "\uE721";
    public const string Delete = "\uE74D";
    public const string OpenFile = "\uE8E5";
    public const string Phone = "\uE8EA";
    public const string ChevronRight = "\uE76C";
    public const string ChevronDown = "\uE70D";
    public const string Save = "\uE74E";
    public const string Refresh = "\uE72C";
    public const string Shield = "\uEA18";
    public const string Send = "\uE724";
    public const string Document = "\uE8A5";
    public const string Sparkle = "\uE945";
    public const string Lock = "\uE72E";
    public const string Unlock = "\uE785";
    public const string Globe = "\uE774";
    public const string Add = "\uE710";

    // Window-chrome glyphs (Segoe Fluent Icons and Segoe MDL2 Assets both carry these code points).
    public const string ChromeMinimize = "\uE921";
    public const string ChromeMaximize = "\uE922";
    public const string ChromeRestore = "\uE923";
    public const string ChromeClose = "\uE8BB";
}
