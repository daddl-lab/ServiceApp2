using CommunityToolkit.Mvvm.ComponentModel;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// Ein einzelner Eintrag im Mehrfachauswahl-Filter "Typ" des Ticket-Dashboards. Bildet
/// einen in den Ticketdaten vorkommenden Wert der Spalte "Typ" auf eine an- oder
/// abwählbare Checkbox ab.
/// </summary>
public sealed partial class TypeFilterOption : ObservableObject
{
    /// <summary>Bezeichnung des Typs, wie sie in der Excel-Spalte "Typ" steht.</summary>
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public TypeFilterOption(string name, bool isSelected)
    {
        Name = name;
        _isSelected = isSelected;
    }
}
