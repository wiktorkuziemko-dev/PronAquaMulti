using System.Text;

namespace MultiRejestrator.Core.Parameters;

/// <summary>Minimalny czytnik CSV (separator „;”, pola w cudzysłowach) dla tabel z dokumentacji.</summary>
internal static class CsvTable
{
    public static List<Dictionary<string, string>> Read(TextReader reader)
    {
        var rows = new List<Dictionary<string, string>>();
        string[]? header = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = Split(line);
            if (header is null)
            {
                header = fields.Select(f => f.Trim('﻿')).ToArray();
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < header.Length; i++)
            {
                row[header[i]] = i < fields.Count ? fields[i] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    public static List<Dictionary<string, string>> ReadResource(string name)
    {
        using var stream = typeof(CsvTable).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Brak zasobu {name}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Read(reader);
    }

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ';')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
