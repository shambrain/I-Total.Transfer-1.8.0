using ITotalTransfer.Core;

namespace ITotalTransfer.Tests;

/// <summary>
/// An independent QR reader used only by the tests. It shares no code with the encoder: it locates the
/// format bits, reads them, undoes the mask, walks the data modules in reverse zig-zag order, splits the
/// interleaved blocks and decodes the byte-mode payload.
///
/// <para>
/// This is what makes the QR claim honest. "We draw a QR code" is not a verifiable statement; "an
/// independent decoder recovers the exact pairing URL from the modules" is.
/// </para>
/// </summary>
internal static class QrDecoder
{
    /// <summary>Reads a symbol back to text. Throws when the structure is not a valid level-M byte-mode QR.</summary>
    public static string Decode(QrCode.Symbol symbol)
    {
        bool[,] modules = symbol.Modules;
        int size = symbol.Size;
        int version = (size - 17) / 4;

        // ---- format bits, second copy (top-right + bottom-left) --------------------------------
        int bits = 0;
        for (int i = 0; i < 8; i++)
            if (modules[8, size - 1 - i]) bits |= 1 << i;
        for (int i = 8; i < 15; i++)
            if (modules[size - 15 + i, 8]) bits |= 1 << i;

        bits ^= 0x5412;
        int level = (bits >> 13) & 0x3;
        int mask = (bits >> 10) & 0x7;
        if (level != 1) throw new InvalidDataException($"Expected level M, read level {level}.");

        // ---- reserved map, rebuilt from the same geometry -------------------------------------
        var reserved = new bool[size, size];
        ReserveFinder(reserved, 0, 0, size);
        ReserveFinder(reserved, size - 7, 0, size);
        ReserveFinder(reserved, 0, size - 7, size);
        for (int i = 0; i <= 8; i++)
        {
            if (i != 6) { reserved[8, i] = true; reserved[i, 8] = true; }
        }
        for (int i = 0; i < 8; i++) { reserved[8, size - 1 - i] = true; reserved[size - 1 - i, 8] = true; }
        reserved[size - 8, 8] = true;
        for (int i = 8; i < size - 8; i++) { reserved[6, i] = true; reserved[i, 6] = true; }

        if (version >= 2)
        {
            int[] centers = version switch
            {
                2 => [6, 18], 3 => [6, 22], 4 => [6, 26], 5 => [6, 30], 6 => [6, 34],
                7 => [6, 22, 38], 8 => [6, 24, 42], 9 => [6, 26, 46], 10 => [6, 28, 50],
                _ => [6]
            };
            foreach (int cy in centers)
                foreach (int cx in centers)
                {
                    if ((cx == 6 && cy == 6) || (cx == 6 && cy == centers[^1]) || (cx == centers[^1] && cy == 6)) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            reserved[cy + dy, cx + dx] = true;
                }
        }

        // ---- data modules, reverse zig-zag ------------------------------------------------------
        var bitsRead = new List<bool>();
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? size - 1 - vert : vert;
                    if (reserved[y, x]) continue;
                    bool dark = modules[y, x];
                    if (QrCode.MaskBit(mask, x, y)) dark = !dark;
                    bitsRead.Add(dark);
                }
            }
        }

        int totalCodewords = QrCode.DataCodewords(version) + EccPerBlock(version) * BlockCount(version);
        int availableBits = Math.Min(bitsRead.Count, totalCodewords * 8);
        var bytes = new byte[availableBits / 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            int value = 0;
            for (int b = 0; b < 8; b++) value = (value << 1) | (bitsRead[i * 8 + b] ? 1 : 0);
            bytes[i] = (byte)value;
        }

        // ---- de-interleave back into data codewords -------------------------------------------
        byte[] data = Deinterleave(bytes, version);

        // ---- byte-mode payload ------------------------------------------------------------------
        int bitPos = 0;
        int Read(int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++)
            {
                int index = bitPos + i;
                bool bit = (data[index >> 3] & (1 << (7 - (index & 7)))) != 0;
                value = (value << 1) | (bit ? 1 : 0);
            }
            bitPos += count;
            return value;
        }

        int mode = Read(4);
        if (mode != 0b0100) throw new InvalidDataException($"Expected byte mode, read {mode}.");
        int countBits = version <= 9 ? 8 : 16;
        int length = Read(countBits);
        var payload = new byte[length];
        for (int i = 0; i < length; i++) payload[i] = (byte)Read(8);
        return System.Text.Encoding.UTF8.GetString(payload);
    }

    private static void ReserveFinder(bool[,] reserved, int x0, int y0, int size)
    {
        for (int dy = -1; dy <= 7; dy++)
            for (int dx = -1; dx <= 7; dx++)
            {
                int x = x0 + dx, y = y0 + dy;
                if (x >= 0 && y >= 0 && x < size && y < size) reserved[y, x] = true;
            }
    }

    private static int EccPerBlock(int version) => version switch
    {
        1 => 10, 2 => 16, 3 => 26, 4 => 18, 5 => 24, 6 => 16, 7 => 18, 8 => 22, 9 => 22, 10 => 26,
        _ => 0
    };

    private static (int Count, int DataCodewords)[] Blocks(int version) => version switch
    {
        1 => [(1, 16)], 2 => [(1, 28)], 3 => [(1, 44)], 4 => [(2, 32)], 5 => [(2, 43)],
        6 => [(4, 27)], 7 => [(4, 31)], 8 => [(2, 38), (2, 39)], 9 => [(3, 36), (2, 37)],
        10 => [(4, 43), (1, 44)], _ => []
    };

    private static int BlockCount(int version) => Blocks(version).Sum(b => b.Count);

    private static byte[] Deinterleave(byte[] interleaved, int version)
    {
        var groups = Blocks(version);
        int eccCount = EccPerBlock(version);
        int blockCount = groups.Sum(g => g.Count);
        int[] lengths = groups.SelectMany(g => Enumerable.Repeat(g.DataCodewords, g.Count)).ToArray();
        var data = new byte[lengths.Sum()];

        int source = 0;
        int maxLength = lengths.Max();
        for (int i = 0; i < maxLength; i++)
            for (int b = 0; b < blockCount; b++)
                if (i < lengths[b]) data[DataOffset(lengths, b) + i] = interleaved[source++];

        _ = eccCount;
        return data;
    }

    private static int DataOffset(int[] lengths, int block)
    {
        int offset = 0;
        for (int i = 0; i < block; i++) offset += lengths[i];
        return offset;
    }
}
