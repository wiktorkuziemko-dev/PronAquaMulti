using MultiRejestrator.Core;
using MultiRejestrator.Core.Device;
using MultiRejestrator.Core.Parameters;
using MultiRejestrator.Protocol;
using MultiRejestrator.Protocol.Transport;
using MultiRejestrator.Simulator;

namespace MultiRejestrator.Tests;

public sealed class DeviceSessionTests : IAsyncLifetime
{
    private readonly SimulatedDevice _device = new("12345678");
    private readonly CancellationTokenSource _cts = new();
    private readonly List<FrameLogEntry> _log = new();
    private InMemoryTransport _master = null!;
    private Task _deviceTask = Task.CompletedTask;

    public Task InitializeAsync()
    {
        var (master, device) = InMemoryTransport.CreatePair("TEST");
        _master = master;
        _deviceTask = _device.RunAsync(device, _cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _deviceTask;
    }

    private Task<DeviceSession> Connect(string code = "000000") =>
        DeviceSession.ConnectAsync(_master, code, new ListLog(_log));

    [Fact]
    public async Task Connect_reads_identity()
    {
        var session = await Connect();
        Assert.Equal("12345678", session.Identity.SerialNumber);
        Assert.Equal(1, session.Identity.ProtocolVersion);
        Assert.Equal(3892, session.Identity.Firmware.SvnRevision);
        Assert.Contains(_log, e => e.Direction == FrameDirection.Sent && e.Description.StartsWith("REQ_HELLO"));
    }

    [Fact]
    public async Task Wrong_access_code_is_rejected_with_error_frame()
    {
        var ex = await Assert.ThrowsAsync<DeviceErrorException>(() => Connect("111111"));
        Assert.Equal(DeviceErrorFlags.WrongPassword, ex.Flags);
    }

    [Fact]
    public async Task Silent_device_times_out()
    {
        _device.Silent = true;
        var client = new ProtocolClient(_master) { ResponseTimeout = TimeSpan.FromMilliseconds(200) };
        await Assert.ThrowsAsync<DeviceTimeoutException>(() => client.HelloAsync());
    }

    [Fact]
    public async Task Read_configuration_returns_all_parameters()
    {
        var session = await Connect();
        var progress = new List<OperationProgress>();
        var result = await session.ReadConfigurationAsync(new SyncProgress<OperationProgress>(progress.Add));

        Assert.Empty(result.MissingIds);
        Assert.Equal("12345678", result.Configuration[1]);
        Assert.Equal("5", result.Configuration[3001]);
        Assert.Equal(52, progress.Last().Total);
    }

    [Fact]
    public async Task Write_configuration_round_trips_and_skips_access_code()
    {
        var session = await Connect();
        var config = Configuration.CreateDefault("999999");
        config[1] = "12345678";
        config[7] = "iot.example";
        config[26] = new string('F', 64);
        config[5001] = "3";

        await session.WriteConfigurationAsync(config, setTime: true, progress: null);

        var read = _device.ReadConfiguration();
        Assert.Equal("iot.example", read[7]);
        Assert.Equal(new string('F', 64), read[26]);
        Assert.Equal("3", read[5001]);
        Assert.Equal("000000", read[ParameterIds.AccessCode]);
        Assert.InRange((_device.Clock - DateTimeOffset.UtcNow).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Interrupted_write_reports_partial_progress()
    {
        var session = await Connect();
        session.Client.WriteResponseTimeout = TimeSpan.FromMilliseconds(200);
        _device.GoSilentAfterWrites = 10;

        var ex = await Assert.ThrowsAsync<PartialWriteException>(() =>
            session.WriteConfigurationAsync(Configuration.CreateDefault("000000"), false, null));
        Assert.Equal(10, ex.FramesDone);
        Assert.IsType<DeviceTimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task Change_access_code_switches_password()
    {
        var session = await Connect();
        await session.ChangeAccessCodeAsync("AB12cd");

        Assert.Equal("AB12cd", _device.Password);
        await session.KeepAliveAsync();
    }

    [Fact]
    public async Task Device_state_and_gsm_monitor()
    {
        var session = await Connect();
        var state = await session.ReadDeviceStateAsync();
        Assert.Equal(15, state.Count);
        Assert.Equal("3580", state[DeviceStateIds.BatteryVoltage]);
        Assert.Equal("89480000000000000000", state[DeviceStateIds.Iccid]);

        await session.StartGsmMonitorAsync();
        Assert.True(_device.GsmMonitorActive);
        var gsm = await session.ReadGsmStatusAsync();
        Assert.Equal(3, gsm.State);
        Assert.InRange(gsm.SignalLevel!.Value, 0, 31);

        Assert.Equal(1, await session.RequestReportAsync(10));
    }

    [Fact]
    public async Task Counters_are_read_and_written()
    {
        var session = await Connect();
        var measure = await session.ReadMeasureCountersAsync();
        Assert.Equal(8, measure.Count);
        Assert.Equal("123456", measure[101]);

        await session.WriteMeasureCountersAsync(new Dictionary<ushort, string> { [301] = "-42", [101] = "1000" });
        Assert.Equal("-42", _device.MeasureCounter(2));
        Assert.Equal("1000", _device.MeasureCounter(0));

        var device = await session.ReadDeviceCountersAsync();
        Assert.Equal(9, device.Count);
        await session.WriteDeviceCountersAsync(device.ToDictionary(kv => kv.Key, _ => "0"));
        Assert.Equal("0", _device.DeviceCounter(1));
    }

    [Fact]
    public async Task Wrong_response_is_not_treated_as_lost_connection()
    {
        var session = await Connect();
        session.Client.ResponseTimeout = TimeSpan.FromMilliseconds(300);
        _device.CorruptNextResponseId = true;

        var ex = await Assert.ThrowsAsync<InvalidResponseException>(() => session.KeepAliveAsync());
        Assert.Contains("numerem", ex.Message);
        await session.KeepAliveAsync(); // połączenie nadal działa (uwaga 8)
    }

    [Fact]
    public async Task Error_frame_keeps_connection_usable()
    {
        var session = await Connect();
        await Assert.ThrowsAsync<DeviceErrorException>(() =>
            session.Client.TransactAsync((CommandType)0x0042, [], TimeSpan.FromSeconds(1)));
        await session.KeepAliveAsync();
    }

    [Fact]
    public async Task Measure_day_collects_all_frames()
    {
        var session = await Connect();
        var day = await session.ReadMeasureDayAsync(4, null);

        Assert.Equal(3, day.Channels.Count);
        Assert.All(day.Channels, c => Assert.Equal(96, c.Records.Count));
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), day.DayStart);
    }

    private sealed class ListLog(List<FrameLogEntry> entries) : IFrameLog
    {
        public void Write(FrameLogEntry entry)
        {
            lock (entries)
            {
                entries.Add(entry);
            }
        }
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
