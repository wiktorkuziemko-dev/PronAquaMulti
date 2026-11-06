namespace MultiRejestrator.Core.Parameters;

/// <summary>Id parametrów konfiguracyjnych, do których odwołuje się logika programu.</summary>
public static class ParameterIds
{
    public const ushort SerialNumber = 1;
    public const ushort AccessCode = 2;
    public const ushort Module = 5;
    public const ushort CatM1Bands = 10;
    public const ushort NbBands = 11;

    // Podstawy Id parametrów kanału (Id = podstawa + N × 1000).
    public const ushort InputType = 1001;
    public const ushort PulseMode = 1002;

    /// <summary>Podstawy Id alarmów dla grup analogowych: progi wywołania/odwołania, aktywność, kierunek.</summary>
    public static (ushort FirstThreshold, ushort FirstActivity, ushort FirstDirection) AlarmBase(InputGroup group) => group switch
    {
        InputGroup.Analog2V5 => (1010, 1018, 1022),
        InputGroup.Analog10V => (1027, 1035, 1039),
        InputGroup.Analog4To20mA => (1046, 1054, 1058),
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    public static ushort AlarmOn(InputGroup group, int alarm, int channel) => ParameterCatalog.ChannelId((ushort)(AlarmBase(group).FirstThreshold + (2 * alarm)), channel);

    public static ushort AlarmOff(InputGroup group, int alarm, int channel) => ParameterCatalog.ChannelId((ushort)(AlarmBase(group).FirstThreshold + (2 * alarm) + 1), channel);

    public static ushort AlarmActivity(InputGroup group, int alarm, int channel) => ParameterCatalog.ChannelId((ushort)(AlarmBase(group).FirstActivity + alarm), channel);

    public static ushort AlarmDirection(InputGroup group, int alarm, int channel) => ParameterCatalog.ChannelId((ushort)(AlarmBase(group).FirstDirection + alarm), channel);

    /// <summary>Grupa parametrów kanału odpowiadająca wartości typu wejścia (Id 1001).</summary>
    public static InputGroup GroupForInputType(long inputType) => inputType switch
    {
        1 => InputGroup.Pulse,
        2 => InputGroup.Binary,
        3 => InputGroup.Analog2V5,
        4 => InputGroup.Analog10V,
        5 => InputGroup.Analog4To20mA,
        _ => InputGroup.None,
    };
}

/// <summary>Id parametrów ramki Device State (Protokół 1.2.8).</summary>
public static class DeviceStateIds
{
    public const ushort SignalLevel = 1;
    public const ushort GsmErrorCode = 2;
    public const ushort GsmState = 3;
    public const ushort Iccid = 4;
    public const ushort Imsi = 5;
    public const ushort Imei = 6;
    public const ushort Band = 7;
    public const ushort CellId = 8;
    public const ushort TimeSinceReset = 9;
    public const ushort ResetReason = 10;
    public const ushort BatteryVoltage = 11;
    public const ushort PowerSource = 12;
    public const ushort RegulatedVoltage = 13;
    public const ushort Tamper = 14;
    public const ushort Temperature = 15;
}
