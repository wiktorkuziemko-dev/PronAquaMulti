using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

public sealed record InfoRow(string Label, string Value, bool Monospace = false);

/// <summary>Edytowalny licznik (Measure Counters / Device Counters, Protokół rev3892 1.2.12–1.2.13).</summary>
public sealed partial class CounterViewModel : ObservableObject
{
    public CounterViewModel(ParameterDefinition definition, string label)
    {
        Definition = definition;
        Label = label;
    }

    public ParameterDefinition Definition { get; }

    public string Label { get; }

    public string Unit => Definition.Unit;

    public string IdText => $"Id {Definition.Id}";

    /// <summary>Wartość odczytana z urządzenia.</summary>
    public string Original { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError), nameof(IsChanged))]
    public partial string Value { get; set; } = string.Empty;

    public string? Error => Value.Length == 0 ? null : ParameterCodec.Validate(Definition, Value);

    public bool HasError => Error is not null;

    public bool IsChanged => Original.Length > 0 && Value != Original;

    public void Load(string value)
    {
        Original = value;
        Value = value;
        OnPropertyChanged(nameof(IsChanged));
    }
}

/// <summary>Diagnostyka (pkt 12 Założeń): stan urządzenia, liczniki, zegar, monitor sygnału GSM, raport na żądanie.</summary>
public sealed partial class DiagnosticsViewModel : PageViewModel
{
    private static readonly ushort[] ModemIds = [DeviceStateIds.GsmState, DeviceStateIds.SignalLevel, DeviceStateIds.GsmErrorCode, DeviceStateIds.Band, DeviceStateIds.CellId, DeviceStateIds.Iccid, DeviceStateIds.Imsi, DeviceStateIds.Imei];
    private static readonly ushort[] DeviceIds = [DeviceStateIds.PowerSource, DeviceStateIds.BatteryVoltage, DeviceStateIds.RegulatedVoltage, DeviceStateIds.Temperature, DeviceStateIds.Tamper, DeviceStateIds.TimeSinceReset, DeviceStateIds.ResetReason];

    private readonly MainViewModel _main;
    private CancellationTokenSource? _monitorCts;
    private IReadOnlyDictionary<ushort, string> _state = new Dictionary<ushort, string>();

    public DiagnosticsViewModel(MainViewModel main)
    {
        _main = main;
        var channel = 0;
        foreach (var p in ParameterCatalog.MeasureCounters.All)
        {
            PulseCounters.Add(new CounterViewModel(p, $"Kanał {++channel}"));
        }

        foreach (var p in ParameterCatalog.DeviceCounters.All)
        {
            DeviceCounters.Add(new CounterViewModel(p, p.Name.Replace("Licznik ", string.Empty, StringComparison.Ordinal) is var n ? char.ToUpper(n[0], CultureInfo.CurrentCulture) + n[1..] : p.Name));
        }

        UpdateRows();
    }

    public override string Title => "Diagnostyka";

    public ObservableCollection<InfoRow> ModemRows { get; } = new();

    public ObservableCollection<InfoRow> DeviceRows { get; } = new();

    public ObservableCollection<double> SignalHistory { get; } = new();

    public ObservableCollection<CounterViewModel> PulseCounters { get; } = new();

    public ObservableCollection<CounterViewModel> DeviceCounters { get; } = new();

    [ObservableProperty]
    public partial string PcTime { get; set; } = "—";

    [ObservableProperty]
    public partial string DeviceTime { get; set; } = "—";

    [ObservableProperty]
    public partial string ClockDifference { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReportDelay { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial string MonitorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastRefresh { get; set; } = "Dane nie zostały jeszcze pobrane.";

    [ObservableProperty]
    public partial bool CountersLoaded { get; set; }

    public bool CanUseDevice => _main.IsConnected && !_main.IsBusy;

    public bool CanWriteCounters => CanUseDevice && CountersLoaded;

    partial void OnCountersLoadedChanged(bool value) => OnPropertyChanged(nameof(CanWriteCounters));

    public void NotifyConnectionChanged()
    {
        OnPropertyChanged(nameof(CanUseDevice));
        OnPropertyChanged(nameof(CanWriteCounters));
        if (!_main.IsConnected)
        {
            StopMonitor();
        }
    }

    /// <summary>„Pobierz stan” (uwaga 4): stan urządzenia i liczniki.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _main.RunDeviceAsync("Pobieranie stanu urządzenia", async (session, ct) =>
        {
            _state = await session.ReadDeviceStateAsync(ct);
            var pulse = await session.ReadMeasureCountersAsync(ct);
            var device = await session.ReadDeviceCountersAsync(ct);
            foreach (var c in PulseCounters)
            {
                c.Load(pulse.GetValueOrDefault(c.Definition.Id, string.Empty));
            }

            foreach (var c in DeviceCounters)
            {
                c.Load(device.GetValueOrDefault(c.Definition.Id, string.Empty));
            }

            CountersLoaded = true;
            LastRefresh = $"Stan pobrano {DateTime.Now:HH:mm:ss}.";
            UpdateRows();
        });
    }

    /// <summary>„Odczytaj z urządzenia” (uwaga 3a): czas urządzenia i komputera.</summary>
    [RelayCommand]
    private Task ReadClockAsync() =>
        _main.RunDeviceAsync("Odczyt zegara", async (session, ct) => ShowClock(await session.GetTimeAsync(ct)));

