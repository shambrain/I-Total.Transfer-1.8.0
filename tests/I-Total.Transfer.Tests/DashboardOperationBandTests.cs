using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Overview band is the operator's only view of a scan that runs for minutes, so its contract is
/// pinned here: it sweeps while the work is unmeasured, becomes determinate only with a real total, and
/// names the phase so a scan and a copy are never confused for one another.
/// </summary>
[TestClass]
public sealed class DashboardOperationBandTests : WinFormsTestBase
{
    [TestMethod]
    public void TheBandSweepsWhileUnmeasuredAndCountsOnceATotalExists()
    {
        using var page = new DashboardPage();
        FluentProgressBar bar = FindBar(page);

        page.SetOperation(new OperationStatus(OperationPhase.Scanning, "Scanning 202608_a"));
        Assert.IsTrue(bar.IsIndeterminate, "An unmeasured scan must sweep rather than invent a percentage.");

        page.SetOperation(new OperationStatus(OperationPhase.Transferring, "Copying…",
            Completed: 400, Total: 1000, Indeterminate: false));
        Assert.IsFalse(bar.IsIndeterminate, "A measured copy must stop sweeping and show real progress.");
        Assert.AreEqual(1000, bar.Maximum);
        Assert.AreEqual(400, bar.Value);
    }

    [TestMethod]
    public void TheBandNamesThePhaseAndCarriesTheDetailLine()
    {
        using var page = new DashboardPage();

        page.SetOperation(new OperationStatus(OperationPhase.Scanning, "Scanning 202608_a · 12,480 files"));
        StringAssert.Contains(Headings(page)[0], "Scanning");
        Assert.IsTrue(Texts(page).Any(t => t.Contains("12,480 files", StringComparison.Ordinal)),
            "The detail line must reach the page, otherwise the operator sees a heading and nothing else.");

        page.SetOperation(OperationStatus.Idle);
        StringAssert.Contains(Headings(page)[0], "Current operation");
    }

    [TestMethod]
    public void AnIdleBandShowsNothingRatherThanZeroPercent()
    {
        using var page = new DashboardPage();
        FluentProgressBar bar = FindBar(page);

        page.SetOperation(new OperationStatus(OperationPhase.Transferring, "Copying…",
            Completed: 700, Total: 1000, Indeterminate: false));
        Assert.AreEqual(700, bar.Value);

        page.SetOperation(OperationStatus.Idle);
        Assert.IsFalse(bar.IsIndeterminate, "Idle must not animate.");
        Assert.AreEqual(0, bar.Value, "Idle must not leave the last job's progress on screen.");
    }

    private static IEnumerable<Control> Walk(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grand in Walk(child)) yield return grand;
        }
    }

    private static FluentProgressBar FindBar(Control root)
    {
        FluentProgressBar[] bars = Walk(root).OfType<FluentProgressBar>().ToArray();
        Assert.AreEqual(1, bars.Length, "The Overview must show exactly one operation band.");
        return bars[0];
    }

    /// <summary>The heading labels, found by their position above the bar rather than by field name.</summary>
    private static List<string> Headings(Control root)
    {
        FluentProgressBar bar = FindBar(root);
        return Texts(root)
            .Where(t => t is "Scanning" or "Transferring" or "Connecting" or "Paused" or "Stopped"
                       or "Current operation")
            .DefaultIfEmpty(bar.Name)
            .ToList();
    }

    private static IEnumerable<string> Texts(Control root) =>
        Walk(root).OfType<Label>().Select(l => l.Text ?? "");
}
