using System.Buffers.Binary;
using System.Text;

namespace MultiRejestrator.Protocol;

/// <summary>Dane z ramki RE_REQ_GET_FW_DATE.</summary>
public sealed record FirmwareInfo(ushort SvnRevision, string BuildDate, string BuildTime)
{
    public const int EncodedLength = 30;
    private const int TextLength = 14;

    /// <summary>Tekst do paska statusu (pytanie 3.3: daty wyświetlane bez zmian).</summary>
    public override string ToString() => $"r{SvnRevision} · {BuildDate} {BuildTime}".Trim();

    public byte[] Encode()
    {
        var data = new byte[EncodedLength];
        BinaryPrimitives.WriteUInt16LittleEndian(data, SvnRevision);
        AsciiText.Write(BuildDate, data.AsSpan(2, TextLength));
        AsciiText.Write(BuildTime, data.AsSpan(2 + TextLength, TextLength));
        return data;
    }

    public static FirmwareInfo Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != EncodedLength)
        {
            throw new ProtocolException($"RE_REQ_GET_FW_DATE: oczekiwano {EncodedLength} B, odebrano {data.Length} B.");
        }

        return new FirmwareInfo(
            BinaryPrimitives.ReadUInt16LittleEndian(data),
            AsciiText.Read(data.Slice(2, TextLength)),
            AsciiText.Read(data.Slice(2 + TextLength, TextLength)));
    }
}

/// <summary>Teksty ASCII o stałej długości: kończą się na 0x00 lub znaku spoza ASCII; reszta pola dopełniana 0x00.</summary>
public static class AsciiText
{
    public static string Read(ReadOnlySpan<byte> field)
    {
        var sb = new StringBuilder(field.Length);
        foreach (var b in field)
        {
            if (b == 0 || b > 0x7E)
            {
                break;
            }

            sb.Append((char)b);
        }

        return sb.ToString();
    }

    public static void Write(string text, Span<byte> field)
    {
        field.Clear();
        for (var i = 0; i < field.Length && i < text.Length; i++)
        {
            var c = text[i];
            field[i] = c <= 0x7E ? (byte)c : (byte)'?';
        }
    }

    public static byte[] Encode(string text, int length)
    {
        var field = new byte[length];
        Write(text, field);
        return field;
    }
}

/// <summary>Czas urządzenia: Unix timestamp UTC w sekundach (pytanie 3.14).</summary>
public static class DeviceTime
{
    public static DateTimeOffset Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != 4)
        {
            throw new ProtocolException($"Pole czasu: oczekiwano 4 B, odebrano {data.Length} B.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32LittleEndian(data));
    }

    public static byte[] Encode(DateTimeOffset time)
    {
        var seconds = time.ToUnixTimeSeconds();
        if (seconds < 0 || seconds > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Czas poza zakresem 32-bitowego znacznika Unix.");
        }

        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)seconds);
        return data;
    }
}

/// <summary>Typ wejścia kanału (Id 1001 konfiguracji, pole Input type ramki Measure Report).</summary>
public enum InputType : byte
{
    Inactive = 0,
    Pulse = 1,
    Binary = 2,
    Analog2V5 = 3,
    Analog10V = 4,
    Analog4To20mA = 5,
}

public abstract record MeasureRecord(ushort MinuteOfDay);

public sealed record PulseRecord(ushort MinuteOfDay, int Counter) : MeasureRecord(MinuteOfDay);

public sealed record BinaryRecord(ushort MinuteOfDay, byte State) : MeasureRecord(MinuteOfDay);

public sealed record AnalogRecord(ushort MinuteOfDay, byte StabilizationStatus, short StabilizationTimeMs, short Min, short Max, short Mean) : MeasureRecord(MinuteOfDay);

