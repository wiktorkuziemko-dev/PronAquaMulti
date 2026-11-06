using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    public abstract string Title { get; }

    public virtual string Lead => string.Empty;
}

public sealed record FieldCard(string Title, IReadOnlyList<ParameterFieldViewModel> Fields)
{
    public bool HasTitle => !string.IsNullOrEmpty(Title);
}

/// <summary>Strona sekcji parametrów ogólnych (Ogólne, Modem/LTE, APN, Serwery i MQTT, Transmisja).</summary>
public sealed partial class SectionPageViewModel : PageViewModel
{
    private readonly string _title;
    private readonly string _lead;

    public SectionPageViewModel(Section section, string title, string lead, IReadOnlyList<FieldCard> cards, MainViewModel main)
    {
        Section = section;
        _title = title;
        _lead = lead;
        Cards = cards;
        Main = main;
    }

    public Section Section { get; }

    public override string Title => _title;

    public override string Lead => _lead;

    public IReadOnlyList<FieldCard> Cards { get; }

    public MainViewModel Main { get; }

    public bool IsGeneral => Section == Section.General;

    public static SectionPageViewModel Create(Section section, ConfigurationEditor editor, MainViewModel main)
    {
        FieldCard Card(string title, params ushort[] ids) => new(title, ids.Select(editor.Field).ToList());
        return section switch
        {
            Section.General => new(section, "Ogólne", "Identyfikacja urządzenia i parametry wspólne. Najedź na nazwę parametru, aby zobaczyć opis.",
                [Card("Identyfikacja", 1, 3, 29), Card("Urządzenie", 4, 27, 28)], main),
            Section.Modem => new(section, "Modem / LTE", "Moduł komórkowy, pasma i parametry oszczędzania energii.",
                [Card("Moduł i sieć", 5, 10, 11, 12, 6), Card("Sesja i oszczędzanie energii", 13, 14, 15)], main),
            Section.Apn => new(section, "APN", "Punkt dostępowy sieci komórkowej. Pola tekstowe: do 18 znaków ASCII.",
                [Card(string.Empty, 7, 8, 9)], main),
            Section.Servers => new(section, "Serwery i MQTT", "Adresy nie są sprawdzane – port, login i hasło dla TCP, HTTPS, FTP i SFTP podaje się w polu URL. Pola: do 32 znaków ASCII.",
                [Card("Serwery", 16, 17), Card("MQTT", 18, 19, 20, 21, 22, 23)], main),
            Section.Transmission => new(section, "Transmisja i szyfrowanie", "Protokół i format raportów oraz klucz szyfrowania AES-256 (64 znaki szesnastkowe).",
                [Card(string.Empty, 24, 25, 26)], main),
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };
    }
}

/// <summary>Wiersz tabeli alarmów (4 alarmy na grupę analogową).</summary>
public sealed class AlarmRowViewModel
{
    public required int Number { get; init; }

    public required ParameterFieldViewModel On { get; init; }

    public required ParameterFieldViewModel Off { get; init; }

    public required ParameterFieldViewModel Activity { get; init; }

    public required ParameterFieldViewModel Direction { get; init; }

    public string IdsText => $"Id {On.Id}, {Off.Id}, {Activity.Id}, {Direction.Id}";
}

/// <summary>Grupa parametrów kanału zależna od typu wejścia.</summary>
public sealed partial class InputGroupViewModel : ObservableObject
{
    public required string Title { get; init; }

    public required InputGroup Group { get; init; }

    public required IReadOnlyList<ParameterFieldViewModel> Fields { get; init; }

    public IReadOnlyList<AlarmRowViewModel> Alarms { get; init; } = [];

    public bool HasAlarms => Alarms.Count > 0;

    public string Unit { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public string StatusText => IsActive ? "aktywne dla wybranego typu" : IsExpanded ? "wyszarzone – kliknij, aby zwinąć" : "nieaktywne dla tego typu – kliknij, aby podejrzeć";

    [RelayCommand]
    private void Toggle()
    {
        if (!IsActive)
        {
            IsExpanded = !IsExpanded;
            OnPropertyChanged(nameof(StatusText));
        }
    }
}

/// <summary>Strona kanału: typ wejścia, grupy parametrów, tabela alarmów, blokada sprzężenia.</summary>
public sealed partial class ChannelPageViewModel : PageViewModel
{
    private readonly ConfigurationEditor _editor;
    private readonly MainViewModel _main;

