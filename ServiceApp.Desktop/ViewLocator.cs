using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ServiceApp.Desktop.ViewModels;

namespace ServiceApp.Desktop;

/// <summary>
/// Verbindet ViewModels automatisch mit ihrer passenden View anhand der Namenskonvention
/// <c>*ViewModel</c> → <c>*View</c> (z. B. <see cref="DashboardViewModel"/> →
/// <c>DashboardView</c>). Das ist das übliche Avalonia-MVVM-Muster, um in XAML nur das
/// ViewModel binden zu müssen (siehe <c>DataTemplates</c> in <c>App.axaml</c>), ohne
/// jede View-ViewModel-Zuordnung manuell zu pflegen.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "(kein ViewModel)" };
        }

        var viewModelName = data.GetType().FullName!;
        var viewName = viewModelName.Replace("ViewModel", "View", StringComparison.Ordinal);
        var viewType = Type.GetType(viewName);

        if (viewType is not null)
        {
            return (Control)Activator.CreateInstance(viewType)!;
        }

        return new TextBlock { Text = $"View für \"{viewName}\" nicht gefunden." };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
