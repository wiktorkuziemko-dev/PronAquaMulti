namespace MultiRejestrator.Protocol;

/// <summary>
/// CRC-16/CCITT-FALSE: wielomian 0x1021, wartość początkowa 0xFFFF, bez odbicia bitów i bez XOR na wyjściu.
/// Wartość kontrolna dla „123456789” = 0x29B1 (potwierdzone przez INTSOL, pytanie 2.3).
/// </summary>
public static class Crc16Ccitt
{
    private static readonly ushort[] Table = BuildTable();

    public static ushort Compute(ReadOnlySpan<byte> data, ushort initial = 0xFFFF)
    {
        var crc = initial;
        foreach (var b in data)
        {
            crc = (ushort)((crc << 8) ^ Table[((crc >> 8) ^ b) & 0xFF]);
        }

        return crc;
    }

    private static ushort[] BuildTable()
    {
        var table = new ushort[256];
        for (var i = 0; i < 256; i++)
        {
            var c = (ushort)(i << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                c = (c & 0x8000) != 0 ? (ushort)((c << 1) ^ 0x1021) : (ushort)(c << 1);
            }

            table[i] = c;
        }

        return table;
    }
}
