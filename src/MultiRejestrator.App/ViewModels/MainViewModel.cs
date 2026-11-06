using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.App.Services;
using MultiRejestrator.App.ViewModels.Dialogs;
using MultiRejestrator.Core;
using MultiRejestrator.Core.Device;
using MultiRejestrator.Core.Files;
using MultiRejestrator.Core.Parameters;
using MultiRejestrator.Core.Settings;
using MultiRejestrator.Protocol;
using MultiRejestrator.Protocol.Transport;

namespace MultiRejestrator.App.ViewModels;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
}

public enum MainView
{
    Configuration,
    Diagnostics,
    Measurements,
    Compare,
}

public sealed record LogLine(string Text, string Color);

/// <summary>Model widoku głównego okna (pkt 19 Założeń).</summary>
public partial class MainViewModel : ViewModelBase
{
    private static readonly string[] TypeShort = ["nieaktywny", "impulsowe", "binarne", "2,5 V", "10 V", "4–20 mA"];
    private static readonly string[] TypeColors = ["#9CA3AF", "#1F5BD8", "#7C3AED", "#0E9F6E", "#0D9488", "#D97706"];

    private readonly Dictionary<Section, SectionPageViewModel> _sections = new();
    private readonly List<ChannelPageViewModel> _channels = new();
    private readonly CommunicationLogFile _logFile = new();
    private readonly DispatcherTimer _keepAlive;
    private readonly Func<string, ITransport> _openTransport;
    private DeviceSession? _session;
    private CancellationTokenSource? _toastCts;
    private string? _filePath;
    private ConfigurationFileInfo _fileInfo = new();
    private string _source = "new";

    public MainViewModel()
        : this(new NullFileService(), AppSettings.DefaultPath, SerialPortTransport.Open)
    {
    }

    public MainViewModel(IFileService files, string settingsPath, Func<string, ITransport> openTransport)
    {
        Files = files;
        SettingsPath = settingsPath;
        _openTransport = openTransport;
        Settings = AppSettings.Load(settingsPath);
        Editor = new ConfigurationEditor(Configuration.CreateDefault(Settings.DefaultAccessCode));
        Editor.Changed += OnConfigurationChanged;

        foreach (var s in new[] { Section.General, Section.Modem, Section.Apn, Section.Servers, Section.Transmission })
        {
            _sections[s] = SectionPageViewModel.Create(s, Editor, this);
        }

        for (var ch = 0; ch < ParameterCatalog.ChannelCount; ch++)
        {
            _channels.Add(new ChannelPageViewModel(ch, Editor, this));
        }

        NavSections = new ObservableCollection<NavItemViewModel>
        {
            new() { Title = "Ogólne", Icon = NavItemViewModel.Icons.General, Section = Section.General },
            new() { Title = "Modem / LTE", Icon = NavItemViewModel.Icons.Modem, Section = Section.Modem },
            new() { Title = "APN", Icon = NavItemViewModel.Icons.Apn, Section = Section.Apn },
            new() { Title = "Serwery i MQTT", Icon = NavItemViewModel.Icons.Servers, Section = Section.Servers },
            new() { Title = "Transmisja i szyfrowanie", Icon = NavItemViewModel.Icons.Transmission, Section = Section.Transmission },
        };
        NavChannels = new ObservableCollection<NavItemViewModel>(
            Enumerable.Range(0, ParameterCatalog.ChannelCount).Select(ch => new NavItemViewModel { Title = $"Kanał {ch + 1}", Icon = NavItemViewModel.Icons.Channel, Channel = ch }));

        Diagnostics = new DiagnosticsViewModel(this);
        Measurements = new MeasurementsViewModel(this);
        Compare = new CompareViewModel(this);

        _logFile.EntryWritten += entry => Dispatcher.UIThread.Post(() => AddLog(entry));
        _keepAlive = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _keepAlive.Tick += async (_, _) => await KeepAliveAsync();

        RefreshPorts();
        Navigate(NavSections[0]);
        OnConfigurationChanged();
    }

