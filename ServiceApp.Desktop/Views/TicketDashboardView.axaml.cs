using Avalonia.Controls;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.ViewModels;

namespace ServiceApp.Desktop.Views;

public partial class TicketDashboardView : UserControl
{
    public TicketDashboardView()
    {
        InitializeComponent();
        BreakdownPieChart.ChartPointPointerDown += OnBreakdownSlicePointerDown;
        CustomerBreakdownDataGrid.SelectionChanged += OnCustomerRowSelectionChanged;
    }

    /// <summary>
    /// Öffnet die Drill-Down-Tabelle für das angeklickte Kuchenstück des gemeinsamen
    /// Fehlerursachen-/Störungsort-Diagramms. Der Name des Kuchenstücks entspricht
    /// dem <c>Name</c> der zugehörigen Serie, siehe
    /// <see cref="ViewModels.TicketDashboardViewModel.ShowCurrentBreakdownDrillDown"/>.
    /// </summary>
    private void OnBreakdownSlicePointerDown(IChartView chart, ChartPoint? point)
    {
        var sliceName = point?.Context.Series.Name;
        if (sliceName is not null && DataContext is TicketDashboardViewModel viewModel)
        {
            viewModel.ShowCurrentBreakdownDrillDown(sliceName);
        }
    }

    /// <summary>
    /// Öffnet die Drill-Down-Tabelle für die in der Kundenauswertungs-Tabelle
    /// angeklickte Zeile. Die Auswahl wird direkt im Anschluss wieder aufgehoben
    /// (statt über ein an <c>SelectedItem</c> gebundenes ViewModel-Property), damit
    /// ein erneuter Klick auf dieselbe, weiterhin sichtbare Zeile nach dem Schließen
    /// der Tabelle zuverlässig wieder ein Auswahl-Ereignis auslöst.
    /// </summary>
    private void OnCustomerRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CustomerBreakdownDataGrid.SelectedItem is TicketCustomerCount customer && DataContext is TicketDashboardViewModel viewModel)
        {
            viewModel.ShowCustomerDrillDown(customer);
            CustomerBreakdownDataGrid.SelectedItem = null;
        }
    }
}

