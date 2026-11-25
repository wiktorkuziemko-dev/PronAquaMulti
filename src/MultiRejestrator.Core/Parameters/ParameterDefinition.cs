namespace MultiRejestrator.Core.Parameters;

/// <summary>Kodowanie wartości w ramce.</summary>
public enum DataType
{
    U8,
    I8,
    U16,
    I16,
    U32,
    I32,
    Ascii,
    Bytes,
}

/// <summary>Sposób edycji i walidacji w programie (kolumna „edycja”).</summary>
public enum EditKind
{
    Number,
    Enum,
    Bitmask,
    Text,
    Digits,
    Bcd,
    Hex,
    Duration,
    Millivolts,

    /// <summary>Liczba z przecinkiem: wartość w programie = wartość w ramce × <see cref="ParameterDefinition.Scale"/>.</summary>
    Decimal,
}

/// <summary>Sekcja interfejsu (odpowiedź 8.3).</summary>
public enum Section
{
    General,
    Modem,
    Apn,
    Servers,
    Transmission,
    Channel,
}

/// <summary>Grupa parametrów kanału zależna od typu wejścia (Id 1001).</summary>
public enum InputGroup
{
    None,
    All,
    Pulse,
    Binary,
    Analog2V5,
    Analog10V,
    Analog4To20mA,
}

public sealed record ParameterOption(long Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Definicja jednego parametru (dla kanałów – już z rzeczywistym Id danego kanału).</summary>
public sealed class ParameterDefinition
{
    public required ushort Id { get; init; }

    /// <summary>Id z tabeli (dla kanałów – podstawa 1001–1061).</summary>
    public required ushort BaseId { get; init; }

    /// <summary>0–7 dla parametrów kanału, inaczej <c>null</c>.</summary>
    public int? Channel { get; init; }

    public required int Size { get; init; }

    public required DataType DataType { get; init; }

    public required EditKind EditKind { get; init; }

    /// <summary>Nazwa bez dopisku „kanału N” – do formularza kanału.</summary>
    public required string Name { get; init; }

    /// <summary>Nazwa z numerem kanału (1–8) – do pliku, porównania i komunikatów.</summary>
    public required string FullName { get; init; }

    public string Unit { get; init; } = string.Empty;

    public IReadOnlyList<ParameterOption> Options { get; init; } = [];

    public InputGroup InputGroup { get; init; }

    public Section Section { get; init; }

    public long? Min { get; init; }

    public long? Max { get; init; }

    public string DefaultValue { get; init; } = string.Empty;

    /// <summary>Dla <see cref="EditKind.Decimal"/>: mnożnik wartości z ramki (np. 0,1 – krok 100 mV pokazywany w V).</summary>
    public decimal Scale { get; init; } = 1m;

    /// <summary>Liczba cyfr po przecinku dla <see cref="EditKind.Decimal"/>.</summary>
    public int Decimals => Scale >= 1m ? 0 : (int)Math.Round(-Math.Log10((double)Scale));

    /// <summary>Pole obowiązkowe – puste blokuje wysyłanie (uwaga 6 z 10.11.2026).</summary>
    public bool IsRequired { get; init; }

    /// <summary>Bajty tekstu w ramce w odwrotnej kolejności (Id 3, uwaga 5 z 10.11.2026).</summary>
    public bool ReverseBytes { get; init; }

    public string Notes { get; init; } = string.Empty;

    public bool IsChannelParameter => Channel is not null;

    public bool IsNumeric => EditKind is EditKind.Number or EditKind.Enum or EditKind.Bitmask or EditKind.Duration or EditKind.Millivolts or EditKind.Decimal;

    public string OptionLabel(long value) =>
        Options.FirstOrDefault(o => o.Value == value)?.Label ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public override string ToString() => $"{Id} {FullName}";
}