    /// <summary>„Ustaw wg komputera” (uwaga 3b): wysyła czas komputera, potem odczytuje oba czasy.</summary>
    [RelayCommand]
    private Task SetClockFromPcAsync() =>
        _main.RunDeviceAsync("Ustawianie zegara", async (session, ct) =>
        {
            await session.SetTimeAsync(DateTimeOffset.UtcNow, ct);
            ShowClock(await session.GetTimeAsync(ct));
            _main.ShowToast("Ustawiono zegar urządzenia według czasu komputera.");
        });

    private void ShowClock(DateTimeOffset device)
    {
        var pc = DateTimeOffset.Now;
        PcTime = pc.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        DeviceTime = device.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var s = (long)Math.Round((device - pc).TotalSeconds);
        ClockDifference = s == 0 ? "Zegary są zgodne." : $"Różnica: {(s > 0 ? "+" : "−")}{Math.Abs(s)} s (urządzenie względem komputera).";
    }

    [RelayCommand]
    private Task WritePulseCountersAsync() => WriteCountersAsync(PulseCounters, "liczników impulsów", (s, v, ct) => s.WriteMeasureCountersAsync(v, ct));

    [RelayCommand]
    private Task WriteDeviceCountersAsync() => WriteCountersAsync(DeviceCounters, "liczników urządzenia", (s, v, ct) => s.WriteDeviceCountersAsync(v, ct));

    [RelayCommand]
    private void ZeroDeviceCounters()
    {
        foreach (var c in DeviceCounters)
        {
            c.Value = "0";
        }
    }

    [RelayCommand]
    private void RevertCounters()
    {
        foreach (var c in PulseCounters.Concat(DeviceCounters))
        {
            c.Value = c.Original;
        }
    }

    /// <summary>Zapis zmienionych liczników po potwierdzeniu (pkt 9, 15 Założeń).</summary>
    private async Task WriteCountersAsync(IEnumerable<CounterViewModel> counters, string what,
        Func<Core.Device.DeviceSession, IReadOnlyDictionary<ushort, string>, CancellationToken, Task> write)
    {
        var list = counters.ToList();
        if (list.FirstOrDefault(c => c.HasError) is { } bad)
        {
            _main.ShowToast($"{bad.Label}: {bad.Error}");
            return;
        }

        var changed = list.Where(c => c.IsChanged).ToList();
        if (changed.Count == 0)
        {
            _main.ShowToast("Nie zmieniono żadnego licznika.");
            return;
        }

        var text = string.Join("\n", changed.Select(c => $"• {c.Label}: {c.Original} → {c.Value}"));
        if (!await _main.ConfirmAsync($"Zapisać zmiany {what} w urządzeniu {_main.DeviceSerial}?", text, "Zapisz w urządzeniu"))
        {
            return;
        }

        var values = changed.ToDictionary(c => c.Definition.Id, c => c.Value);
        if (await _main.RunDeviceAsync($"Zapis {what}", (s, ct) => write(s, values, ct)))
        {
            foreach (var c in changed)
            {
                c.Load(c.Value);
            }

            _main.ShowToast($"Zapisano zmiany {what} ({changed.Count}).");
        }
    }

    [RelayCommand]
    private async Task RequestReportAsync()
    {
        if (!ushort.TryParse(ReportDelay, out var delay))
        {
            _main.ShowToast("Opóźnienie raportu: liczba sekund 0–65535.");
            return;
        }

        await _main.RunDeviceAsync("Raport na żądanie", async (session, ct) =>
        {
            var id = await session.RequestReportAsync(delay, ct);
            _main.ShowToast($"Urządzenie przyjęło żądanie raportu (ID raportu {id}, opóźnienie {delay} s).");
        });
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        if (value)
        {
            _ = StartMonitorAsync();
        }
        else
        {
            StopMonitor();
        }
    }

    /// <summary>Monitor sygnału GSM (odpowiedź 6.1.13): start ramką 0x0017, potem co 2 s stan modułu i poziom sygnału.</summary>
    private async Task StartMonitorAsync()
    {
        if (!_main.IsConnected)
        {
            IsMonitoring = false;
            return;
        }

        var ok = await _main.RunDeviceAsync("Start monitora sygnału GSM", (session, ct) => session.StartGsmMonitorAsync(ct));
        if (!ok)
        {
            IsMonitoring = false;
            return;
        }

        SignalHistory.Clear();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;
        while (!token.IsCancellationRequested && _main.IsConnected)
        {
            await _main.RunDeviceAsync("Monitor sygnału GSM", async (session, ct) =>
            {
                var status = await session.ReadGsmStatusAsync(ct);
                var stateDef = ParameterCatalog.DeviceState[DeviceStateIds.GsmState];
                var state = status.State is { } s ? stateDef.OptionLabel(s) : "—";
                MonitorText = $"CSQ {status.SignalLevel?.ToString(CultureInfo.InvariantCulture) ?? "—"} z 31 · {state} · {DateTime.Now:HH:mm:ss}";
                SignalHistory.Add(Math.Clamp(status.SignalLevel ?? 0, 0, 31) / 31.0 * 60 + 2);
                while (SignalHistory.Count > 60)
                {
                    SignalHistory.RemoveAt(0);
                }
            }, quiet: true);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
        if (IsMonitoring)
        {
            IsMonitoring = false;
        }
    }

    private void UpdateRows()
    {
        Fill(ModemRows, ModemIds);
        Fill(DeviceRows, DeviceIds);
    }

    private void Fill(ObservableCollection<InfoRow> rows, ushort[] ids)
    {
        rows.Clear();
        foreach (var id in ids)
        {
            var def = ParameterCatalog.DeviceState[id];
            var value = _state.TryGetValue(id, out var v) ? ParameterCodec.Format(def, v) : "—";
            rows.Add(new InfoRow(def.Name, value, def.DataType == DataType.Ascii));
        }
    }
}
