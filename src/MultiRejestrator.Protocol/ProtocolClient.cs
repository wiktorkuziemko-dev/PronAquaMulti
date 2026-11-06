using System.Buffers.Binary;
using MultiRejestrator.Protocol.Transport;

namespace MultiRejestrator.Protocol;

/// <summary>
/// Strona Master protokołu: wysyła zapytania i czeka na odpowiedzi.
/// Jedna transakcja naraz; brak ponawiania (pytanie 2.6).
/// </summary>
public sealed class ProtocolClient
{
    /// <summary>Maksymalna liczba Id w zapytaniu i bloków w ramce (Protokół 1.2.6).</summary>
    public const int MaxItemsPerFrame = 10;

    /// <summary>Limit pola DATA ramek konfiguracyjnych (pytanie 3.6).</summary>
    public const int MaxConfigurationData = 1024;

    private readonly ITransport _transport;
    private readonly IFrameLog? _log;
    private readonly FrameParser _parser = new(framesHavePassword: false);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Queue<Frame> _received = new();
    private byte[] _password = AsciiText.Encode("000000", Frame.PasswordLength);
    private byte _nextId;

    public ProtocolClient(ITransport transport, IFrameLog? log = null)
    {
        _transport = transport;
        _log = log;
        _parser.Discarded += (reason, bytes) => Log(FrameDirection.Error, "Odrzucono bajty: " + reason, bytes);
    }

    /// <summary>Czas oczekiwania na odpowiedź (pytanie 2.6: 1 s).</summary>
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Czas oczekiwania na odpowiedź po zapisie do pamięci urządzenia (pytanie 2.6: 3 s).</summary>
    public TimeSpan WriteResponseTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public ITransport Transport => _transport;

    /// <summary>Kod dostępu wysyłany w polu Password każdego zapytania (6 znaków ASCII).</summary>
    public string Password
    {
        get => AsciiText.Read(_password);
        set => _password = AsciiText.Encode(value, Frame.PasswordLength);
    }

    public Task HelloAsync(CancellationToken ct = default) =>
        TransactAsync(CommandType.Hello, [], ResponseTimeout, ct);

