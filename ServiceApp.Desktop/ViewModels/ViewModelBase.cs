using CommunityToolkit.Mvvm.ComponentModel;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// Gemeinsame Basisklasse aller ViewModels. Stellt über
/// <see cref="ObservableObject"/> Änderungsbenachrichtigungen (INotifyPropertyChanged)
/// bereit, die Avalonias Bindungssystem benötigt, um Oberflächenänderungen automatisch
/// nachzuziehen.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
