namespace MultiRejestrator.Protocol;

/// <summary>
/// Ramka protokołu: magic "PMU" (3 B), size (2 B), type (2 B), id (1 B), CRC (2 B),
/// password (6 B, tylko Master → Slave), data. Wszystkie pola little endian.
/// </summary>
/// <param name="Type">Pole TYPE.</param>
/// <param name="Id">Numer sekwencyjny; w odpowiedzi równy id zapytania.</param>
/// <param name="Password">Hasło (6 B) – tylko w ramkach zapytań; <c>null</c> w odpowiedziach.</param>
/// <param name="Data">Pole DATA.</param>
public sealed record Frame(ushort Type, byte Id, byte[]? Password, byte[] Data)
{
    public static ReadOnlySpan<byte> Magic => "PMU"u8;

    public const int MagicLength = 3;
    public const int SizeFieldLength = 2;
    public const int PasswordLength = 6;

    /// <summary>Bajty liczone w polu size poza hasłem i danymi: type (2) + id (1) + CRC (2).</summary>
    public const int SizeOverhead = 5;

    /// <summary>Offset pola CRC od początku ramki.</summary>
    public const int CrcOffset = 8;

    /// <summary>Maksymalny rozmiar pola DATA (pytanie 3.6: 1024 B; ramki firmware do 1029 B).</summary>
    public const int MaxDataLength = 1029;

    public bool HasPassword => Password is not null;

    /// <summary>Koduje ramkę do postaci bajtów gotowych do wysłania, z wyliczonym CRC.</summary>
    public byte[] Encode()
    {
        if (Password is not null && Password.Length != PasswordLength)
        {
            throw new ArgumentException($"Hasło musi mieć {PasswordLength} bajtów.", nameof(Password));
        }

        if (Data.Length > MaxDataLength)
        {
            throw new ArgumentException($"Pole DATA przekracza {MaxDataLength} B.", nameof(Data));
        }

        var passwordLength = Password?.Length ?? 0;
        var size = SizeOverhead + passwordLength + Data.Length;
        var buffer = new byte[MagicLength + SizeFieldLength + size];
        Magic.CopyTo(buffer);
        WriteUInt16(buffer, 3, (ushort)size);
        WriteUInt16(buffer, 5, Type);
        buffer[7] = Id;
        Password?.CopyTo(buffer, 10);
        Data.CopyTo(buffer, 10 + passwordLength);

        var crc = ComputeCrc(buffer);
        WriteUInt16(buffer, CrcOffset, crc);
        return buffer;
    }

    /// <summary>CRC liczone ze wszystkich pól ramki z pominięciem 2 bajtów CRC (pytania 2.2, 2.4).</summary>
    internal static ushort ComputeCrc(ReadOnlySpan<byte> frame)
    {
        var crc = Crc16Ccitt.Compute(frame[..CrcOffset]);
        return Crc16Ccitt.Compute(frame[(CrcOffset + 2)..], crc);
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }
}