    public async Task<ushort> GetProtocolVersionAsync(CancellationToken ct = default)
    {
        var data = await TransactAsync(CommandType.GetProtocolRevision, [], ResponseTimeout, ct).ConfigureAwait(false);
        ExpectLength(data, 2, "RE_REQ_GET_PROT_VER");
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    public async Task<FirmwareInfo> GetFirmwareInfoAsync(CancellationToken ct = default)
    {
        var data = await TransactAsync(CommandType.GetFirmwareDate, [], ResponseTimeout, ct).ConfigureAwait(false);
        return FirmwareInfo.Decode(data);
    }

    /// <summary>Odczyt do 10 parametrów konfiguracyjnych. Nieznane Id urządzenie pomija (pytanie 3.7).</summary>
    public async Task<IReadOnlyList<ConfBlock>> GetConfigurationAsync(IReadOnlyList<ushort> ids, CancellationToken ct = default)
    {
        CheckIds(ids);
        var data = await TransactAsync(CommandType.GetConfiguration, ConfBlock.EncodeIds(ids), ResponseTimeout, ct).ConfigureAwait(false);
        return ConfBlock.DecodeMany(data);
    }

    /// <summary>Zapis do 10 bloków (≤ 1024 B). Urządzenie zapisuje bloki, które rozpozna (pytanie 3.9).</summary>
    public async Task SetConfigurationAsync(IReadOnlyList<ConfBlock> blocks, CancellationToken ct = default)
    {
        if (blocks.Count is 0 or > MaxItemsPerFrame)
        {
            throw new ArgumentException($"Ramka może zawierać od 1 do {MaxItemsPerFrame} bloków.", nameof(blocks));
        }

        var data = ConfBlock.EncodeMany(blocks);
        if (data.Length > MaxConfigurationData)
        {
            throw new ArgumentException($"Bloki zajmują {data.Length} B, limit to {MaxConfigurationData} B.", nameof(blocks));
        }

        await TransactAsync(CommandType.SetConfiguration, data, WriteResponseTimeout, ct).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset> GetTimeAsync(CancellationToken ct = default)
    {
        var data = await TransactAsync(CommandType.GetTime, [], ResponseTimeout, ct).ConfigureAwait(false);
        return DeviceTime.Decode(data);
    }

    public Task SetTimeAsync(DateTimeOffset time, CancellationToken ct = default) =>
        TransactAsync(CommandType.SetTime, DeviceTime.Encode(time), WriteResponseTimeout, ct);

    /// <summary>Odczyt do 10 parametrów stanu urządzenia (Protokół 1.2.8).</summary>
    public async Task<IReadOnlyList<ConfBlock>> GetDeviceStateAsync(IReadOnlyList<ushort> ids, CancellationToken ct = default)
    {
        CheckIds(ids);
        var data = await TransactAsync(CommandType.GetDeviceState, ConfBlock.EncodeIds(ids), ResponseTimeout, ct).ConfigureAwait(false);
        return ConfBlock.DecodeMany(data);
    }

    /// <summary>Zleca raport na żądanie; zwraca ID raportu (Protokół 1.2.9).</summary>
    public async Task<ushort> RequestReportOnDemandAsync(ushort delaySeconds, CancellationToken ct = default)
    {
        var request = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(request, delaySeconds);
        var data = await TransactAsync(CommandType.ReportOnDemand, request, ResponseTimeout, ct).ConfigureAwait(false);
        ExpectLength(data, 2, "RE_REQ_REPORT_ON_DEMAND");
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    public Task StartGsmMonitorAsync(CancellationToken ct = default) =>
        TransactAsync(CommandType.MonitorGsm, [], ResponseTimeout, ct);

    public async Task<IReadOnlyList<MeasureReportSection>> GetMeasureReportAsync(uint dayIndex, ushort startMinute, CancellationToken ct = default)
    {
        var data = await TransactAsync(CommandType.GetMeasureReport, MeasureReportSection.EncodeRequest(dayIndex, startMinute), ResponseTimeout, ct).ConfigureAwait(false);
        return MeasureReportSection.DecodeResponse(data);
    }

    /// <summary>Dzieli bloki na ramki: maks. 10 bloków i 1024 B danych w ramce.</summary>
    public static IReadOnlyList<IReadOnlyList<ConfBlock>> PackBlocks(IEnumerable<ConfBlock> blocks)
    {
        var frames = new List<IReadOnlyList<ConfBlock>>();
        var current = new List<ConfBlock>();
        var size = 0;
        foreach (var block in blocks)
        {
            if (block.EncodedLength > MaxConfigurationData)
            {
                throw new ArgumentException($"Blok Id {block.Id} jest większy niż {MaxConfigurationData} B.");
            }

            if (current.Count == MaxItemsPerFrame || size + block.EncodedLength > MaxConfigurationData)
            {
                frames.Add(current);
                current = new List<ConfBlock>();
                size = 0;
            }

            current.Add(block);
            size += block.EncodedLength;
        }

        if (current.Count > 0)
        {
            frames.Add(current);
        }

        return frames;
    }

    /// <summary>Wysyła zapytanie i zwraca pole DATA odpowiedzi.</summary>
    public async Task<byte[]> TransactAsync(CommandType command, byte[] data, TimeSpan timeout, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _transport.DiscardInput();
            _parser.Reset();
            _received.Clear();

            var id = _nextId++;
            var request = new Frame((ushort)command, id, _password, data);
            var bytes = request.Encode();
            var name = FrameTypes.NameOf((ushort)command);
            Log(FrameDirection.Sent, $"{name} (id {id})", bytes);
            await _transport.WriteAsync(bytes, ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            while (true)
            {
                while (_received.TryDequeue(out var frame))
                {
                    var raw = frame.Encode();
                    if (frame.Id != id)
                    {
                        Log(FrameDirection.Error, $"Pominięto ramkę {FrameTypes.NameOf(frame.Type)} z id {frame.Id} (oczekiwano {id})", raw);
                        continue;
                    }

                    if (frame.Type == FrameTypes.Error)
                    {
                        var flags = frame.Data.Length >= 2 ? (DeviceErrorFlags)BinaryPrimitives.ReadUInt16LittleEndian(frame.Data) : (DeviceErrorFlags)(frame.Data.Length == 1 ? frame.Data[0] : 0);
                        var error = new DeviceErrorException(flags);
                        Log(FrameDirection.Error, $"ERROR_FRAME (id {id}): {error.Message}", raw);
                        throw error;
                    }

                    if (frame.Type != FrameTypes.ResponseOf(command))
                    {
                        Log(FrameDirection.Error, $"Nieoczekiwana odpowiedź {FrameTypes.NameOf(frame.Type)} na {name}", raw);
                        throw new ProtocolException($"Urządzenie odpowiedziało ramką {FrameTypes.NameOf(frame.Type)} zamiast {FrameTypes.NameOf(FrameTypes.ResponseOf(command))}.");
                    }

                    Log(FrameDirection.Received, $"{FrameTypes.NameOf(frame.Type)} (id {id}, {frame.Data.Length} B danych)", raw);
                    return frame.Data;
                }

                byte[] chunk;
                try
                {
                    chunk = await _transport.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    var leftover = _parser.BufferedCount;
                    Log(FrameDirection.Error, $"Brak odpowiedzi na {name} w ciągu {timeout.TotalSeconds:0.#} s" + (leftover > 0 ? $" ({leftover} B niekompletnej ramki)" : string.Empty), []);
                    throw new DeviceTimeoutException(name, timeout);
                }

                foreach (var frame in _parser.Feed(chunk))
                {
                    _received.Enqueue(frame);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void LogInfo(string message) => Log(FrameDirection.Info, message, []);

    private void Log(FrameDirection direction, string description, byte[] bytes) =>
        _log?.Write(new FrameLogEntry(DateTime.Now, direction, description, bytes));

    private static void CheckIds(IReadOnlyList<ushort> ids)
    {
        if (ids.Count is 0 or > MaxItemsPerFrame)
        {
            throw new ArgumentException($"Zapytanie może zawierać od 1 do {MaxItemsPerFrame} Id.", nameof(ids));
        }
    }

    private static void ExpectLength(byte[] data, int expected, string name)
    {
        if (data.Length != expected)
        {
            throw new ProtocolException($"{name}: oczekiwano {expected} B danych, odebrano {data.Length} B.");
        }
    }
}
