using System.Text;
using MultiRejestrator.Protocol;

namespace MultiRejestrator.Core.Files;

/// <summary>
/// Log komunikacji w folderze programu, nadpisywany przy każdym nowym połączeniu (odpowiedź 7.10).
/// Gdy system blokuje zapis, log nie jest zapisywany nigdzie indziej – zgłaszany jest tylko komunikat.
/// </summary>
public sealed class CommunicationLogFile : IFrameLog, IDisposable
{
    public const string FileName = "MultiRejestrator.log";

    private readonly object _sync = new();
    private StreamWriter? _writer;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>Opis błędu zapisu, jeśli plik nie mógł zostać utworzony.</summary>
    public string? Error { get; private set; }

    /// <summary>Wpisy przekazywane dalej (np. do panelu logu w oknie).</summary>
    public event Action<FrameLogEntry>? EntryWritten;

    /// <summary>Rozpoczyna nowy plik logu (nadpisuje poprzedni). Zwraca opis błędu albo <c>null</c>.</summary>
    public string? Start(string? path = null, string? header = null)
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
            path ??= DefaultPath;
            try
            {
                _writer = new StreamWriter(path, append: false, new UTF8Encoding(false)) { AutoFlush = true };
                _writer.WriteLine($"# MultiRejestrator – log komunikacji, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                if (header is not null)
                {
                    _writer.WriteLine($"# {header}");
                }

                Error = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error = $"Nie można zapisać logu komunikacji w {path}: {ex.Message}";
            }

            return Error;
        }
    }

    public void Write(FrameLogEntry entry)
    {
        lock (_sync)
        {
            try
            {
                _writer?.WriteLine(entry.ToString());
            }
            catch (IOException ex)
            {
                Error = $"Błąd zapisu logu komunikacji: {ex.Message}";
                _writer?.Dispose();
                _writer = null;
            }
        }

        EntryWritten?.Invoke(entry);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
