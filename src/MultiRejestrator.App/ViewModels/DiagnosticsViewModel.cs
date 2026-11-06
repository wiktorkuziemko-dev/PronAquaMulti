using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

public sealed record InfoRow(string Label, string Value, bool Monospace = false);

/// <summary>Diagnostyka (pkt 12 Założeń): stan urządzenia, zegar, monitor GSM, raport na żądanie.</summary>
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
        UpdateRows();
    }

    public override string Title => "Diagnostyka";

    public ObservableCollection<InfoRow> ModemRows { get; } = new();

    public ObservableCollection<InfoRow> DeviceRows { get; } = new();

    public ObservableCollection<double> SignalHistory { get; } = new();

    [ObservableProperty]
    public partial string DeviceTime { get; set; } = "—";

    [ObservableProperty]
    public partial string NewTime { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReportDelay { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial string MonitorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastRefresh { get; set; } = "Dane nie zostały jeszcze odczytane.";

    public bool CanUseDevice => _main.IsConnected && !_main.IsBusy;

    public void NotifyConnectionChanged()
    {
        OnPropertyChanged(nameof(CanUseDevice));
        if (!_main.IsConnected)
        {
            StopMonitor();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _main.RunDeviceAsync("Odczyt diagnostyki", async (session, ct) =>
        {
            _state = await session.ReadDeviceStateAsync(ct);
            var time = await session.GetTimeAsync(ct);
            var diff = time - DateTimeOffset.UtcNow;
            DeviceTime = $"{time.ToLocalTime():dd.MM.yyyy HH:mm:ss} ({FormatOffset(diff)} względem komputera)";
            LastRefresh = $"Odczytano {DateTime.Now:HH:mm:ss}.";
            UpdateRows();
        });
    }

    [RelayCommand]
    private void UsePcTime() => NewTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    [RelayCommand]
    private async Task SetTimeAsync()
    {
        if (!DateTime.TryParseExact(NewTime.Trim(), "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
        {
            _main.ShowToast("Wpisz czas w formacie DD.MM.RRRR GG:MM:SS albo użyj „Czas komputera”.");
            return;
        }

        await _main.RunDeviceAsync("Ustawianie zegara", async (session, ct) =>
        {
            await session.SetTimeAsync(new DateTimeOffset(local), ct);
            var time = await session.GetTimeAsync(ct);
            DeviceTime = $"{time.ToLocalTime():dd.MM.yyyy HH:mm:ss} ({FormatOffset(time - DateTimeOffset.UtcNow)} względem komputera)";
            _main.ShowToast($"Ustawiono zegar urządzenia: {local:dd.MM.yyyy HH:mm:ss}.");
        });
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

    /// <summary>Monitor GSM (odpowiedź 6.1.13): start ramką 0x0017, potem co 2 s stan modułu i poziom sygnału.</summary>
    private async Task StartMonitorAsync()
    {
        if (!_main.IsConnected)
        {
            IsMonitoring = false;
            return;
        }

        var ok = await _main.RunDeviceAsync("Start monitora GSM", (session, ct) => session.StartGsmMonitorAsync(ct));
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
            await _main.RunDeviceAsync("Monitor GSM", async (session, ct) =>
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

    private static string FormatOffset(TimeSpan diff)
    {
        var s = (long)Math.Round(diff.TotalSeconds);
        return s == 0 ? "zgodny" : $"{(s > 0 ? "+" : "−")}{Math.Abs(s)} s";
    }
}
