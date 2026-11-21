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

/// <summary>
/// The main sample view: hosts the map control with a rendered tile layer
/// loaded from a style source, plus the debug layer, widgets and UI controls
/// for style visibility, rotation and feature info.
/// </summary>
public partial class MainView : UserControl
{
    private readonly string _path = "Files";
    private readonly TileGridDebugLayer _debugLayer;
    private readonly OMTRenderedTileSource _tileSource;
    private readonly RenderedTileLayer _tileLayer;

    /// <summary>
    /// Initializes the main view: adds the rendered tile layer and the tile
    /// grid debug layer to the map, creates the style checkboxes and the zoom
    /// widget and centers the map on the style's center/zoom.
    /// </summary>
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

        MapControl.PointerReleased += MapControl_PointerReleased;

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

        // Diagnostic ticker for the placement status line: shows the
        // placement result the renderer draws from (per zoom the counts of
        // the placed symbols per style layer), so a missing icon on the map
        // can be classified as "not placed" (count 0), "placed" (count > 0,
        // a drawing problem) or "stale frame" (no update at all)
        var placementTicker = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        placementTicker.Tick += (_, _) => UpdatePlacementStatus();
        placementTicker.Start();
    }

    private string _lastPlacementStatus = string.Empty;

    private void UpdatePlacementStatus()
    {
        var placement = _tileLayer.CurrentPlacement?.Placement;
        if (placement is null)
        {
            return;
        }

        var layers = string.Join(", ", placement.PlacedByLayer
            .Where(pair => pair.Value.Count > 0)
            .Select(pair => $"{pair.Key}:{pair.Value.Count}"));

        // Counts of the diagnostic symbol subclass: separates a placement
        // that rejected the symbol (placed 0, rejected > 0) from a drawing
        // problem (placed > 0, but the symbol is missing on the map)
        var placedCanoe = placement.PlacedByLayer
            .SelectMany(pair => pair.Value)
            .Count(entry => entry.Symbol is VecTiles.Common.Primitives.Symbol { Subclass: "canoe_polo" });
        var rejectedCanoe = placement.Rejected
            .OfType<VecTiles.Common.Primitives.Symbol>()
            .Count(symbol => symbol.Subclass == "canoe_polo");

        var status = $"z{placement.ZoomLevel} holdover={(placement.IsHoldover ? placement.SourceZoomLevel.ToString() : "-")} " +
            $"incomplete={placement.Incomplete} {layers} canoe:{placedCanoe}/{rejectedCanoe}";

        if (status == _lastPlacementStatus)
        {
            return;
        }

        _lastPlacementStatus = status;
        PlacementStatusText.Text = status;
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

    private async void MapControl_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left)
        {
            return;
        }

        var position = e.GetPosition(MapControl);

        // Over the standard Mapsui feature info interface: every layer that
        // implements ILayerFeatureInfo answers feature info requests with the
        // same contract, no cast to the concrete layer type is needed
        if (_tileLayer is not Mapsui.Layers.ILayerFeatureInfo featureInfoProvider)
        {
            HitTestText.Text = string.Empty;
            return;
        }

        var result = await featureInfoProvider.GetFeatureInfoAsync(
            MapControl.Map.Navigator.Viewport, new Mapsui.Manipulations.ScreenPosition(position.X, position.Y));

        var hits = result.SelectMany(entry => entry.Value).OfType<PlacedPointFeature>().ToList();

        HitTestText.Text = hits.Count == 0
            ? string.Empty
            : string.Join("\n", hits.Select(h =>
            {
                // The consumer decides what to show for features without a
                // name: here the source category as fallback display
                var label = !string.IsNullOrEmpty(h.Title) ? h.Title
                    : !string.IsNullOrEmpty(h.SourceClass) ? h.SourceClass
                    : h.SourceSubclass;
                return $"{label} (id {h.Id}) in layer '{h.StyleLayer}' at {Math.Round(h.Point.X, 1)}/{Math.Round(h.Point.Y, 1)}";
            }));
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
