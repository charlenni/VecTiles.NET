using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;

namespace SampleApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    /// <summary>Gets or sets the map that is displayed and manipulated by the bound map control.</summary>
    public Map BoundMap { get; set; } = new Map();

    [ObservableProperty]
    private double rotation;
}
