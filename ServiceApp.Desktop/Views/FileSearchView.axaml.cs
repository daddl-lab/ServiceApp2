using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.ViewModels;

namespace ServiceApp.Desktop.Views;

public partial class FileSearchView : UserControl
{
    public FileSearchView()
    {
        InitializeComponent();
    }

    private FileSearchViewModel? ViewModel => DataContext as FileSearchViewModel;

    private FileArchiveEntry? SelectedResult => ResultsGrid.SelectedItem as FileArchiveEntry;

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel is { } viewModel && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
        }
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedResult is { } entry && ViewModel is { } viewModel)
        {
            viewModel.OpenFileCommand.Execute(entry);
        }
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedResult is { } entry && ViewModel is { } viewModel)
        {
            viewModel.OpenFileCommand.Execute(entry);
        }
    }

    private void OnOpenContainingFolderClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedResult is { } entry && ViewModel is { } viewModel)
        {
            viewModel.OpenContainingFolderCommand.Execute(entry);
        }
    }

    private async void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedResult is not { } entry)
        {
            return;
        }

        // Der Zugriff auf die Zwischenablage hängt am TopLevel-Fenster und lässt sich nicht
        // sauber ins ViewModel injizieren, ohne dessen Avalonia-Unabhängigkeit aufzugeben -
        // bewusste, kleine Ausnahme vom MVVM-Muster nur für diesen Codebehind-Handler.
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(entry.FullPath);
        }
    }
}