    /// <summary>Dodatkowe „porty” (np. SYMULATOR w trybie demonstracyjnym).</summary>
    public static List<string> ExtraPorts { get; } = new();

    public IFileService Files { get; set; }

    public string SettingsPath { get; }

    public AppSettings Settings { get; }

    public ConfigurationEditor Editor { get; }

    public ObservableCollection<NavItemViewModel> NavSections { get; }

    public ObservableCollection<NavItemViewModel> NavChannels { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public MeasurementsViewModel Measurements { get; }

    public CompareViewModel Compare { get; }

    public ObservableCollection<string> Ports { get; } = new();

    public ObservableCollection<LogLine> LogLines { get; } = new();

    public string AppVersion => "v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0");

    // ---------------- Widok ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfigurationView), nameof(IsDiagnosticsView), nameof(IsMeasurementsView), nameof(IsCompareView))]
    public partial MainView View { get; set; }

    public bool IsConfigurationView => View == MainView.Configuration;

    public bool IsDiagnosticsView => View == MainView.Diagnostics;

    public bool IsMeasurementsView => View == MainView.Measurements;

    public bool IsCompareView => View == MainView.Compare;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    [ObservableProperty]
    public partial PageViewModel? ConfigurationPage { get; set; }

    partial void OnViewChanged(MainView value) => CurrentPage = value switch
    {
        MainView.Diagnostics => Diagnostics,
        MainView.Measurements => Measurements,
        MainView.Compare => Compare,
        _ => ConfigurationPage,
    };

    [RelayCommand]
    private void ShowView(MainView view) => View = view;

    [RelayCommand]
    private void Navigate(NavItemViewModel item)
    {
        foreach (var n in NavSections.Concat(NavChannels))
        {
            n.IsSelected = n == item;
        }

        ConfigurationPage = item.Channel is { } ch ? _channels[ch] : _sections[item.Section!.Value];
        View = MainView.Configuration;
        CurrentPage = ConfigurationPage;
    }

    public void NavigateToChannel(int channel) => Navigate(NavChannels[channel]);

    // ---------------- Dokument ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocumentName))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial int ErrorCount { get; set; }

    public bool HasErrors => ErrorCount > 0;

    partial void OnErrorCountChanged(int value) => OnPropertyChanged(nameof(HasErrors));

    public string DocumentName => _filePath is not null ? Path.GetFileName(_filePath)
        : _source == "device" ? $"konfiguracja z urządzenia {DeviceSerial}"
        : "nowa konfiguracja";

