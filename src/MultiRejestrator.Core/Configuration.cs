using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.Core;

/// <summary>Konfiguracja urządzenia: wartości wszystkich parametrów w postaci tekstowej (Id → wartość).</summary>
public sealed class Configuration
{
    private readonly Dictionary<ushort, string> _values;

    public Configuration(ParameterCatalog catalog)
        : this(catalog, new Dictionary<ushort, string>())
    {
    }

    private Configuration(ParameterCatalog catalog, Dictionary<ushort, string> values)
    {
        Catalog = catalog;
        _values = values;
    }

    public ParameterCatalog Catalog { get; }

    public IReadOnlyDictionary<ushort, string> Values => _values;

    public string this[ushort id]
    {
        get => _values.TryGetValue(id, out var v) ? v : string.Empty;
        set => _values[id] = value ?? string.Empty;
    }

    public long? Number(ushort id) => ParameterCodec.TryGetNumber(this[id]);

    /// <summary>Nowa konfiguracja: zera i puste napisy, kanały nieaktywne (4.1.2), kod dostępu z ustawień (5.3).</summary>
    public static Configuration CreateDefault(string accessCode, ParameterCatalog? catalog = null)
    {
        catalog ??= ParameterCatalog.Configuration;
        var config = new Configuration(catalog);
        foreach (var p in catalog.All)
        {
            config[p.Id] = p.DefaultValue;
        }

        config[ParameterIds.AccessCode] = accessCode;
        return config;
    }

    public Configuration Clone() => new(Catalog, new Dictionary<ushort, string>(_values));

    public bool ContentEquals(Configuration other) =>
        Catalog.All.All(p => string.Equals(this[p.Id], other[p.Id], StringComparison.Ordinal));

    /// <summary>Kopiuje wszystkie parametry kanału na inny kanał (odpowiedź 4.3.12).</summary>
    public void CopyChannel(int from, int to)
    {
        foreach (var p in Catalog.ForChannel(from))
        {
            this[ParameterCatalog.ChannelId(p.BaseId, to)] = this[p.Id];
        }
    }

    public long InputType(int channel) => Number(ParameterCatalog.ChannelId(ParameterIds.InputType, channel)) ?? 0;

    /// <summary>
    /// Kanał zablokowany przez sprzężenie: drugi kanał pary (1+2, 3+4, 5+6, 7+8), gdy pierwszy jest
    /// wejściem impulsowym w trybie sprzężonym (odpowiedź 4.3.2). Zwraca indeks kanału nadrzędnego albo <c>null</c>.
    /// </summary>
    public int? CoupledOwner(int channel)
    {
        if (channel % 2 == 0)
        {
            return null;
        }

        var owner = channel - 1;
        var mode = Number(ParameterCatalog.ChannelId(ParameterIds.PulseMode, owner)) ?? 0;
        return InputType(owner) == 1 && mode != 0 ? owner : null;
    }

    public bool IsCoupledPairOwner(int channel) =>
        channel % 2 == 0 && InputType(channel) == 1 && (Number(ParameterCatalog.ChannelId(ParameterIds.PulseMode, channel)) ?? 0) != 0;
}
