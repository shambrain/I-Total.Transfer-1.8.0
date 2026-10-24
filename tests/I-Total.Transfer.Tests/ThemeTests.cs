using System.Drawing;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class ThemeTests
{
    [TestMethod]
    public void DefaultThemeIsTheBannerTheme()
    {
        Assert.AreEqual(AppTheme.Neon, AppSettings.Defaults().Theme);
        Assert.AreSame(Theme.NeonInstance, Theme.For(AppTheme.Neon));
    }

    [TestMethod]
    public void LegacyDarkSettingStillLoadsAndMapsToTheBannerTheme()
    {
        string dir = Path.Combine(Path.GetTempPath(), "itotal-theme", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ \"Theme\": \"Dark\", \"MaxRetries\": 7 }");

            AppSettings loaded = new SettingsService(path).Load();

            Assert.AreEqual(AppTheme.Dark, loaded.Theme, "The legacy alias must still deserialize.");
            Assert.AreEqual(7, loaded.MaxRetries, "Unrelated settings must survive the migration.");
            Assert.AreSame(Theme.NeonInstance, Theme.For(loaded.Theme), "Dark is an alias for the banner theme.");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The banner (I-Total Transfer_ iPhone to Windows.png) is the authoritative colour source, so the
    /// sampled values are pinned here: if someone "nudges" the palette the regression is caught.
    /// </summary>
    [TestMethod]
    public void BannerPaletteMatchesTheSampledBannerArtwork()
    {
        Theme neon = Theme.NeonInstance;

        Assert.IsTrue(neon.IsDark);
        Assert.AreEqual(Color.FromArgb(11, 85, 241), neon.Accent);          // #0B55F1 dominant icon blue
        Assert.AreEqual(Color.FromArgb(4, 10, 30), neon.Background);        // #040A1E deep navy base
        Assert.IsTrue(neon.HasBackdrop, "The banner theme paints its blue glow backdrop.");
        Assert.AreEqual(Color.FromArgb(12, 60, 166), neon.BackdropTop);     // #0C3CA6 banner glow
        Assert.AreEqual(Color.FromArgb(249, 148, 92), neon.Warm);           // #F9945C banner orange

        Assert.IsNotNull(neon.Gradient);
        Assert.AreEqual(6, neon.Gradient!.Length);
        Assert.AreEqual(Color.FromArgb(20, 240, 252), neon.Gradient![0]);   // #14F0FC cyan
        Assert.AreEqual(Color.FromArgb(5, 161, 252), neon.Gradient![1]);    // #05A1FC sky
        Assert.AreEqual(Color.FromArgb(11, 85, 241), neon.Gradient![2]);    // #0B55F1 blue
        Assert.AreEqual(Color.FromArgb(34, 46, 236), neon.Gradient![3]);    // #222EEC indigo
        Assert.AreEqual(Color.FromArgb(111, 50, 251), neon.Gradient![4]);   // #6F32FB violet
        Assert.AreEqual(Color.FromArgb(243, 52, 248), neon.Gradient![^1]);  // #F334F8 magenta
        Assert.IsTrue(neon.HasGradient);
    }

    [TestMethod]
    public void CategoryAccentsKeepTheFourBannerGroupsDistinct()
    {
        Theme neon = Theme.NeonInstance;

        Assert.AreEqual(Color.FromArgb(183, 56, 252), neon.CategoryAccent(MediaCategory.Photos));
        Assert.AreEqual(Color.FromArgb(5, 161, 252), neon.CategoryAccent(MediaCategory.Videos));
        Assert.AreEqual(Color.FromArgb(243, 52, 248), neon.CategoryAccent(MediaCategory.Music));
        Assert.AreEqual(Color.FromArgb(20, 240, 252), neon.CategoryAccent(MediaCategory.Files));

        var accents = MediaCategories.All.Select(neon.CategoryAccent).ToList();
        CollectionAssert.AllItemsAreUnique(accents, "Every banner group needs its own accent.");
    }

    [TestMethod]
    public void SlateStaysNeutralWithARestrainedAccent()
    {
        Theme slate = Theme.SlateInstance;

        Assert.IsTrue(slate.IsDark);
        // 2026-10-22: the near-black base was lifted to a charcoal stack so cards, inputs and the page
        // remain distinguishable without an outline on every surface. The accent is a greyer steel blue
        // than Neon's #0B55F1, which keeps the two dark themes recognisably different.
        Assert.AreEqual(Color.FromArgb(16, 17, 20), slate.Background);
        Assert.AreEqual(Color.FromArgb(23, 25, 29), slate.Surface);
        Assert.AreEqual(Color.FromArgb(74, 107, 196), slate.Accent);
        Assert.AreNotEqual(Theme.NeonInstance.Accent, slate.Accent, "Slate must not be a recoloured Neon.");
        Assert.IsFalse(slate.HasBackdrop, "The neutral theme keeps a flat backdrop.");
        Assert.IsNull(slate.Gradient, "The grey theme is intentionally flat.");
        Assert.IsFalse(slate.HasGradient);
    }

    [TestMethod]
    public void LightPaletteIsActuallyLight()
    {
        Theme light = Theme.LightInstance;

        Assert.IsFalse(light.IsDark);
        Assert.AreEqual(Color.FromArgb(243, 244, 247), light.Background);
        Assert.AreEqual(Color.White, light.Surface);
        Assert.AreEqual(Color.FromArgb(11, 85, 241), light.Accent);
        Assert.IsFalse(light.HasBackdrop);
    }

    [TestMethod]
    public void EveryPaletteCarriesTheFullFluentTokenSet()
    {
        foreach (Theme theme in new[] { Theme.NeonInstance, Theme.SlateInstance, Theme.LightInstance })
        {
            Assert.IsTrue(theme.Radius > 0);
            Assert.IsTrue(theme.ControlRadius > 0);
            Assert.AreEqual(255, theme.Accent.A);
            Assert.AreEqual(255, theme.Surface.A);
            Assert.AreEqual(255, theme.Border.A);
            Assert.AreEqual(255, theme.Text.A);
            Assert.AreEqual(255, theme.Ok.A);
            Assert.AreEqual(255, theme.Warn.A);
            Assert.AreEqual(255, theme.Error.A);
            Assert.AreEqual(255, theme.Info.A);
            Assert.AreEqual(255, theme.HeaderSurface.A);
            Assert.AreEqual(255, theme.Focus.A);
            Assert.AreEqual(255, theme.SelectionFill.A);
            Assert.AreEqual(255, theme.SelectionBorder.A);
            Assert.AreEqual(255, theme.DisabledFill.A);
            Assert.AreEqual(255, theme.DisabledText.A);
            // The modal scrim is the one deliberately translucent token.
            Assert.IsTrue(theme.Scrim.A is > 100 and < 230, $"{theme.Key}: the scrim must dim without hiding the page.");
        }
    }

    /// <summary>
    /// Contrast is measured from the real tokens, not eyeballed. Text meets WCAG AA (4.5:1) and body
    /// text meets AAA (7:1); status colours used as small text meet AA; focus rings and status marks
    /// meet the 3:1 graphical-object threshold. This is what stops a palette tweak from silently
    /// making a warning unreadable.
    /// </summary>
    [TestMethod]
    public void EveryPaletteKeepsTextStatusAndFocusReadable()
    {
        foreach (Theme theme in new[] { Theme.NeonInstance, Theme.SlateInstance, Theme.LightInstance })
        {
            AssertContrast(theme, "body text / page", theme.Text, theme.Background, 7.0);
            AssertContrast(theme, "body text / card", theme.Text, theme.Surface, 7.0);
            AssertContrast(theme, "muted text / page", theme.Muted, theme.Background, 4.5);
            AssertContrast(theme, "muted text / card", theme.Muted, theme.Surface, 4.5);
            AssertContrast(theme, "primary button label", theme.AccentText, theme.Accent, 4.5);
            AssertContrast(theme, "error text", theme.Error, theme.Surface, 4.5);
            AssertContrast(theme, "warning text", theme.Warn, theme.Surface, 3.0);
            AssertContrast(theme, "success mark", theme.Ok, theme.Surface, 3.0);
            AssertContrast(theme, "focus ring", theme.Focus, theme.Surface, 3.0);
            AssertContrast(theme, "focus ring on page", theme.Focus, theme.Background, 3.0);
        }
    }

    private static void AssertContrast(Theme theme, string pair, Color fore, Color back, double minimum)
    {
        double measured = Theme.Contrast(fore, back);
        Assert.IsTrue(measured >= minimum,
            $"{theme.Key}/{pair}: {fore.Name} on {back.Name} is {measured:N2}:1 (needs {minimum:N1}:1).");
    }

    [TestMethod]
    public void ForResolvesEachPalette()
    {
        Assert.AreSame(Theme.LightInstance, Theme.For(AppTheme.Light));
        Assert.AreSame(Theme.SlateInstance, Theme.For(AppTheme.Slate));
        Assert.AreSame(Theme.NeonInstance, Theme.For(AppTheme.Neon));
        Assert.IsFalse(ReferenceEquals(Theme.LightInstance, Theme.SlateInstance));
    }

    [TestMethod]
    public void SystemResolvesOnlyToALightOrDarkPalette()
    {
        Theme resolved = Theme.For(AppTheme.System);

        Assert.IsTrue(ReferenceEquals(resolved, Theme.LightInstance) || ReferenceEquals(resolved, Theme.NeonInstance),
            "System must resolve to one of the built-in palettes, never a synthetic one.");
    }

    [TestMethod]
    public void BlendMovesBetweenTheTwoColours()
    {
        Color black = Color.FromArgb(0, 0, 0);
        Color white = Color.FromArgb(255, 255, 255);

        Assert.AreEqual(white.ToArgb(), Theme.Blend(black, white, 0).ToArgb());
        Assert.AreEqual(black.ToArgb(), Theme.Blend(black, white, 1).ToArgb());
        Assert.AreEqual(Color.FromArgb(128, 128, 128).ToArgb(), Theme.Blend(black, white, 0.5).ToArgb());
    }
}
