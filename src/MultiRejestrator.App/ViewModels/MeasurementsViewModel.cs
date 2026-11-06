using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core.Device;
using MultiRejestrator.Core.Files;
using MultiRejestrator.Protocol;

namespace MultiRejestrator.App.ViewModels;

public sealed record MeasureRow(string Channel, string InputType, string Time, string Value, string Details);

/// <summary>
/// Dane pomiarowe z ramki Measure Report (0x0018).
/// ZAŁOŻENIE: zastępuje „bufor zdarzeń” z pkt 13 Założeń, którego Protokół rev3871 nie opisuje (docs/decyzje.md).
/// </summary>
public sealed partial class MeasurementsViewModel : PageViewModel
{
    private static readonly string[] TypeNames = ["nieaktywne", "impulsowe", "binarne", "analogowe 2,5 V", "analogowe 10 V", "analogowe 4–20 mA"];
    private readonly MainViewModel _main;
    private MeasureDay? _day;
    private CancellationTokenSource? _cts;

    public MeasurementsViewModel(MainViewModel main) => _main = main;

    public override string Title => "Dane pomiarowe";

    public ObservableCollection<MeasureRow> Rows { get; } = new();

    [ObservableProperty]
    public partial string DayIndex { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsReading { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "Brak odczytanych danych. Połącz z urządzeniem, wpisz indeks dnia i wybierz „Odczytaj dzień”.";

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    public bool HasRows => Rows.Count > 0;

    public bool CanRead => _main.IsConnected && !_main.IsBusy && !IsReading;

    public void NotifyConnectionChanged() => OnPropertyChanged(nameof(CanRead));

    partial void OnIsReadingChanged(bool value) => OnPropertyChanged(nameof(CanRead));

    partial void OnSortDescendingChanged(bool value) => Fill();

    [RelayCommand]
    private async Task ReadAsync()
    {
        if (!uint.TryParse(DayIndex, NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            _main.ShowToast("Indeks dnia: liczba całkowita ≥ 0.");
            return;
        }

        _cts = new CancellationTokenSource();
        IsReading = true;
        Progress = 0;
        var progress = new Progress<OperationProgress>(p => Progress = p.Fraction * 100);
        try
        {
            await _main.RunDeviceAsync("Odczyt danych pomiarowych", async (session, ct) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                _day = await session.ReadMeasureDayAsync(day, progress, linked.Token);
                Fill();
                var date = _day.DayStart is { } d ? d.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "nieznana data";
                Summary = $"Dzień {_day.DayIndex} ({date}): {_day.RecordCount} raportów z {_day.Channels.Count} kanałów. Odczyt nie kasuje danych w urządzeniu.";
            });
        }
        finally
        {
            IsReading = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        _main.ShowToast("Przerwano odczyt danych pomiarowych.");
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_day is null)
        {
            return;
        }

        var name = MeasureCsv.FileName(_main.DeviceSerial ?? "urzadzenie", DateTime.Now);
        var path = await _main.Files.PickSaveAsync("Eksport danych pomiarowych", name, "Plik CSV", "*.csv", _main.Settings.ExportFolder);
        if (path is null)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, MeasureCsv.Serialize(_day), new System.Text.UTF8Encoding(true));
            _main.Settings.ExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            _main.SaveSettings();
            _main.ShowToast($"Zapisano {Path.GetFileName(path)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _main.ShowMessageAsync("Błąd zapisu pliku", $"Nie można zapisać {path}: {ex.Message}", "Sprawdź, czy plik nie jest otwarty w innym programie i czy masz prawo zapisu w tym folderze.", isError: true);
        }
    }

    [RelayCommand]
    private void ToggleSort() => SortDescending = !SortDescending;

    private void Fill()
    {
        Rows.Clear();
        if (_day is null)
        {
            OnPropertyChanged(nameof(HasRows));
            return;
        }

        var rows = new List<(int Minute, MeasureRow Row)>();
        foreach (var ch in _day.Channels)
        {
            foreach (var r in ch.Records)
            {
                var time = _day.DayStart is { } ds ? ds.AddMinutes(r.MinuteOfDay).ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : $"minuta {r.MinuteOfDay}";
                var (value, details) = r switch
                {
                    PulseRecord p => ($"licznik {p.Counter}", string.Empty),
                    BinaryRecord b => (b.State == 1 ? "stan wysoki" : "stan niski", string.Empty),
                    AnalogRecord a => ($"śr. {a.Mean}", $"min {a.Min} · max {a.Max} · stabilizacja {(a.StabilizationStatus == 1 ? "poprawna" : "błąd")} ({a.StabilizationTimeMs} ms)"),
                    _ => (string.Empty, string.Empty),
                };
                var typeName = (int)ch.InputType < TypeNames.Length ? TypeNames[(int)ch.InputType] : ((int)ch.InputType).ToString(CultureInfo.InvariantCulture);
                rows.Add((r.MinuteOfDay, new MeasureRow($"Kanał {ch.ChannelNumber}", typeName, time, value, details)));
            }
        }

        var ordered = SortDescending ? rows.OrderByDescending(r => r.Minute) : rows.OrderBy(r => r.Minute);
        foreach (var (_, row) in ordered)
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(HasRows));
    }
}
