using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Overview's unfinished-work bar is the only place an operator learns that the last backup to their
/// chosen folder did not finish - and the only place that can offer to continue it without walking the
/// camera roll again. Its contract is pinned here: it appears only when the folder really holds unfinished
/// work, it quotes the folder's own numbers, it never offers a button that cannot work, and an unreadable
/// record is reported rather than swallowed.
/// </summary>
[TestClass]
public sealed class DashboardRecoveryTests : WinFormsTestBase
{
    private static RecoveryState Interrupted() => new()
    {
        HasSession = true,
        Destination = @"C:\backup",
        DeviceName = "Apple iPhone",
        BackendName = "Windows Portable Devices (native)",
        UpdatedUtc = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
        Status = "RUNNING",
        Total = 26_335,
        Verified = 1_203,
        Pending = 24_876,
        Failed = 12,
        Inaccessible = 4,
        VerifiedBytes = 4_800_000_000,
        OutstandingBytes = 100_000_000_000
    };

    [TestMethod]
    public void AFinishedFolderCostsNoSpaceAndShowsNoBar()
    {
        using var page = new DashboardPage();
        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        TableLayoutPanel root = Find<TableLayoutPanel>(page, "_root");

        Assert.IsFalse(bar.Visible, "The bar must not be in the way when there is nothing to continue.");
        Assert.AreEqual(0F, root.RowStyles[3].Height, "A hidden bar must collapse its row to nothing, not leave a gap.");
        Assert.AreEqual(page.LogicalToDeviceUnits(742), page.AutoScrollMinSize.Height,
            "The anti-clipping floor must not count a row that is not there.");
    }

    [TestMethod]
    public void AnUnfinishedSessionQuotesItsOwnNumbersAndOffersToContinue()
    {
        using var page = new DashboardPage();
        page.SetRecovery(Interrupted(), deviceReady: true, "");

        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        TableLayoutPanel root = Find<TableLayoutPanel>(page, "_root");
        RoundedButton action = Find<RoundedButton>(bar, "_action");
        RoundedButton dismiss = Find<RoundedButton>(bar, "_dismiss");

        Assert.IsTrue(bar.Visible);
        Assert.AreEqual((float)page.LogicalToDeviceUnits(94), root.RowStyles[3].Height);
        Assert.AreEqual(page.LogicalToDeviceUnits(742 + 94), page.AutoScrollMinSize.Height,
            "A shown bar must be inside the scroll floor, or it is clipped.");

        StringAssert.Contains(bar.Title, "interrupted");
        StringAssert.Contains(bar.Message, "Apple iPhone");
        StringAssert.Contains(bar.Message, "26,335 planned");
        StringAssert.Contains(bar.Message, "1,203 copied and verified");
        StringAssert.Contains(bar.Message, "24,892 outstanding");
        StringAssert.Contains(bar.Message, "12 failed");
        StringAssert.Contains(bar.Message, "4 unreadable");
        StringAssert.Contains(bar.Message, "Only outstanding files are copied");

        Assert.AreEqual("Resume transfer", action.Text);
        Assert.IsTrue(action.Visible);
        Assert.IsTrue(action.Enabled);
        Assert.AreEqual("Not now", dismiss.Text);
        Assert.IsTrue(dismiss.Visible);
    }

    [TestMethod]
    public void WithoutAnIphoneTheOfferStaysVisibleButCannotBePressed()
    {
        using var page = new DashboardPage();
        page.SetRecovery(Interrupted(), deviceReady: false, "");

        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        RoundedButton action = Find<RoundedButton>(bar, "_action");

        Assert.IsTrue(action.Visible, "Hiding the action would hide the offer; it has to stay on screen and explain itself.");
        Assert.IsFalse(action.Enabled);
        StringAssert.Contains(bar.Message, "Connect and unlock");
    }

    [TestMethod]
    public void AnUnreadableRecordIsReportedAndOffersNoAction()
    {
        using var page = new DashboardPage();
        page.SetRecovery(new RecoveryState { Destination = @"C:\backup", Problem = "The session record could not be read (JsonException); it was left untouched." },
            deviceReady: true, "");

        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        RoundedButton action = Find<RoundedButton>(bar, "_action");
        TableLayoutPanel root = Find<TableLayoutPanel>(page, "_root");

        Assert.IsTrue(bar.Visible);
        Assert.AreEqual((float)page.LogicalToDeviceUnits(94), root.RowStyles[3].Height);
        Assert.IsFalse(action.Visible, "There is nothing to continue, so there is no action to offer.");
        StringAssert.Contains(bar.Message, "left untouched");
    }

    [TestMethod]
    public void TellingThePageToHideTheBarCollapsesTheRowAgain()
    {
        using var page = new DashboardPage();
        TableLayoutPanel root = Find<TableLayoutPanel>(page, "_root");

        page.SetRecovery(Interrupted(), deviceReady: true, "");
        Assert.AreEqual((float)page.LogicalToDeviceUnits(94), root.RowStyles[3].Height);

        page.SetRecovery(null, deviceReady: true, "");
        Assert.AreEqual(0F, root.RowStyles[3].Height);
        Assert.AreEqual(page.LogicalToDeviceUnits(742), page.AutoScrollMinSize.Height);
    }

