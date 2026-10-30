using System.Drawing;
using System.Windows.Forms;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Wireless page's contract, pinned without a sender. These are the rules an operator relies on:
/// Start cannot be pressed while wireless is switched off, a ticket is shown as both a URL and a QR
/// image, the approval list shows exactly what is waiting, and the decision buttons are enabled only
/// when they can do something.
/// </summary>
[TestClass]
public sealed class WirelessPageTests : WinFormsTestBase
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

    private static OfferRecord Offer(string path, long bytes = 4096) => new(
        new IncomingOffer(path, bytes, null, "192.168.1.9 (Shortcut/1.0)", DateTimeOffset.UtcNow),
        OfferState.Pending, "Waiting for the operator to approve.");

    [TestMethod]
    public void StartIsDisabledUntilWirelessIsEnabled()
    {
        using var page = new WirelessPage();
        var start = Find<RoundedButton>(page, "_startButton");
        var enable = Find<FluentCheckBox>(page, "_enableCheck");

        enable.Checked = false;
        Assert.IsFalse(start.Enabled, "A receiver that is switched off must not offer Start.");

        enable.Checked = true;
        Assert.IsTrue(start.Enabled);
    }

    [TestMethod]
    public void ATicketShowsTheUrlAndAScannableQrImage()
    {
        using var page = new WirelessPage();
        string url = "http://192.168.1.24:47821/pair#token=" + PairingState.NewToken();
        var ticket = new PairingTicket(
            PairingState.NewToken(), DateTimeOffset.UtcNow.AddMinutes(5), url, QrCode.Encode(url));

        page.SetTicket(ticket);

        Assert.AreEqual(url, Find<TextBox>(page, "_pairUrlBox").Text);
        var qr = Find<PictureBox>(page, "_qrBox");
        Assert.IsTrue(qr.Visible, "The QR must be shown, not merely built.");
        Assert.IsNotNull(qr.Image);
        Assert.IsTrue(qr.Image!.Width > 100 && qr.Image.Height == qr.Image.Width, "A QR image is square and large enough to scan.");
        StringAssert.Contains(Find<Label>(page, "_ticketStateLabel").Text, "Single use");
    }

    [TestMethod]
    public void ClearingTheTicketHidesTheQr()
    {
        using var page = new WirelessPage();
        string url = "http://192.168.1.24:47821/pair#token=" + PairingState.NewToken();
        page.SetTicket(new PairingTicket(PairingState.NewToken(), DateTimeOffset.UtcNow.AddMinutes(5), url, QrCode.Encode(url)));

        page.SetTicket(null);

        var qr = Find<PictureBox>(page, "_qrBox");
        Assert.IsFalse(qr.Visible);
        Assert.IsNull(qr.Image);
        Assert.AreEqual("", Find<TextBox>(page, "_pairUrlBox").Text);
    }

    [TestMethod]
    public void ARedeemedTicketWithdrawsTheUrlAndTheCode()
    {
        using var page = new WirelessPage();
        string url = "http://192.168.1.24:47821/pair#token=" + PairingState.NewToken();
        page.SetTicket(new PairingTicket(PairingState.NewToken(), DateTimeOffset.UtcNow.AddMinutes(5), url, QrCode.Encode(url)));

        page.MarkTicketSpent();

        Assert.IsFalse(Find<PictureBox>(page, "_qrBox").Visible,
            "A spent secret must not leave a code that looks scannable but cannot work.");
        Assert.IsNull(Find<PictureBox>(page, "_qrBox").Image);
        Assert.AreEqual("", Find<TextBox>(page, "_pairUrlBox").Text);
        StringAssert.Contains(Find<Label>(page, "_ticketStateLabel").Text, "withdrawn");
    }

    [TestMethod]
    public void TheApprovalListShowsExactlyTheWaitingFiles()
    {
        using var page = new WirelessPage();

        page.SetPending([Offer("DCIM/IMG_1.HEIC"), Offer("DCIM/IMG_2.MOV", 2_000_000)]);

        var grid = Find<DataGridView>(page, "_pendingGrid");
        Assert.AreEqual(2, grid.Rows.Count);
        Assert.AreEqual("DCIM/IMG_1.HEIC", grid.Rows[0].Cells[0].Value);
        Assert.AreEqual("4,096", grid.Rows[0].Cells[1].Value);
        Assert.AreEqual("192.168.1.9 (Shortcut/1.0)", grid.Rows[0].Cells[2].Value);
        StringAssert.Contains(Find<Label>(page, "_pendingTitle").Text, "2");
    }

    [TestMethod]
    public void DecidedOffersAreNotListedAsWaiting()
    {
        using var page = new WirelessPage();
        var approved = new OfferRecord(
            new IncomingOffer("DCIM/DONE.HEIC", 1, null, "a", DateTimeOffset.UtcNow),
            OfferState.Approved, "Approved by the operator.");

        page.SetPending([approved]);

        Assert.AreEqual(0, Find<DataGridView>(page, "_pendingGrid").Rows.Count);
        StringAssert.Contains(Find<Label>(page, "_pendingTitle").Text, "none");
    }

    [TestMethod]
    public void TheDecisionButtonsAreEnabledOnlyWhenTheyCanAct()
    {
        using var page = new WirelessPage();
        var approve = Find<RoundedButton>(page, "_approveButton");
        var refuse = Find<RoundedButton>(page, "_refuseButton");
        var approveAll = Find<RoundedButton>(page, "_approveAllButton");

        page.SetPending([]);
        Assert.IsFalse(approve.Enabled, "Nothing is selected, so Approve cannot act.");
        Assert.IsFalse(refuse.Enabled);
        Assert.IsFalse(approveAll.Enabled, "Nothing is waiting, so Approve all cannot act.");

        page.SetPending([Offer("DCIM/IMG_1.HEIC")]);
        Assert.IsTrue(approveAll.Enabled);
    }

    [TestMethod]
    public void ChoosingApproveRaisesTheSelectedPath()
    {
        using var page = new WirelessPage();
        string? raised = null;
        page.ApproveRequested += path => raised = path;
        page.SetPending([Offer("DCIM/IMG_1.HEIC"), Offer("DCIM/IMG_2.HEIC")]);
        var grid = Find<DataGridView>(page, "_pendingGrid");
        grid.Rows[1].Selected = true;

        Find<RoundedButton>(page, "_approveButton").PerformClick();

        Assert.AreEqual("DCIM/IMG_2.HEIC", raised);
    }

    [TestMethod]
    public void TheAutoApproveSwitchIsReportedWhenItChanges()
    {
        using var page = new WirelessPage();
        bool? reported = null;
        page.AutoApproveChanged += value => reported = value;

        Find<FluentCheckBox>(page, "_autoApproveCheck").Checked = true;

        Assert.IsTrue(reported, "A switch that silently does nothing is worse than no switch.");
    }

    [TestMethod]
    public void StoppingClearsTheTicketAndTheApprovalList()
    {
        using var page = new WirelessPage();
        string url = "http://192.168.1.24:47821/pair#token=" + PairingState.NewToken();
        page.SetTicket(new PairingTicket(PairingState.NewToken(), DateTimeOffset.UtcNow.AddMinutes(5), url, QrCode.Encode(url)));
        page.SetPending([Offer("DCIM/IMG_1.HEIC")]);

        page.SetRunning(false, "", false);

        Assert.AreEqual(0, Find<DataGridView>(page, "_pendingGrid").Rows.Count);
        Assert.IsFalse(Find<PictureBox>(page, "_qrBox").Visible);
    }

    [TestMethod]
    public void SettingsRoundTripThroughThePage()
    {
        using var page = new WirelessPage();
        AppSettings settings = AppSettings.Defaults();
        settings.WirelessEnabled = true;
        settings.WirelessPort = 51234;
        settings.PairingTokenMinutes = 42;
        settings.WirelessRequireTls = false;
        settings.WirelessAutoApprove = true;

        page.LoadSettings(settings);

        Assert.IsTrue(page.WirelessEnabled);
        Assert.AreEqual(51234, page.Port);
        Assert.AreEqual(42, page.TokenMinutes);
        Assert.IsFalse(page.RequireTls);
        Assert.IsTrue(page.AutoApprove);
    }
}
