using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MultiRejestrator.App.Converters;

/// <summary>Tekst ścieżki SVG → geometria (ikony menu).</summary>
public sealed class GeometryConverter : IValueConverter
{
    public static readonly GeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s.Length > 0 ? StreamGeometry.Parse(s) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Kolor „#RRGGBB” → pędzel.</summary>
public sealed class ColorBrushConverter : IValueConverter
{
    public static readonly ColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && Color.TryParse(s, out var c) ? new SolidColorBrush(c) : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Wysokość słupka wykresu sygnału.</summary>
public sealed class BarHeightConverter : IValueConverter
{
    public static readonly BarHeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is double d ? d : 0d;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
