using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel des Hauptfensters. Verantwortlich einzig für die Navigation zwischen den
/// beiden Hauptbereichen der Anwendung (Dashboard und Einstellungen) - die eigentliche
/// Fachlogik jedes Bereichs liegt in <see cref="DashboardViewModel"/> bzw.
/// <see cref="SettingsViewModel"/>.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public DashboardViewModel Dashboard { get; }

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(DashboardViewModel dashboard, SettingsViewModel settings)
    {
        Dashboard = dashboard;
        Settings = settings;
        _currentPage = dashboard;

        // Wenn Einstellungen gespeichert werden, sollen die Dashboard-Daten automatisch
        // mit den neuen Ordnerpfaden neu geladen werden, ohne dass der Benutzer manuell
        // zwischen den Ansichten wechseln und aktualisieren muss.
        settings.SettingsSaved += async (_, _) => await dashboard.ReloadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowDashboard() => CurrentPage = Dashboard;

    [RelayCommand]
    private void ShowSettings() => CurrentPage = Settings;
}
