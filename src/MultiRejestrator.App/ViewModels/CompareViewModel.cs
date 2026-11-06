using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core;
using MultiRejestrator.Core.Files;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

public sealed record CompareRow(string Id, string Label, string Left, string Right, bool IsDifferent)
{
    public string Tag => IsDifferent ? "różnica" : "zgodne";
}

/// <summary>Porównanie konfiguracji parametr po parametrze (pkt 10 Założeń). Niczego nie zapisuje.</summary>
public sealed partial class CompareViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private IReadOnlyList<ParameterDifference> _diffs = [];

    public CompareViewModel(MainViewModel main) => _main = main;

    public override string Title => "Porównanie konfiguracji";

    public ObservableCollection<CompareRow> Rows { get; } = new();

    [ObservableProperty]
    public partial bool OnlyDifferences { get; set; } = true;

    [ObservableProperty]
    public partial string LeftTitle { get; set; } = "Lewa";

    [ObservableProperty]
    public partial string RightTitle { get; set; } = "Prawa";

    [ObservableProperty]
    public partial string Summary { get; set; } = "Wybierz, co porównać. Porównanie nie zapisuje niczego do urządzenia ani do plików.";

    public bool CanCompareDevice => _main.IsConnected && !_main.IsBusy;

    public bool HasRows => Rows.Count > 0;

    public void NotifyConnectionChanged() => OnPropertyChanged(nameof(CanCompareDevice));

    partial void OnOnlyDifferencesChanged(bool value) => Fill();

    [RelayCommand]
    private async Task CompareWithDeviceAsync()
    {
        var read = await _main.ReadDeviceConfigurationForCompareAsync();
        if (read is null)
        {
            return;
        }

        Show(_main.Editor.Configuration, read, "Program", $"Urządzenie {_main.DeviceSerial}");
    }

    [RelayCommand]
    private async Task CompareFilesAsync()
    {
        var first = await _main.Files.PickOpenAsync("Pierwszy plik konfiguracji", _main.Settings.ConfigurationFolder);
        if (first is null)
        {
            return;
        }

        var second = await _main.Files.PickOpenAsync("Drugi plik konfiguracji", Path.GetDirectoryName(first));
        if (second is null)
        {
            return;
        }

        try
        {
            var a = await ConfigurationFile.LoadAsync(first, _main.Settings.DefaultAccessCode);
            var b = await ConfigurationFile.LoadAsync(second, _main.Settings.DefaultAccessCode);
            Show(a.Configuration, b.Configuration, Path.GetFileName(first), Path.GetFileName(second));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _main.ShowMessageAsync("Błąd odczytu pliku", ex.Message, "Sprawdź, czy plik istnieje i czy masz do niego dostęp.", isError: true);
        }
    }

    private void Show(Configuration left, Configuration right, string leftTitle, string rightTitle)
    {
        _diffs = ConfigurationComparer.Compare(left, right);
        LeftTitle = leftTitle;
        RightTitle = rightTitle;
        var count = _diffs.Count(d => d.IsDifferent);
        Summary = count == 0
            ? $"Konfiguracje są zgodne ({_diffs.Count} parametrów)."
            : $"{count} różnic na {_diffs.Count} parametrów.";
        Fill();
    }

    private void Fill()
    {
        Rows.Clear();
        foreach (var d in _diffs.Where(d => !OnlyDifferences || d.IsDifferent))
        {
            Rows.Add(new CompareRow(d.Parameter.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), d.Parameter.FullName,
                ParameterCodec.Format(d.Parameter, d.Left), ParameterCodec.Format(d.Parameter, d.Right), d.IsDifferent));
        }

        OnPropertyChanged(nameof(HasRows));
    }
}
