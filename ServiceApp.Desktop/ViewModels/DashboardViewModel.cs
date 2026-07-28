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

    // Zuletzt berechnete Statistiken für den aktuellen Zeitraum, zwischengespeichert
    // damit ein Wechsel der Diagramm-Auswahl (<see cref="ChartViewMode"/>) oder der
    // Balken/Linie-Darstellung die Kennzahlen nicht neu berechnen muss.
    private ServiceNumberStatistics? _firstStats;
    private ServiceNumberStatistics? _secondStats;
    private ServiceNumberStatistics? _combinedStats;

    [ObservableProperty]
    private DateRangePreset _selectedPreset = DateRangePreset.ThisMonth;

    [ObservableProperty]
    private DateTime? _customRangeStart = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? _customRangeEnd = DateTime.Today;

    [ObservableProperty]
    private FrequencyChartMode _frequencyChartMode = FrequencyChartMode.Bar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChartViewSeparate))]
    [NotifyPropertyChangedFor(nameof(IsChartViewSingle))]
    private ChartViewMode _chartViewMode = ChartViewMode.Combined;

    /// <summary>Ob aktuell beide Servicenummern als getrennte Serien dargestellt werden.</summary>
    public bool IsChartViewSeparate => ChartViewMode == ChartViewMode.Separate;

    /// <summary>Ob aktuell eine einzelne Serie (Summe, Nr. 1 oder Nr. 2 allein) dargestellt wird.</summary>
    public bool IsChartViewSingle => !IsChartViewSeparate;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Kennzahlen - folgen wie die Diagramme der aktuellen ChartViewMode (Summe,
    // Servicenummer 1 oder 2 allein; bei "Beide einzeln" wird die Gesamtstatistik
    // angezeigt, da einzelne Kennzahlenfelder keine zwei Werte gleichzeitig fassen).
    [ObservableProperty] private int _totalCalls;
    [ObservableProperty] private double _averageCallsPerDay;
    [ObservableProperty] private string _bestDayText = "–";
    [ObservableProperty] private string _worstDayText = "–";
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
    [ObservableProperty] private ISeries[] _firstAnswerPieSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _secondAnswerPieSeries = Array.Empty<ISeries>();

    // Vergleich beider Servicenummern (Textkarten, unabhängig von ChartViewMode -
    // zeigen immer beide Nummern nebeneinander)
    [ObservableProperty] private ServiceNumberSummary _firstSummary = ServiceNumberSummary.Empty("Servicenummer 1");
    [ObservableProperty] private ServiceNumberSummary _secondSummary = ServiceNumberSummary.Empty("Servicenummer 2");

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

        UpdateDisplayedStatistics();
    }

    [RelayCommand]
    private void SelectCombinedChartView()
    {
        ChartViewMode = ChartViewMode.Combined;
        UpdateDisplayedStatistics();
    }

    [RelayCommand]
    private void SelectSeparateChartView()
    {
        ChartViewMode = ChartViewMode.Separate;
        UpdateDisplayedStatistics();
    }

    [RelayCommand]
    private void SelectFirstOnlyChartView()
    {
        ChartViewMode = ChartViewMode.FirstOnly;
        UpdateDisplayedStatistics();
    }

    [RelayCommand]
    private void SelectSecondOnlyChartView()
    {
        ChartViewMode = ChartViewMode.SecondOnly;
        UpdateDisplayedStatistics();
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

        _firstStats = firstStats;
        _secondStats = secondStats;
        _combinedStats = combined;

        FirstSummary = new ServiceNumberSummary(firstStats.ServiceNumberName, firstStats.TotalCalls, firstStats.AnsweredCalls, firstStats.MissedCalls, Math.Round(firstStats.AnsweredPercent, 1));
        SecondSummary = new ServiceNumberSummary(secondStats.ServiceNumberName, secondStats.TotalCalls, secondStats.AnsweredCalls, secondStats.MissedCalls, Math.Round(secondStats.AnsweredPercent, 1));

        UpdateDisplayedStatistics();
    }

    /// <summary>
    /// Baut die Kennzahlen-Felder und die Serien der drei Hauptdiagramme (Zeitlicher
    /// Verlauf, Häufigkeit, Angenommen/Verpasst) anhand der zwischengespeicherten
    /// Statistiken und der aktuell gewählten <see cref="ChartViewMode"/> neu auf. Wird
    /// sowohl nach einer vollständigen Neuberechnung als auch bei einem reinen
    /// Anzeige-Wechsel (Auswahl-Buttons, Balken/Linie-Umschalter) aufgerufen, ohne
    /// dass dafür die zugrunde liegenden Kennzahlen neu berechnet werden müssen.
    /// </summary>
    private void UpdateDisplayedStatistics()
    {
        if (_firstStats is null || _secondStats is null || _combinedStats is null)
        {
            return;
        }

        // Bei "Beide einzeln" gibt es keine sinnvolle einzelne Kennzahl - die
        // Kennzahlen-Karten zeigen dann weiterhin die Gesamtstatistik, während die
        // Diagramme beide Nummern getrennt darstellen.
        var kpiStats = ChartViewMode switch
        {
            ChartViewMode.FirstOnly => _firstStats,
            ChartViewMode.SecondOnly => _secondStats,
            _ => _combinedStats
        };

        TotalCalls = kpiStats.TotalCalls;
        AverageCallsPerDay = Math.Round(kpiStats.AverageCallsPerDay, 1);
        AnsweredCalls = kpiStats.AnsweredCalls;
        AnsweredPercent = Math.Round(kpiStats.AnsweredPercent, 1);
        MissedCalls = kpiStats.MissedCalls;
        MissedPercent = Math.Round(kpiStats.MissedPercent, 1);
        BestDayText = kpiStats.BestDay is null ? "–" : $"{kpiStats.BestDay.Date:dd.MM.yyyy} ({kpiStats.BestDay.TotalCalls} Anrufe)";
        WorstDayText = kpiStats.WorstDay is null ? "–" : $"{kpiStats.WorstDay.Date:dd.MM.yyyy} ({kpiStats.WorstDay.TotalCalls} Anrufe)";

        TrendXAxes = ChartFactory.CreateDailyTrendXAxes(_combinedStats.DailyCounts);
        FrequencyXAxes = ChartFactory.CreateHourlyXAxes();
        var asLineChart = FrequencyChartMode == FrequencyChartMode.Line;

        switch (ChartViewMode)
        {
            case ChartViewMode.Separate:
                TrendSeries = ChartFactory.CreateComparisonTrendSeries(
                    _firstStats.ServiceNumberName, _firstStats.DailyCounts,
                    _secondStats.ServiceNumberName, _secondStats.DailyCounts);
                FrequencySeries = ChartFactory.CreateHourlyFrequencyComparisonSeries(
                    _firstStats.ServiceNumberName, _firstStats.HourlyDistribution,
                    _secondStats.ServiceNumberName, _secondStats.HourlyDistribution,
                    asLineChart);
                FirstAnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(_firstStats.AnsweredCalls, _firstStats.MissedCalls);
                SecondAnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(_secondStats.AnsweredCalls, _secondStats.MissedCalls);
                break;

            case ChartViewMode.FirstOnly:
                TrendSeries = ChartFactory.CreateDailyTrendSeries(_firstStats.DailyCounts);
                FrequencySeries = ChartFactory.CreateHourlyFrequencySeries(_firstStats.HourlyDistribution, asLineChart);
                AnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(_firstStats.AnsweredCalls, _firstStats.MissedCalls);
                break;

            case ChartViewMode.SecondOnly:
                TrendSeries = ChartFactory.CreateDailyTrendSeries(_secondStats.DailyCounts);
                FrequencySeries = ChartFactory.CreateHourlyFrequencySeries(_secondStats.HourlyDistribution, asLineChart);
                AnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(_secondStats.AnsweredCalls, _secondStats.MissedCalls);
                break;

            case ChartViewMode.Combined:
            default:
                TrendSeries = ChartFactory.CreateDailyTrendSeries(_combinedStats.DailyCounts);
                FrequencySeries = ChartFactory.CreateHourlyFrequencySeries(_combinedStats.HourlyDistribution, asLineChart);
                AnswerPieSeries = ChartFactory.CreateAnsweredVsMissedPieSeries(_combinedStats.AnsweredCalls, _combinedStats.MissedCalls);
                break;
        }
    }
}
