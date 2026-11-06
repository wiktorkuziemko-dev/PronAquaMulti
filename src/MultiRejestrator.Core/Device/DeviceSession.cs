using MultiRejestrator.Core.Parameters;
using MultiRejestrator.Protocol;
using MultiRejestrator.Protocol.Transport;

namespace MultiRejestrator.Core.Device;

public sealed record DeviceIdentity(string SerialNumber, FirmwareInfo Firmware, ushort ProtocolVersion)
{
    public bool IsNewerProtocol => ProtocolVersion > DeviceSession.SupportedProtocolVersion;
}

public sealed record OperationProgress(int Done, int Total, string Text)
{
    public double Fraction => Total == 0 ? 0 : (double)Done / Total;
}

/// <summary>Zapis przerwany po części ramek – stan urządzenia niepewny (pkt 9 Założeń).</summary>
public sealed class PartialWriteException : Exception
{
    public PartialWriteException(int framesDone, int framesTotal, Exception inner)
        : base($"Zapis przerwany po {framesDone} z {framesTotal} ramek: {inner.Message}", inner)
    {
        FramesDone = framesDone;
        FramesTotal = framesTotal;
    }

    public int FramesDone { get; }

    public int FramesTotal { get; }
}

public sealed record ConfigurationReadResult(Configuration Configuration, IReadOnlyList<ushort> MissingIds);

public sealed record GsmStatus(long? SignalLevel, long? State);

/// <summary>Dane pomiarowe jednego dnia, zebrane z kolejnych ramek Measure Report.</summary>
public sealed record MeasureDay(uint DayIndex, DateTimeOffset? DayStart, uint DayBufferSize, IReadOnlyList<MeasureChannelData> Channels)
{
    public int RecordCount => Channels.Sum(c => c.Records.Count);
}

public sealed record MeasureChannelData(byte ChannelNumber, InputType InputType, IReadOnlyList<MeasureRecord> Records);

/// <summary>
/// Operacje na podłączonym urządzeniu zbudowane na <see cref="ProtocolClient"/>:
/// identyfikacja, odczyt i zapis konfiguracji, zmiana kodu, zegar, stan urządzenia, dane pomiarowe.
/// </summary>
public sealed class DeviceSession : IAsyncDisposable
{
    /// <summary>Wersja protokołu obsługiwana przez program (Protokół 1.2.4).</summary>
    public const ushort SupportedProtocolVersion = 1;

    private DeviceSession(ProtocolClient client, DeviceIdentity identity)
    {
        Client = client;
        Identity = identity;
    }

    public ProtocolClient Client { get; }

    public DeviceIdentity Identity { get; private set; }

    /// <summary>
    /// Połączenie (pkt 6.2): HELLO, wersja protokołu, wersja firmware, numer seryjny (Id 1).
    /// Wszystkie komendy wymagają hasła (odpowiedź 3.2) – używany jest kod z bieżącej konfiguracji (5.7).
    /// </summary>
    public static async Task<DeviceSession> ConnectAsync(ITransport transport, string accessCode, IFrameLog? log, CancellationToken ct = default)
    {
        var client = new ProtocolClient(transport, log) { Password = accessCode };
        client.LogInfo($"Połączenie przez {transport.Name}, 115200 8N1");
        await client.HelloAsync(ct).ConfigureAwait(false);
        var protocol = await client.GetProtocolVersionAsync(ct).ConfigureAwait(false);
        var firmware = await client.GetFirmwareInfoAsync(ct).ConfigureAwait(false);
        var serialDef = ParameterCatalog.Configuration[ParameterIds.SerialNumber];
        var blocks = await client.GetConfigurationAsync([ParameterIds.SerialNumber], ct).ConfigureAwait(false);
        var serial = blocks.FirstOrDefault(b => b.Id == ParameterIds.SerialNumber) is { } b
            ? ParameterCodec.Decode(serialDef, b.Value)
            : "—";
        return new DeviceSession(client, new DeviceIdentity(serial, firmware, protocol));
    }

    public Task KeepAliveAsync(CancellationToken ct = default) => Client.HelloAsync(ct);

