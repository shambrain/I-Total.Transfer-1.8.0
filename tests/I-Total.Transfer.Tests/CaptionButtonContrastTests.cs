using System.Drawing;
using ITotalTransfer.Core;
using ITotalTransfer.UI;
using ITotalTransfer.UI.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The caption buttons (minimize / maximize / close) are painted on the brand band, whose ramp is dark
/// in Neon and Slate but <b>light</b> in the Light theme. Their glyphs used to be a hard-coded near-white
/// (<c>#E8F0FF</c>), which on the Light theme's white band is a contrast ratio of about 1.06:1 — the
/// buttons were effectively invisible, exactly as reported. These tests pin a readable contrast on every
/// theme so the same mistake cannot come back with the next band palette.
///
/// <para>
/// Every WinForms control is created, styled and disposed on a dedicated STA thread. Constructing a
/// <see cref="Control"/> installs a <c>WindowsFormsSynchronizationContext</c> on the calling thread, whose
/// <c>Post</c> only ever runs inside a message pump. On a test thread there is no pump, so a later
/// <c>await</c> in an unrelated test — <c>TransferEngineTests</c>, for example — has its continuation
/// posted to a queue that is never drained and the run hangs. Keeping the control work on a thread that
/// dies with the test leaves the runner's own context untouched, and disposing the band releases its
/// handles and fonts.
/// </para>
/// </summary>
[TestClass]
public sealed class CaptionButtonContrastTests : WinFormsTestBase
{
    /// <summary>WCAG AA for non-text/graphical objects is 3:1.</summary>
    private const double MinimumContrast = 3.0;

    [TestMethod]
    [DataRow(AppTheme.Neon)]
    [DataRow(AppTheme.Slate)]
    [DataRow(AppTheme.Light)]
    public void CaptionGlyphsStayReadableOnTheBandOfEveryTheme(AppTheme appTheme)
    {
        List<(string Name, Color Glyph, Color Band, double Contrast)> measured = OnStaThread(() =>
        {
            Theme theme = Theme.For(appTheme);
            using var band = new BrandBand();
            using var minimize = new RoundedButton { Tag = "chrome" };
            using var maximize = new RoundedButton { Tag = "chrome" };
            using var close = new RoundedButton { Tag = "chrome close" };
            band.Controls.Add(minimize);
            band.Controls.Add(maximize);
            band.Controls.Add(close);

            theme.ApplyAll(band);
            Color host = band.MidColor;

            var results = new List<(string, Color, Color, double)>();
            foreach ((string name, RoundedButton button) in new[]
                     {
                         ("minimize", minimize), ("maximize", maximize), ("close", close)
                     })
            {
                results.Add((name, button.ForeColor, host, Contrast(button.ForeColor, host)));
            }
            return results;
        });

        foreach ((string name, Color glyph, Color bandColor, double contrast) in measured)
        {
            Assert.IsTrue(contrast >= MinimumContrast,
                $"{appTheme}/{name}: glyph {glyph.Name} on band {bandColor.Name} " +
                $"is only {contrast:N2}:1 (needs {MinimumContrast:N1}:1).");
        }
    }

    [TestMethod]
    public void TheLightThemeStillHasALightBandSoThisTestIsWorthHaving()
    {
        // Guards the premise: if the Light theme ever gains a dark band, the contrast test above would
        // pass for the wrong reason and stop protecting anything.
        Theme light = Theme.LightInstance;
        Assert.IsFalse(light.IsDark);
        Assert.IsTrue(Luminance(light.HeaderSurface) > 0.5);
    }

    /// <summary>
    /// Runs WinForms control work on its own STA thread so the test runner's synchronization context is
    /// never replaced by a Windows Forms one, and fails loudly rather than blocking the whole run.
    /// </summary>
    private static T OnStaThread<T>(Func<T> work)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = ex; }
        })
        {
            IsBackground = true,
            Name = "caption-contrast-sta"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)),
            "The WinForms control work never finished: the STA thread hung.");
        if (failure is not null)
            throw new AssertFailedException($"WinForms control work failed: {failure.Message}", failure);
        return result!;
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        double lighter = Math.Max(la, lb), darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte value)
        {
            double s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
