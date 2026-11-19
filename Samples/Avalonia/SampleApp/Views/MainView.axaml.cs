using System.IO;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Mapsui.Extensions;
using Mapsui.Rendering.Skia;
using Mapsui.Styles;
using Mapsui.Widgets.InfoWidgets;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Styles.OpenMapTiles;
using SampleApp.Widgets;

namespace SampleApp.Views;

public partial class MainView : UserControl
{
    private readonly string _path = "Files";
    private readonly RenderedSymbolsLayer _symbolsLayer;
    private readonly TileGridDebugLayer _debugLayer;
    private readonly OMTRenderedTileSource _tileSource;
    private readonly RenderedTileLayer _tileLayer;

    public MainView()
    {
        InitializeComponent();

        MapRenderer.RegisterStyleRenderer(typeof(RenderedTileStyle), new RenderedTileStyleRenderer());

        var stream = File.Open(Path.Combine(_path, "bm_web_col.json"), FileMode.Open, FileAccess.Read);

        _tileSource = new OMTRenderedTileSource(stream);
        _tileLayer = new RenderedTileLayer(_tileSource);
        _symbolsLayer = new RenderedSymbolsLayer(_tileSource);
        _debugLayer = new TileGridDebugLayer(_tileLayer.TileSource.Schema)
        {
            Enabled = CheckBoxDebugLayer.IsChecked ?? false
        };

        //MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        MapControl.Map.Layers.Add(_tileLayer);
        MapControl.Map.Layers.Add(_symbolsLayer);
        MapControl.Map.Layers.Add(_debugLayer);

        // The PerformanceWidget is created as part of the map.
        var performanceWidget = MapControl.Map.Widgets.OfType<PerformanceWidget>().First();
        performanceWidget.Performance.IsActive = Mapsui.Widgets.ActiveMode.Yes; // The default in ActiveMode.OnlyInDebugMode which is usually the best option. This is just to show how to change it.
        performanceWidget.BackColor = Color.FromRgba(255, 255, 32, 32);
        performanceWidget.Opacity = 1;

        MapControl.Map.Widgets.Add(new MouseCoordinatesWidget());

        MapControl.Map.Widgets.Add(new ZoomWidget(MapControl.Map)
        {
            // Change ZoomPosition to place the widget: TopLeft, TopCenter, TopRight,
            // MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight
            ZoomPosition = ZoomWidgetPosition.TopRight
        });

        MapControl.Map.Navigator.RotationLock = false;

        // Create a checkbox for each style of the style file. Initial state is the visibility from the style file.
        foreach (var style in _tileSource.Styles)
        {
            var checkBox = new CheckBox
            {
                Content = style.Name,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                IsChecked = style.Visible
            };
            checkBox.Click += (_, _) => StyleCheckBox_Click(style, checkBox);
            StylesPanel.Children.Add(checkBox);
        }

        var rotationSliderObservable = RotationSlider.GetObservable(Slider.ValueProperty);
        rotationSliderObservable.Subscribe(new AnonymousObserver<double>(value => 
        {
            MapControl.Map.Navigator.RotateTo(value);
        }));

        // Center and zoom the map on the center/zoom of the style file, if they are set
        var center = _tileSource.Center;
        var centerPoint = center.Length >= 2
            ? Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(center[0], center[1]))
            : Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(8.5417, 47.3769));
        var zoom = _tileSource.Zoom ?? 17.9f;

        MapControl.Map.Navigator.CenterOnAndZoomTo(centerPoint, zoom.ToResolution());
    }

    private void ButtonCenter_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MapControl.Map.Navigator.CenterOnAndZoomTo(Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(8.5417, 47.3769)), 14.ToResolution());
    }

    private void CheckBoxDebugLayer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _debugLayer.Enabled = CheckBoxDebugLayer.IsChecked ?? false;

        MapControl.Map.RefreshGraphics();
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

    private void StyleCheckBox_Click(OMTLayerStyle style, CheckBox checkBox)
    {
        style.Layout.Visibility = checkBox.IsChecked == true ? "visible" : "none";

        // Styles are baked into the rendered tiles, so all caches have to be cleared, before redrawing the map
        _tileSource.ClearCache();
        _tileLayer.ClearCache();
        _symbolsLayer.ClearCache();

        MapControl.Map.RefreshData(Mapsui.ChangeType.Discrete);
    }
}
