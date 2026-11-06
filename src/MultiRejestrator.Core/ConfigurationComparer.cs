using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.Core;

public sealed record ParameterDifference(ParameterDefinition Parameter, string Left, string Right)
{
    public bool IsDifferent => !string.Equals(Left, Right, StringComparison.Ordinal);
}

/// <summary>Porównanie dwóch konfiguracji parametr po parametrze (pkt 10 Założeń). Niczego nie zapisuje.</summary>
public static class ConfigurationComparer
{
    public static IReadOnlyList<ParameterDifference> Compare(Configuration left, Configuration right) =>
        left.Catalog.All.Select(p => new ParameterDifference(p, left[p.Id], right[p.Id])).ToList();
}
