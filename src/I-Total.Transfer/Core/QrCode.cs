using System.Text;

namespace ITotalTransfer.Core;

/// <summary>
/// A minimal QR encoder for the one thing this application puts in a QR code: a short pairing URL
/// (about 90–120 ASCII characters, byte mode, error correction level M).
///
/// <para>
/// Why hand-written rather than a package: the release ships a single self-contained executable, and
/// the project's own dependency policy says a binary is only trusted when it is pinned by hash and its
/// licence is recorded. A QR library would add a third-party binary to that policy for a feature that
/// needs one small, fully specified algorithm. This implementation follows ISO/IEC 18004 for byte
/// mode, versions 1–10, level M, and is proven in the test suite by an independent decoder that walks
/// the modules back — reading the format bits, undoing the mask and decoding the data codewords.
/// </para>
///
/// <para>
/// It is deliberately narrow: byte mode only, level M only, versions 1–10. A pairing URL is far below
/// that ceiling. Anything longer throws rather than silently producing an unscannable code.
/// </para>
/// </summary>
public static class QrCode
{
    /// <summary>Error correction level M (≈15% recovery), the level phone cameras handle best.</summary>
    private const int EccLevelM = 1;

    /// <summary>A rendered symbol: its module grid, the version and mask chosen, and the text.</summary>
    public sealed record Symbol(bool[,] Modules, int Version, int Mask, string Text)
    {
        public int Size => Modules.GetLength(0);
    }

    /// <summary>
    /// Encodes UTF-8 text. Throws <see cref="ArgumentException"/> when the text will not fit in
    /// version 10 at level M, because a truncated QR is worse than an error.
    /// </summary>
    public static Symbol Encode(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        byte[] payload = Encoding.UTF8.GetBytes(text);

        // ---- 1. smallest version that fits -----------------------------------------------------
        int version = 0;
        for (int v = 1; v <= 10; v++)
        {
            int needed = 4 + CharacterCountBits(v) + payload.Length * 8;
            if (needed <= DataCodewords(v) * 8) { version = v; break; }
        }
        if (version == 0)
            throw new ArgumentException($"Text is too long for a version 10 QR code ({payload.Length} bytes).", nameof(text));

        // ---- 2. bit stream ---------------------------------------------------------------------
        int capacityBits = DataCodewords(version) * 8;
        var bits = new BitWriter(capacityBits);
        bits.Append(0b0100, 4);                                  // byte mode
        bits.Append(payload.Length, CharacterCountBits(version));
        foreach (byte b in payload) bits.Append(b, 8);
        bits.Append(0, Math.Min(4, capacityBits - bits.Length)); // terminator
        while (bits.Length % 8 != 0) bits.Append(0, 1);

        bool padHigh = true;
        while (bits.Length < capacityBits)
        {
            bits.Append(padHigh ? 0xEC : 0x11, 8);
            padHigh = !padHigh;
        }

        // ---- 3. blocks + error correction + interleave ------------------------------------------
        byte[] all = Interleave(bits.ToBytes(), version);

        // ---- 4. modules, choosing the least-penalised mask --------------------------------------
        bool[,] best = null!;
        int bestMask = 0;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            bool[,] modules = BuildMatrix(version, all, mask);
            int penalty = Penalty(modules);
            if (penalty >= bestPenalty) continue;
            bestPenalty = penalty;
            best = modules;
            bestMask = mask;
        }

