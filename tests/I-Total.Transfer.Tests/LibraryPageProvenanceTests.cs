using ITotalTransfer.Core;
using ITotalTransfer.UI.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ITotalTransfer.Tests;

/// <summary>
/// A cached listing is the one place in the interface where what is on screen is deliberately not the result
/// of the run in progress, so the page's contract is that it says so. These tests pin the difference between
/// "here is your library" and "here is what it looked like when it was last read".
/// </summary>
[TestClass]
public sealed class LibraryPageProvenanceTests : WinFormsTestBase
{
    private static MediaFile File(string name, long size = 1024) => new("DCIM", name, size, ".HEIC");

    [TestMethod]
    public void ALiveListingShowsPlainCountsAndNoProvenance()
    {
        using var page = new LibraryPage();

        page.SetProvenance(null);
        page.SetFiles([File("IMG_0001.HEIC"), File("IMG_0002.HEIC")], 2);

        string summary = Summary(page);
        StringAssert.Contains(summary, "2 file(s)");
        Assert.IsFalse(summary.Contains("Last known library", StringComparison.OrdinalIgnoreCase),
            "A live scan must not be labelled as history.");
    }

    [TestMethod]
    public void ACachedListingSaysWhenItWasCapturedAndLeadsTheLine()
    {
        using var page = new LibraryPage();

        page.SetProvenance("Last known library (captured 2026-10-17 21:03) — the live scan is still running and will replace this");
        page.SetFiles([File("IMG_0001.HEIC")], 1);

        string summary = Summary(page);
        StringAssert.StartsWith(summary, "Last known library");
        StringAssert.Contains(summary, "2026-10-17 21:03");
        StringAssert.Contains(summary, "1 file(s)");
    }

    [TestMethod]
    public void ClearingTheProvenanceReturnsTheLineToPlainCounts()
    {
        using var page = new LibraryPage();

        page.SetProvenance("Last known library (captured 2026-10-17 21:03) — the live scan is still running");
        page.SetFiles([File("IMG_0001.HEIC")], 1);
        StringAssert.Contains(Summary(page), "Last known library");

        // This is what happens the moment the live scan lands: the grid becomes the measurement.
        page.SetProvenance(null);
        Assert.IsFalse(Summary(page).Contains("Last known library", StringComparison.Ordinal));
        StringAssert.Contains(Summary(page), "1 file(s)");
    }

    [TestMethod]
    public void ACachedListingStaysLabelledEvenWhenTheFilterHidesEveryRow()
    {
        using var page = new LibraryPage();

        page.SetProvenance("Last known library (captured 2026-10-17 21:03) — the live scan is still running");
        page.SetFiles([], 26_335);   // the filter excluded everything

        string summary = Summary(page);
        StringAssert.Contains(summary, "Last known library",
            "The provenance must survive the empty state: otherwise 'nothing here' and 'the filter hides everything' look identical.");
        StringAssert.Contains(summary, "26,335");
    }

    private static string Summary(LibraryPage page) =>
        Walk(page).OfType<Label>()
            .Select(label => label.Text ?? "")
            .FirstOrDefault(text => text.Contains("file(s)", StringComparison.Ordinal) || text.Contains("Last known library", StringComparison.Ordinal))
        ?? "";

    private static IEnumerable<Control> Walk(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grand in Walk(child)) yield return grand;
        }
    }
}
