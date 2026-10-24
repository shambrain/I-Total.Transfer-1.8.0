using System.Windows.Forms;
using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Appearance section has to do more than list three words: the operator must be able to see the
/// palettes, choose one, and have that choice persist while a saved preference is never overwritten.
/// These tests drive the real controls, not a copied mapping table.
/// </summary>
[TestClass]
public sealed class AppearanceSettingsTests : WinFormsTestBase
{
    private static ThemeSwatch FindSwatch(SettingsPage page, AppTheme option)
    {
        foreach (Control control in Walk(page))
            if (control is ThemeSwatch swatch && swatch.SwatchTheme == option) return swatch;
        Assert.Fail($"No appearance swatch was built for {option}.");
        throw new InvalidOperationException();
    }

    private static IEnumerable<Control> Walk(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Walk(child)) yield return nested;
        }
    }

    [TestMethod]
    public void AllThreeEstablishedThemesHaveAVisiblePreview()
    {
        using var page = new SettingsPage();
        foreach (AppTheme option in new[] { AppTheme.Neon, AppTheme.Slate, AppTheme.Light })
        {
            ThemeSwatch swatch = FindSwatch(page, option);
            Assert.IsTrue(swatch.Visible, $"{option} must be visible without opening anything else.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(swatch.Caption));
            Assert.AreEqual(168, swatch.Width);
        }
    }

    [TestMethod]
    public void ChoosingASwatchSelectsTheThemeAndRaisesTheLivePreviewEvent()
    {
        using var page = new SettingsPage();
        int previews = 0;
        page.ThemePreviewRequested += (_, _) => previews++;

        FindSwatch(page, AppTheme.Slate).Activate();

        Assert.AreEqual(AppTheme.Slate, page.SelectedTheme);
        Assert.AreEqual(1, previews, "The shell must be asked to repaint exactly once for one choice.");
        Assert.IsTrue(FindSwatch(page, AppTheme.Slate).Selected);
        Assert.IsFalse(FindSwatch(page, AppTheme.Neon).Selected);
    }

    [TestMethod]
    public void ASavedThemeIsRestoredAndThePreviewFollowsIt()
    {
        using var page = new SettingsPage();
        AppSettings settings = AppSettings.Defaults();
        settings.Theme = AppTheme.Light;

        page.LoadSettings(settings);

        Assert.AreEqual(AppTheme.Light, page.SelectedTheme);
        Assert.IsTrue(FindSwatch(page, AppTheme.Light).Selected, "The restored theme must be the highlighted swatch.");
        Assert.IsFalse(FindSwatch(page, AppTheme.Neon).Selected);
    }

    [TestMethod]
    public void AFreshProfileStillDefaultsToNeon()
    {
        using var page = new SettingsPage();
        page.LoadSettings(AppSettings.Defaults());

        Assert.AreEqual(AppTheme.Neon, AppSettings.Defaults().Theme);
        Assert.AreEqual(AppTheme.Neon, page.SelectedTheme);
        Assert.IsTrue(FindSwatch(page, AppTheme.Neon).Selected);
    }
}
