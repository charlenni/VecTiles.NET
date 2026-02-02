using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;

namespace SampleApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public Map BoundMap { get; set; } = new Map();

    [ObservableProperty]
    private double rotation;
}
