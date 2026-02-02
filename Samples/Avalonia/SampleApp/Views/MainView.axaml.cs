using Avalonia;
using Avalonia.Controls;
using Mapsui.Extensions;
using Mapsui.Rendering.Skia;
using Mapsui.Widgets.InfoWidgets;
using System.IO;
using System.Reactive;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;

namespace SampleApp.Views;

public partial class MainView : UserControl
{
    private readonly string _path = "files";
    private readonly RenderedSymbolsLayer _symbolsLayer;

    public MainView()
    {
        InitializeComponent();

        MapRenderer.RegisterStyleRenderer(typeof(RenderedTileStyle), new RenderedTileStyleRenderer());

        var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

        var tileSource = new OMTRenderedTileSource(stream);
        var tileLayer = new RenderedTileLayer(tileSource);
        _symbolsLayer = new RenderedSymbolsLayer(tileSource);

        //MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        MapControl.Map.Layers.Add(tileLayer);
        MapControl.Map.Layers.Add(_symbolsLayer);

        MapControl.Map.Widgets.Add(new MouseCoordinatesWidget());

        MapControl.Map.Navigator.RotationLock = false;

        var rotationSliderObservable = RotationSlider.GetObservable(Slider.ValueProperty);
        rotationSliderObservable.Subscribe(new AnonymousObserver<double>(value => 
        {
            MapControl.Map.Navigator.RotateTo(value);
        }));

        MapControl.Map.Navigator.CenterOnAndZoomTo(Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(8.5417, 47.3769)), 2.ToResolution());
    }

    private void ButtonCenter_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MapControl.Map.Navigator.CenterOnAndZoomTo(Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(8.5417, 47.3769)), 14.ToResolution());
    }

    private void CheckBoxValid_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _symbolsLayer.ShowValidBorders = CheckBoxValidBorder.IsChecked ?? false;

        MapControl.Map.RefreshGraphics();
    }

    private void CheckBoxInvalid_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _symbolsLayer.ShowInvalidBorders = CheckBoxInvalidBorder.IsChecked ?? false;

        MapControl.Map.RefreshGraphics();
    }
}
