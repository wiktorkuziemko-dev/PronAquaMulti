namespace MultiRejestrator.Protocol;

/// <summary>
/// Składa ramki z napływającego strumienia bajtów. Szuka znacznika "PMU", sprawdza rozmiar i CRC;
/// przy błędzie odrzuca bajty i synchronizuje się ponownie.
/// </summary>
public sealed class FrameParser
{
    private readonly bool _framesHavePassword;
    private readonly List<byte> _buffer = new();

    /// <param name="framesHavePassword">
    /// <c>true</c> dla strony urządzenia (odbiera zapytania z hasłem),
    /// <c>false</c> dla strony komputera (odpowiedzi bez hasła).
    /// </param>
    public FrameParser(bool framesHavePassword)
    {
        _framesHavePassword = framesHavePassword;
    }

    /// <summary>Wywoływane, gdy odrzucono bajty (np. błąd CRC) – do logu.</summary>
    public event Action<string, byte[]>? Discarded;

    public int BufferedCount => _buffer.Count;

    public void Reset() => _buffer.Clear();

    /// <summary>Dodaje odebrane bajty i zwraca wszystkie kompletne, poprawne ramki.</summary>
    public IReadOnlyList<Frame> Feed(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            _buffer.Add(b);
        }

        var frames = new List<Frame>();
        while (TryExtract(out var frame))
        {
            frames.Add(frame);
        }

        return frames;
    }

    private bool TryExtract(out Frame frame)
    {
        frame = null!;
        while (true)
        {
            var start = FindMagic();
            if (start < 0)
            {
                // Zachowaj ewentualny początek znacznika na końcu bufora.
                var keep = Math.Min(_buffer.Count, Frame.MagicLength - 1);
                DropFront(_buffer.Count - keep, "bajty spoza ramki");
                return false;
            }

            if (start > 0)
            {
                DropFront(start, "bajty przed znacznikiem PMU");
            }

            if (_buffer.Count < Frame.MagicLength + Frame.SizeFieldLength)
            {
                return false;
            }

            var size = _buffer[3] | (_buffer[4] << 8);
            var minSize = Frame.SizeOverhead + (_framesHavePassword ? Frame.PasswordLength : 0);
            var maxSize = minSize + Frame.MaxDataLength;
            if (size < minSize || size > maxSize)
            {
                DropFront(1, $"nieprawidłowe pole size ({size})");
                continue;
            }

            var total = Frame.MagicLength + Frame.SizeFieldLength + size;
            if (_buffer.Count < total)
            {
                return false;
            }

            var raw = _buffer.GetRange(0, total).ToArray();
            var crcReceived = (ushort)(raw[Frame.CrcOffset] | (raw[Frame.CrcOffset + 1] << 8));
            if (Frame.ComputeCrc(raw) != crcReceived)
            {
                DropFront(1, "błąd CRC");
                continue;
            }

            _buffer.RemoveRange(0, total);
            var type = (ushort)(raw[5] | (raw[6] << 8));
            var id = raw[7];
            byte[]? password = null;
            var dataStart = 10;
            if (_framesHavePassword)
            {
                password = raw[10..16];
                dataStart = 16;
            }

            frame = new Frame(type, id, password, raw[dataStart..]);
            return true;
        }
    }

    private int FindMagic()
    {
        for (var i = 0; i + Frame.MagicLength <= _buffer.Count; i++)
        {
            if (_buffer[i] == (byte)'P' && _buffer[i + 1] == (byte)'M' && _buffer[i + 2] == (byte)'U')
            {
                return i;
            }
        }

        // Niepełny znacznik na końcu bufora traktujemy jako „jeszcze nie znaleziono”.
        return -1;
    }

    private void DropFront(int count, string reason)
    {
        if (count <= 0)
        {
            return;
        }

        var dropped = _buffer.GetRange(0, count).ToArray();
        _buffer.RemoveRange(0, count);
        Discarded?.Invoke(reason, dropped);
    }
}
