using System.Globalization;
using System.Text;
using MultiRejestrator.Core.Device;
using MultiRejestrator.Protocol;

namespace MultiRejestrator.Core.Files;

/// <summary>Eksport danych pomiarowych do CSV z separatorem „;” (odpowiedź 7.7).</summary>
public static class MeasureCsv
{
    public static string FileName(string serialNumber, DateTime when) =>
        $"{serialNumber}_pomiary_{when:yyyyMMdd_HHmm}.csv";

    public static string Serialize(MeasureDay day)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Kanał;Typ wejścia;Data i czas (lokalny);Minuta dnia;Licznik;Stan;Status stabilizacji;Czas stabilizacji [ms];Min;Max;Średnia");
        foreach (var ch in day.Channels)
        {
            foreach (var r in ch.Records)
            {
                var time = day.DayStart is { } ds ? ds.AddMinutes(r.MinuteOfDay).ToLocalTime().ToString("yyyy-MM-dd HH:mm", inv) : string.Empty;
                var prefix = $"{ch.ChannelNumber};{(byte)ch.InputType};{time};{r.MinuteOfDay}";
                sb.AppendLine(r switch
                {
                    PulseRecord p => $"{prefix};{p.Counter.ToString(inv)};;;;;;",
                    BinaryRecord b => $"{prefix};;{b.State};;;;;",
                    AnalogRecord a => $"{prefix};;;{a.StabilizationStatus};{a.StabilizationTimeMs};{a.Min};{a.Max};{a.Mean}",
                    _ => prefix,
                });
            }
        }

        return sb.ToString();
    }
}
