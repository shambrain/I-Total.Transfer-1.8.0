using System.Windows.Forms;

namespace ITotalTransfer.UI;

/// <summary>
/// Gives every interactive control a screen-reader name. Windows exposes a control's <c>Text</c> as its
/// accessible name only for some control types and only when the accessibility bridge chooses to; a
/// button whose caption is drawn by a custom paint path (this app's rounded buttons) can otherwise be
/// announced as an unnamed "button". The fallback is the control's visible text, then its designer name,
/// which is enough for every action on every page and never invents a meaning the control does not have.
/// </summary>
public static class AccessibleNames
{
    /// <summary>Walks the tree and fills in missing accessible names. Returns how many were assigned.</summary>
    public static int Ensure(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        int assigned = 0;
        foreach (Control child in root.Controls) assigned += Ensure(child);

        if (!string.IsNullOrWhiteSpace(root.AccessibleName) || !NeedsName(root)) return assigned;

        string text = (root.Text ?? string.Empty).Trim();
        string name = text.Length > 0 ? text : (root.Name ?? string.Empty).Trim();
        if (name.Length == 0) return assigned;
        root.AccessibleName = name;
        return assigned + 1;
    }

    /// <summary>
    /// Controls a keyboard or screen-reader user operates. Decorative labels and containers are left
    /// alone: naming them would make a screen reader announce the whole visual tree.
    /// </summary>
    private static bool NeedsName(Control control) => control switch
    {
        ButtonBase => true,
        TextBoxBase => true,
        ComboBox => true,
        ListBox => true,
        ListView => true,
        TreeView => true,
        ProgressBar => true,
        DataGridView => true,
        _ => false
    };
}
