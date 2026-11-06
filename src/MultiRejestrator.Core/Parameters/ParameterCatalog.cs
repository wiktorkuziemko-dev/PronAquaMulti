using System.Globalization;

namespace MultiRejestrator.Core.Parameters;

/// <summary>
/// Katalog parametrów wczytany z wbudowanej tabeli <c>docs/specyfikacja/parametry.csv</c>.
/// Parametry kanałów są rozwinięte na 8 kanałów (Id = podstawa + N × 1000).
/// </summary>
public sealed class ParameterCatalog
{
    public const int ChannelCount = 8;

    private static readonly Lazy<ParameterCatalog> LazyConfiguration = new(() => Load("parametry.csv", isDeviceState: false));
    private static readonly Lazy<ParameterCatalog> LazyDeviceState = new(() => Load("stan-urzadzenia.csv", isDeviceState: true));

    private readonly Dictionary<ushort, ParameterDefinition> _byId;

    private ParameterCatalog(IReadOnlyList<ParameterDefinition> all)
    {
        All = all;
        _byId = all.ToDictionary(p => p.Id);
    }

    /// <summary>Parametry konfiguracyjne (517).</summary>
    public static ParameterCatalog Configuration => LazyConfiguration.Value;

    /// <summary>Parametry ramki Device State (15).</summary>
    public static ParameterCatalog DeviceState => LazyDeviceState.Value;

    public IReadOnlyList<ParameterDefinition> All { get; }

    public ParameterDefinition this[ushort id] => _byId[id];

    public bool TryGet(ushort id, out ParameterDefinition definition) => _byId.TryGetValue(id, out definition!);

    public bool Contains(ushort id) => _byId.ContainsKey(id);

    public IEnumerable<ParameterDefinition> InSection(Section section) => All.Where(p => p.Section == section);

    public IEnumerable<ParameterDefinition> ForChannel(int channel) => All.Where(p => p.Channel == channel);

    public static ushort ChannelId(ushort baseId, int channel) => (ushort)(baseId + (channel * 1000));

    private static ParameterCatalog Load(string resource, bool isDeviceState)
    {
        var rows = CsvTable.ReadResource(resource);
        var list = new List<ParameterDefinition>();
        foreach (var row in rows)
        {
            var baseId = ushort.Parse(row["id"], CultureInfo.InvariantCulture);
            if (isDeviceState)
            {
                list.Add(Create(row, baseId, null, isDeviceState));
                continue;
            }

            if (row["kanal"] == "tak")
            {
                for (var ch = 0; ch < ChannelCount; ch++)
                {
                    list.Add(Create(row, baseId, ch, isDeviceState));
                }
            }
            else
            {
                list.Add(Create(row, baseId, null, isDeviceState));
            }
        }

        return new ParameterCatalog(list.OrderBy(p => p.Id).ToList());
    }

    private static ParameterDefinition Create(Dictionary<string, string> row, ushort baseId, int? channel, bool isDeviceState)
    {
        var rawName = row["nazwa"];
        var name = rawName.Replace(" kanału N", string.Empty, StringComparison.Ordinal).Trim();
        var fullName = channel is null ? rawName : rawName.Replace("kanału N", $"kanału {channel + 1}", StringComparison.Ordinal);
        var editColumn = isDeviceState ? row["prezentacja"] : row["edycja"];
        var dataType = row["typ"] switch
        {
            "u8" => DataType.U8,
            "i8" => DataType.I8,
            "u16" => DataType.U16,
            "i16" => DataType.I16,
            "u32" => DataType.U32,
            "ascii" => DataType.Ascii,
            "bytes" => DataType.Bytes,
            var t => throw new InvalidDataException($"Nieznany typ „{t}” parametru {baseId}."),
        };
        var editKind = editColumn switch
        {
            "number" => EditKind.Number,
            "enum" => EditKind.Enum,
            "bitmask" => EditKind.Bitmask,
            "text" => EditKind.Text,
            "digits" => EditKind.Digits,
            "bcd" => EditKind.Bcd,
            "hex" => EditKind.Hex,
            "duration" => EditKind.Duration,
            "millivolts" => EditKind.Millivolts,
            var e => throw new InvalidDataException($"Nieznany sposób edycji „{e}” parametru {baseId}."),
        };

        var (typeMin, typeMax) = TypeRange(dataType);
        long? min = null;
        long? max = null;
        if (!isDeviceState)
        {
            min = ParseLong(row["min"]);
            max = ParseLong(row["max"]);
        }

        if (editKind is EditKind.Number or EditKind.Enum or EditKind.Bitmask && dataType is not DataType.Ascii and not DataType.Bytes)
        {
            min ??= typeMin;
            max ??= typeMax;
        }

        var defaultValue = isDeviceState ? string.Empty : row["domyslna"];
        if (defaultValue.StartsWith('('))
        {
            defaultValue = string.Empty; // np. „(z ustawień programu)” – uzupełniane przy tworzeniu konfiguracji
        }

        return new ParameterDefinition
        {
            Id = channel is null ? baseId : ParameterCatalog.ChannelId(baseId, channel.Value),
            BaseId = baseId,
            Channel = channel,
            Size = int.Parse(row["rozmiar_B"], CultureInfo.InvariantCulture),
            DataType = dataType,
            EditKind = editKind,
            Name = char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..],
            FullName = fullName,
            Unit = row.GetValueOrDefault("jednostka", string.Empty),
            Options = ParseOptions(row["wartosci"]),
            InputGroup = isDeviceState ? InputGroup.None : row["grupa_wejscia"] switch
            {
                "wszystkie" => InputGroup.All,
                "impulsowe" => InputGroup.Pulse,
                "binarne" => InputGroup.Binary,
                "analog_2_5V" => InputGroup.Analog2V5,
                "analog_10V" => InputGroup.Analog10V,
                "analog_4_20mA" => InputGroup.Analog4To20mA,
                _ => InputGroup.None,
            },
            Section = isDeviceState ? Section.General : row["sekcja"] switch
            {
                "ogolne" => Section.General,
                "modem" => Section.Modem,
                "apn" => Section.Apn,
                "serwery" => Section.Servers,
                "transmisja" => Section.Transmission,
                _ => Section.Channel,
            },
            Min = min,
            Max = max,
            DefaultValue = defaultValue,
            Notes = row.GetValueOrDefault("uwagi", string.Empty),
        };
    }

    internal static (long Min, long Max) TypeRange(DataType type) => type switch
    {
        DataType.U8 => (0, byte.MaxValue),
        DataType.I8 => (sbyte.MinValue, sbyte.MaxValue),
        DataType.U16 => (0, ushort.MaxValue),
        DataType.I16 => (short.MinValue, short.MaxValue),
        DataType.U32 => (0, uint.MaxValue),
        _ => (0, 0),
    };

    private static long? ParseLong(string text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static List<ParameterOption> ParseOptions(string text)
    {
        var options = new List<ParameterOption>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return options;
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq].Trim();
            var value = key.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(key[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(key, CultureInfo.InvariantCulture);
            options.Add(new ParameterOption(value, part[(eq + 1)..].Trim()));
        }

        return options;
    }
}
