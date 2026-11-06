namespace MultiRejestrator.Protocol;

public enum FrameDirection
{
    Sent,
    Received,
    Info,
    Error,
}

/// <summary>Wpis logu komunikacji: pełna zawartość pakietu (pkt 18 Założeń, pytanie 5.9 – bez maskowania).</summary>
public sealed record FrameLogEntry(DateTime Timestamp, FrameDirection Direction, string Description, byte[] Bytes)
{
    public string Hex => Convert.ToHexString(Bytes) is { Length: > 0 } h ? string.Join(' ', Enumerable.Range(0, h.Length / 2).Select(i => h.Substring(i * 2, 2))) : string.Empty;

    public override string ToString()
    {
        var dir = Direction switch
        {
            FrameDirection.Sent => "TX",
            FrameDirection.Received => "RX",
            FrameDirection.Error => "!!",
            _ => "--",
        };
        var text = $"{Timestamp:HH:mm:ss.fff}  {dir}  {Description}";
        return Bytes.Length > 0 ? text + Environment.NewLine + "               " + Hex : text;
    }
}

public interface IFrameLog
{
    void Write(FrameLogEntry entry);
}