    private void OnConfigurationChanged()
    {
        IsDirty = Editor.IsDirty;
        ErrorCount = Editor.Issues.Count;
        OnPropertyChanged(nameof(DocumentName));
        var config = Editor.Configuration;
        var sectionErrors = Editor.Issues.Select(i => config.Catalog[i.ParameterId]).ToList();
        foreach (var n in NavSections)
        {
            n.Errors = sectionErrors.Count(p => p.Section == n.Section);
        }

        foreach (var n in NavChannels)
        {
            var ch = n.Channel!.Value;
            var owner = config.CoupledOwner(ch);
            var type = (int)Math.Clamp(config.InputType(ch), 0, 5);
            n.IsLocked = owner is not null;
            n.IsCoupled = config.IsCoupledPairOwner(ch);
            n.SubText = owner is not null ? $"sprzężony z {owner + 1}" : TypeShort[type];
            n.DotColor = owner is not null ? TypeColors[1] : TypeColors[type];
            n.Errors = sectionErrors.Count(p => p.Channel == ch);
            _channels[ch].Refresh();
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        Editor.Load(Configuration.CreateDefault(Settings.DefaultAccessCode));
        _filePath = null;
        _source = "new";
        _fileInfo = new ConfigurationFileInfo { Created = DateTime.Now };
        OnPropertyChanged(nameof(DocumentName));
        Navigate(NavSections[0]);
        ShowToast("Utworzono nową konfigurację z wartościami domyślnymi. Kod dostępu pochodzi z ustawień programu.");
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        var path = await Files.PickOpenAsync("Otwórz konfigurację", Settings.ConfigurationFolder);
        if (path is null)
        {
            return;
        }

        ConfigurationFileResult result;
        try
        {
            result = await ConfigurationFile.LoadAsync(path, Settings.DefaultAccessCode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("Błąd odczytu pliku", $"Nie można odczytać pliku {Path.GetFileName(path)}: {ex.Message}", "Sprawdź, czy plik istnieje i czy nie jest otwarty w innym programie.", isError: true);
            return;
        }

        // Wczytanie pliku przy aktywnym połączeniu – najpierw rozłączenie (odpowiedź 7.3).
        var wasConnected = IsConnected;
        if (wasConnected)
        {
            await DisconnectAsync();
        }

        Editor.Load(result.Configuration);
        _filePath = path;
        _source = "file";
        _fileInfo = result.Info;
        Settings.ConfigurationFolder = Path.GetDirectoryName(path) ?? string.Empty;
        SaveSettings();
        OnPropertyChanged(nameof(DocumentName));

        var notes = new List<string>(result.Warnings);
        if (result.Info.ProtocolVersion is { } pv && pv != DeviceSession.SupportedProtocolVersion)
        {
            notes.Insert(0, $"Plik pochodzi z wersji protokołu {pv}, a program obsługuje wersję {DeviceSession.SupportedProtocolVersion}. Sprawdź konfigurację przed wysłaniem.");
        }

        if (notes.Count > 0)
        {
            await ShowMessageAsync("Wczytano z uwagami", string.Join("\n", notes), wasConnected ? "Połączenie z urządzeniem zostało zamknięte przed wczytaniem pliku." : string.Empty);
        }
        else
        {
            ShowToast(wasConnected
                ? $"Wczytano {Path.GetFileName(path)}. Połączenie zostało zamknięte – wczytanie pliku niczego nie wysyła do urządzenia."
                : $"Wczytano {Path.GetFileName(path)}.");
        }
    }

    [RelayCommand]
    private Task SaveAsync() => SaveCoreAsync(saveAs: false);

    [RelayCommand]
    private Task SaveAsAsync() => SaveCoreAsync(saveAs: true);

    private async Task<bool> SaveCoreAsync(bool saveAs)
    {
        var path = _filePath;
        if (path is null || saveAs)
        {
            // Proponowana nazwa = numer seryjny (pkt 11.1).
            var serial = Editor.Configuration[ParameterIds.SerialNumber];
            var suggested = (string.IsNullOrWhiteSpace(serial) ? "konfiguracja" : serial) + ConfigurationFile.Extension;
            path = await Files.PickSaveAsync("Zapisz konfigurację", suggested, "Konfiguracja MultiRejestrator", "*.txt", Settings.ConfigurationFolder);
            if (path is null)
            {
                return false;
            }
        }

        var now = DateTime.Now;
        _fileInfo = _fileInfo with
        {
            ProtocolVersion = _session?.Identity.ProtocolVersion ?? _fileInfo.ProtocolVersion ?? DeviceSession.SupportedProtocolVersion,
            SerialNumber = Editor.Configuration[ParameterIds.SerialNumber],
            Firmware = _session?.Identity.Firmware.ToString() ?? _fileInfo.Firmware,
            Created = _fileInfo.Created ?? now,
            Modified = now,
        };

        try
        {
            await ConfigurationFile.SaveAsync(path, Editor.Configuration, _fileInfo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("Błąd zapisu pliku", $"Nie można zapisać {Path.GetFileName(path)}: {ex.Message}", "Wybierz inny folder albo sprawdź, czy plik nie jest otwarty w innym programie.", isError: true);
            return false;
        }

        _filePath = path;
        _source = "file";
        Editor.MarkSaved();
        Settings.ConfigurationFolder = Path.GetDirectoryName(path) ?? string.Empty;
        SaveSettings();
        OnPropertyChanged(nameof(DocumentName));
        ShowToast($"Zapisano {Path.GetFileName(path)}.");
        return true;
    }

    /// <summary>Ostrzeżenie o niezapisanych zmianach (pkt 16). Zwraca <c>true</c>, gdy można kontynuować.</summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!Editor.IsDirty)
        {
            return true;
        }

        var dialog = new UnsavedDialogViewModel { DocumentName = DocumentName };
        await ShowDialogAsync(dialog);
        return dialog.Choice switch
        {
            UnsavedChoice.Discard => true,
            UnsavedChoice.Save => await SaveCoreAsync(saveAs: false),
            _ => false,
        };
    }

    public async Task CopyChannelAsync(int source)
    {
        var config = Editor.Configuration;
        var dialog = new CopyChannelDialogViewModel { SourceChannel = source };
        for (var ch = 0; ch < ParameterCatalog.ChannelCount; ch++)
        {
            var enabled = ch != source && config.CoupledOwner(ch) is null && !(config.IsCoupledPairOwner(source) && ch == source + 1);
            dialog.Targets.Add(new CopyTargetViewModel(dialog, ch, enabled));
        }

        if (!await ShowDialogAsync(dialog))
        {
            return;
        }

        var targets = dialog.SelectedChannels.ToList();
        foreach (var t in targets)
        {
            Editor.CopyChannel(source, t);
        }

        ShowToast($"Skopiowano ustawienia kanału {source + 1} do kanałów: {string.Join(", ", targets.Select(t => t + 1))}.");
    }

    // ---------------- Połączenie ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(ConnectionText), nameof(ConnectionColor), nameof(CanConnect), nameof(CanDisconnect), nameof(PortLocked), nameof(DeviceSerialText), nameof(FirmwareText), nameof(ProtocolText), nameof(CanUseDevice))]
    public partial ConnectionState Connection { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseDevice), nameof(CanConnect))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? SelectedPort { get; set; }

    public bool IsConnected => Connection == ConnectionState.Connected;

    public bool CanUseDevice => IsConnected && !IsBusy;

    public bool CanConnect => Connection is ConnectionState.Disconnected or ConnectionState.Error && !IsBusy;

    public bool CanDisconnect => Connection is ConnectionState.Connected or ConnectionState.Error;

    public bool PortLocked => Connection is ConnectionState.Connected or ConnectionState.Connecting;

    public string ConnectionText => Connection switch
    {
        ConnectionState.Connecting => "Łączenie…",
        ConnectionState.Connected => "Połączono",
        ConnectionState.Error => "Błąd komunikacji",
        _ => "Brak połączenia",
    };

    public string ConnectionColor => Connection switch
    {
        ConnectionState.Connecting => "#C27C0E",
        ConnectionState.Connected => "#0F7B0F",
        ConnectionState.Error => "#C42B1C",
        _ => "#8A8A8A",
    };

    public string? DeviceSerial => _session?.Identity.SerialNumber;

    public string DeviceSerialText => IsConnected ? DeviceSerial ?? "—" : "—";

    public string FirmwareText => IsConnected && _session is not null ? _session.Identity.Firmware.ToString() : "—";

    public string ProtocolText => IsConnected && _session is not null ? _session.Identity.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";

    partial void OnConnectionChanged(ConnectionState value) => NotifyPagesConnection();

    partial void OnIsBusyChanged(bool value) => NotifyPagesConnection();

    private void NotifyPagesConnection()
    {
        Diagnostics.NotifyConnectionChanged();
        Measurements.NotifyConnectionChanged();
        Compare.NotifyConnectionChanged();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var current = SelectedPort ?? Settings.LastPort;
        Ports.Clear();
        foreach (var p in SerialPortTransport.GetPortNames().Concat(ExtraPorts))
        {
            Ports.Add(p);
        }

        SelectedPort = Ports.Contains(current) ? current : Ports.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            await ShowMessageAsync("Nie wybrano portu COM", "Wybierz port, do którego podłączony jest konwerter USB–UART.", "Jeśli listy nie ma, podłącz konwerter i kliknij przycisk odświeżania obok listy portów.", isError: true);
            return;
        }

        var code = Editor.Configuration[ParameterIds.AccessCode];
        if (ParameterCodec.Validate(Editor.Configuration.Catalog[ParameterIds.AccessCode], code) is not null)
        {
            await ShowMessageAsync("Nieprawidłowy kod dostępu", "Program łączy się kodem dostępu z bieżącej konfiguracji (sekcja Ogólne), a ten nie ma 6 znaków ASCII.", "Popraw kod w sekcji Ogólne albo otwórz konfigurację tego urządzenia.", isError: true);
            NavigateTo(Section.General);
            return;
        }

        Connection = ConnectionState.Connecting;
        IsBusy = true;
        LogLines.Clear();
        if (_logFile.Start(header: $"Port {SelectedPort}") is { } logError)
        {
            ShowToast(logError);
        }

        ITransport? transport = null;
        try
        {
            transport = _openTransport(SelectedPort);
            _session = await DeviceSession.ConnectAsync(transport, code, _logFile);
            Settings.LastPort = SelectedPort;
            SaveSettings();
            Connection = ConnectionState.Connected;
            _keepAlive.Start();
            OnPropertyChanged(nameof(DeviceSerial));
            OnPropertyChanged(nameof(DocumentName));
            if (_session.Identity.IsNewerProtocol)
            {
                await ShowMessageAsync("Nowsza wersja protokołu",
                    $"Urządzenie używa protokołu w wersji {_session.Identity.ProtocolVersion}, a program obsługuje wersję {DeviceSession.SupportedProtocolVersion}.",
                    "Nie wszystkie parametry mogą zostać skonfigurowane. Sprawdź konfigurację albo pobierz ją z urządzenia.");
            }
            else
            {
                ShowToast($"Połączono z urządzeniem {DeviceSerial} na {SelectedPort}.");
            }
        }
        catch (Exception ex) when (ex is ProtocolException or OperationCanceledException)
        {
            Connection = ConnectionState.Disconnected;
            if (transport is not null)
            {
                await transport.DisposeAsync();
            }

            _session = null;
            await ShowConnectErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ShowConnectErrorAsync(Exception ex) => ex switch
    {
        TransportException => ShowMessageAsync("Nie można otworzyć portu", ex.Message, "Sprawdź, czy wybrano właściwy port i czy nie używa go inny program.", isError: true),
        DeviceTimeoutException => ShowMessageAsync("Brak odpowiedzi urządzenia", $"Urządzenie nie odpowiada na {SelectedPort}.", "Sprawdź kabel, zasilanie urządzenia i czy wybrano właściwy port.", isError: true),
        DeviceErrorException { Flags: var f } when f.HasFlag(DeviceErrorFlags.WrongPassword) => ShowMessageAsync("Błędny kod dostępu",
            $"Urządzenie odrzuciło kod dostępu „{Editor.Configuration[ParameterIds.AccessCode]}” z bieżącej konfiguracji (błąd 0x08).",
            "Wpisz właściwy kod w sekcji Ogólne albo otwórz konfigurację tego urządzenia i połącz ponownie.", isError: true),
        _ => ShowMessageAsync("Nieobsługiwane urządzenie", ex.Message, "Sprawdź, czy podłączone urządzenie to MultiRejestrator.", isError: true),
    };

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        _keepAlive.Stop();
        var session = _session;
        _session = null;
        Connection = ConnectionState.Disconnected;
        if (session is not null)
        {
            session.Client.LogInfo("Rozłączono");
            await session.DisposeAsync();
        }

        OnPropertyChanged(nameof(DeviceSerial));
    }

    private async Task KeepAliveAsync()
    {
        if (_session is null || !IsConnected || IsBusy || CurrentDialog is not null || Diagnostics.IsMonitoring)
        {
            return;
        }

        await RunDeviceAsync("HELLO", (s, ct) => s.KeepAliveAsync(ct), quiet: true);
    }

    /// <summary>
    /// Wykonuje operację na urządzeniu i obsługuje błędy komunikacji (pkt 6.4, 17 Założeń).
    /// Zwraca <c>true</c>, gdy operacja się powiodła.
    /// </summary>
    public async Task<bool> RunDeviceAsync(string description, Func<DeviceSession, CancellationToken, Task> operation, bool quiet = false)
    {
        var session = _session;
        if (session is null || Connection != ConnectionState.Connected)
        {
            if (!quiet)
            {
                ShowToast("Najpierw połącz z urządzeniem.");
            }

            return false;
        }

        if (!quiet)
        {
            IsBusy = true;
        }

        try
        {
            await operation(session, CancellationToken.None);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is DeviceTimeoutException or TransportException || ex.InnerException is DeviceTimeoutException or TransportException)
        {
            await OnConnectionLostAsync(description, ex);
            return false;
        }
        catch (Exception ex) when (ex is ProtocolException or PartialWriteException)
        {
            await ShowMessageAsync($"{description} – błąd", ex.Message, "Spróbuj ponownie. Jeśli błąd się powtarza, zapisz log komunikacji i przekaż go serwisowi.", isError: true);
            return false;
        }
        finally
        {
            if (!quiet)
            {
                IsBusy = false;
            }
        }
    }

    private async Task OnConnectionLostAsync(string description, Exception ex)
    {
        _keepAlive.Stop();
        Connection = ConnectionState.Error;
        Diagnostics.IsMonitoring = false;
        await ShowMessageAsync("Utrata komunikacji", $"{description}: {ex.Message}", "Sprawdź kabel i zasilanie urządzenia, a następnie połącz ponownie.", isError: true);
    }

    // ---------------- Odczyt i zapis urządzenia ----------------

    [RelayCommand]
    private async Task ReadFromDeviceAsync()
    {
        if (!CanUseDevice || !await ConfirmDiscardChangesAsync())
        {
            return;
        }

        var read = await ReadConfigurationWithProgressAsync("Odczyt konfiguracji z urządzenia");
        if (read is null)
        {
            return;
        }

        Editor.Load(read.Configuration);
        _filePath = null;
        _source = "device";
        _fileInfo = new ConfigurationFileInfo { Created = DateTime.Now };
        OnPropertyChanged(nameof(DocumentName));
        var missing = read.MissingIds.Count > 0 ? $" Urządzenie nie zwróciło {read.MissingIds.Count} parametrów (np. Id {read.MissingIds[0]}) – przyjęto wartości domyślne." : string.Empty;
        await ShowMessageAsync("Odczyt zakończony", $"Odczytano konfigurację z urządzenia {DeviceSerial}.{missing}", "Konfiguracja nie jest jeszcze zapisana na dysku – użyj „Zapisz”.", success: read.MissingIds.Count == 0);
    }

    public async Task<Configuration?> ReadDeviceConfigurationForCompareAsync()
    {
        var read = await ReadConfigurationWithProgressAsync("Odczyt konfiguracji do porównania");
        return read?.Configuration;
    }

    private async Task<ConfigurationReadResult?> ReadConfigurationWithProgressAsync(string title)
    {
        var dialog = new ProgressDialogViewModel { Title = $"{title} {DeviceSerial}" };
        CurrentDialog = dialog;
        ConfigurationReadResult? result = null;
        var progress = new Progress<OperationProgress>(p =>
        {
            dialog.Text = p.Text;
            dialog.Percent = p.Fraction * 100;
        });
        await RunDeviceAsync("Odczyt konfiguracji", async (s, ct) => result = await s.ReadConfigurationAsync(progress, ct));
        CloseDialog(dialog);
        return result;
    }

    [RelayCommand]
    private async Task SendToDeviceAsync()
    {
        if (!CanUseDevice)
        {
            return;
        }

        // Walidacja przed wysłaniem (pkt 9). Przy błędach – przejście do pierwszej sekcji z błędem.
        if (Editor.Issues.Count > 0)
        {
            var first = Editor.Configuration.Catalog[Editor.Issues[0].ParameterId];
            if (first.Channel is { } ch)
            {
                NavigateToChannel(ch);
            }
            else
            {
                NavigateTo(first.Section);
            }

            ShowToast($"Popraw błędy przed wysłaniem ({Editor.Issues.Count}). Przeniesiono do pierwszej sekcji z błędem.");
            return;
        }

        var config = Editor.Configuration.Clone();
        var confirm = new SendDialogViewModel
        {
            DeviceSerial = DeviceSerial ?? "—",
            Port = SelectedPort ?? string.Empty,
            ConfigurationSerial = config[ParameterIds.SerialNumber],
            DocumentName = DocumentName,
            FrameCount = DeviceSession.CountWriteFrames(config),
            SetTime = Settings.SetTimeOnSend,
        };
        if (!await ShowDialogAsync(confirm))
        {
            return;
        }

        var progressDialog = new ProgressDialogViewModel { Title = $"Wysyłanie konfiguracji do {DeviceSerial}" };
        CurrentDialog = progressDialog;
        var progress = new Progress<OperationProgress>(p =>
        {
            progressDialog.Text = p.Text;
            progressDialog.Percent = p.Fraction * 100;
        });

        string? failure = null;
        var ok = false;
        IsBusy = true;
        try
        {
            await _session!.WriteConfigurationAsync(config, confirm.SetTime, progress);
            ok = true;
        }
        catch (PartialWriteException ex)
        {
            failure = $"Urządzenie przestało odpowiadać po {ex.FramesDone} z {ex.FramesTotal} ramek ({ex.InnerException?.Message}). Konfiguracja w urządzeniu nie została prawidłowo zapisana – jej stan jest niepewny.";
            if (ex.InnerException is DeviceTimeoutException or TransportException)
            {
                _keepAlive.Stop();
                Connection = ConnectionState.Error;
            }
        }
        catch (ProtocolException ex)
        {
            failure = $"Zapis nie powiódł się: {ex.Message} Stan konfiguracji w urządzeniu jest niepewny.";
        }
        finally
        {
            IsBusy = false;
            CloseDialog(progressDialog);
        }

        OnPropertyChanged(nameof(DeviceSerial));
        OnPropertyChanged(nameof(DeviceSerialText));
        if (ok)
        {
            await ShowMessageAsync("Konfiguracja zapisana",
                $"Urządzenie {DeviceSerial} przyjęło konfigurację ({confirm.FrameCount} ramek).{(confirm.SetTime ? " Ustawiono zegar." : string.Empty)}",
                "Kod dostępu nie był wysyłany. Odczyt kontrolny nie jest wykonywany – do sprawdzenia służy widok „Porównanie”.", success: true);
        }
        else
        {
            await ShowMessageAsync("Konfiguracja zapisana częściowo", failure!, "Sprawdź połączenie, połącz ponownie i wyślij konfigurację jeszcze raz.", isError: true);
        }
    }

    [RelayCommand]
    private async Task ChangeAccessCodeAsync()
    {
        if (!CanUseDevice)
        {
            ShowToast("Najpierw połącz z urządzeniem.");
            return;
        }

        var dialog = new ChangeCodeDialogViewModel { DeviceSerial = DeviceSerial ?? "—", CurrentCode = _session!.Client.Password };
        if (!await ShowDialogAsync(dialog) || !dialog.IsValid)
        {
            return;
        }

        var code = dialog.NewCode;
        if (await RunDeviceAsync("Zmiana kodu dostępu", (s, ct) => s.ChangeAccessCodeAsync(code, ct)))
        {
            Editor.SetValue(ParameterIds.AccessCode, code);
            ShowToast($"Kod dostępu w urządzeniu {DeviceSerial} został zmieniony. Zapisz konfigurację, aby zachować nowy kod w pliku.");
        }
    }

    // ---------------- Ustawienia, O programie ----------------

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var dialog = new SettingsDialogViewModel { SettingsPath = SettingsPath, DefaultAccessCode = Settings.DefaultAccessCode, SetTimeOnSend = Settings.SetTimeOnSend };
        if (!await ShowDialogAsync(dialog) || !dialog.IsValid)
        {
            return;
        }

        Settings.DefaultAccessCode = dialog.DefaultAccessCode;
        Settings.SetTimeOnSend = dialog.SetTimeOnSend;
        if (SaveSettings() is null)
        {
            ShowToast($"Zapisano ustawienia w {Path.GetFileName(SettingsPath)}.");
        }
    }

