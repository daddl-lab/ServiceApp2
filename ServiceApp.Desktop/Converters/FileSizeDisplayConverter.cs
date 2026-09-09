using System.Globalization;
using Avalonia.Data.Converters;

namespace ServiceApp.Desktop.Converters;

/// <summary>Formatiert eine Dateigröße in Bytes für die "Größe"-Spalte der Trefferliste menschenlesbar (B/KB/MB/GB).</summary>
public sealed class FileSizeDisplayConverter : IValueConverter
{
    public static readonly FileSizeDisplayConverter Instance = new();

    private static readonly string[] Units = { "Bytes", "KB", "MB", "GB", "TB" };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long sizeBytes)
        {
            return value?.ToString();
        }

        double size = sizeBytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.0";
        return $"{size.ToString(format, CultureInfo.GetCultureInfo("de-DE"))} {Units[unitIndex]}";
    }

    /// <summary>
    /// Diese Spalte ist nur lesend gedacht (siehe <c>Mode=OneWay</c> in
    /// <c>FileSearchView.axaml</c>); Avalonias Bindungs-Engine ruft <c>ConvertBack</c> aber
    /// auch für nominell einseitige Bindungen auf (z. B. im Rahmen der
    /// DataGrid-Spaltensortierung), unabhängig vom konkreten Suchergebnis. Ein Werfen einer
    /// Exception hier führte deshalb zu einem harten Absturz der Anwendung - anders als in
    /// WPF ist <see cref="Avalonia.Data.BindingOperations.DoNothing"/> der korrekte Weg,
    /// "kein Rückweg unterstützt" zu signalisieren, ohne die Anwendung zu beenden.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
