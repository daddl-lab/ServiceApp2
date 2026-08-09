using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.SettingsTests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _settingsFilePath;

    public JsonSettingsServiceTests()
    {
        _settingsFilePath = Path.Combine(Path.GetTempPath(), $"ServiceAppSettingsTest_{Guid.NewGuid()}", "settings.json");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_NoSettingsFileExists_ReturnsDefaultSettingsWithoutThrowing()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _settingsFilePath);

        var settings = service.Load();

        Assert.Equal(2, settings.ServiceNumbers.Count);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _settingsFilePath);
        var settings = new AppSettings
        {
            ServiceNumbers = new List<ServiceNumberSettings>
            {
                new() { Id = "service-1", Name = "Support-Hotline", PdfFolderPath = "/data/support" },
                new() { Id = "service-2", Name = "Vertrieb", PdfFolderPath = "/data/vertrieb" }
            }
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(2, loaded.ServiceNumbers.Count);
        Assert.Equal("Support-Hotline", loaded.ServiceNumbers[0].Name);
        Assert.Equal("/data/support", loaded.ServiceNumbers[0].PdfFolderPath);
        Assert.Equal("Vertrieb", loaded.ServiceNumbers[1].Name);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTicketErrorLocationTopCount()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _settingsFilePath);
        var settings = new AppSettings { TicketErrorLocationTopCount = 12 };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(12, loaded.TicketErrorLocationTopCount);
    }

    [Fact]
    public void Save_CreatesDirectoryIfItDoesNotExist()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _settingsFilePath);

        service.Save(new AppSettings());

        Assert.True(File.Exists(_settingsFilePath));
    }

    [Fact]
    public void Load_CorruptJsonFile_ReturnsDefaultSettingsInsteadOfThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        File.WriteAllText(_settingsFilePath, "{ this is not valid json");
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _settingsFilePath);

        var settings = service.Load();

        Assert.Equal(2, settings.ServiceNumbers.Count);
    }
}
