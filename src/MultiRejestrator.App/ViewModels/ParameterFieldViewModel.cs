using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

/// <summary>Jedno pole formularza powiązane z parametrem konfiguracji.</summary>
public partial class ParameterFieldViewModel : ObservableObject
{
    private readonly ConfigurationEditor _editor;
    private IReadOnlyList<ParameterOption> _options;

    public ParameterFieldViewModel(ParameterDefinition definition, ConfigurationEditor editor)
    {
        Definition = definition;
        _editor = editor;
        _options = definition.Options;
        Bits = definition.EditKind == EditKind.Bitmask
            ? definition.Options.Select(o => new BitOptionViewModel(this, o)).ToList()
            : [];
    }

    public ParameterDefinition Definition { get; }

    public ushort Id => Definition.Id;

    public string Label => Definition.Name;

    public string IdText => $"Id {Definition.Id}";

    public string Unit => Definition.EditKind switch
    {
        EditKind.Bcd => "8 cyfr",
        EditKind.Hex => $"{Value.Length}/{Definition.Max ?? Definition.Size * 2}",
        EditKind.Text when Definition.Size >= 18 => $"{Value.Length}/{Definition.Max ?? Definition.Size}",
        _ => Definition.Unit,
    };

    public string Tooltip => string.IsNullOrEmpty(Definition.Notes) ? Definition.FullName : $"{Definition.FullName}\n{Definition.Notes}";

    public bool IsEnum => Definition.EditKind == EditKind.Enum;

    public bool IsBitmask => Definition.EditKind == EditKind.Bitmask;

    public bool IsTextBox => !IsEnum && !IsBitmask;

    public bool IsMonospace => Definition.EditKind is EditKind.Hex or EditKind.Bcd or EditKind.Digits;

    public double InputWidth => Definition.EditKind switch
    {
        EditKind.Hex => 560,
        EditKind.Bcd => 140,
        EditKind.Digits => 90,
        EditKind.Text => Definition.Size switch
        {
            <= 2 => 70,
            <= 6 => 110,
            <= 18 => 330,
            _ => 520,
        },
        _ => 120,
    };

    public int MaxLength => Definition.EditKind switch
    {
        EditKind.Text or EditKind.Digits => (int)(Definition.Max ?? Definition.Size),
        EditKind.Hex => (int)(Definition.Max ?? Definition.Size * 2),
        EditKind.Bcd => 8,
        _ => 12,
    };

    public IReadOnlyList<BitOptionViewModel> Bits { get; }

    public IReadOnlyList<ParameterOption> Options
    {
        get => _options;
        set => SetProperty(ref _options, value);
    }

    public string Value
    {
        get => _editor.Configuration[Definition.Id];
        set
        {
            if (value == _editor.Configuration[Definition.Id])
            {
                return;
            }

            _editor.SetValue(Definition.Id, value);
        }
    }

    public long Number => ParameterCodec.TryGetNumber(Value) ?? 0;

    public ParameterOption? SelectedOption
    {
        get
        {
            var n = ParameterCodec.TryGetNumber(Value);
            return n is null ? null : Options.FirstOrDefault(o => o.Value == n) ?? new ParameterOption(n.Value, $"{n} (spoza listy)");
        }

        set
        {
            if (value is not null)
            {
                Value = value.Value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial bool IsChanged { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    /// <summary>Wywoływane po zmianie wartości w konfiguracji (także spoza tego pola).</summary>
    public void NotifyValueChanged()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(Unit));
        foreach (var bit in Bits)
        {
            bit.NotifyChanged();
        }
    }
}

public partial class BitOptionViewModel : ObservableObject
{
    private readonly ParameterFieldViewModel _field;

    public BitOptionViewModel(ParameterFieldViewModel field, ParameterOption option)
    {
        _field = field;
        Option = option;
    }

    public ParameterOption Option { get; }

    public string Label => Option.Label;

    public bool IsChecked
    {
        get => (_field.Number & Option.Value) != 0;
        set
        {
            var n = _field.Number;
            n = value ? n | Option.Value : n & ~Option.Value;
            _field.Value = n.ToString(CultureInfo.InvariantCulture);
        }
    }

    public void NotifyChanged() => OnPropertyChanged(nameof(IsChecked));
}