    public ChannelPageViewModel(int channel, ConfigurationEditor editor, MainViewModel main)
    {
        Channel = channel;
        _editor = editor;
        _main = main;
        ushort Id(ushort baseId) => ParameterCatalog.ChannelId(baseId, channel);
        var catalog = editor.Configuration.Catalog;
        TypeField = editor.Field(Id(ParameterIds.InputType));
        ModeField = editor.Field(Id(ParameterIds.PulseMode));

        var groups = new List<InputGroupViewModel>
        {
            new() { Title = "Wejście impulsowe", Group = InputGroup.Pulse, Fields = Fields(catalog, channel, InputGroup.Pulse, editor) },
            new() { Title = "Wejście binarne", Group = InputGroup.Binary, Fields = Fields(catalog, channel, InputGroup.Binary, editor) },
        };
        foreach (var (group, title, unit) in new[]
                 {
                     (InputGroup.Analog2V5, "Wejście analogowe 2,5 V", "mV"),
                     (InputGroup.Analog10V, "Wejście analogowe 10 V", "mV"),
                     (InputGroup.Analog4To20mA, "Wejście analogowe 4–20 mA", "mA"),
                 })
        {
            var alarms = Enumerable.Range(0, 4).Select(a => new AlarmRowViewModel
            {
                Number = a + 1,
                On = editor.Field(ParameterIds.AlarmOn(group, a, channel)),
                Off = editor.Field(ParameterIds.AlarmOff(group, a, channel)),
                Activity = editor.Field(ParameterIds.AlarmActivity(group, a, channel)),
                Direction = editor.Field(ParameterIds.AlarmDirection(group, a, channel)),
            }).ToList();
            var alarmIds = alarms.SelectMany(a => new[] { a.On.Id, a.Off.Id, a.Activity.Id, a.Direction.Id }).ToHashSet();
            groups.Add(new InputGroupViewModel
            {
                Title = title,
                Group = group,
                Unit = unit,
                Fields = Fields(catalog, channel, group, editor).Where(f => !alarmIds.Contains(f.Id)).ToList(),
                Alarms = alarms,
            });
        }

        Groups = groups;
        Refresh();
    }

    public int Channel { get; }

    public override string Title => $"Kanał {Channel + 1}";

    public override string Lead => "Wszystkie parametry kanału są zawsze odczytywane i wysyłane. Grupy niepasujące do typu wejścia są wyszarzone.";

    public ParameterFieldViewModel TypeField { get; }

    public ParameterFieldViewModel ModeField { get; }

    public IReadOnlyList<InputGroupViewModel> Groups { get; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial string LockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModeHint { get; set; } = string.Empty;

    private int? _owner;

    /// <summary>Odświeża stan po zmianie konfiguracji: blokada, aktywna grupa, dostępność pól.</summary>
    public void Refresh()
    {
        var config = _editor.Configuration;
        _owner = config.CoupledOwner(Channel);
        IsLocked = _owner is not null;
        LockText = IsLocked
            ? $"Kanał {Channel + 1} jest sprzężony z kanałem {_owner + 1} (tryb sprzężony wejścia impulsowego). Jego ustawienia określa kanał {_owner + 1}."
            : string.Empty;
        ModeHint = Channel % 2 == 0
            ? $"Tryby sprzężone łączą kanały {Channel + 1} i {Channel + 2}. Kanał {Channel + 2} zostanie wtedy zablokowany."
            : $"Tryby sprzężone ustawia się na kanale {Channel} (pierwszym z pary {Channel}+{Channel + 1}).";

        // Tryby sprzężone tylko na pierwszym kanale pary (odpowiedź 4.3.2).
        ModeField.Options = Channel % 2 == 0
            ? ModeField.Definition.Options
            : ModeField.Definition.Options.Where(o => o.Value == 0 || o.Value == ModeField.Number).ToList();

        var active = ParameterIds.GroupForInputType(config.InputType(Channel));
        TypeField.IsEnabled = !IsLocked;
        foreach (var g in Groups)
        {
            g.IsActive = !IsLocked && g.Group == active;
            if (g.IsActive)
            {
                g.IsExpanded = true;
            }

            var enabled = g.IsActive;
            foreach (var f in g.Fields)
            {
                f.IsEnabled = enabled;
            }

            foreach (var a in g.Alarms)
            {
                a.On.IsEnabled = a.Off.IsEnabled = a.Activity.IsEnabled = a.Direction.IsEnabled = enabled;
            }
        }
    }

    [RelayCommand]
    private void GoToOwner()
    {
        if (_owner is { } o)
        {
            _main.NavigateToChannel(o);
        }
    }

    [RelayCommand]
    private Task CopySettings() => _main.CopyChannelAsync(Channel);

    private static List<ParameterFieldViewModel> Fields(ParameterCatalog catalog, int channel, InputGroup group, ConfigurationEditor editor) =>
        catalog.ForChannel(channel).Where(p => p.InputGroup == group).Select(p => editor.Field(p.Id)).ToList();
}
