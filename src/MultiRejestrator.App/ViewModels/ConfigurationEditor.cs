using MultiRejestrator.Core;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

/// <summary>
/// Konfiguracja edytowana w programie: bieżące wartości, punkt odniesienia (ostatnio wczytana / zapisana),
/// pola formularza dla wszystkich parametrów i wynik walidacji.
/// </summary>
public sealed class ConfigurationEditor
{
    private readonly Dictionary<ushort, ParameterFieldViewModel> _fields = new();

    public ConfigurationEditor(Configuration configuration)
    {
        Configuration = configuration;
        Baseline = configuration.Clone();
        foreach (var p in configuration.Catalog.All)
        {
            _fields[p.Id] = new ParameterFieldViewModel(p, this);
        }

        Revalidate();
    }

    public Configuration Configuration { get; private set; }

    /// <summary>Stan, względem którego oznaczane są zmiany i ostrzega się o niezapisanych zmianach (pkt 16).</summary>
    public Configuration Baseline { get; private set; }

    public IReadOnlyList<ValidationIssue> Issues { get; private set; } = [];

    public bool IsDirty => !Configuration.ContentEquals(Baseline);

    /// <summary>Po każdej zmianie wartości lub wczytaniu konfiguracji.</summary>
    public event Action? Changed;

    public ParameterFieldViewModel Field(ushort id) => _fields[id];

    public void SetValue(ushort id, string value)
    {
        Configuration[id] = value;
        _fields[id].NotifyValueChanged();
        Revalidate();
        Changed?.Invoke();
    }

    public void Load(Configuration configuration, bool markAsSaved = true)
    {
        Configuration = configuration.Clone();
        if (markAsSaved)
        {
            Baseline = configuration.Clone();
        }

        foreach (var f in _fields.Values)
        {
            f.NotifyValueChanged();
        }

        Revalidate();
        Changed?.Invoke();
    }

    /// <summary>Oznacza bieżący stan jako zapisany.</summary>
    public void MarkSaved()
    {
        Baseline = Configuration.Clone();
        Revalidate();
        Changed?.Invoke();
    }

    /// <summary>Zmienia wartość w bieżącym stanie i w punkcie odniesienia (np. po zmianie kodu, gdy plik już go zawiera).</summary>
    public void SetValueEverywhere(ushort id, string value)
    {
        Baseline[id] = value;
        SetValue(id, value);
    }

    public void CopyChannel(int from, int to)
    {
        Configuration.CopyChannel(from, to);
        foreach (var p in Configuration.Catalog.ForChannel(to))
        {
            _fields[p.Id].NotifyValueChanged();
        }

        Revalidate();
        Changed?.Invoke();
    }

    public int ErrorCount(Func<ParameterDefinition, bool> predicate) =>
        Issues.Count(i => predicate(Configuration.Catalog[i.ParameterId]));

    private void Revalidate()
    {
        Issues = ConfigurationValidator.Validate(Configuration);
        var errors = Issues.GroupBy(i => i.ParameterId).ToDictionary(g => g.Key, g => g.First().Message);
        foreach (var (id, field) in _fields)
        {
            field.Error = errors.GetValueOrDefault(id);
            field.IsChanged = !string.Equals(Configuration[id], Baseline[id], StringComparison.Ordinal);
        }
    }
}
