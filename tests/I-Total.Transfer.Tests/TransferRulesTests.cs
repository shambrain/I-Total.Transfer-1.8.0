using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class TransferRulesTests
{
    [TestMethod]
    public void CompletionRequiresNonEmptyPlanAndAllItemsProcessed() =>
        Assert.IsFalse(TransferRules.CanDeclareComplete(26409, 1, 0, 0));

    [TestMethod]
    public void CompletionFailsIfAnyItemFailed() =>
        Assert.IsFalse(TransferRules.CanDeclareComplete(100, 100, 1, 0));

    [TestMethod]
    public void CompletionFailsIfAnyItemWasInaccessible() =>
        Assert.IsFalse(TransferRules.CanDeclareComplete(100, 100, 0, 1));

    [TestMethod]
    public void EmptyPlanCannotBeComplete() =>
        Assert.IsFalse(TransferRules.CanDeclareComplete(0, 0, 0, 0));

    [TestMethod]
    public void ExactSizeMatches() =>
        Assert.IsTrue(TransferRules.DestinationSizeMatches(123456789, 123456789));

    [TestMethod]
    public void SameSizeMismatchIsRejected() =>
        Assert.IsFalse(TransferRules.DestinationSizeMatches(100, 101));

    [TestMethod]
    public void ZeroByteFileCanMatchZeroByteDestination() =>
        Assert.IsTrue(TransferRules.DestinationSizeMatches(0, 0));

    [TestMethod]
    public void TraversalFilenameIsRejected() =>
        Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("202608_a", "..\\outside.txt"));

    [TestMethod]
    public void ValidMediaPathIsPreserved() =>
        Assert.AreEqual(Path.Combine("202608_a", "IMG_0001.HEIC"), TransferRules.ValidateRelativePath("202608_a", "IMG_0001.HEIC"));
    [TestMethod]
    public void NestedMediaFolderIsPreservedSafely() =>
        Assert.AreEqual(Path.Combine("DCIM", "nested", "IMG_0001.HEIC"),
            TransferRules.ValidateRelativePath("DCIM\\nested", "IMG_0001.HEIC"));

    [TestMethod]
    public void NestedTraversalIsRejected() =>
        Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("DCIM\\..\\outside", "IMG_0001.HEIC"));

    [TestMethod]
    public void AbsoluteSourceFolderIsRejected() =>
        Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("C:\\outside", "IMG_0001.HEIC"));

}