    [RelayCommand]
    private Task OpenAboutAsync() => ShowDialogAsync(new AboutDialogViewModel { Version = AppVersion });

    public string? SaveSettings()
    {
        var error = Settings.TrySave(SettingsPath);
        if (error is not null)
        {
            ShowToast(error);
        }

        return error;
    }

    // ---------------- Log ----------------

    [ObservableProperty]
    public partial bool IsLogVisible { get; set; }

    [RelayCommand]
    private void ToggleLog() => IsLogVisible = !IsLogVisible;

    private void AddLog(FrameLogEntry entry)
    {
        var kind = entry.Direction switch
        {
            FrameDirection.Sent => "#7DD3FC",
            FrameDirection.Received => "#86EFAC",
            FrameDirection.Error => "#FCA5A5",
            _ => "#94A3B8",
        };
        LogLines.Add(new LogLine(entry.ToString(), kind));
        while (LogLines.Count > 400)
        {
            LogLines.RemoveAt(0);
        }
    }

    // ---------------- Okna dialogowe i komunikaty ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDialog))]
    public partial DialogViewModel? CurrentDialog { get; set; }

    public bool HasDialog => CurrentDialog is not null;

    public async Task<bool> ShowDialogAsync(DialogViewModel dialog)
    {
        var previous = CurrentDialog;
        CurrentDialog = dialog;
        var result = await dialog.Result;
        CurrentDialog = previous is ProgressDialogViewModel ? previous : null;
        return result;
    }

