using System.Drawing;
using System.Windows.Forms;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The diagnostics page is where an operator goes when the phone is plugged in and nothing happens, so
/// two things have to be true of it: the USB evidence has to be the first thing shown, and none of the
/// findings may be silently cut off. Both are pinned here on real controls.
/// </summary>
[TestClass]
public sealed class DiagnosticsPageTests : WinFormsTestBase
{
    private static T Find<T>(Control root, string name) where T : Control
    {
        foreach (Control control in Walk(root))
            if (control is T typed && control.Name == name) return typed;
        Assert.Fail($"Control '{name}' of type {typeof(T).Name} was not found.");
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

    private static DeviceInfo AbsentDevice() =>
        new(DeviceStatus.NotFound, "", "No Apple portable device is attached.");

    private static AttachmentReport CachedOnly() => AttachmentDiagnosis.Decide(
        new AttachmentEvidence([], ["Apple iPhone", "Apple iPad"], ["Apple Mobile Device USB Driver"]));

    private static DiagnosticsPage PageWith(IReadOnlyList<DiagnosticItem> items, Theme theme)
    {
        var page = new DiagnosticsPage();
        page.ApplyTheme(theme);
        page.SetDiagnostics(items);
        return page;
    }

    [TestMethod]
    public void TheUsbEvidenceIsTheFirstRowAndItsStatusIsShownWhole()
    {
        Theme theme = Theme.For(AppTheme.Light);
        using DiagnosticsPage page = PageWith(
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell", null, CachedOnly()), theme);
        DataGridView grid = Find<DataGridView>(page, "_diagnosticsGrid");

        Assert.AreEqual("Apple handsets seen over USB", grid.Rows[0].Cells[0].Value,
            "The operator's actual question - is it plugged in? - is answered before anything else.");
        Assert.AreEqual("CACHED RECORD ONLY (2)", grid.Rows[0].Cells[1].Value,
            "The status must say which of the two readings this is, because they are not the same thing.");
        string detail = Convert.ToString(grid.Rows[0].Cells[2].Value) ?? "";
        StringAssert.Contains(detail, "Apple iPhone");
        StringAssert.Contains(detail, "Apple iPad");
        StringAssert.Contains(detail, "not connected to the computer");
        Assert.AreEqual(theme.Warn.ToArgb(), grid.Rows[0].DefaultCellStyle.ForeColor.ToArgb(),
            "A machine that only remembers a handset is worth attention, not a failure and not a pass.");
    }

    [TestMethod]
    public void TheThreeVerdictsAreColouredDifferentlySoTheyCannotBeConfused()
    {
        Theme theme = Theme.For(AppTheme.Light);

        using DiagnosticsPage attached = PageWith(
            ConnectionDiagnostics.Collect(
                new DeviceInfo(DeviceStatus.MediaAvailable, "Apple iPhone", "Media is available."),
                "Windows Portable Devices", null,
                AttachmentDiagnosis.Decide(["Apple iPhone"], [], [])), theme);
        using DiagnosticsPage cached = PageWith(
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell", null, CachedOnly()), theme);
        using DiagnosticsPage none = PageWith(
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell", null,
                AttachmentDiagnosis.Decide(new AttachmentEvidence([], [], []))), theme);

        Assert.AreEqual("ATTACHED", Find<DataGridView>(attached, "_diagnosticsGrid").Rows[0].Cells[1].Value);
        Assert.AreEqual(theme.Ok.ToArgb(),
            Find<DataGridView>(attached, "_diagnosticsGrid").Rows[0].DefaultCellStyle.ForeColor.ToArgb());

        Assert.AreEqual(theme.Warn.ToArgb(),
            Find<DataGridView>(cached, "_diagnosticsGrid").Rows[0].DefaultCellStyle.ForeColor.ToArgb());

        Assert.AreEqual("NONE", Find<DataGridView>(none, "_diagnosticsGrid").Rows[0].Cells[1].Value);
        Assert.AreEqual(theme.Error.ToArgb(),
            Find<DataGridView>(none, "_diagnosticsGrid").Rows[0].DefaultCellStyle.ForeColor.ToArgb(),
            "Nothing ever seen over USB points at a missing driver or a charge-only cable.");
    }

    [TestMethod]
    public void FindingsAreWrappedRatherThanCutOffAtTheColumnEdge()
    {
        Theme theme = Theme.For(AppTheme.Light);
        using DiagnosticsPage page = PageWith(
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell", null, CachedOnly()), theme);

        DataGridView diagnostics = Find<DataGridView>(page, "_diagnosticsGrid");
        Assert.AreEqual(DataGridViewTriState.True, diagnostics.DefaultCellStyle.WrapMode,
            "The details are full sentences; unwrapped they are truncated with no indication that they are.");
        Assert.AreEqual(DataGridViewAutoSizeRowsMode.AllCells, diagnostics.AutoSizeRowsMode,
            "A wrapped row must be allowed to grow, or the text is simply clipped one line lower.");

        DataGridView dependencies = Find<DataGridView>(page, "_dependenciesGrid");
        Assert.AreEqual(DataGridViewTriState.True, dependencies.DefaultCellStyle.WrapMode);
        Assert.AreEqual(DataGridViewAutoSizeRowsMode.AllCells, dependencies.AutoSizeRowsMode);
    }

    [TestMethod]
    public void StatusesThatAreFineStillReadAsFine()
    {
        Theme theme = Theme.For(AppTheme.Light);
        using DiagnosticsPage page = PageWith(
        [
            new DiagnosticItem("Windows device access", "PASS", "Media storage was accessible during this probe."),
            new DiagnosticItem("Apple USB driver files", "FOUND", @"C:\Windows\System32\drivers\usbaapl64.sys"),
            new DiagnosticItem("Trust state", "CHECK IPHONE", "Unlock the iPhone and accept Trust if prompted.")
        ], theme);
        DataGridView grid = Find<DataGridView>(page, "_diagnosticsGrid");

        Assert.AreEqual(theme.Ok.ToArgb(), grid.Rows[0].DefaultCellStyle.ForeColor.ToArgb());
        Assert.AreEqual(theme.Ok.ToArgb(), grid.Rows[1].DefaultCellStyle.ForeColor.ToArgb());
        Assert.AreEqual(theme.Warn.ToArgb(), grid.Rows[2].DefaultCellStyle.ForeColor.ToArgb(),
            "A check that needs the operator's hands stays amber rather than reading as a failure.");
    }

    [TestMethod]
    public void CallersThatSupplyNoUsbEvidenceAreUnaffected()
    {
        IReadOnlyList<DiagnosticItem> items =
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell");

        Assert.IsFalse(items.Any(i => i.Name.Contains("handsets", StringComparison.OrdinalIgnoreCase)),
            "The row is added by the caller, so the check list is unchanged for callers that have no evidence.");
        Assert.AreEqual("Windows device access", items[0].Name);
    }

    [TestMethod]
    public void EveryFindingHasANameAStatusAndAnExplanation()
    {
        IReadOnlyList<DiagnosticItem> items =
            ConnectionDiagnostics.Collect(AbsentDevice(), "Windows Shell", null, CachedOnly());

        foreach (DiagnosticItem item in items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Status));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Details),
                $"'{item.Name}' would show a blank explanation, which reads as a check that did not run.");
        }
        Assert.IsTrue(items.Count >= 6, "A diagnosis with fewer checks than this cannot describe a failure.");
    }
}
