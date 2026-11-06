using MultiRejestrator.Protocol.Transport;
using MultiRejestrator.Simulator;

// Symulator MultiRejestratora na porcie szeregowym, np. jednym z pary wirtualnych portów (com0com).
// Program konfiguracyjny łączy się z drugim portem pary.
if (args.Length < 1)
{
    Console.WriteLine("Użycie: MultiRejestrator.Simulator <port> [numer_seryjny]");
    Console.WriteLine("Dostępne porty: " + string.Join(", ", SerialPortTransport.GetPortNames()));
    return 1;
}

var device = new SimulatedDevice(args.Length > 1 ? args[1] : "12345678");
device.Activity += message => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {message}");
await using var transport = SerialPortTransport.Open(args[0]);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Symulator nasłuchuje na {args[0]} (115200 8N1). Kod dostępu: {device.Password}. Ctrl+C kończy.");
await device.RunAsync(transport, cts.Token);
return 0;
