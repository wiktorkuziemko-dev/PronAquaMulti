namespace MultiRejestrator.Protocol;

/// <summary>Blok parametru: Id (2 B), Size (2 B), Value (Size B). Używany w ramkach Configuration i Device State.</summary>
public sealed record ConfBlock(ushort Id, byte[] Value)
{
    public const int HeaderLength = 4;

    public int EncodedLength => HeaderLength + Value.Length;

    public static byte[] EncodeMany(IEnumerable<ConfBlock> blocks)
    {
        var bytes = new List<byte>();
        foreach (var block in blocks)
        {
            bytes.Add((byte)block.Id);
            bytes.Add((byte)(block.Id >> 8));
            bytes.Add((byte)block.Value.Length);
            bytes.Add((byte)(block.Value.Length >> 8));
            bytes.AddRange(block.Value);
        }

        return bytes.ToArray();
    }

    public static IReadOnlyList<ConfBlock> DecodeMany(ReadOnlySpan<byte> data)
    {
        var blocks = new List<ConfBlock>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < HeaderLength)
            {
                throw new ProtocolException($"Niekompletny nagłówek bloku parametru (offset {offset}).");
            }

            var id = (ushort)(data[offset] | (data[offset + 1] << 8));
            var size = data[offset + 2] | (data[offset + 3] << 8);
            offset += HeaderLength;
            if (data.Length - offset < size)
            {
                throw new ProtocolException($"Blok parametru Id {id} deklaruje {size} B, a dostępnych jest {data.Length - offset} B.");
            }

            blocks.Add(new ConfBlock(id, data.Slice(offset, size).ToArray()));
            offset += size;
        }

        return blocks;
    }

    public static byte[] EncodeIds(IReadOnlyList<ushort> ids)
    {
        var bytes = new byte[ids.Count * 2];
        for (var i = 0; i < ids.Count; i++)
        {
            bytes[i * 2] = (byte)ids[i];
            bytes[(i * 2) + 1] = (byte)(ids[i] >> 8);
        }

        return bytes;
    }

    public static IReadOnlyList<ushort> DecodeIds(ReadOnlySpan<byte> data)
    {
        if (data.Length % 2 != 0)
        {
            throw new ProtocolException("Lista Id ma nieparzystą długość.");
        }

        var ids = new ushort[data.Length / 2];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = (ushort)(data[i * 2] | (data[(i * 2) + 1] << 8));
        }

        return ids;
    }
}
