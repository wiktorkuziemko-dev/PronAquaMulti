using System.Buffers.Binary;
using System.Globalization;
using MultiRejestrator.Protocol;

namespace MultiRejestrator.Core.Parameters;

/// <summary>
/// Zamiana wartości parametrów między postacią tekstową (formularz, plik) a bajtami w ramce.
/// Postać tekstowa: liczby dziesiętne, numer seryjny jako 8 cyfr BCD, klucz AES jako hex, teksty jak są.
/// </summary>
public static class ParameterCodec
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static byte[] Encode(ParameterDefinition def, string text)
    {
        var bytes = new byte[def.Size];
        switch (def.EditKind)
        {
            case EditKind.Bcd:
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, uint.Parse(text, NumberStyles.HexNumber, Inv));
                return bytes;
            case EditKind.Hex:
                var raw = Convert.FromHexString(text);
                raw.AsSpan(0, Math.Min(raw.Length, bytes.Length)).CopyTo(bytes);
                return bytes;
        }

        switch (def.DataType)
        {
            case DataType.Ascii:
                return AsciiText.Encode(text, def.Size);
            case DataType.Bytes:
                return Convert.FromHexString(text.PadRight(def.Size * 2, '0'))[..def.Size];
        }

        var value = long.Parse(text, NumberStyles.Integer, Inv);
        WriteInteger(bytes, value);
        return bytes;
    }

    /// <summary>Dekoduje wartość. Liczby są czytane według faktycznej długości pola (Device State Id 3).</summary>
    public static string Decode(ParameterDefinition def, ReadOnlySpan<byte> value)
    {
        switch (def.EditKind)
        {
            case EditKind.Bcd:
                return ReadUnsigned(value).ToString("X8", Inv);
            case EditKind.Hex:
                return Convert.ToHexString(value);
        }

        return def.DataType switch
        {
            DataType.Ascii => AsciiText.Read(value),
            DataType.Bytes => Convert.ToHexString(value),
            DataType.I8 or DataType.I16 => ReadSigned(value).ToString(Inv),
            _ => ReadUnsigned(value).ToString(Inv),
        };
    }

    /// <summary>Sprawdza format i zakres wartości; zwraca opis błędu albo <c>null</c>.</summary>
    public static string? Validate(ParameterDefinition def, string? text)
    {
        text ??= string.Empty;
        switch (def.EditKind)
        {
            case EditKind.Bcd:
                return text.Length == 8 && text.All(char.IsAsciiDigit) ? null : "Wpisz dokładnie 8 cyfr.";
            case EditKind.Hex:
                var hexLen = (int)(def.Max ?? def.Size * 2);
                return text.Length == hexLen && text.All(char.IsAsciiHexDigit) ? null : $"Wpisz dokładnie {hexLen} znaki szesnastkowe (0–9, A–F).";
            case EditKind.Digits:
                if (!text.All(char.IsAsciiDigit))
                {
                    return "Dozwolone są tylko cyfry.";
                }

                return LengthError(def, text);
            case EditKind.Text:
                if (text.Any(c => c < 0x20 || c > 0x7E))
                {
                    return "Dozwolone są tylko znaki ASCII – bez polskich liter.";
                }

                return LengthError(def, text);
        }

        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var value))
        {
            return "Wpisz liczbę całkowitą.";
        }

        if (def.EditKind == EditKind.Enum && def.Options.Count > 0 && def.Options.All(o => o.Value != value))
        {
            return $"Wartość {value} nie należy do listy dozwolonych.";
        }

        var min = def.Min ?? ParameterCatalog.TypeRange(def.DataType).Min;
        var max = def.Max ?? ParameterCatalog.TypeRange(def.DataType).Max;
        return value < min || value > max ? $"Dozwolony zakres: {min}–{max}." : null;
    }

    /// <summary>Wartość do wyświetlenia: opis z listy wartości, liczby z jednostką.</summary>
    public static string Format(ParameterDefinition def, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return def.IsNumeric ? "—" : "(puste)";
        }

        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var n) || !def.IsNumeric)
        {
            return text;
        }

        return def.EditKind switch
        {
            EditKind.Enum => def.OptionLabel(n),
            EditKind.Bitmask => FormatBitmask(def, n),
            EditKind.Millivolts => (n / 1000.0).ToString("0.00", CultureInfo.CurrentCulture) + " V",
            EditKind.Duration => FormatDuration(n),
            _ => string.IsNullOrEmpty(def.Unit) ? text : $"{text} {def.Unit}",
        };
    }

    public static long? TryGetNumber(string? text) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var v) ? v : null;

    private static string FormatBitmask(ParameterDefinition def, long value)
    {
        var labels = def.Options.Where(o => (value & o.Value) != 0).Select(o => o.Label).ToList();
        return labels.Count == 0 ? "brak" : string.Join(", ", labels);
    }

    private static string FormatDuration(long seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.Days > 0 ? $"{t.Days} d {t:hh\\:mm\\:ss}" : t.ToString("hh\\:mm\\:ss", Inv);
    }

    private static string? LengthError(ParameterDefinition def, string text)
    {
        var min = (int)(def.Min ?? 0);
        var max = (int)(def.Max ?? def.Size);
        if (text.Length >= min && text.Length <= max)
        {
            return null;
        }

        return min == max ? $"Wpisz dokładnie {max} znaków." : $"Długość od {min} do {max} znaków.";
    }

    private static void WriteInteger(Span<byte> bytes, long value)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(value >> (8 * i));
        }
    }

    private static ulong ReadUnsigned(ReadOnlySpan<byte> value)
    {
        ulong result = 0;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            result = (result << 8) | value[i];
        }

        return result;
    }

    private static long ReadSigned(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        var u = ReadUnsigned(value);
        var bits = value.Length * 8;
        if (bits < 64 && (u & (1UL << (bits - 1))) != 0)
        {
            return (long)(u | (ulong.MaxValue << bits));
        }

        return (long)u;
    }
}
