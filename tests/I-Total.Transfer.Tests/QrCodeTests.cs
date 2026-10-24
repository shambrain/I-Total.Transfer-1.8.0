using ITotalTransfer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Proves the pairing QR is a real QR code, not a picture of one. Every case is read back by an
/// independent decoder (<see cref="QrDecoder"/>) that shares no code with the encoder: it locates the
/// format bits, undoes the chosen mask, walks the data modules and de-interleaves the blocks.
///
/// <para>
/// The URLs here are the shapes the receiver actually produces, at the lengths the receiver actually
/// produces them, so a version or block-split regression fails here rather than at a phone.
/// </para>
/// </summary>
[TestClass]
public sealed class QrCodeTests
{
    /// <summary>A realistic pairing URL: private address, port, and a 32-character token.</summary>
    private static string PairingUrl(int tokenLength = 32) =>
        "http://192.168.1.24:47821/pair?token=" + new string('a', tokenLength);

    [TestMethod]
    public void APairingUrlRoundTripsThroughAnIndependentDecoder()
    {
        string url = PairingUrl();
        QrCode.Symbol symbol = QrCode.Encode(url);

        Assert.AreEqual(url, QrDecoder.Decode(symbol));
    }

    [TestMethod]
    public void ShortTextUsesVersionOneAndRoundTrips()
    {
        QrCode.Symbol symbol = QrCode.Encode("hello");

        Assert.AreEqual(1, symbol.Version, "Five bytes fit in version 1 at level M.");
        Assert.AreEqual(21, symbol.Size);
        Assert.AreEqual("hello", QrDecoder.Decode(symbol));
    }

    [TestMethod]
    public void EveryVersionUpToTenRoundTrips()
    {
        // Grow the payload so the encoder must cross each version boundary it claims to support.
        for (int length = 1; length <= 200; length += 7)
        {
            string text = new('x', length);
            QrCode.Symbol symbol = QrCode.Encode(text);
            Assert.AreEqual(text, QrDecoder.Decode(symbol), $"Length {length} (version {symbol.Version}) did not round-trip.");
            Assert.IsTrue(symbol.Version is >= 1 and <= 10);
        }
    }

    [TestMethod]
    public void MultiBlockVersionsRoundTrip()
    {
        // Versions 4 and up are split into several Reed–Solomon blocks; a single-block implementation
        // still decodes small inputs, which is exactly why this case exists.
        foreach (int length in new[] { 60, 90, 120, 160, 200 })
        {
            string text = new string('Q', length);
            QrCode.Symbol symbol = QrCode.Encode(text);
            Assert.IsTrue(symbol.Version >= 4, $"Length {length} should be a multi-block version, got v{symbol.Version}.");
            Assert.AreEqual(text, QrDecoder.Decode(symbol));
        }
    }

    [TestMethod]
    public void TheEncodedTextIsTheExactPairingUrlWithItsToken()
    {
        string token = PairingState.NewToken();
        string url = $"http://10.0.0.7:47821/pair?token={token}";

        QrCode.Symbol symbol = QrCode.Encode(url);
        string decoded = QrDecoder.Decode(symbol);

        Assert.AreEqual(url, decoded);
        StringAssert.Contains(decoded, token);
    }

    [TestMethod]
    public void TheQuietZoneIsFourModulesOfLight()
    {
        QrCode.Symbol symbol = QrCode.Encode(PairingUrl());
        bool[,] padded = QrCode.WithQuietZone(symbol);

        Assert.AreEqual(symbol.Size + 8, padded.GetLength(0));
        for (int i = 0; i < padded.GetLength(0); i++)
        {
            for (int edge = 0; edge < 4; edge++)
            {
                Assert.IsFalse(padded[edge, i], "Top quiet zone must be light.");
                Assert.IsFalse(padded[padded.GetLength(0) - 1 - edge, i], "Bottom quiet zone must be light.");
                Assert.IsFalse(padded[i, edge], "Left quiet zone must be light.");
                Assert.IsFalse(padded[i, padded.GetLength(0) - 1 - edge], "Right quiet zone must be light.");
            }
        }
    }

    [TestMethod]
    public void TheThreeFinderPatternsAreWhereAScannerLooksForThem()
    {
        QrCode.Symbol symbol = QrCode.Encode(PairingUrl());
        int size = symbol.Size;

        foreach ((int x0, int y0) in new[] { (0, 0), (size - 7, 0), (0, size - 7) })
        {
            // Outer ring dark, ring inside it light, centre 3x3 dark.
            Assert.IsTrue(symbol.Modules[y0, x0], "Finder corner must be dark.");
            Assert.IsTrue(symbol.Modules[y0 + 1, x0 + 1] == false, "Finder inner ring must be light.");
            for (int dy = 2; dy <= 4; dy++)
                for (int dx = 2; dx <= 4; dx++)
                    Assert.IsTrue(symbol.Modules[y0 + dy, x0 + dx], "Finder centre must be dark.");
        }
    }

    [TestMethod]
    public void TextTooLongForVersionTenIsRefusedRatherThanTruncated()
    {
        string tooLong = new('z', 400);
        Assert.ThrowsException<ArgumentException>(() => QrCode.Encode(tooLong));
    }

    [TestMethod]
    public void AnEmptyPayloadIsRefused()
    {
        Assert.ThrowsException<ArgumentException>(() => QrCode.Encode(""));
    }
}
