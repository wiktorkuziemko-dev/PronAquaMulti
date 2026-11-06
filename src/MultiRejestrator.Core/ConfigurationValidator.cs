using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.Core;

public sealed record ValidationIssue(ushort ParameterId, string Message)
{
    /// <summary>Dla błędów alarmów – numer alarmu (0–3), inaczej <c>null</c>.</summary>
    public int? Alarm { get; init; }
}

/// <summary>
/// Walidacja przed wysłaniem (pkt 7.1, 9 Założeń). Sprawdza format i zakres każdego pola oraz dwie
/// reguły zależności ustalone 5.11.2026: co najmniej jedno pasmo CAT-M1 i NB oraz kolejność progów alarmów.
/// </summary>
public static class ConfigurationValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(Configuration config)
    {
        var issues = new List<ValidationIssue>();
        foreach (var p in config.Catalog.All)
        {
            var error = ParameterCodec.Validate(p, config[p.Id]);
            if (error is not null)
            {
                issues.Add(new ValidationIssue(p.Id, error));
            }
        }

        foreach (var id in new[] { ParameterIds.CatM1Bands, ParameterIds.NbBands })
        {
            if (config.Number(id) is 0)
            {
                issues.Add(new ValidationIssue(id, "Zaznacz co najmniej jedno pasmo."));
            }
        }

        for (var ch = 0; ch < ParameterCatalog.ChannelCount; ch++)
        {
            issues.AddRange(ValidateAlarms(config, ch));
        }

        return issues;
    }

    /// <summary>
    /// Kolejność progów (odpowiedź 4.3.8): wywołanie przy narastaniu – próg wywołania wyższy niż odwołania;
    /// przy opadaniu – niższy. Sprawdzane tylko dla aktywnej grupy analogowej i alarmów z włączoną sygnalizacją.
    /// </summary>
    public static IEnumerable<ValidationIssue> ValidateAlarms(Configuration config, int channel)
    {
        if (config.CoupledOwner(channel) is not null)
        {
            yield break;
        }

        var group = ParameterIds.GroupForInputType(config.InputType(channel));
        if (group is not (InputGroup.Analog2V5 or InputGroup.Analog10V or InputGroup.Analog4To20mA))
        {
            yield break;
        }

        for (var alarm = 0; alarm < 4; alarm++)
        {
            var activity = config.Number(ParameterIds.AlarmActivity(group, alarm, channel)) ?? 0;
            var on = config.Number(ParameterIds.AlarmOn(group, alarm, channel));
            var off = config.Number(ParameterIds.AlarmOff(group, alarm, channel));
            var direction = config.Number(ParameterIds.AlarmDirection(group, alarm, channel)) ?? 0;
            if (activity == 0 || on is null || off is null)
            {
                continue;
            }

            if (direction == 0 && on <= off)
            {
                yield return new ValidationIssue(ParameterIds.AlarmOn(group, alarm, channel),
                    "Wywołanie przy narastaniu: próg wywołania musi być wyższy niż próg odwołania.") { Alarm = alarm };
            }
            else if (direction == 1 && on >= off)
            {
                yield return new ValidationIssue(ParameterIds.AlarmOn(group, alarm, channel),
                    "Wywołanie przy opadaniu: próg wywołania musi być niższy niż próg odwołania.") { Alarm = alarm };
            }
        }
    }
}
