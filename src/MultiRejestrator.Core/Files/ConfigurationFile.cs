using System.Globalization;
using System.Text;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.Core.Files;

/// <summary>Informacje zapisywane w nagłówku pliku (pkt 11.3 Założeń).</summary>
public sealed record ConfigurationFileInfo
{
    /// <summary>Wersja protokołu – jednocześnie wersja formatu pliku (pkt 11.3, odpowiedź 7.4).</summary>
    public int? ProtocolVersion { get; init; }

    public string? SerialNumber { get; init; }

    public string? Firmware { get; init; }

    public DateTime? Created { get; init; }

    public DateTime? Modified { get; init; }
}

public sealed record ConfigurationFileResult(Configuration Configuration, ConfigurationFileInfo Info, IReadOnlyList<string> Warnings);

/// <summary>
/// Plik konfiguracji w czytelnym formacie tekstowym (odpowiedź 7.1): sekcje jak w programie,
/// jeden parametr w wierszu <c>Id = wartość  # nazwa</c>. Teksty w cudzysłowach.
/// </summary>
public static class ConfigurationFile
{
    public const string Extension = ".txt";
    private const string InfoSection = "Plik";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Serialize(Configuration config, ConfigurationFileInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MultiRejestrator – plik konfiguracji urządzenia");
        sb.AppendLine("# Format: Id = wartość   # nazwa parametru. Teksty w cudzysłowach, liczby dziesiętnie,");
        sb.AppendLine("# numer seryjny jako 8 cyfr, klucz AES szesnastkowo. Wiersze od # to komentarze.");
        sb.AppendLine();
        sb.AppendLine($"[{InfoSection}]");
        if (info.ProtocolVersion is { } pv) sb.AppendLine($"wersja_protokolu = {pv}");
        if (info.SerialNumber is { } sn) sb.AppendLine($"numer_seryjny = {Quote(sn)}");
        if (info.Firmware is { } fw) sb.AppendLine($"firmware = {Quote(fw)}");
        if (info.Created is { } c) sb.AppendLine($"utworzono = {c.ToString("yyyy-MM-dd HH:mm:ss", Inv)}");
        if (info.Modified is { } m) sb.AppendLine($"zmodyfikowano = {m.ToString("yyyy-MM-dd HH:mm:ss", Inv)}");

        foreach (var (title, items) in Group(config.Catalog))
        {
            sb.AppendLine();
            sb.AppendLine($"[{title}]");
            foreach (var p in items)
            {
                var value = config[p.Id];
                var text = p.IsNumeric ? (value.Length == 0 ? "0" : value) : Quote(value);
                var line = $"{p.Id} = {text}";
                var comment = p.FullName;
                if (p.EditKind is EditKind.Enum or EditKind.Bitmask && ParameterCodec.TryGetNumber(value) is not null)
                {
                    comment += $" ({ParameterCodec.Format(p, value)})";
                }
                else if (!string.IsNullOrEmpty(p.Unit))
                {
                    comment += $" [{p.Unit}]";
                }

                sb.Append(line.PadRight(28)).Append("# ").AppendLine(comment);
            }
        }

        return sb.ToString();
    }

    public static ConfigurationFileResult Parse(string text, string defaultAccessCode, ParameterCatalog? catalog = null)
    {
        catalog ??= ParameterCatalog.Configuration;
        var config = Configuration.CreateDefault(defaultAccessCode, catalog);
        var warnings = new List<string>();
        var seen = new HashSet<ushort>();
        var unknown = new List<ushort>();
        var info = new ConfigurationFileInfo();
        var section = string.Empty;
        var lineNo = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            lineNo++;
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                warnings.Add($"Wiersz {lineNo}: pominięto (brak znaku „=”).");
                continue;
            }

            var key = line[..eq].Trim();
            if (!TryParseValue(line[(eq + 1)..], out var value))
            {
                warnings.Add($"Wiersz {lineNo}: nieprawidłowa wartość – pominięto.");
                continue;
            }

            if (section == InfoSection)
            {
                info = key switch
                {
                    "wersja_protokolu" => info with { ProtocolVersion = int.TryParse(value, Inv, out var v) ? v : null },
                    "numer_seryjny" => info with { SerialNumber = value },
                    "firmware" => info with { Firmware = value },
                    "utworzono" => info with { Created = ParseDate(value) },
                    "zmodyfikowano" => info with { Modified = ParseDate(value) },
                    _ => info,
                };
                continue;
            }

            if (!ushort.TryParse(key, Inv, out var id))
            {
                warnings.Add($"Wiersz {lineNo}: „{key}” nie jest numerem parametru – pominięto.");
                continue;
            }

            if (!catalog.Contains(id))
            {
                unknown.Add(id);
                continue;
            }

            config[id] = value;
            seen.Add(id);
        }

        if (unknown.Count > 0)
        {
            warnings.Add($"Plik zawiera {unknown.Count} parametrów nieznanych tej wersji programu (np. Id {unknown[0]}) – pominięto je.");
        }

        var missing = catalog.All.Count(p => !seen.Contains(p.Id));
        if (missing > 0)
        {
            warnings.Add($"W pliku brakuje {missing} parametrów – przyjęto dla nich wartości domyślne.");
        }

        return new ConfigurationFileResult(config, info, warnings);
    }

    public static async Task SaveAsync(string path, Configuration config, ConfigurationFileInfo info, CancellationToken ct = default) =>
        await File.WriteAllTextAsync(path, Serialize(config, info), new UTF8Encoding(false), ct).ConfigureAwait(false);

    public static async Task<ConfigurationFileResult> LoadAsync(string path, string defaultAccessCode, CancellationToken ct = default) =>
        Parse(await File.ReadAllTextAsync(path, Encoding.UTF8, ct).ConfigureAwait(false), defaultAccessCode);

    /// <summary>Sekcje pliku w kolejności interfejsu.</summary>
    public static IEnumerable<(string Title, IReadOnlyList<ParameterDefinition> Items)> Group(ParameterCatalog catalog)
    {
        yield return ("Ogólne", catalog.InSection(Section.General).ToList());
        yield return ("Modem LTE", catalog.InSection(Section.Modem).ToList());
        yield return ("APN", catalog.InSection(Section.Apn).ToList());
        yield return ("Serwery i MQTT", catalog.InSection(Section.Servers).ToList());
        yield return ("Transmisja i szyfrowanie", catalog.InSection(Section.Transmission).ToList());
        for (var ch = 0; ch < ParameterCatalog.ChannelCount; ch++)
        {
            yield return ($"Kanał {ch + 1}", catalog.ForChannel(ch).ToList());
        }
    }

    internal static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static bool TryParseValue(string text, out string value)
    {
        text = text.TrimStart();
        if (text.StartsWith('"'))
        {
            var sb = new StringBuilder();
            for (var i = 1; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    sb.Append(text[++i]);
                }
                else if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                else
                {
                    sb.Append(c);
                }
            }

            value = string.Empty;
            return false;
        }

        var hash = text.IndexOf('#');
        value = (hash >= 0 ? text[..hash] : text).Trim();
        return true;
    }

    private static DateTime? ParseDate(string value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var d) ? d : null;
}