    /// <summary>
    /// The row the bar occupies must be paid for by the page, never by the action panel at the bottom:
    /// a squeezed action panel slices its own integration line in half, which is exactly the clipping the
    /// scroll floor exists to prevent. This pins the invariant at a window height shorter than the stack,
    /// which is where the squeeze used to happen.
    /// </summary>
    [TestMethod]
    public void TheOfferNeverSqueezesTheActionPanel()
    {
        using var page = new DashboardPage { Size = new System.Drawing.Size(1076, 560) };
        TableLayoutPanel root = Find<TableLayoutPanel>(page, "_root");
        CardPanel actions = Find<CardPanel>(page, "_actionsCard");

        page.SetRecovery(null, deviceReady: true, "");
        page.PerformLayout();
        Assert.IsTrue(root.Height >= page.LogicalToDeviceUnits(742),
            $"The stack needs 742 logical px even with no bar; the table was {root.Height}.");
        Assert.IsTrue(actions.Height >= page.LogicalToDeviceUnits(232),
            $"The action panel was squeezed to {actions.Height} px with no bar.");

        page.SetRecovery(Interrupted(), deviceReady: true, "");
        page.PerformLayout();
        Assert.IsTrue(root.Height >= page.LogicalToDeviceUnits(742 + 94),
            $"Showing the bar must grow the stack to 836 logical px; the table was {root.Height}.");
        Assert.IsTrue(actions.Height >= page.LogicalToDeviceUnits(232),
            $"The action panel was squeezed to {actions.Height} px by the recovery row.");
        Assert.AreEqual(page.LogicalToDeviceUnits(836), page.AutoScrollMinSize.Height);
        Assert.AreEqual(page.LogicalToDeviceUnits(836), root.MinimumSize.Height);

        // The row and the floor are logical units that must be converted for the display, exactly like the
        // designer's own values. Read as raw device pixels at 150% scaling, the row came out shorter than the
        // bar's own content and the second line of the message was painted past the bottom of the card.
        Assert.AreEqual(page.LogicalToDeviceUnits(94), (int)Math.Round(root.RowStyles[3].Height),
            "The row must be measured the way the rest of the page measures: for the display it is on.");
        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        bar.PerformLayout();
        Label message = Find<Label>(bar, "_message");
        Assert.IsTrue(message.Bottom <= bar.ClientSize.Height,
            $"The two-line message (bottom {message.Bottom}) must fit inside the bar ({bar.ClientSize.Height} tall).");
    }

    [TestMethod]
    public void TheActionAndTheDismissalReportThemselvesSeparately()
    {
        using var page = new DashboardPage();
        int resumes = 0, dismissals = 0;
        page.RecoveryResumeRequested += (_, _) => resumes++;
        page.RecoveryDismissRequested += (_, _) => dismissals++;
        page.SetRecovery(Interrupted(), deviceReady: true, "");

        InfoBar bar = Find<InfoBar>(page, "_recoveryBar");
        Find<RoundedButton>(bar, "_action").PerformClick();
        Assert.AreEqual(1, resumes, "Resuming is its own decision.");
        Assert.AreEqual(0, dismissals);

        Find<RoundedButton>(bar, "_dismiss").PerformClick();
        Assert.AreEqual(1, dismissals, "Setting the session aside is a separate decision from continuing it.");
        Assert.AreEqual(1, resumes);
    }

    [TestMethod]
    public void TheMessageNeverRunsUnderTheButtons()
    {
        using var bar = new InfoBar { Width = 900 };
        bar.SetAction("Resume transfer");
        bar.SetDismiss("Not now");

        Label message = Find<Label>(bar, "_message");
        RoundedButton action = Find<RoundedButton>(bar, "_action");
        RoundedButton dismiss = Find<RoundedButton>(bar, "_dismiss");

        // One more layout pass now that the host knows its size.
        bar.PerformLayout();
        Assert.IsTrue(message.Right <= dismiss.Left + 1,
            $"The message (right {message.Right}) must end before the buttons (left {dismiss.Left}).");
        Assert.IsTrue(dismiss.Right <= action.Left + 1, "The dismissal sits beside the action, not under it.");
        Assert.IsTrue(action.Right <= bar.ClientSize.Width, "The action must stay inside the bar.");
        Assert.IsTrue(message.Height >= 32, $"The message needs two lines; it was {message.Height} px tall.");
        Assert.IsTrue(message.Bottom <= bar.ClientSize.Height,
            $"The message (bottom {message.Bottom}) must fit inside the bar ({bar.ClientSize.Height} tall), or the second line is painted outside the card.");
        Assert.IsTrue(message.AutoEllipsis, "If even two lines are not enough, the text must say it was cut rather than just stop.");
    }

    [TestMethod]
    public void AnEventlessBarIsStillUsable()
    {
        // The bar is a general control: with neither action nor dismissal it must not lay out anything on
        // top of the message.
        using var bar = new InfoBar { Width = 600 };
        Label message = Find<Label>(bar, "_message");
        bar.PerformLayout();
        Assert.IsTrue(message.Width > 300, "With no buttons the message may use the width.");
    }

    private static IEnumerable<Control> Walk(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grand in Walk(child)) yield return grand;
        }
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        foreach (Control control in Walk(root))
            if (control is T typed && control.Name == name) return typed;
        Assert.Fail($"Control '{name}' of type {typeof(T).Name} was not found.");
        throw new InvalidOperationException();
    }
}
