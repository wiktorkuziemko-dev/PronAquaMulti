using System.Globalization;
using Avalonia.Data.Converters;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

/// <summary>Wybiera pole konkretnego parametru z edytora konfiguracji (np. kod dostępu w sekcji Ogólne).</summary>
public sealed class EditorFieldConverter(ushort id) : IValueConverter
{
    public static readonly EditorFieldConverter AccessCode = new(ParameterIds.AccessCode);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ConfigurationEditor editor ? editor.Field(id) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Kolor nagłówka grupy: aktywna – ciemny, nieaktywna – szary.</summary>
public sealed class ActiveBrushConverter : IValueConverter
{
    public static readonly ActiveBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Avalonia.Media.Brushes.Black : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6B7280"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Tło wiersza porównania: różnica – żółte.</summary>
public sealed class DiffBrushConverter : IValueConverter
{
    public static readonly DiffBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFBEB")) : Avalonia.Media.Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
