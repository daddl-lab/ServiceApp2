using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.Configuration;

/// <summary>
/// Persistiert <see cref="AppSettings"/> als lesbare JSON-Datei im
/// benutzerspezifischen Anwendungsdatenordner (unter Windows z. B.
/// <c>%AppData%\ServiceApp\settings.json</c>, unter Linux
/// <c>~/.config/ServiceApp/settings.json</c>). Die Verwendung von
/// <see cref="Environment.SpecialFolder.ApplicationData"/> stellt sicher, dass die
/// Einstellungen unabhängig vom Installationsort der Anwendung erhalten bleiben.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<JsonSettingsService> _logger;

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
        : this(logger, GetDefaultSettingsFilePath())
    {
    }

    /// <summary>
    /// Ermöglicht Tests, einen abweichenden Dateipfad zu verwenden, ohne das echte
    /// Benutzerprofil zu berühren.
    /// </summary>
    public JsonSettingsService(ILogger<JsonSettingsService> logger, string settingsFilePath)
    {
        _logger = logger;
        _settingsFilePath = settingsFilePath;
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ServiceApp");
        return Path.Combine(appDataFolder, "settings.json");
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            _logger.LogInformation("Keine Einstellungsdatei unter {Path} gefunden, verwende Standardeinstellungen.", _settingsFilePath);
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            _logger.LogInformation("Einstellungen aus {Path} geladen.", _settingsFilePath);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Einstellungen unter {Path} konnten nicht gelesen werden, verwende Standardeinstellungen.", _settingsFilePath);
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
        _logger.LogInformation("Einstellungen nach {Path} gespeichert.", _settingsFilePath);
    }
}
