using System.Globalization;
using System.Text;

namespace MultiRejestrator.Core.Settings;

/// <summary>
/// Ustawienia programu w pliku <c>MultiRejestrator.ini</c> obok programu (odpowiedzi 5.3, 7.12).
/// </summary>
public sealed class AppSettings
{
    public const string FileName = "MultiRejestrator.ini";

    public string LastPort { get; set; } = string.Empty;

    /// <summary>Kod dostępu wpisywany do nowej konfiguracji (Id 2). Fabrycznie „000000” (odpowiedź 5.1).</summary>
    public string DefaultAccessCode { get; set; } = "000000";

    /// <summary>Domyślnie ustawiaj zegar urządzenia przy wysyłaniu konfiguracji (odpowiedź 3.15).</summary>
    public bool SetTimeOnSend { get; set; }

    public string ConfigurationFolder { get; set; } = string.Empty;

    public string ExportFolder { get; set; } = string.Empty;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static AppSettings Parse(string text)
    {
        var s = new AppSettings();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            switch (key)
            {
                case "OstatniPort": s.LastPort = value; break;
                case "DomyslnyKodDostepu": s.DefaultAccessCode = value; break;
                case "UstawZegarPrzyWysylaniu": s.SetTimeOnSend = value is "1" or "tak" or "true"; break;
                case "FolderKonfiguracji": s.ConfigurationFolder = value; break;
                case "FolderEksportu": s.ExportFolder = value; break;
            }
        }

        return s;
    }

    public string Serialize()
    {
        var sb = new StringBuilder();
        sb.AppendLine("; Ustawienia programu MultiRejestrator");
        sb.AppendLine("[Program]");
        sb.AppendLine($"OstatniPort={LastPort}");
        sb.AppendLine("; Kod dostępu wpisywany przy tworzeniu nowej konfiguracji (6 znaków ASCII)");
        sb.AppendLine($"DomyslnyKodDostepu={DefaultAccessCode}");
        sb.AppendLine($"UstawZegarPrzyWysylaniu={(SetTimeOnSend ? 1 : 0).ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"FolderKonfiguracji={ConfigurationFolder}");
        sb.AppendLine($"FolderEksportu={ExportFolder}");
        return sb.ToString();
    }

    /// <summary>Wczytuje ustawienia; brak pliku lub błąd odczytu – wartości domyślne.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Zapisuje ustawienia. Zwraca opis błędu albo <c>null</c>.</summary>
    public string? TrySave(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            File.WriteAllText(path, Serialize(), new UTF8Encoding(false));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Nie można zapisać ustawień w {path}: {ex.Message}";
        }
    }
}
