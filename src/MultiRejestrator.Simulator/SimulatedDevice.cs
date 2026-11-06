using System.Buffers.Binary;
using MultiRejestrator.Core;
using MultiRejestrator.Core.Parameters;
using MultiRejestrator.Protocol;
using MultiRejestrator.Protocol.Transport;

namespace MultiRejestrator.Simulator;

/// <summary>
/// Urządzenie MultiRejestrator w pamięci: odpowiada na ramki zgodnie z Protokołem rev3871.
/// Każde zapytanie wymaga poprawnego hasła (odpowiedź 3.2). Nic nie wysyła samo z siebie (2.10).
/// </summary>
public sealed class SimulatedDevice
{
    private readonly object _sync = new();
    private readonly Dictionary<ushort, byte[]> _config = new();
    private readonly Dictionary<ushort, byte[]> _state = new();
    private readonly Random _random = new(1234);
    private TimeSpan _clockOffset = TimeSpan.FromSeconds(-73);
    private ushort _nextReportId = 1;

    public SimulatedDevice(string serialNumber = "12345678")
    {
        var config = Configuration.CreateDefault("000000");
        config[ParameterIds.SerialNumber] = serialNumber;
        config[ParameterIds.Module] = "4";
        config[ParameterCatalog.ChannelId(ParameterIds.InputType, 0)] = "1";
        config[ParameterCatalog.ChannelId(ParameterIds.InputType, 2)] = "5";
        config[ParameterCatalog.ChannelId(ParameterIds.InputType, 3)] = "2";
        LoadConfiguration(config);

        SetState(DeviceStateIds.SignalLevel, "18");
        SetState(DeviceStateIds.GsmErrorCode, "0");
        SetState(DeviceStateIds.GsmState, "3");
        SetState(DeviceStateIds.Iccid, "89480000000000000000");
        SetState(DeviceStateIds.Imsi, "260010000000000");
        SetState(DeviceStateIds.Imei, "860000000000000");
        SetState(DeviceStateIds.Band, "20");
        SetState(DeviceStateIds.CellId, "27309884");
        SetState(DeviceStateIds.TimeSinceReset, "1052253");
        SetState(DeviceStateIds.ResetReason, "3");
        SetState(DeviceStateIds.BatteryVoltage, "3580");
        SetState(DeviceStateIds.PowerSource, "1");
        SetState(DeviceStateIds.RegulatedVoltage, "3300");
        SetState(DeviceStateIds.Tamper, "0");
        SetState(DeviceStateIds.Temperature, "21");
    }

    public ushort ProtocolVersion { get; set; } = 1;

    public FirmwareInfo Firmware { get; set; } = new(3871, "Nov  5 2026", "14:22:05");

    /// <summary>Gdy <c>true</c>, urządzenie milczy (symulacja odłączenia lub zawieszenia).</summary>
    public bool Silent { get; set; }

    /// <summary>Opóźnienie odpowiedzi.</summary>
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

    /// <summary>Po tylu zapytaniach SET_CONF urządzenie przestaje odpowiadać (symulacja zerwania w trakcie zapisu).</summary>
    public int? GoSilentAfterWrites { get; set; }

    public bool GsmMonitorActive { get; private set; }

    public int WriteFrameCount { get; private set; }

    public string Password => AsciiText.Read(GetConfigBytes(ParameterIds.AccessCode));

    public DateTimeOffset Clock => DateTimeOffset.UtcNow + _clockOffset;

    public event Action<string>? Activity;

    public byte[] GetConfigBytes(ushort id)
    {
        lock (_sync)
        {
            return _config.TryGetValue(id, out var v) ? v.ToArray() : [];
        }
    }

    public Configuration ReadConfiguration()
    {
        var config = Configuration.CreateDefault(string.Empty);
        lock (_sync)
        {
            foreach (var p in ParameterCatalog.Configuration.All)
            {
                config[p.Id] = ParameterCodec.Decode(p, _config[p.Id]);
            }
        }

        return config;
    }

    public void LoadConfiguration(Configuration config)
    {
        lock (_sync)
        {
            foreach (var p in ParameterCatalog.Configuration.All)
            {
                _config[p.Id] = ParameterCodec.Encode(p, config[p.Id]);
            }
        }
    }

    public void SetState(ushort id, string value) =>
        _state[id] = ParameterCodec.Encode(ParameterCatalog.DeviceState[id], value);

