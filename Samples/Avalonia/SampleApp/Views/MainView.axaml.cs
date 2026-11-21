using System;
using System.IO;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Mapsui;
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
    private readonly TileGridDebugLayer _debugLayer;
    private readonly OMTRenderedTileSource _tileSource;
    private readonly RenderedTileLayer _tileLayer;

    public MainView()
    {
        InitializeComponent();

        var styleSource = $"file://{_path}/osm-liberty.json";
        //var styleSource = "https://sgx.geodatenzentrum.de/gdz_basemapde_vektor/styles/bm_web_col.json";

        _tileSource = new OMTRenderedTileSource(styleSource);

        _tileLayer = new RenderedTileLayer(_tileSource);
        MapControl.Map.Layers.Add(_tileLayer);

        _debugLayer = new TileGridDebugLayer(_tileLayer.TileSource.Schema)
        {
            Enabled = CheckBoxDebugLayer.IsChecked ?? false
        };
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

        // Restrict the zooming to the zoom range of the tile schema (Mapsui's
        // default zoom bounds allow one level beyond the biggest resolution)
        MapControl.Map.Navigator.OverrideZoomBounds = _tileLayer.ZoomBounds;

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

        var (centerPoint, zoom) = GetCenterPoint();

        MapControl.Map.Navigator.CenterOnAndZoomTo(centerPoint, zoom.ToResolution());
    }

    private void ButtonCenter_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var (centerPoint, zoom) = GetCenterPoint();

        MapControl.Map.Navigator.CenterOnAndZoomTo(centerPoint, zoom.ToResolution());
    }

    private void CheckBoxDebugLayer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _debugLayer.Enabled = CheckBoxDebugLayer.IsChecked ?? false;

        MapControl.Map.RefreshGraphics();
    }


    private void CheckBoxValid_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tileLayer.ShowValidBorders = CheckBoxValidBorder.IsChecked ?? false;

        MapControl.Map.RefreshGraphics();
    }

    private void StyleCheckBox_Click(OMTLayerStyle style, CheckBox checkBox)
    {
        style.Layout.Visibility = checkBox.IsChecked == true ? "visible" : "none";

        // Styles are baked into the rendered tiles, so all caches have to be cleared, before redrawing the map
        _tileSource.ClearCache();
        _tileLayer.ClearCache();

        MapControl.Map.RefreshData(Mapsui.ChangeType.Discrete);
    }
    
    private (MPoint centerPoint, float zoom) GetCenterPoint()
    {
        // Center and zoom the map on the center/zoom of the style file, if they are set
        var center = _tileSource.Center;
        var mPoint = center.Length >= 2
            ? Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(center[0], center[1]))
            : Mapsui.Projections.SphericalMercator.FromLonLat(new Mapsui.MPoint(8.5417, 47.3769));
        var f = _tileSource.Zoom ?? 17.9f;
        return (mPoint, f);
    }
}
