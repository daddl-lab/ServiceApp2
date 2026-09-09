using System.Globalization;
using Avalonia.Data.Converters;
using ServiceApp.Core.Models;

namespace ServiceApp.Desktop.Converters;

/// <summary>
/// Zeigt einen <see cref="FileTypeFilter"/>-Wert in der Dateityp-Auswahlliste mit seinem
/// benutzerfreundlichen Namen an (z. B. "Alle Dateitypen" statt <c>All</c>).
/// </summary>
public sealed class FileTypeFilterDisplayConverter : IValueConverter
{
    public static readonly FileTypeFilterDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FileTypeFilter filter ? filter.DisplayName() : value?.ToString();

    /// <summary>
    /// Siehe <see cref="FileSizeDisplayConverter.ConvertBack"/>: Avalonias Bindungs-Engine
    /// kann <c>ConvertBack</c> auch für nominell einseitige Bindungen aufrufen, ein Werfen
    /// einer Exception hier führte zu einem harten Absturz der Anwendung.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