    /// <summary>Obsługuje połączenie aż do anulowania lub zamknięcia transportu.</summary>
    public async Task RunAsync(ITransport transport, CancellationToken ct)
    {
        var parser = new FrameParser(framesHavePassword: true);
        parser.Discarded += (reason, _) => Activity?.Invoke("Odrzucono bajty: " + reason);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var chunk = await transport.ReadAsync(ct).ConfigureAwait(false);
                foreach (var request in parser.Feed(chunk))
                {
                    var response = Handle(request);
                    if (response is null || Silent)
                    {
                        continue;
                    }

                    if (ResponseDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(ResponseDelay, ct).ConfigureAwait(false);
                    }

                    await transport.WriteAsync(response.Encode(), ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (TransportException)
        {
        }
    }

    /// <summary>Przetwarza jedno zapytanie i zwraca odpowiedź (bez hasła) albo <c>null</c>.</summary>
    public Frame? Handle(Frame request)
    {
        if (Silent)
        {
            return null;
        }

        var name = FrameTypes.NameOf(request.Type);
        if (request.Password is null || AsciiText.Read(request.Password) != Password)
        {
            Activity?.Invoke($"{name}: błędne hasło");
            return Error(request, DeviceErrorFlags.WrongPassword);
        }

        Activity?.Invoke(name);
        var command = (CommandType)request.Type;
        try
        {
            byte[]? data = command switch
            {
                CommandType.Hello => [],
                CommandType.GetProtocolRevision => U16(ProtocolVersion),
                CommandType.GetFirmwareDate => Firmware.Encode(),
                CommandType.GetConfiguration => GetBlocks(_config, request.Data),
                CommandType.SetConfiguration => SetConfiguration(request.Data),
                CommandType.GetTime => DeviceTime.Encode(Clock),
                CommandType.SetTime => SetTime(request.Data),
                CommandType.GetDeviceState => GetDeviceState(request.Data),
                CommandType.ReportOnDemand => request.Data.Length == 2 ? U16(_nextReportId++) : null,
                CommandType.MonitorGsm => StartMonitor(),
                CommandType.GetMeasureReport => MeasureReport(request.Data),
                _ => null,
            };

            if (data is null)
            {
                return Error(request, Enum.IsDefined(command) ? DeviceErrorFlags.FrameSize : DeviceErrorFlags.UnknownFrameType);
            }

            if (command == CommandType.SetConfiguration && GoSilentAfterWrites is { } limit && WriteFrameCount > limit)
            {
                Silent = true;
                return null;
            }

            return new Frame((ushort)(request.Type | FrameTypes.ResponseFlag), request.Id, null, data);
        }
        catch (ProtocolException)
        {
            return Error(request, DeviceErrorFlags.FrameSize);
        }
    }

    private static Frame Error(Frame request, DeviceErrorFlags flags) =>
        new(FrameTypes.Error, request.Id, null, U16((ushort)flags));

    private static byte[] U16(ushort value)
    {
        var b = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        return b;
    }

    private byte[] GetBlocks(Dictionary<ushort, byte[]> source, byte[] request)
    {
        var ids = ConfBlock.DecodeIds(request);
        if (ids.Count > ProtocolClient.MaxItemsPerFrame)
        {
            throw new ProtocolException("Za dużo Id.");
        }

        lock (_sync)
        {
            // Nieznane Id są pomijane (odpowiedź 3.7); kolejność jak w zapytaniu (3.8).
            return ConfBlock.EncodeMany(ids.Where(source.ContainsKey).Select(id => new ConfBlock(id, source[id])));
        }
    }

    private byte[] SetConfiguration(byte[] data)
    {
        var blocks = ConfBlock.DecodeMany(data);
        WriteFrameCount++;
        lock (_sync)
        {
            // Zapisywane są bloki, które urządzenie rozpozna (odpowiedź 3.9).
            foreach (var block in blocks)
            {
                if (_config.TryGetValue(block.Id, out var current) && current.Length == block.Value.Length)
                {
                    _config[block.Id] = block.Value;
                }
            }
        }

        return [];
    }

    private byte[] SetTime(byte[] data)
    {
        _clockOffset = DeviceTime.Decode(data) - DateTimeOffset.UtcNow;
        return [];
    }

    private byte[] StartMonitor()
    {
        GsmMonitorActive = true;
        return [];
    }

    private byte[] GetDeviceState(byte[] request)
    {
        if (GsmMonitorActive)
        {
            var csq = ParameterCodec.TryGetNumber(ParameterCodec.Decode(ParameterCatalog.DeviceState[DeviceStateIds.SignalLevel], _state[DeviceStateIds.SignalLevel])) ?? 18;
            SetState(DeviceStateIds.SignalLevel, Math.Clamp(csq + _random.Next(-3, 4), 0, 31).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return GetBlocks(_state, request);
    }

    /// <summary>
    /// Dane pomiarowe: co 15 minut dla każdego aktywnego kanału, w porcjach 60-minutowych.
    /// ZAŁOŻENIE symulatora: jedna sekcja na aktywny kanał w każdej ramce (patrz docs/decyzje.md).
    /// </summary>
    private byte[] MeasureReport(byte[] data)
    {
        var (dayIndex, startMinute) = MeasureReportSection.DecodeRequest(data);
        var dayStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayIndex);
        var result = new List<byte>();
        var config = ReadConfiguration();
        var sections = 0;
        for (var ch = 0; ch < ParameterCatalog.ChannelCount; ch++)
        {
            var type = (InputType)config.InputType(ch);
            if (type == InputType.Inactive)
            {
                continue;
            }

            var records = new List<MeasureRecord>();
            var first = ((startMinute + 14) / 15) * 15;
            for (var m = first; m < Math.Min(startMinute + 60, 1440); m += 15)
            {
                var minute = (ushort)m;
                records.Add(type switch
                {
                    InputType.Pulse => new PulseRecord(minute, (int)(dayIndex * 1000) + (m / 15 * 7)),
                    InputType.Binary => new BinaryRecord(minute, (byte)(m / 60 % 2)),
                    _ => new AnalogRecord(minute, 1, (short)(120 + (m % 40)), (short)(800 + (m % 100)), (short)(1300 + (m % 100)), (short)(1050 + (m % 100))),
                });
            }

            result.AddRange(new MeasureReportSection(dayIndex, 1440, dayStart, type, (byte)(ch + 1), records).Encode());
            sections++;
        }

        if (sections == 0)
        {
            result.AddRange(new MeasureReportSection(dayIndex, 0, dayStart, InputType.Inactive, 1, []).Encode());
        }

        return result.ToArray();
    }
}