    private void CloseDialog(DialogViewModel dialog)
    {
        if (CurrentDialog == dialog)
        {
            CurrentDialog = null;
        }
    }

    public Task ShowMessageAsync(string title, string text, string hint = "", bool isError = false, bool success = false) =>
        ShowDialogAsync(new MessageDialogViewModel { Title = title, Text = text, Hint = hint, IsError = isError, IsSuccess = success });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; set; }

    public bool HasToast => Toast is not null;

    public async void ShowToast(string text)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        Toast = text;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4.5), cts.Token);
            Toast = null;
        }
        catch (TaskCanceledException)
        {
        }
    }

    public void NavigateTo(Section section) => Navigate(NavSections.First(n => n.Section == section));

    /// <summary>Przy zamykaniu okna: ostrzeżenie o niezapisanych zmianach i zamknięcie portu.</summary>
    public async Task<bool> PrepareCloseAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return false;
        }

        await DisconnectAsync();
        _logFile.Dispose();
        return true;
    }

    private sealed class NullFileService : IFileService
    {
        public Task<string?> PickOpenAsync(string title, string? folder) => Task.FromResult<string?>(null);

        public Task<string?> PickSaveAsync(string title, string suggestedName, string filterName, string pattern, string? folder) => Task.FromResult<string?>(null);
    }
}
