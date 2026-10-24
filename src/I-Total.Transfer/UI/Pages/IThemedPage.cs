namespace ITotalTransfer.UI.Pages;

/// <summary>
/// Implemented by the shell's pages. The shell pushes the active <see cref="Theme"/> into each page
/// so the page's own token lookups (status colours, category accents) resolve against the palette
/// the user actually selected — not a hard-coded default.
/// </summary>
public interface IThemedPage
{
    /// <summary>Records the active palette and applies it to the page's control tree.</summary>
    void ApplyTheme(Theme theme);
}
