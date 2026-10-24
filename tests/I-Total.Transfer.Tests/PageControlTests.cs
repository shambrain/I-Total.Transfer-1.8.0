using System.Drawing;
using System.Windows.Forms;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Pins the page-level wiring the UI-18 audit exercised by hand: every visible toolbar control must
/// raise a real request (or be deliberately disabled by its page), and the grid/summary methods used
/// by the shell must display what they are given. These are component tests; they do not claim that a
/// handler in <c>MainForm</c> succeeded against a real device.
/// </summary>
[TestClass]
public sealed class PageControlTests : WinFormsTestBase
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

    [TestMethod]
    public void DashboardShowsAPtpWarningInTheMediaCard()
    {
        using var page = new DashboardPage();
        page.SetMediaHint("Camera-only connection (PTP): only DCIM and Pictures are visible.");
        Assert.AreEqual("Camera-only connection (PTP): only DCIM and Pictures are visible.",
            Find<Label>(page, "_mediaHint").Text);
    }

    [TestMethod]
    public void TransferToolbarRaisesItsRequestsAndClearViewOnlyClearsTheConsole()
    {
        using var page = new TransferPage();
        int destination = 0, log = 0, export = 0, clear = 0;
        page.OpenDestinationRequested += (_, _) => destination++;
        page.OpenLogRequested += (_, _) => log++;
        page.ExportReportRequested += (_, _) => export++;
        page.ClearLogRequested += (_, _) => { clear++; page.ClearLog(); };

        Find<RoundedButton>(page, "_openDestinationButton").PerformClick();
        Find<RoundedButton>(page, "_openLogButton").PerformClick();
        Find<RoundedButton>(page, "_exportReportButton").PerformClick();

        page.AppendLog("first line", "INFO");
        page.AppendLog("second line", "WARN");
        StringAssert.Contains(page.LogText, "second line");

        RoundedButton clearButton = Find<RoundedButton>(page, "_clearLogButton");
        Assert.AreEqual("Clear view", clearButton.Text);
        StringAssert.Contains(clearButton.AccessibleDescription ?? "", "console");
        clearButton.PerformClick();

        Assert.AreEqual(1, destination);
        Assert.AreEqual(1, log);
        Assert.AreEqual(1, export);
        Assert.AreEqual(1, clear);
        Assert.AreEqual("", page.LogText);
    }

    [TestMethod]
    public void LibraryChipsApplyFilterAndReportEveryAction()
    {
        using var page = new LibraryPage();
        int applied = 0, verify = 0, export = 0;
        page.ApplyFilterRequested += (_, _) => applied++;
        page.VerifyRequested += (_, _) => verify++;
        page.ExportScanRequested += (_, _) => export++;

        Find<RoundedButton>(page, "_chipPhotos").PerformClick();
        Assert.AreEqual("Photos", page.KindFilter);
        Assert.AreEqual(1, applied);

        Find<TextBox>(page, "_filterBox").Text = "beach";
        Find<RoundedButton>(page, "_applyButton").PerformClick();
        Assert.AreEqual("beach", page.FilterText);
        Assert.AreEqual(2, applied);

        Find<FluentCheckBox>(page, "_repairCheck").Checked = true;
        Assert.IsTrue(page.RepairRequested);

        page.SetFiles(
        [
            new MediaFile("DCIM", "IMG_1.HEIC", 1200, ".heic"),
            new MediaFile("DCIM", "IMG_2.MOV", 2400, ".mov")
        ], totalDiscovered: 5);

        Assert.AreEqual(2, Find<DataGridView>(page, "_grid").Rows.Count);
        StringAssert.Contains(Find<Label>(page, "_summaryLabel").Text, "2 of 5");

        page.SetFiles([], totalDiscovered: 0);
        string emptyText = string.Join(" ", Walk(Find<EmptyState>(page, "_emptyState")).OfType<Label>().Select(label => label.Text));
        StringAssert.Contains(emptyText, "Scan device");
        StringAssert.Contains(emptyText, "If no phone is attached");

        Find<RoundedButton>(page, "_verifyButton").PerformClick();
        Find<RoundedButton>(page, "_exportScanButton").PerformClick();
        Assert.AreEqual(1, verify);
        Assert.AreEqual(1, export);
    }

    [TestMethod]
    public void HistoryShowsRowsAndRaisesRefreshAndOpenDestination()
    {
        using var page = new HistoryPage();
        int refresh = 0, open = 0;
        page.RefreshRequested += (_, _) => refresh++;
        page.OpenDestinationRequested += (_, _) => open++;

        Find<RoundedButton>(page, "_refreshButton").PerformClick();
        Find<RoundedButton>(page, "_openDestinationButton").PerformClick();
        Assert.AreEqual(1, refresh);
        Assert.AreEqual(1, open);

        page.SetDatabasePath(@"C:\Backup\.i-total\session.db");
        StringAssert.Contains(Find<Label>(page, "_databaseLabel").Text, "session.db");

        page.SetSessions(
        [
            new SessionHistoryRow("0123456789abcdef0123", "Apple iPhone", @"C:\Backup",
                "2026-10-22 08:00:00", "2026-10-22 08:01:00", "Completed")
        ]);
        Assert.AreEqual(1, Find<DataGridView>(page, "_sessionsGrid").Rows.Count);
        StringAssert.Contains(Find<Label>(page, "_sessionsTitle").Text, "1");

        page.SetProblems(
        [
            new FileHistoryRow("DCIM/IMG_1.HEIC", 1200, "Failed", 2, "Device disconnected")
        ]);
        Assert.AreEqual(1, Find<DataGridView>(page, "_problemsGrid").Rows.Count);
        StringAssert.Contains(Find<Label>(page, "_problemsTitle").Text, "1");
    }

    [TestMethod]
    public void DiagnosticsToolbarRaisesAllFourRequestsAndRendersChecks()
    {
        using var page = new DiagnosticsPage();
        int run = 0, export = 0, guide = 0, devices = 0;
        page.RunRequested += (_, _) => run++;
        page.ExportBundleRequested += (_, _) => export++;
        page.OpenAppleGuideRequested += (_, _) => guide++;
        page.OpenAppleDevicesRequested += (_, _) => devices++;

        Find<RoundedButton>(page, "_runButton").PerformClick();
        Find<RoundedButton>(page, "_exportButton").PerformClick();
        Find<RoundedButton>(page, "_supportButton").PerformClick();
        Find<RoundedButton>(page, "_appleButton").PerformClick();
        Assert.AreEqual(1, run);
        Assert.AreEqual(1, export);
        Assert.AreEqual(1, guide);
        Assert.AreEqual(1, devices);

        page.SetSummary("No phone attached.");
        Assert.AreEqual("No phone attached.", Find<Label>(page, "_summaryLabel").Text);

        page.SetDiagnostics(
        [
            new DiagnosticItem("USB device", "NONE", "No Apple or Android handset was found.")
        ]);
        Assert.AreEqual(1, Find<DataGridView>(page, "_diagnosticsGrid").Rows.Count);
        StringAssert.Contains(Find<Label>(page, "_diagnosticsTitle").Text, "1 check");

        var optional = new NativeDependency("libfoo", "libfoo.dll", "1.0", "x64", "MIT", "", "", Required: false);
        page.SetDependencies(new DependencyReport(
            [new DependencyFileStatus(optional, DependencyFileState.Missing, "Native binary is not present.")],
            "native"));
        string dependencyTitle = Find<Label>(page, "_dependenciesTitle").Text;
        StringAssert.Contains(dependencyTitle, "no native runtime verified");
        Assert.IsFalse(dependencyTitle.Contains("required dependencies verified", StringComparison.OrdinalIgnoreCase),
            "An empty required set must not be displayed as a verified native runtime.");
    }

    [TestMethod]
    public void WirelessTransportButtonsAreEnabledByStateAndRaiseRequests()
    {
        using var page = new WirelessPage();
        int start = 0, stop = 0, pair = 0;
        page.StartRequested += (_, _) => start++;
        page.StopRequested += (_, _) => stop++;
        page.PairRequested += (_, _) => pair++;

        RoundedButton startButton = Find<RoundedButton>(page, "_startButton");
        RoundedButton stopButton = Find<RoundedButton>(page, "_stopButton");
        RoundedButton pairButton = Find<RoundedButton>(page, "_pairButton");
        Assert.IsFalse(startButton.Enabled);
        Assert.IsFalse(stopButton.Enabled);
        Assert.IsFalse(pairButton.Enabled);

        Find<FluentCheckBox>(page, "_enableCheck").Checked = true;
        Assert.IsTrue(startButton.Enabled);
        startButton.PerformClick();

        page.SetRunning(true, "http://192.168.8.91:47821/", tls: false);
        Assert.IsFalse(startButton.Enabled);
        Assert.IsTrue(stopButton.Enabled);
        Assert.IsTrue(pairButton.Enabled);
        stopButton.PerformClick();
        pairButton.PerformClick();

        Assert.AreEqual(1, start);
        Assert.AreEqual(1, stop);
        Assert.AreEqual(1, pair);
        // The fallback caption must not imply that a certificate exists when none was found.
        StringAssert.Contains(Find<Label>(page, "_statusLabel").Text, "no certificate");
        StringAssert.Contains(Find<FluentCheckBox>(page, "_tlsCheck").Text, "matching certificate");
    }

    [TestMethod]
    public void WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth()
    {
        // A 96-DPI render of the real shell showed the caption running past the settings card, cutting
        // the closing bracket. The caption is measured as it is painted, not from the Designer hint.
        using var page = new WirelessPage { Size = new Size(1100, 640) };
        page.PerformLayout();
        FluentCheckBox tls = Find<FluentCheckBox>(page, "_tlsCheck");
        Control card = tls.Parent ?? throw new InvalidOperationException("The TLS caption has no card.");
        int rendered = TextRenderer.MeasureText(tls.Text, tls.Font).Width + 18; // 18 px reserves the check glyph.
        int available = card.Width - (tls.Left * 2); // the same inset on the right edge as on the left.
        Assert.IsTrue(rendered <= available,
            $"\"{tls.Text}\" needs {rendered} px but only {available} px fit in {card.Name} ({card.Width} px wide).");
    }

    [TestMethod]
    public void TheWirelessCardReflowsAtTheMinimumWindowWidth()
    {
        // The Designer sizes the card for a 450 px column; the shell's minimum window leaves about 360.
        using var page = new WirelessPage { Size = new Size(884, 532) };
        page.PerformLayout();
        CardPanel card = Find<CardPanel>(page, "_settingsCard");
        FluentCheckBox tls = Find<FluentCheckBox>(page, "_tlsCheck");
        FluentCheckBox auto = Find<FluentCheckBox>(page, "_autoApproveCheck");
        RoundedButton start = Find<RoundedButton>(page, "_startButton");
        RoundedButton stop = Find<RoundedButton>(page, "_stopButton");
        RoundedButton pair = Find<RoundedButton>(page, "_pairButton");

        Assert.IsTrue(tls.Height > tls.Font.Height,
            "The TLS caption must wrap onto a second line instead of running past the card.");
        Assert.IsTrue(auto.Height > auto.Font.Height,
            "The auto-approval caption must wrap as well.");
        Assert.IsTrue(start.Right < stop.Left && stop.Right < pair.Left,
            "The three transport buttons must not overlap.");
        Assert.IsTrue(pair.Right <= card.ClientSize.Width,
            $"The New ticket button ends at {pair.Right} in a {card.ClientSize.Width} px card.");
        foreach (RoundedButton button in new[] { start, stop, pair })
            Assert.IsTrue(TextRenderer.MeasureText(button.Text, button.Font).Width + 8 <= button.Width,
                $"'{button.Text}' must fit inside its {button.Width} px button.");
        Assert.IsTrue(card.AutoScroll, "A short window must be able to scroll to the sender notice.");
        Assert.IsTrue(card.AutoScrollMinSize.Height >= card.ClientSize.Height,
            "The notice defines a scrollable extent taller than the visible card.");
    }

    [TestMethod]
    public void TheLibraryToolbarWrapsInsteadOfOverlappingAtTheMinimumWidth()
    {
        using var page = new LibraryPage { Size = new Size(884, 532) };
        page.PerformLayout();
        CardPanel card = Find<CardPanel>(page, "_toolbarCard");
        FlowLayoutPanel kind = Find<FlowLayoutPanel>(page, "_kindRow");
        FluentCheckBox repair = Find<FluentCheckBox>(page, "_repairCheck");
        RoundedButton apply = Find<RoundedButton>(page, "_applyButton");
        TextBox filter = Find<TextBox>(page, "_filterBox");

        Assert.IsTrue(card.Height > 70, "The toolbar must take two rows when one cannot hold it.");
        Assert.IsTrue(kind.Bottom <= card.ClientSize.Height, "The group chips must stay inside the card.");
        Assert.IsTrue(repair.Left > kind.Right && repair.Right <= card.ClientSize.Width,
            "Repair missing must follow the chips, not sit on top of them.");
        Assert.IsTrue(apply.Right <= card.ClientSize.Width, "Apply must stay inside the card.");
        Assert.IsTrue(apply.Left > filter.Right, "Apply must not cover the filter box.");
    }

    [TestMethod]
    public void TheHistoryToolbarGrowsSoTheDatabasePathStaysVisible()
    {
        using var page = new HistoryPage { Size = new Size(884, 532) };
        page.PerformLayout();
        FlowLayoutPanel toolbar = Find<FlowLayoutPanel>(page, "_toolbar");
        Label database = Find<Label>(page, "_databaseLabel");

        Assert.IsTrue(toolbar.Height > 46, "The toolbar must grow to a second row for the database path.");
        Assert.IsTrue(database.Bottom <= toolbar.ClientSize.Height,
            "The database path must render inside the toolbar rather than behind the sessions card.");
    }

    [TestMethod]
    public void TheTransferLogToolbarWrapsInsteadOfHidingClearView()
    {
        using var page = new TransferPage { Size = new Size(884, 532) };
        page.PerformLayout();
        FlowLayoutPanel toolbar = Find<FlowLayoutPanel>(page, "_logToolbar");
        RoundedButton clear = Find<RoundedButton>(page, "_clearLogButton");

        Assert.AreEqual(4, toolbar.Controls.Count);
        Assert.IsTrue(toolbar.Height > 34, "Four log actions need a second row at this width.");
        Assert.IsTrue(clear.Bottom <= toolbar.ClientSize.Height,
            "Clear view must be inside the grown toolbar, not clipped by the log box.");
    }

}
