using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.PdfParser;
using ServiceApp.Core.Repository;
using ServiceApp.Core.Statistics;
using ServiceApp.Core.TicketParser;
using ServiceApp.Desktop.ViewModels;

namespace ServiceApp.Desktop.Services;

/// <summary>
/// Registriert alle Services, Parser und ViewModels der Anwendung im
/// Dependency-Injection-Container an einer einzigen, zentralen Stelle. Das hält die
/// Zusammensetzung der Anwendung ("composition root") getrennt von der eigentlichen
/// Fachlogik in <c>ServiceApp.Core</c> und den ViewModels.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceAppServices(this IServiceCollection services)
    {
        // Core-Services: austauschbar über ihre Interfaces, damit z. B. der PDF-Parser
        // später um weitere Formate erweitert oder in Tests durch ein Fake ersetzt
        // werden kann, ohne dass Aufrufer davon wissen müssen.
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IReportLineInterpreter, GermanTableLineInterpreter>();
        services.AddSingleton<IReportLineInterpreter, IsoTableLineInterpreter>();
        services.AddSingleton<IReportLineInterpreter, SwyxVisualGroupsHourlyInterpreter>();
        services.AddSingleton<IPdfReportParser, PdfPigReportParser>();
        services.AddSingleton<ICallRecordRepository, PdfCallRecordRepository>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();

        // Serviceticket-Auswertung (zweites Dashboard): eigener Excel-Parser,
        // -Repository und -Statistik-Service, unabhängig von der Telefonberichts-Kette.
        services.AddSingleton<IServiceTicketParser, ClosedXmlServiceTicketParser>();
        services.AddSingleton<ITicketRepository, ExcelTicketRepository>();
        services.AddSingleton<ITicketStatisticsService, TicketStatisticsService>();

        // Zeichnungsarchiv-Dateisuche: lokaler SQLite-Index (Speicherort wird einmalig aus
        // den Einstellungen aufgelöst; ein späteres Ändern des Speicherorts wirkt erst nach
        // einem Neustart der Anwendung, der Archivpfad selbst dagegen wird bei jedem
        // Scan-Zyklus frisch aus den Einstellungen gelesen), Index- und Suchservice sowie
        // ein Hintergrunddienst, der den Index automatisch aktuell hält.
        services.AddSingleton<IFileSystemWalker, LocalFileSystemWalker>();
        services.AddSingleton<IFileArchiveIndexStore>(provider =>
        {
            var settings = provider.GetRequiredService<ISettingsService>().Load();
            var databasePath = string.IsNullOrWhiteSpace(settings.FileArchiveIndexDatabasePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceApp", "index", "filearchive-index.db")
                : settings.FileArchiveIndexDatabasePath;
            return new FileArchiveIndexStore(provider.GetRequiredService<ILogger<FileArchiveIndexStore>>(), databasePath);
        });
        services.AddSingleton<IFileArchiveIndexService, FileArchiveIndexService>();
        services.AddSingleton<IFileArchiveSearchService, FileArchiveSearchService>();
        services.AddSingleton<IShellLaunchService, ShellLaunchService>();
        services.AddHostedService<FileArchiveIndexHostedService>();

        // ViewModels: als Singleton registriert, da die Anwendung ein einzelnes
        // Hauptfenster mit fest verdrahteter Navigation zwischen den Dashboards und
        // Einstellungen besitzt (kein Bedarf an mehreren unabhängigen Instanzen).
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TicketDashboardViewModel>();
        services.AddSingleton<FileSearchViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