    /// <summary>Odczyt wszystkich parametrów (pkt 8), po 10 w ramce, w kolejności Id.</summary>
    public async Task<ConfigurationReadResult> ReadConfigurationAsync(IProgress<OperationProgress>? progress, CancellationToken ct = default)
    {
        var catalog = ParameterCatalog.Configuration;
        var config = Configuration.CreateDefault(string.Empty, catalog);
        var batches = catalog.All.Select(p => p.Id).Chunk(ProtocolClient.MaxItemsPerFrame).ToList();
        var received = new HashSet<ushort>();
        for (var i = 0; i < batches.Count; i++)
        {
            progress?.Report(new OperationProgress(i, batches.Count, $"Ramka {i + 1} z {batches.Count}"));
            foreach (var block in await Client.GetConfigurationAsync(batches[i], ct).ConfigureAwait(false))
            {
                if (catalog.TryGet(block.Id, out var def))
                {
                    config[block.Id] = ParameterCodec.Decode(def, block.Value);
                    received.Add(block.Id);
                }
            }
        }

        progress?.Report(new OperationProgress(batches.Count, batches.Count, "Odczyt zakończony"));
        if (received.Contains(ParameterIds.SerialNumber))
        {
            Identity = Identity with { SerialNumber = config[ParameterIds.SerialNumber] };
        }

        var missing = catalog.All.Select(p => p.Id).Where(id => !received.Contains(id)).ToList();
        return new ConfigurationReadResult(config, missing);
    }

    /// <summary>Liczba ramek potrzebnych do wysłania konfiguracji (bez kodu dostępu).</summary>
    public static int CountWriteFrames(Configuration config) => ProtocolClient.PackBlocks(BlocksToWrite(config)).Count;

