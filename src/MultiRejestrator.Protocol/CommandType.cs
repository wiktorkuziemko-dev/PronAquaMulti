namespace MultiRejestrator.Protocol;

/// <summary>Kody ramek zapytań (pole TYPE). Kod odpowiedzi = kod zapytania | 0x8000.</summary>
public enum CommandType : ushort
{
    Hello = 0x0001,
    Reset = 0x0002,
    GetFirmwareDate = 0x0003,
    GetProtocolRevision = 0x0004,
    GetConfiguration = 0x0011,
    SetConfiguration = 0x0012,
    GetTime = 0x0013,
    SetTime = 0x0014,
    GetDeviceState = 0x0015,
    ReportOnDemand = 0x0016,
    MonitorGsm = 0x0017,
    GetMeasureReport = 0x0018,
}

public static class FrameTypes
{
    /// <summary>Ramka błędu wysyłana przez urządzenie (ERROR_FRAME).</summary>
    public const ushort Error = 0xFFFF;

    public const ushort ResponseFlag = 0x8000;

    public static ushort ResponseOf(CommandType command) => (ushort)((ushort)command | ResponseFlag);

    /// <summary>Nazwa ramki do logu, zgodna z nazwami z Protokołu.</summary>
    public static string NameOf(ushort type)
    {
        if (type == Error)
        {
            return "ERROR_FRAME";
        }

        var isResponse = (type & ResponseFlag) != 0 && type != Error;
        var command = (CommandType)(type & ~ResponseFlag & 0xFFFF);
        var name = command switch
        {
            CommandType.Hello => "REQ_HELLO",
            CommandType.Reset => "REQ_RESET",
            CommandType.GetFirmwareDate => "REQ_GET_FW_DATE",
            CommandType.GetProtocolRevision => "REQ_GET_PROT_REV",
            CommandType.GetConfiguration => "REQ_GET_CONF",
            CommandType.SetConfiguration => "REQ_SET_CONF",
            CommandType.GetTime => "REQ_GET_TIME",
            CommandType.SetTime => "REQ_SET_TIME",
            CommandType.GetDeviceState => "REQ_DEVICE_STATE",
            CommandType.ReportOnDemand => "REQ_REPORT_ON_DEMAND",
            CommandType.MonitorGsm => "REQ_MONITOR_GSM",
            CommandType.GetMeasureReport => "REQ_GET_MEASURE_REPORT",
            _ => $"0x{(ushort)command:X4}",
        };

        return isResponse ? "RE_" + name : name;
    }
}
