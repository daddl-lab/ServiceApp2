using Avalonia.Controls;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using ServiceApp.Desktop.ViewModels;

namespace ServiceApp.Desktop.Views;

public partial class TicketDashboardView : UserControl
{
    public TicketDashboardView()
    {
        InitializeComponent();
        CauseBreakdownPieChart.ChartPointPointerDown += OnCauseSlicePointerDown;
    }

    /// <summary>
    /// Öffnet die Drill-Down-Tabelle für die angeklickte Fehlerursache. Der Name des
    /// Kuchenstücks entspricht dem <c>Name</c> der zugehörigen Serie, siehe
    /// <see cref="Charts.TicketChartFactory.CreateCauseBreakdownPieSeries"/>.
    /// </summary>
    private void OnCauseSlicePointerDown(IChartView chart, ChartPoint? point)
    {
        var causeName = point?.Context.Series.Name;
        if (causeName is not null && DataContext is TicketDashboardViewModel viewModel)
        {
            viewModel.ShowDrillDown(causeName);
        }
    }
}
