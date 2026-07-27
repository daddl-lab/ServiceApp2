using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServiceApp.Desktop.ViewModels;
using ServiceApp.Desktop.Views;

namespace ServiceApp.Desktop;

/// <summary>
/// Avalonia-Anwendungsklasse. Verbindet den in <see cref="Program"/> aufgebauten
/// DI-Container mit dem Avalonia-Fensterlebenszyklus: sobald das UI-Framework bereit
/// ist, wird das Hauptfenster samt seinem ViewModel aus dem Container aufgelöst.
/// </summary>
public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Program.AppHost.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
