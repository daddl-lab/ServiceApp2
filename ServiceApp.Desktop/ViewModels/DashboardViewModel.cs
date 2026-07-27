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
/// ViewModel des Dashboards - die zentrale Ansicht der Anwendung. Lädt die Anrufdaten
/// beider konfigurierten Servicenummern, berechnet über <see cref="IStatisticsService"/>
/// alle Kennzahlen für den gewählten Zeitraum und stellt sie als bindbare Eigenschaften
/// und LiveCharts2-Diagrammserien für die Oberfläche bereit.
/// </summary>
/// <remarks>
/// Zwei Vorgänge sind bewusst getrennt: <see cref="ReloadAsync"/> liest die PDF-Dateien
/// neu von der Festplatte ein (teuer, nötig nach Einstellungsänderungen oder auf
/// Benutzeranfrage), während ein Wechsel des Zeitraum-Filters lediglich die bereits im
/// Speicher gehaltenen Anrufdatensätze neu filtert und die Kennzahlen neu berechnet
/// (günstig, da keine erneute Dateizugriffe nötig sind).
/// </remarks>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ICallRecordRepository _repository;
    private readonly IStatisticsService _statisticsService;
    private readonly ILogger<DashboardViewModel> _logger;

    private IReadOnlyList<CallRecord> _firstRecords = Array.Empty<CallRecord>();
    private IReadOnlyList<CallRecord> _secondRecords = Array.Empty<CallRecord>();
    private ServiceNumberSettings _firstSettings = new() { Id = "service-1", Name = "Servicenummer 1" };
    private ServiceNumberSettings _secondSettings = new() { Id = "service-2", Name = "Servicenummer 2" };

    [ObservableProperty]
    private DateRangePreset _selectedPreset = DateRangePreset.ThisMonth;

    [ObservableProperty]
    private DateTime? _customRangeStart = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? _customRangeEnd = DateTime.Today;

    [ObservableProperty]
    private FrequencyChartMode _frequencyChartMode = FrequencyChartMode.Bar;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Kennzahlen (Gesamtstatistik über beide Servicenummern zusammen)
    [ObservableProperty] private int _totalCalls;
    [ObservableProperty] private double _averageCallsPerDay;
    [ObservableProperty] private string _bestDayText = "–";
    [ObservableProperty] private string _worstDayText = "–";
    [ObservableProperty] private double _averageAnswerRatePercent;
    [ObservableProperty] private int _answeredCalls;
    [ObservableProperty] private double _answeredPercent;
    [ObservableProperty] private int _missedCalls;
    [ObservableProperty] private double _missedPercent;

    // Diagramme
    [ObservableProperty] private ISeries[] _trendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _trendXAxes = { new Axis() };
    [ObservableProperty] private ISeries[] _frequencySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _frequencyXAxes = { new Axis() };
    [ObservableProperty] private ISeries[] _answerPieSeries = Array.Empty<ISeries>();

    // Vergleich beider Servicenummern
    [ObservableProperty] private ServiceNumberSummary _firstSummary = ServiceNumberSummary.Empty("Servicenummer 1");
    [ObservableProperty] private ServiceNumberSummary _secondSummary = ServiceNumberSummary.Empty("Servicenummer 2");
    [ObservableProperty] private ISeries[] _comparisonTotalsSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _comparisonTrendSeries = Array.Empty<ISeries>();

    public DashboardViewModel(
        ISettingsService settingsService,
        ICallRecordRepository repository,
        IStatisticsService statisticsService,
        ILogger<DashboardViewModel> logger)
    {
        _settingsService = settingsService;
        _repository = repository;
        _statisticsService = statisticsService;
        _logger = logger;

        _ = ReloadAsync();
    }

    /// <summary>
    /// Lädt die Einstellungen und alle PDF-Berichte beider Servicenummern neu von der
    /// Festplatte. Fehler einer einzelnen Servicenummer (z. B. nicht konfigurierter oder
    /// nicht existierender Ordner) verhindern nicht die Anzeige der jeweils anderen.
    /// </summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var settings = await Task.Run(_settingsService.Load);
            _firstSettings = settings.ServiceNumbers.ElementAtOrDefault(0) ?? _firstSettings;
            _secondSettings = settings.ServiceNumbers.ElementAtOrDefault(1) ?? _secondSettings;

            var errors = new List<string>();
            _firstRecords = await Task.Run(() => LoadRecordsSafely(_firstSettings, errors));
            _secondRecords = await Task.Run(() => LoadRecordsSafely(_secondSettings, errors));

            if (errors.Count > 0)
            {
                ErrorMessage = string.Join(Environment.NewLine, errors);
            }

            RecomputeStatistics();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IReadOnlyList<CallRecord> LoadRecordsSafely(ServiceNumberSettings settings, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(settings.PdfFolderPath))
        {
            errors.Add($"{settings.Name}: Es ist noch kein PDF-Ordner konfiguriert (siehe Einstellungen).");
            return Array.Empty<CallRecord>();
        }

        try
        {
            var result = _repository.GetCallRecords(settings);

            foreach (var warning in result.Warnings)
            {
                errors.Add($"{settings.Name} – {warning.FileName}: {warning.Message}");
            }

            return result.Records;
        }
        catch (PdfImportException ex)
        {
            _logger.LogError(ex, "Anrufdaten für {ServiceNumber} konnten nicht geladen werden.", settings.Name);
            errors.Add($"{settings.Name}: {ex.Message}");
            return Array.Empty<CallRecord>();
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

    [RelayCommand]
    private void ToggleFrequencyChartMode()
    {
        FrequencyChartMode = FrequencyChartMode == FrequencyChartMode.Bar
            ? FrequencyChartMode.Line
            : FrequencyChartMode.Bar;

        var combined = BuildCombinedStatistics(BuildCurrentRange());
        FrequencySeries = ChartFactory.CreateHourlyFrequencySeries(combined.HourlyDistribution, FrequencyChartMode == FrequencyChartMode.Line);
    }

    /// <summary>
    /// Berechnet den aktuell gewählten Zeitraum aus <see cref="SelectedPreset"/> bzw.
    /// den benutzerdefinierten Datumsangaben.
    /// </summary>
    private DateRangeFilter BuildCurrentRange() => SelectedPreset switch
    {
        DateRangePreset.Today => DateRangeFilter.Today(),
        DateRangePreset.ThisWeek => DateRangeFilter.ThisWeek(),
        DateRangePreset.ThisMonth => DateRangeFilter.ThisMonth(),
        DateRangePreset.Custom => DateRangeFilter.CustomRange(
            DateOnly.FromDateTime(CustomRangeStart ?? DateTime.Today),
            DateOnly.FromDateTime(CustomRangeEnd ?? DateTime.Today)),
        _ => DateRangeFilter.ThisMonth()
    };

    private ServiceNumberStatistics BuildCombinedStatistics(DateRangeFilter range)
    {
        var allRecords = _firstRecords.Concat(_secondRecords).ToList();
        return _statisticsService.Compute("combined", "Gesamt", allRecords, range);
    }

    /// <summary>
    /// Berechnet alle Kennzahlen und Diagrammdaten für den aktuell gewählten Zeitraum
    /// neu, ohne die Anrufdaten erneut von der Festplatte zu lesen.
    /// </summary>
    private void RecomputeStatistics()
    {
        var range = BuildCurrentRange();

        var firstStats = _statisticsService.Compute(_firstSettings.Id, _firstSettings.Name, _firstRecords, range);
        var secondStats = _statisticsService.Compute(_secondSettings.Id, _secondSettings.Name, _secondRecords, range);
        var combined = BuildCombinedStatistics(range);

        TotalCalls = combined.TotalCalls;
        AverageCallsPerDay = Math.Round(combined.AverageCallsPerDay, 1);
        AverageAnswerRatePercent = Math.Round(combined.AverageAnswerRatePercent, 1);
        AnsweredCalls = combined.AnsweredCalls;
        AnsweredPercent = Math.Round(combined.AnsweredPercent, 1);
        MissedCalls = combined.MissedCalls;
        MissedPercent = Math.Round(combined.MissedPercent, 1);
        BestDayText = combined.BestDay is null ? "–" : $"{combined.BestDay.Date:dd.MM.yyyy} ({combined.BestDay.TotalCalls} Anrufe)";
        WorstDayText = combined.WorstDay is null ? "–" : $"{combined.WorstDay.Date:dd.MM.yyyy} ({combined.WorstDay.TotalCalls} Anrufe)";

        TrendSeries = ChartFactory.CreateDailyTrendSeries(combined.DailyCounts);
        TrendXAxes = ChartFactory.CreateDailyTrendXAxes(combined.DailyCounts);
        FrequencySeries = ChartFactory.CreateHourlyFrequencySeries(combined.HourlyDistribution, FrequencyChartMode == FrequencyChartMode.Line);
        FrequencyXAxes = ChartFactory.CreateHourlyXAxes();
        AnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(combined.AnsweredCalls, combined.MissedCalls);

        FirstSummary = new ServiceNumberSummary(firstStats.ServiceNumberName, firstStats.TotalCalls, firstStats.AnsweredCalls, firstStats.MissedCalls, Math.Round(firstStats.AnsweredPercent, 1));
        SecondSummary = new ServiceNumberSummary(secondStats.ServiceNumberName, secondStats.TotalCalls, secondStats.AnsweredCalls, secondStats.MissedCalls, Math.Round(secondStats.AnsweredPercent, 1));

        ComparisonTotalsSeries = ChartFactory.CreateComparisonColumnSeries(
            firstStats.ServiceNumberName, firstStats.TotalCalls,
            secondStats.ServiceNumberName, secondStats.TotalCalls,
            "Gesamtanrufe");

        ComparisonTrendSeries = ChartFactory.CreateComparisonTrendSeries(
            firstStats.ServiceNumberName, firstStats.DailyCounts,
            secondStats.ServiceNumberName, secondStats.DailyCounts);
    }
}
