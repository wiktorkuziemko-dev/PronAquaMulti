namespace MultiRejestrator.Protocol.Transport;

/// <summary>
/// Kanał bajtowy do urządzenia. Logika protokołu korzysta wyłącznie z tego interfejsu,
/// dzięki czemu jest testowalna bez portu COM.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>Nazwa do komunikatów, np. „COM3”.</summary>
    string Name { get; }

    bool IsOpen { get; }

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    /// <summary>Czeka na co najmniej jeden bajt i zwraca wszystkie dostępne.</summary>
    ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Odrzuca bajty oczekujące w buforze odbiorczym.</summary>
    void DiscardInput();
}
