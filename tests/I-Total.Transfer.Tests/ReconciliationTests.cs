using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class ClassifyExistingDestinationTests
{
    [TestMethod]
    public void NoSavedHash_WithMatchingSize_RecordsBaseline()
        => Assert.AreEqual(ResumeDecision.RecordBaselineSkip,
            TransferRules.ClassifyExistingDestination(1000, 1000, null, "abc123"));

    [TestMethod]
    public void MatchingSavedAndExistingHash_SkipsVerified()
        => Assert.AreEqual(ResumeDecision.SkipVerified,
            TransferRules.ClassifyExistingDestination(1000, 1000, "abc123", "abc123"));

    [TestMethod]
    public void SavedHashDiffersFromExistingHash_PreservesForReview()
        => Assert.AreEqual(ResumeDecision.PreserveHashMismatch,
            TransferRules.ClassifyExistingDestination(1000, 1000, "abc123", "def456"));

    [TestMethod]
    public void SavedHashExistsButExistingHashMissing_PreservesForReview()
        => Assert.AreEqual(ResumeDecision.PreserveHashMismatch,
            TransferRules.ClassifyExistingDestination(1000, 1000, "abc123", null));

    [TestMethod]
    public void SizeDiffers_PreservesCollision()
        => Assert.AreEqual(ResumeDecision.PreserveCollision,
            TransferRules.ClassifyExistingDestination(1000, 999, null, null));

    [TestMethod]
    public void UnknownSourceSize_PreservesCollision()
        => Assert.AreEqual(ResumeDecision.PreserveCollision,
            TransferRules.ClassifyExistingDestination(0, 1000, null, null));

    [TestMethod]
    public void HashComparisonIsCaseInsensitive()
        => Assert.AreEqual(ResumeDecision.SkipVerified,
            TransferRules.ClassifyExistingDestination(1000, 1000, "ABC123", "abc123"));
}

[TestClass]
public sealed class ClassifyVerificationTests
{
    [TestMethod]
    public void MatchingSourceAndDestination_PromotesToSourceToDestinationHash()
        => Assert.AreEqual(VerificationLevel.SourceAndDestinationHash,
            TransferRules.ClassifyVerification("abc", "abc", "abc", true));

    [TestMethod]
    public void StagedMatchesDestination_WithoutSource_IsOnlyDestinationHash()
        => Assert.AreEqual(VerificationLevel.DestinationHash,
            TransferRules.ClassifyVerification(null, "abc", "abc", true));

    [TestMethod]
    public void SizeMatches_WithoutAnyHash_IsOnlySizeOnly()
        => Assert.AreEqual(VerificationLevel.SizeOnly,
            TransferRules.ClassifyVerification(null, null, null, true));

    [TestMethod]
    public void UnknownSize_WithoutHash_IsNone()
        => Assert.AreEqual(VerificationLevel.None,
            TransferRules.ClassifyVerification(null, null, null, false));

    [TestMethod]
    public void StagedAndDestinationDiffer_NeverReachesDestinationHash()
        => Assert.AreEqual(VerificationLevel.None,
            TransferRules.ClassifyVerification(null, "abc", "def", false));
}

[TestClass]
public sealed class ReservedNameTests
{
    [TestMethod] public void ConIsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("CON"));
    [TestMethod] public void ConWithExtensionIsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("CON.txt"));
    [TestMethod] public void ReservedNamesAreCaseInsensitive() => Assert.IsTrue(TransferRules.IsReservedWindowsName("con"));
    [TestMethod] public void PrnIsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("PRN"));
    [TestMethod] public void NulIsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("NUL"));
    [TestMethod] public void Com1IsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("COM1.jpg"));
    [TestMethod] public void Lpt9IsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName("LPT9"));
    [TestMethod] public void NormalFileIsNotReserved() => Assert.IsFalse(TransferRules.IsReservedWindowsName("IMG_0001.HEIC"));
    [TestMethod] public void EmptyNameIsReserved() => Assert.IsTrue(TransferRules.IsReservedWindowsName(""));

    [TestMethod] public void ReservedFilenameIsRejected()
        => Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("DCIM", "CON.HEIC"));

    [TestMethod] public void TrailingSpaceFilenameIsRejected()
        => Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("DCIM", "IMG_.jpg "));

    [TestMethod] public void TrailingDotFilenameIsRejected()
        => Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("DCIM", "IMG.jpg."));

    [TestMethod] public void ControlCharacterFilenameIsRejected()
        => Assert.ThrowsException<InvalidDataException>(() => TransferRules.ValidateRelativePath("DCIM", "IMG\x01.jpg"));
}
