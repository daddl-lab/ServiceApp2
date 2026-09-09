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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
