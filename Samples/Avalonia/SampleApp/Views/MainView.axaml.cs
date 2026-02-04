using Avalonia;
using Avalonia.Controls;
using Mapsui.Extensions;
using Mapsui.Rendering.Skia;
using Mapsui.Widgets.InfoWidgets;
using System.IO;
using System.Linq;
using System.Reactive;
using BruTile;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Styles;
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
        
        // The PerformanceWidget is created as part of the map.
        var performanceWidget = MapControl.Map.Widgets.OfType<PerformanceWidget>().First();
        performanceWidget.Performance.IsActive = Mapsui.Widgets.ActiveMode.Yes; // The default in ActiveMode.OnlyInDebugMode which is usually the best option. This is just to show how to change it.
        performanceWidget.BackColor = Color.FromRgba(255, 255, 32, 32);
        performanceWidget.Opacity = 1;

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
