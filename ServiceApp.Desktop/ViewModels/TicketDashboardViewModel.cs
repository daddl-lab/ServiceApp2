using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;
using ServiceApp.Core.Repository;
using ServiceApp.Core.Statistics;
using ServiceApp.Desktop.Charts;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel des Ticket-Dashboards. Lädt die Servicetickets aus der in den
/// Einstellungen konfigurierten Excel-Datei, berechnet über
/// <see cref="ITicketStatisticsService"/> den zeitlichen Verlauf sowie die
/// Fehlerursachen-Verteilung für den gewählten Zeitraum und stellt sie als bindbare
/// Eigenschaften und LiveCharts2-Diagrammserien bereit.
/// </summary>
/// <remarks>
/// Analog zu <see cref="DashboardViewModel"/> sind Laden (<see cref="ReloadAsync"/>,
/// liest die Excel-Datei neu ein) und Neuberechnung (Zeitraum-Wechsel, filtert nur die
/// bereits geladenen Tickets neu) bewusst getrennt, damit ein Filterwechsel keinen
/// erneuten Dateizugriff braucht.
/// </remarks>
public sealed partial class TicketDashboardViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ITicketRepository _repository;
    private readonly ITicketStatisticsService _statisticsService;
    private readonly ILogger<TicketDashboardViewModel> _logger;

    private IReadOnlyList<ServiceTicket> _tickets = Array.Empty<ServiceTicket>();

    [ObservableProperty]
    private DateRangePreset _selectedPreset = DateRangePreset.ThisMonth;

    [ObservableProperty]
    private DateTime? _customRangeStart = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? _customRangeEnd = DateTime.Today;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty] private int _totalTickets;

    [ObservableProperty] private ISeries[] _timeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _timeSeriesXAxes = { new Axis() };
    [ObservableProperty] private ISeries[] _causeBreakdownSeries = Array.Empty<ISeries>();

    public TicketDashboardViewModel(
        ISettingsService settingsService,
        ITicketRepository repository,
        ITicketStatisticsService statisticsService,
        ILogger<TicketDashboardViewModel> logger)
    {
        _settingsService = settingsService;
        _repository = repository;
        _statisticsService = statisticsService;
        _logger = logger;

        _ = ReloadAsync();
    }

    /// <summary>
    /// Lädt die Einstellungen und die Ticket-Exceldatei neu von der Festplatte.
    /// </summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var settings = await Task.Run(_settingsService.Load);
            var filePath = settings.TicketExcelFilePath;

            var (tickets, error) = await Task.Run(() => LoadTicketsSafely(filePath));
            _tickets = tickets;

            // ErrorMessage wird bewusst erst nach dem await gesetzt (zurück auf dem
            // UI-Thread), da Property-Änderungen an gebundenen Eigenschaften nicht aus
            // dem Task.Run-Hintergrundthread heraus ausgelöst werden sollen.
            ErrorMessage = error;

            RecomputeStatistics();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private (IReadOnlyList<ServiceTicket> Tickets, string? Error) LoadTicketsSafely(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return (Array.Empty<ServiceTicket>(), "Es ist noch keine Excel-Datei mit Servicetickets konfiguriert (siehe Einstellungen).");
        }

        try
        {
            return (_repository.GetTickets(filePath), null);
        }
        catch (TicketImportException ex)
        {
            _logger.LogError(ex, "Servicetickets konnten nicht geladen werden.");
            return (Array.Empty<ServiceTicket>(), ex.Message);
        }
    }

    [RelayCommand]
    private void SelectToday()
    {
        SelectedPreset = DateRangePreset.Today;
        RecomputeStatistics();
    }

    [RelayCommand]
    private void SelectThisWeek()
    {
        SelectedPreset = DateRangePreset.ThisWeek;
        RecomputeStatistics();
    }

    [RelayCommand]
    private void SelectThisMonth()
    {
        SelectedPreset = DateRangePreset.ThisMonth;
        RecomputeStatistics();
    }

    [RelayCommand]
    private void SelectThisYear()
    {
        SelectedPreset = DateRangePreset.ThisYear;
        RecomputeStatistics();
    }

    [RelayCommand]
    private void ApplyCustomRange()
    {
        if (CustomRangeStart is null || CustomRangeEnd is null)
        {
            ErrorMessage = "Bitte wählen Sie ein Start- und ein Enddatum für den benutzerdefinierten Zeitraum.";
            return;
        }

        SelectedPreset = DateRangePreset.Custom;
        RecomputeStatistics();
    }

    private DateRangeFilter BuildCurrentRange() => SelectedPreset switch
    {
        DateRangePreset.Today => DateRangeFilter.Today(),
        DateRangePreset.ThisWeek => DateRangeFilter.ThisWeek(),
        DateRangePreset.ThisMonth => DateRangeFilter.ThisMonth(),
        DateRangePreset.ThisYear => DateRangeFilter.ThisYear(),
        DateRangePreset.Custom => DateRangeFilter.CustomRange(
            DateOnly.FromDateTime(CustomRangeStart ?? DateTime.Today),
            DateOnly.FromDateTime(CustomRangeEnd ?? DateTime.Today)),
        _ => DateRangeFilter.ThisMonth()
    };

    /// <summary>
    /// Berechnet Kennzahlen und Diagrammdaten für den aktuell gewählten Zeitraum neu,
    /// ohne die Excel-Datei erneut von der Festplatte zu lesen.
    /// </summary>
    private void RecomputeStatistics()
    {
        var range = BuildCurrentRange();
        var stats = _statisticsService.Compute(_tickets, range);

        TotalTickets = stats.TotalTickets;
        TimeSeries = TicketChartFactory.CreateTimeSeriesSeries(stats.TimeSeries);
        TimeSeriesXAxes = TicketChartFactory.CreateTimeSeriesXAxes(stats.TimeSeries);
        CauseBreakdownSeries = TicketChartFactory.CreateCauseBreakdownPieSeries(stats.CauseBreakdown);
    }
}
