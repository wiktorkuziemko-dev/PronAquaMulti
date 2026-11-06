using System.Threading.Channels;

namespace MultiRejestrator.Protocol.Transport;

/// <summary>Para połączonych końcówek w pamięci – do testów i symulatora.</summary>
public sealed class InMemoryTransport : ITransport
{
    private readonly Channel<byte[]> _incoming;
    private readonly Channel<byte[]> _outgoing;
    private bool _open = true;

    private InMemoryTransport(string name, Channel<byte[]> incoming, Channel<byte[]> outgoing)
    {
        Name = name;
        _incoming = incoming;
        _outgoing = outgoing;
    }

    public string Name { get; }

    public bool IsOpen => _open;

    /// <summary>Gdy ustawione, każdy zapis przechodzi przez tę funkcję (np. by zasymulować przekłamania).</summary>
    public Func<byte[], byte[]>? WriteFilter { get; set; }

    public static (InMemoryTransport Master, InMemoryTransport Device) CreatePair(string name = "MEM")
    {
        var toDevice = Channel.CreateUnbounded<byte[]>();
        var toMaster = Channel.CreateUnbounded<byte[]>();
        return (new InMemoryTransport(name, toMaster, toDevice), new InMemoryTransport(name + "-device", toDevice, toMaster));
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (!_open)
        {
            throw new TransportException($"Port {Name} jest zamknięty.");
        }

        var bytes = data.ToArray();
        if (WriteFilter is not null)
        {
            bytes = WriteFilter(bytes);
        }

        return _outgoing.Writer.WriteAsync(bytes, cancellationToken).AsTask();
    }

    public async ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _incoming.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            throw new TransportException($"Połączenie {Name} zostało zamknięte.", ex);
        }
    }

    public void DiscardInput()
    {
        while (_incoming.Reader.TryRead(out _))
        {
        }
    }

    /// <summary>Symuluje odłączenie kabla: druga strona przestaje odbierać i wysyłać.</summary>
    public void Disconnect()
    {
        _open = false;
        _outgoing.Writer.TryComplete();
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
