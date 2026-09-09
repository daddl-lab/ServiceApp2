using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel des Hauptfensters. Verantwortlich einzig für die Navigation zwischen den
/// Hauptbereichen der Anwendung (Telefonberichts-Dashboard, Ticket-Dashboard und
/// Einstellungen) - die eigentliche Fachlogik jedes Bereichs liegt in
/// <see cref="DashboardViewModel"/>, <see cref="TicketDashboardViewModel"/> bzw.
/// <see cref="SettingsViewModel"/>.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public DashboardViewModel Dashboard { get; }

    public TicketDashboardViewModel TicketDashboard { get; }

    public FileSearchViewModel FileSearch { get; }

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(
        DashboardViewModel dashboard, TicketDashboardViewModel ticketDashboard, FileSearchViewModel fileSearch, SettingsViewModel settings)
    {
        Dashboard = dashboard;
        TicketDashboard = ticketDashboard;
        FileSearch = fileSearch;
        Settings = settings;
        _currentPage = dashboard;

        // Wenn Einstellungen gespeichert werden, sollen beide Dashboards automatisch
        // mit den neuen Pfaden neu geladen werden, ohne dass der Benutzer manuell
        // zwischen den Ansichten wechseln und aktualisieren muss.
        settings.SettingsSaved += async (_, _) => await dashboard.ReloadCommand.ExecuteAsync(null);
        settings.SettingsSaved += async (_, _) => await ticketDashboard.ReloadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowDashboard() => CurrentPage = Dashboard;

    [RelayCommand]
    private void ShowTicketDashboard() => CurrentPage = TicketDashboard;

    [RelayCommand]
    private void ShowFileSearch() => CurrentPage = FileSearch;

    [RelayCommand]
    private void ShowSettings() => CurrentPage = Settings;
}
