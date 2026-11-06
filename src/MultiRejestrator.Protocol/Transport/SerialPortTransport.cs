using System.IO.Ports;
using System.Threading.Channels;

namespace MultiRejestrator.Protocol.Transport;

/// <summary>
/// Port szeregowy 115200 bps 8N1 (Protokół, rozdz. 1). Linie DTR/RTS nieużywane (pytanie 2.9).
/// Odbiór w zdarzeniu DataReceived trafia do kolejki, z której czyta klient protokołu.
/// </summary>
public sealed class SerialPortTransport : ITransport
{
    public const int BaudRate = 115200;

    private readonly SerialPort _port;
    private readonly Channel<byte[]> _received = Channel.CreateUnbounded<byte[]>();

    private SerialPortTransport(SerialPort port)
    {
        _port = port;
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += (_, e) => _received.Writer.TryWrite([]);
    }

    public string Name => _port.PortName;

    public bool IsOpen => _port.IsOpen;

    public static string[] GetPortNames() =>
        SerialPort.GetPortNames().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(PortSortKey).ToArray();

    /// <summary>Otwiera port. Rzuca <see cref="TransportException"/> z czytelnym opisem przyczyny.</summary>
    public static SerialPortTransport Open(string portName)
    {
        var port = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = 2000,
        };

        try
        {
            port.Open();
        }
        catch (UnauthorizedAccessException ex)
        {
            port.Dispose();
            throw new TransportException($"Port {portName} jest zajęty przez inny program albo brak do niego dostępu.", ex);
        }
        catch (IOException ex)
        {
            port.Dispose();
            throw new TransportException($"Nie można otworzyć portu {portName}: urządzenie nie istnieje lub zostało odłączone.", ex);
        }
        catch (ArgumentException ex)
        {
            port.Dispose();
            throw new TransportException($"Nieprawidłowa nazwa portu „{portName}”.", ex);
        }

        port.DiscardInBuffer();
        return new SerialPortTransport(port);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await _port.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await _port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            throw new TransportException($"Błąd zapisu do portu {Name}: {ex.Message}", ex);
        }
    }

    public async ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var chunk = await _received.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (chunk.Length > 0)
            {
                return chunk;
            }

            if (!_port.IsOpen)
            {
                throw new TransportException($"Port {Name} został zamknięty.");
            }
        }
    }

    public void DiscardInput()
    {
        while (_received.Reader.TryRead(out _))
        {
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.DiscardInBuffer();
            }
        }
        catch (IOException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _port.DataReceived -= OnDataReceived;
        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        catch (IOException)
        {
            // Port mógł zniknąć (odłączony konwerter USB) – zamknięcie i tak się kończy.
        }

        _port.Dispose();
        _received.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var count = _port.BytesToRead;
            if (count <= 0)
            {
                return;
            }

            var buffer = new byte[count];
            var read = _port.Read(buffer, 0, count);
            _received.Writer.TryWrite(read == count ? buffer : buffer[..read]);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            _received.Writer.TryWrite([]);
        }
    }

    private static (int, string) PortSortKey(string name) =>
        name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(3), out var n) ? (n, name) : (int.MaxValue, name);
}
