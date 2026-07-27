using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceApp.Desktop.Services;

namespace ServiceApp.Desktop;

/// <summary>
/// Einstiegspunkt der Anwendung. Baut zuerst einen .NET Generic Host auf, der die
/// Dependency-Injection-Container mit allen Services und ViewModels bereitstellt
/// (siehe <see cref="ServiceCollectionExtensions"/>), und startet anschließend die
/// Avalonia-Oberfläche. Die Trennung von Host (Logik/DI) und UI-Framework hält die
/// Anwendung testbar und erlaubt es, Services unabhängig von der Oberfläche zu prüfen.
/// </summary>
public static class Program
{
    /// <summary>
    /// Globaler Zugriffspunkt auf den DI-Container, damit <see cref="App"/> beim
    /// Erstellen des Hauptfensters die benötigten ViewModels auflösen kann. Avalonias
    /// Lebenszyklus (<c>App.axaml.cs</c>) hat keinen eigenen Konstruktor-Parameter für
    /// externe Abhängigkeiten, daher dieser bewusst schlanke statische Zugriffspunkt.
    /// </summary>
    public static IHost AppHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        AppHost = Host.CreateDefaultBuilder(args)
            .UseSerilog((_, loggerConfiguration) =>
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ServiceApp", "logs");

                loggerConfiguration
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(logDirectory, "serviceapp-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14);
            })
            .ConfigureServices(services => services.AddServiceAppServices())
            .Build();

        var logger = AppHost.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("ServiceApp wird gestartet.");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppHost.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
