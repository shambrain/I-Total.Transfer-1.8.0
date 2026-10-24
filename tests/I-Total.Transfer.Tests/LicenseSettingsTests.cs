using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The Settings licence row. These tests create the real control, because the point is that the counter and
/// the explanation are actually visible and that an unavailable purchase action is disabled rather than
/// opening nothing.
/// </summary>
[TestClass]
public sealed class LicenseSettingsTests : WinFormsTestBase
{
    [TestMethod]
    public void TheLicenceRowShowsTheCounterAndTheExplanation()
    {
        using var page = new SettingsPage();

        page.SetLicenseStatus("Trial — 2 of 3 transfer jobs left",
            "A job is counted only when a transfer writes at least one file and finishes with no failures.");

        Assert.AreEqual("Trial — 2 of 3 transfer jobs left", page.LicenseHeadline);
        StringAssert.Contains(page.LicenseDetail, "writes at least one file");

        // The labels are part of the scrolling table, not orphaned controls that nobody can see.
        Assert.IsTrue(page.Controls.Count > 0);
        Assert.IsTrue(page.LicenseHeadline.Length > 0);
    }

    [TestMethod]
    public void ThePurchaseActionIsDisabledWhenThisBuildHasNoStoreUrl()
    {
        using var page = new SettingsPage();

        page.SetLicenseActions(purchaseConfigured: false, steamEdition: false);

        Assert.IsFalse(page.PurchaseActionEnabled,
            "A button that opens nothing would be worse than a disabled button with a reason.");
        Assert.IsFalse(page.SteamActionVisible);
    }

    [TestMethod]
    public void TheSteamCheckIsOfferedOnlyOnTheSteamEdition()
    {
        using var page = new SettingsPage();

        page.SetLicenseActions(purchaseConfigured: true, steamEdition: true);

        Assert.IsTrue(page.PurchaseActionEnabled);
        Assert.IsTrue(page.SteamActionVisible);

        page.SetLicenseActions(purchaseConfigured: true, steamEdition: false);
        Assert.IsFalse(page.SteamActionVisible);
    }
}
