using Microsoft.Extensions.DependencyInjection;
using ServiceApp.Core.Configuration;
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

        // ViewModels: als Singleton registriert, da die Anwendung ein einzelnes
        // Hauptfenster mit fest verdrahteter Navigation zwischen den Dashboards und
        // Einstellungen besitzt (kein Bedarf an mehreren unabhängigen Instanzen).
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TicketDashboardViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
