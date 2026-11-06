namespace MultiRejestrator.Protocol;

/// <summary>Kody błędów z ramki ERROR_FRAME (maska bitowa – pytanie 2.11).</summary>
[Flags]
public enum DeviceErrorFlags : ushort
{
    None = 0,
    Crc = 0x01,
    FrameSize = 0x02,
    FlashAccess = 0x04,
    WrongPassword = 0x08,
    UnknownFrameType = 0x10,
}

public class ProtocolException : Exception
{
    public ProtocolException(string message)
        : base(message)
    {
    }

    public ProtocolException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>Urządzenie odpowiedziało ramką błędu.</summary>
public sealed class DeviceErrorException : ProtocolException
{
    public DeviceErrorException(DeviceErrorFlags flags)
        : base(Describe(flags))
    {
        Flags = flags;
    }

    public DeviceErrorFlags Flags { get; }

    public static string Describe(DeviceErrorFlags flags)
    {
        var parts = new List<string>();
        if (flags.HasFlag(DeviceErrorFlags.Crc)) parts.Add("błąd CRC");
        if (flags.HasFlag(DeviceErrorFlags.FrameSize)) parts.Add("błędny rozmiar ramki");
        if (flags.HasFlag(DeviceErrorFlags.FlashAccess)) parts.Add("błąd dostępu do pamięci flash");
        if (flags.HasFlag(DeviceErrorFlags.WrongPassword)) parts.Add("błędne hasło dostępu");
        if (flags.HasFlag(DeviceErrorFlags.UnknownFrameType)) parts.Add("nieznany typ ramki");
        var known = DeviceErrorFlags.Crc | DeviceErrorFlags.FrameSize | DeviceErrorFlags.FlashAccess | DeviceErrorFlags.WrongPassword | DeviceErrorFlags.UnknownFrameType;
        if ((flags & ~known) != 0) parts.Add($"nieznany kod 0x{(ushort)(flags & ~known):X2}");
        return $"Urządzenie zgłosiło błąd 0x{(ushort)flags:X2}: {string.Join(", ", parts)}.";
    }
}

/// <summary>Urządzenie nie odpowiedziało w wyznaczonym czasie.</summary>
public sealed class DeviceTimeoutException : ProtocolException
{
    public DeviceTimeoutException(string command, TimeSpan timeout)
        : base($"Brak odpowiedzi urządzenia na {command} w ciągu {timeout.TotalSeconds:0.#} s.")
    {
        Command = command;
    }

    public string Command { get; }
}

/// <summary>Port szeregowy nie działa (zamknięty, odłączony, błąd zapisu).</summary>
public sealed class TransportException : ProtocolException
{
    public TransportException(string message, Exception? inner = null)
        : base(message, inner ?? new IOException(message))
    {
    }
}