/// <summary>Jedna sekcja odpowiedzi RE_REQ_GET_MEASURE_REPORT: nagłówek 18 B i bloki raportów kanału.</summary>
public sealed record MeasureReportSection(
    uint DayIndex,
    uint DayBufferSize,
    DateTimeOffset DayStart,
    InputType InputType,
    byte ChannelNumber,
    IReadOnlyList<MeasureRecord> Records)
{
    public const int HeaderLength = 18;

    public static int RecordLength(InputType type) => type switch
    {
        InputType.Pulse => 6,
        InputType.Binary => 3,
        InputType.Analog2V5 or InputType.Analog10V or InputType.Analog4To20mA => 11,
        _ => 0,
    };

    public static byte[] EncodeRequest(uint dayIndex, ushort startMinute)
    {
        var data = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(data, dayIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), startMinute);
        return data;
    }

    public static (uint DayIndex, ushort StartMinute) DecodeRequest(ReadOnlySpan<byte> data)
    {
        if (data.Length != 6)
        {
            throw new ProtocolException($"REQ_GET_MEASURE_REPORT: oczekiwano 6 B, odebrano {data.Length} B.");
        }

        return (BinaryPrimitives.ReadUInt32LittleEndian(data), BinaryPrimitives.ReadUInt16LittleEndian(data[4..]));
    }

    public byte[] Encode()
    {
        var length = RecordLength(InputType);
        var data = new byte[HeaderLength + (Records.Count * length)];
        var span = data.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, DayIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], DayBufferSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)DayStart.ToUnixTimeSeconds());
        span[12] = (byte)InputType;
        span[13] = ChannelNumber;
        BinaryPrimitives.WriteUInt32LittleEndian(span[14..], (uint)Records.Count);
        var offset = HeaderLength;
        foreach (var record in Records)
        {
            var r = span.Slice(offset, length);
            BinaryPrimitives.WriteUInt16LittleEndian(r, record.MinuteOfDay);
            switch (record)
            {
                case PulseRecord p:
                    BinaryPrimitives.WriteInt32LittleEndian(r[2..], p.Counter);
                    break;
                case BinaryRecord b:
                    r[2] = b.State;
                    break;
                case AnalogRecord a:
                    r[2] = a.StabilizationStatus;
                    BinaryPrimitives.WriteInt16LittleEndian(r[3..], a.StabilizationTimeMs);
                    BinaryPrimitives.WriteInt16LittleEndian(r[5..], a.Min);
                    BinaryPrimitives.WriteInt16LittleEndian(r[7..], a.Max);
                    BinaryPrimitives.WriteInt16LittleEndian(r[9..], a.Mean);
                    break;
            }

            offset += length;
        }

        return data;
    }

    // ZAŁOŻENIE: odpowiedź może zawierać kilka sekcji (kanałów) jedna po drugiej – dekodujemy do końca danych.
    // Protokół rev3871 opisuje jedną sekcję i nie podaje, jak wybrać kanał (patrz docs/decyzje.md).
    public static IReadOnlyList<MeasureReportSection> DecodeResponse(ReadOnlySpan<byte> data)
    {
        var sections = new List<MeasureReportSection>();
        var offset = 0;
        do
        {
            if (data.Length - offset < HeaderLength)
            {
                throw new ProtocolException($"RE_REQ_GET_MEASURE_REPORT: niekompletny nagłówek (offset {offset}, {data.Length - offset} B).");
            }

            var h = data[offset..];
            var dayIndex = BinaryPrimitives.ReadUInt32LittleEndian(h);
            var bufferSize = BinaryPrimitives.ReadUInt32LittleEndian(h[4..]);
            var time = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32LittleEndian(h[8..]));
            var type = (InputType)h[12];
            var channel = h[13];
            var count = BinaryPrimitives.ReadUInt32LittleEndian(h[14..]);
            offset += HeaderLength;

            var length = RecordLength(type);
            if (count > 0 && length == 0)
            {
                throw new ProtocolException($"RE_REQ_GET_MEASURE_REPORT: nieznany rozmiar bloku dla typu wejścia {(byte)type}.");
            }

            if ((long)count * length > data.Length - offset)
            {
                throw new ProtocolException($"RE_REQ_GET_MEASURE_REPORT: zadeklarowano {count} raportów, brakuje danych.");
            }

            var records = new List<MeasureRecord>((int)count);
            for (var i = 0; i < count; i++)
            {
                var r = data.Slice(offset, length);
                var minute = BinaryPrimitives.ReadUInt16LittleEndian(r);
                records.Add(type switch
                {
                    InputType.Pulse => new PulseRecord(minute, BinaryPrimitives.ReadInt32LittleEndian(r[2..])),
                    InputType.Binary => new BinaryRecord(minute, r[2]),
                    _ => new AnalogRecord(minute, r[2], BinaryPrimitives.ReadInt16LittleEndian(r[3..]), BinaryPrimitives.ReadInt16LittleEndian(r[5..]), BinaryPrimitives.ReadInt16LittleEndian(r[7..]), BinaryPrimitives.ReadInt16LittleEndian(r[9..])),
                });
                offset += length;
            }

            sections.Add(new MeasureReportSection(dayIndex, bufferSize, time, type, channel, records));
        }
        while (offset < data.Length);

        return sections;
    }
}