    /// <summary>
    /// Wysyła całą konfigurację (odpowiedź 8.7) bez kodu dostępu Id 2 (odpowiedź 5.4).
    /// Opcjonalnie ustawia zegar na czas komputera (odpowiedź 3.15).
    /// </summary>
    public async Task WriteConfigurationAsync(Configuration config, bool setTime, IProgress<OperationProgress>? progress, CancellationToken ct = default)
    {
        var frames = ProtocolClient.PackBlocks(BlocksToWrite(config));
        var total = frames.Count + (setTime ? 1 : 0);
        for (var i = 0; i < frames.Count; i++)
        {
            progress?.Report(new OperationProgress(i, total, $"Ramka {i + 1} z {frames.Count}"));
            try
            {
                await Client.SetConfigurationAsync(frames[i], ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ProtocolException or OperationCanceledException)
            {
                throw new PartialWriteException(i, frames.Count, ex);
            }
        }

        if (setTime)
        {
            progress?.Report(new OperationProgress(frames.Count, total, "Ustawianie zegara"));
            await Client.SetTimeAsync(DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        if (ParameterCodec.Validate(ParameterCatalog.Configuration[ParameterIds.SerialNumber], config[ParameterIds.SerialNumber]) is null)
        {
            Identity = Identity with { SerialNumber = config[ParameterIds.SerialNumber] };
        }

        progress?.Report(new OperationProgress(total, total, "Zapis zakończony"));
    }

    /// <summary>Zmiana kodu dostępu jako osobna transmisja (odpowiedź 5.4). Od kolejnej ramki obowiązuje nowy kod.</summary>
    public async Task ChangeAccessCodeAsync(string newCode, CancellationToken ct = default)
    {
        var def = ParameterCatalog.Configuration[ParameterIds.AccessCode];
        if (ParameterCodec.Validate(def, newCode) is { } error)
        {
            throw new ArgumentException(error, nameof(newCode));
        }

        await Client.SetConfigurationAsync([new ConfBlock(ParameterIds.AccessCode, ParameterCodec.Encode(def, newCode))], ct).ConfigureAwait(false);
        Client.Password = newCode;
    }

    public Task<DateTimeOffset> GetTimeAsync(CancellationToken ct = default) => Client.GetTimeAsync(ct);

    public Task SetTimeAsync(DateTimeOffset time, CancellationToken ct = default) => Client.SetTimeAsync(time, ct);

    /// <summary>Odczyt wszystkich parametrów stanu urządzenia (Protokół 1.2.8). Wartości w postaci tekstowej.</summary>
    public async Task<IReadOnlyDictionary<ushort, string>> ReadDeviceStateAsync(CancellationToken ct = default)
    {
        var catalog = ParameterCatalog.DeviceState;
        var result = new Dictionary<ushort, string>();
        foreach (var batch in catalog.All.Select(p => p.Id).Chunk(ProtocolClient.MaxItemsPerFrame))
        {
            foreach (var block in await Client.GetDeviceStateAsync(batch, ct).ConfigureAwait(false))
            {
                if (catalog.TryGet(block.Id, out var def))
                {
                    result[block.Id] = ParameterCodec.Decode(def, block.Value);
                }
            }
        }

        return result;
    }

    /// <summary>Uruchamia monitor GSM w urządzeniu (Protokół 1.2.10).</summary>
    public Task StartGsmMonitorAsync(CancellationToken ct = default) => Client.StartGsmMonitorAsync(ct);

    /// <summary>
    /// Odczyt w trybie monitorowania (odpowiedź 6.1.13): stan modułu i poziom sygnału.
    /// ZAŁOŻENIE: odpytywanie ramką Device State (Id 1 i 3) – Protokół nie opisuje osobnej ramki.
    /// </summary>
    public async Task<GsmStatus> ReadGsmStatusAsync(CancellationToken ct = default)
    {
        var blocks = await Client.GetDeviceStateAsync([DeviceStateIds.SignalLevel, DeviceStateIds.GsmState], ct).ConfigureAwait(false);
        var catalog = ParameterCatalog.DeviceState;
        long? Get(ushort id) => blocks.FirstOrDefault(b => b.Id == id) is { } b ? ParameterCodec.TryGetNumber(ParameterCodec.Decode(catalog[id], b.Value)) : null;
        return new GsmStatus(Get(DeviceStateIds.SignalLevel), Get(DeviceStateIds.GsmState));
    }

    public Task<ushort> RequestReportAsync(ushort delaySeconds, CancellationToken ct = default) =>
        Client.RequestReportOnDemandAsync(delaySeconds, ct);

    /// <summary>
    /// Pobiera dane pomiarowe jednego dnia kolejnymi ramkami Measure Report.
    /// ZAŁOŻENIE: kolejna ramka zaczyna od minuty następnej po ostatniej otrzymanej; koniec, gdy brak
    /// raportów albo minuta ≥ 1439 (Protokół rev3871 nie opisuje końca danych – docs/decyzje.md).
    /// </summary>
    public async Task<MeasureDay> ReadMeasureDayAsync(uint dayIndex, IProgress<OperationProgress>? progress, CancellationToken ct = default)
    {
        const int minutesPerDay = 1440;
        var channels = new SortedDictionary<byte, (InputType Type, List<MeasureRecord> Records)>();
        DateTimeOffset? dayStart = null;
        uint bufferSize = 0;
        var start = 0;
        while (start < minutesPerDay)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new OperationProgress(start, minutesPerDay, $"Minuta {start} z {minutesPerDay}"));
            var sections = await Client.GetMeasureReportAsync(dayIndex, (ushort)start, ct).ConfigureAwait(false);
            var last = -1;
            foreach (var s in sections)
            {
                dayStart ??= s.DayStart;
                bufferSize = s.DayBufferSize;
                if (!channels.TryGetValue(s.ChannelNumber, out var data))
                {
                    data = (s.InputType, new List<MeasureRecord>());
                    channels[s.ChannelNumber] = data;
                }

                foreach (var r in s.Records.Where(r => r.MinuteOfDay >= start))
                {
                    data.Records.Add(r);
                    last = Math.Max(last, r.MinuteOfDay);
                }
            }

            if (last < start)
            {
                break;
            }

            start = last + 1;
        }

        progress?.Report(new OperationProgress(minutesPerDay, minutesPerDay, "Odczyt zakończony"));
        return new MeasureDay(dayIndex, dayStart, bufferSize,
            channels.Select(kv => new MeasureChannelData(kv.Key, kv.Value.Type, kv.Value.Records)).ToList());
    }

    public ValueTask DisposeAsync() => Client.Transport.DisposeAsync();

    private static IEnumerable<ConfBlock> BlocksToWrite(Configuration config) =>
        config.Catalog.All
            .Where(p => p.Id != ParameterIds.AccessCode)
            .Select(p => new ConfBlock(p.Id, ParameterCodec.Encode(p, config[p.Id])));
}
