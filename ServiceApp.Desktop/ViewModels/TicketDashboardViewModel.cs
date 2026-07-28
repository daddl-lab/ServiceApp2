using System.Collections.ObjectModel;
using System.ComponentModel;
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

    /// <summary>
    /// Fehlerursachen-Aufschlüsselung der zuletzt berechneten Statistik, benötigt um
    /// beim Klick auf ein Kuchenstück (<see cref="ShowDrillDown"/>) die zugehörigen
    /// Tickets ohne erneute Neuberechnung nachzuschlagen.
    /// </summary>
    private IReadOnlyList<TicketCauseCount> _causeBreakdown = Array.Empty<TicketCauseCount>();

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

    /// <summary>
    /// Mehrfachauswahl-Filter über die Excel-Spalte "Typ", gilt für beide Diagramme.
    /// Die Auswahl wird bei jeder Änderung dauerhaft gespeichert (siehe
    /// <see cref="PersistSelectedTypes"/>) und beim nächsten Programmstart wieder
    /// geladen.
    /// </summary>
    [ObservableProperty] private ObservableCollection<TypeFilterOption> _typeOptions = new();

    [ObservableProperty] private string _typeFilterSummary = "Alle Typen";

    /// <summary>
    /// Drill-Down-Tabelle der Tickets einer beim Klick im Fehlerursachen-Kuchendiagramm
    /// ausgewählten Ursache (siehe <see cref="ShowDrillDown"/>).
    /// </summary>
    [ObservableProperty] private ObservableCollection<ServiceTicket> _drillDownTickets = new();

    [ObservableProperty] private bool _isDrillDownVisible;

    [ObservableProperty] private string _drillDownTitle = string.Empty;

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

            RebuildTypeOptions(settings.TicketSelectedTypes);
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

    /// <summary>
    /// Baut die Liste der Typ-Filter-Optionen aus den geladenen Tickets neu auf und
    /// übernimmt dabei die zuletzt gespeicherte Auswahl. Eine leere gespeicherte
    /// Auswahl (noch nie konfiguriert, oder der Benutzer hat bewusst alle Typen
    /// ausgewählt) führt dazu, dass beim Aufbau alle Optionen angehakt werden.
    /// </summary>
    private void RebuildTypeOptions(IReadOnlyCollection<string> savedSelection)
    {
        var distinctTypes = _statisticsService.GetDistinctTypes(_tickets);
        var options = distinctTypes
            .Select(type => new TypeFilterOption(type, savedSelection.Count == 0 || savedSelection.Contains(type)))
            .ToList();

        foreach (var option in options)
        {
            option.PropertyChanged += OnTypeOptionChanged;
        }

        TypeOptions = new ObservableCollection<TypeFilterOption>(options);
        UpdateTypeFilterSummary();
    }

    private void OnTypeOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TypeFilterOption.IsSelected))
        {
            return;
        }

        PersistSelectedTypes(GetEffectiveSelectedTypes());
        UpdateTypeFilterSummary();
        RecomputeStatistics();
    }

    /// <summary>
    /// Liefert die aktuell angehakten Typen, normalisiert auf die im gesamten
    /// Anwendung geltende Konvention "leere Liste = kein Filter, alle Typen": sind
    /// entweder alle oder keine Optionen angehakt, wird eine leere Liste
    /// zurückgegeben, statt die Namen einzeln aufzuzählen. Das hält die gespeicherte
    /// Auswahl robust gegenüber neuen Typen, die später in der Exceldatei auftauchen.
    /// </summary>
    private List<string> GetEffectiveSelectedTypes()
    {
        var checkedNames = TypeOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList();
        var isUnfiltered = checkedNames.Count == 0 || checkedNames.Count == TypeOptions.Count;
        return isUnfiltered ? new List<string>() : checkedNames;
    }

    private void UpdateTypeFilterSummary()
    {
        var effective = GetEffectiveSelectedTypes();
        TypeFilterSummary = effective.Count == 0
            ? "Alle Typen"
            : $"{effective.Count} von {TypeOptions.Count} Typen";
    }

    /// <summary>Speichert die Typ-Auswahl dauerhaft, ohne die übrigen Einstellungen zu verändern.</summary>
    private void PersistSelectedTypes(List<string> selection)
    {
        var settings = _settingsService.Load();
        settings.TicketSelectedTypes = selection;
        _settingsService.Save(settings);
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
        var stats = _statisticsService.Compute(_tickets, range, GetEffectiveSelectedTypes());

        TotalTickets = stats.TotalTickets;
        TimeSeries = TicketChartFactory.CreateTimeSeriesSeries(stats.TimeSeries);
        TimeSeriesXAxes = TicketChartFactory.CreateTimeSeriesXAxes(stats.TimeSeries);
        CauseBreakdownSeries = TicketChartFactory.CreateCauseBreakdownPieSeries(stats.CauseBreakdown);
        _causeBreakdown = stats.CauseBreakdown;

        // Ein Zeitraum- oder Filterwechsel kann die zuvor angeklickte Ursache aus dem
        // Diagramm entfernen (z. B. keine Tickets mehr in diesem Zeitraum) - die
        // geöffnete Tabelle würde dann veraltete Daten zeigen und wird deshalb
        // geschlossen.
        CloseDrillDown();
    }

    /// <summary>
    /// Öffnet die Drill-Down-Tabelle für die angeklickte Fehlerursache (Name der
    /// <see cref="LiveChartsCore.SkiaSharpView.PieSeries{TModel}"/>, siehe
    /// <see cref="Charts.TicketChartFactory.CreateCauseBreakdownPieSeries"/>). Wird vom
    /// View-Code-Behind aus dem PointerDown-Event des Kuchendiagramms aufgerufen.
    /// </summary>
    public void ShowDrillDown(string causeName)
    {
        var match = _causeBreakdown.FirstOrDefault(c => c.Cause == causeName);
        if (match is null)
        {
            return;
        }

        DrillDownTickets = new ObservableCollection<ServiceTicket>(match.Tickets);
        DrillDownTitle = $"{match.Cause} ({match.Count} Tickets)";
        IsDrillDownVisible = true;
    }

    [RelayCommand]
    private void CloseDrillDown()
    {
        IsDrillDownVisible = false;
    }
}