        return new Symbol(best, version, bestMask, text);
    }

    // ------------------------------------------------------------------ capacity (level M)

    /// <summary>Data codewords at level M, versions 1–10.</summary>
    public static int DataCodewords(int version) => version switch
    {
        1 => 16, 2 => 28, 3 => 44, 4 => 64, 5 => 86, 6 => 108, 7 => 124, 8 => 154, 9 => 182, 10 => 216,
        _ => 0
    };

    /// <summary>Error-correction codewords per block at level M, versions 1–10.</summary>
    private static int EccPerBlock(int version) => version switch
    {
        1 => 10, 2 => 16, 3 => 26, 4 => 18, 5 => 24, 6 => 16, 7 => 18, 8 => 22, 9 => 22, 10 => 26,
        _ => 0
    };

    /// <summary>
    /// The block split at level M. A version above 3 is not one Reed–Solomon block: the data is divided
    /// into groups (some with one more codeword than others), each group gets its own ECC codewords, and
    /// the result is interleaved. Treating it as a single block produces a symbol no scanner can read.
    /// </summary>
    private static (int Count, int DataCodewords)[] Blocks(int version) => version switch
    {
        1 => [(1, 16)],
        2 => [(1, 28)],
        3 => [(1, 44)],
        4 => [(2, 32)],
        5 => [(2, 43)],
        6 => [(4, 27)],
        7 => [(4, 31)],
        8 => [(2, 38), (2, 39)],
        9 => [(3, 36), (2, 37)],
        10 => [(4, 43), (1, 44)],
        _ => []
    };

    private static int CharacterCountBits(int version) => version <= 9 ? 8 : 16;

    /// <summary>Splits the data into blocks, appends each block's ECC, and interleaves both.</summary>
    private static byte[] Interleave(byte[] data, int version)
    {
        var groups = Blocks(version);
        int eccCount = EccPerBlock(version);
        var blocks = new List<byte[]>();
        int offset = 0;
        foreach ((int count, int dataLength) in groups)
        {
            for (int i = 0; i < count; i++)
            {
                byte[] block = data[offset..(offset + dataLength)];
                offset += dataLength;
                blocks.Add(block);
            }
        }

        var ecc = blocks.Select(b => ReedSolomon(b, eccCount)).ToList();
        int maxData = blocks.Max(b => b.Length);
        var result = new List<byte>(data.Length + eccCount * blocks.Count);

        for (int i = 0; i < maxData; i++)
            foreach (byte[] block in blocks)
                if (i < block.Length) result.Add(block[i]);

        for (int i = 0; i < eccCount; i++)
            foreach (byte[] block in ecc)
                result.Add(block[i]);

        return [.. result];
    }

    // ------------------------------------------------------------------ Reed–Solomon over GF(256)

    private static byte[] ReedSolomon(byte[] data, int eccCount)
    {
        byte[] generator = GeneratorPolynomial(eccCount);
        var result = new byte[eccCount];
        foreach (byte b in data)
        {
            byte factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (int i = 0; i < eccCount; i++)
                result[i] ^= Multiply(generator[i], factor);
        }
        return result;
    }

    private static byte[] GeneratorPolynomial(int degree)
    {
        var poly = new byte[] { 1 };
        byte root = 1;
        for (int i = 0; i < degree; i++)
        {
            var next = new byte[poly.Length + 1];
            for (int j = 0; j < poly.Length; j++)
            {
                next[j] ^= poly[j];
                next[j + 1] ^= Multiply(poly[j], root);
            }
            poly = next;
            root = Multiply(root, (byte)2);
        }
        return poly[1..];
    }

    private static byte Multiply(byte a, byte b)
    {
        int result = 0, x = a, y = b;
        while (y != 0)
        {
            if ((y & 1) != 0) result ^= x;
            x <<= 1;
            if ((x & 0x100) != 0) x ^= 0x11D;
            y >>= 1;
        }
        return (byte)result;
    }

    // ------------------------------------------------------------------ matrix

    private static bool[,] BuildMatrix(int version, byte[] codewords, int mask)
    {
        int size = 17 + version * 4;
        var modules = new bool[size, size];
        var reserved = new bool[size, size];

        PlaceFinder(modules, reserved, 0, 0);
        PlaceFinder(modules, reserved, size - 7, 0);
        PlaceFinder(modules, reserved, 0, size - 7);
        PlaceAlignment(modules, reserved, version);
        PlaceTiming(modules, reserved, size);

        // Reserve the format areas by writing dummy bits (mask 0); the real bits go in afterwards.
        WriteFormatBits(modules, reserved, size, mask: 0);

        int bitIndex = 0;
        int totalBits = codewords.Length * 8;
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5; // the vertical timing column is stepped over
            for (int vert = 0; vert < size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? size - 1 - vert : vert;
                    if (reserved[y, x]) continue;

                    bool dark = false;
                    if (bitIndex < totalBits)
                        dark = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                    bitIndex++;

                    if (MaskBit(mask, x, y)) dark = !dark;
                    modules[y, x] = dark;
                }
            }
        }

        WriteFormatBits(modules, reserved, size, mask);
        return modules;
    }

    /// <summary>Finder pattern plus its separator ring; the ring is light and reserved.</summary>
    private static void PlaceFinder(bool[,] modules, bool[,] reserved, int x0, int y0)
    {
        int size = modules.GetLength(0);
        for (int dy = -1; dy <= 7; dy++)
        {
            for (int dx = -1; dx <= 7; dx++)
            {
                int x = x0 + dx, y = y0 + dy;
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                bool inside = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6;
                bool dark = inside && (dx is 0 or 6 || dy is 0 or 6 || (dx is >= 2 and <= 4 && dy is >= 2 and <= 4));
                modules[y, x] = dark;
                reserved[y, x] = true;
            }
        }
    }

    private static void PlaceAlignment(bool[,] modules, bool[,] reserved, int version)
    {
        if (version < 2) return;
        int[] centers = version switch
        {
            2 => [6, 18], 3 => [6, 22], 4 => [6, 26], 5 => [6, 30], 6 => [6, 34],
            7 => [6, 22, 38], 8 => [6, 24, 42], 9 => [6, 26, 46], 10 => [6, 28, 50],
            _ => [6]
        };
        foreach (int cy in centers)
        {
            foreach (int cx in centers)
            {
                // The three corners belong to the finder patterns.
                if ((cx == 6 && cy == 6) || (cx == 6 && cy == centers[^1]) || (cx == centers[^1] && cy == 6)) continue;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        bool dark = Math.Abs(dx) == 2 || Math.Abs(dy) == 2 || (dx == 0 && dy == 0);
                        modules[cy + dy, cx + dx] = dark;
                        reserved[cy + dy, cx + dx] = true;
                    }
            }
        }
    }

    private static void PlaceTiming(bool[,] modules, bool[,] reserved, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = i % 2 == 0;
            if (!reserved[6, i]) { modules[6, i] = dark; reserved[6, i] = true; }
            if (!reserved[i, 6]) { modules[i, 6] = dark; reserved[i, 6] = true; }
        }
    }

    /// <summary>Writes both copies of the 15 format bits and reserves their cells.</summary>
    private static void WriteFormatBits(bool[,] modules, bool[,] reserved, int size, int mask)
    {
        int data = (EccLevelM << 3) | mask;
        int remainder = data;
        for (int i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        int bits = ((data << 10) | remainder) ^ 0x5412;

        void Set(int x, int y, int index)
        {
            bool dark = ((bits >> index) & 1) != 0;
            modules[y, x] = dark;
            reserved[y, x] = true;
        }

        // Copy 1: around the top-left finder.
        for (int i = 0; i <= 5; i++) Set(8, i, i);
        Set(8, 7, 6);
        Set(8, 8, 7);
        Set(7, 8, 8);
        for (int i = 9; i < 15; i++) Set(14 - i, 8, i);

        // Copy 2: split between the top-right and bottom-left finders.
        for (int i = 0; i < 8; i++) Set(size - 1 - i, 8, i);
        for (int i = 8; i < 15; i++) Set(8, size - 15 + i, i);

        // The always-dark module.
        modules[size - 8, 8] = true;
        reserved[size - 8, 8] = true;
    }

    /// <summary>The eight mask predicates of ISO/IEC 18004 §8.8.1.</summary>
    public static bool MaskBit(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => (x / 3 + y / 2) % 2 == 0,
        5 => (x * y) % 2 + (x * y) % 3 == 0,
        6 => ((x * y) % 2 + (x * y) % 3) % 2 == 0,
        7 => ((x + y) % 2 + (x * y) % 3) % 2 == 0,
        _ => false
    };

    /// <summary>The four penalty rules used only to pick the mask; every mask is decodable.</summary>
    private static int Penalty(bool[,] modules)
    {
        int size = modules.GetLength(0);
        int score = 0;

        for (int y = 0; y < size; y++)
        {
            int run = 1;
            for (int x = 1; x < size; x++)
            {
                if (modules[y, x] == modules[y, x - 1]) run++;
                else { if (run >= 5) score += 3 + (run - 5); run = 1; }
            }
            if (run >= 5) score += 3 + (run - 5);
        }
        for (int x = 0; x < size; x++)
        {
            int run = 1;
            for (int y = 1; y < size; y++)
            {
                if (modules[y, x] == modules[y - 1, x]) run++;
                else { if (run >= 5) score += 3 + (run - 5); run = 1; }
            }
            if (run >= 5) score += 3 + (run - 5);
        }

        for (int y = 0; y < size - 1; y++)
            for (int x = 0; x < size - 1; x++)
            {
                bool c = modules[y, x];
                if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1]) score += 3;
            }

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size - 10; x++)
            {
                if (FinderLike(modules, x, y, 1, 0)) score += 40;
                if (FinderLike(modules, x, y, 0, 1)) score += 40;
            }

        int dark = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (modules[y, x]) dark++;
        score += Math.Abs(dark * 100 / (size * size) - 50) / 5 * 10;
        return score;
    }

    private static bool FinderLike(bool[,] modules, int x, int y, int dx, int dy)
    {
        int size = modules.GetLength(0);
        int[] want = [1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0];
        for (int i = 0; i < want.Length; i++)
        {
            int px = x + dx * i, py = y + dy * i;
            if (px >= size || py >= size) return false;
            if (modules[py, px] != (want[i] == 1)) return false;
        }
        return true;
    }

    /// <summary>The symbol with a 4-module quiet zone, as scanners require.</summary>
    public static bool[,] WithQuietZone(Symbol symbol, int quiet = 4)
    {
        int size = symbol.Size;
        var padded = new bool[size + quiet * 2, size + quiet * 2];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                padded[y + quiet, x + quiet] = symbol.Modules[y, x];
        return padded;
    }

    private sealed class BitWriter(int capacity)
    {
        private readonly List<bool> _bits = new(capacity);
        public int Capacity { get; } = capacity;
        public int Length => _bits.Count;

        public void Append(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--) _bits.Add(((value >> i) & 1) != 0);
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[_bits.Count / 8];
            for (int i = 0; i < bytes.Length; i++)
            {
                int value = 0;
                for (int b = 0; b < 8; b++) value = (value << 1) | (_bits[i * 8 + b] ? 1 : 0);
                bytes[i] = (byte)value;
            }
            return bytes;
        }
    }
}
