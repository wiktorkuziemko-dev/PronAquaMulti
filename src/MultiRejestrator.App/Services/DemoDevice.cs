using MultiRejestrator.Protocol.Transport;
using MultiRejestrator.Simulator;

namespace MultiRejestrator.App.Services;

/// <summary>Tryb demonstracyjny: port „SYMULATOR” łączy z urządzeniem symulowanym w pamięci.</summary>
public static class DemoDevice
{
    public const string PortName = "SYMULATOR";

    public static bool Enabled { get; set; }

    public static SimulatedDevice Device { get; } = new("12345678");

    public static ITransport Open(string port)
    {
        if (!Enabled || port != PortName)
        {
            return SerialPortTransport.Open(port);
        }

        var (master, device) = InMemoryTransport.CreatePair(PortName);
        _ = Task.Run(() => Device.RunAsync(device, CancellationToken.None));
        return master;
    }
}
