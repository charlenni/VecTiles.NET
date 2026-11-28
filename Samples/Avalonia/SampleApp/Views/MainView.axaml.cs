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
using VecTiles.Styles.MapLibre;
using SampleApp.Widgets;
using VecTiles.Converters.Mapsforge;
using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common.Interfaces;

namespace SampleApp.Views;

/// <summary>
/// The main sample view: hosts the map control with a rendered tile layer
/// loaded from a style source, plus the debug layer, widgets and UI controls
/// for style visibility, rotation and feature info.
/// </summary>
public partial class MainView : UserControl
{
    private readonly string _pathOMT = "Files/OpenMapTiles";
    private readonly string _pathMF = "Files/Mapsforge";
    private readonly string _pathPMT = "Files/Protomaps";
    private readonly string _pathVT = "Files/VersaTiles";
    private readonly string _pathMLT = "Files/MapLibreTiles";
    private readonly TileGridDebugLayer _debugLayer;
    private readonly IRenderedTileSource _tileSource;
    private readonly RenderedTileLayer _tileLayer;

    /// <summary>
    /// Initializes the main view: adds the rendered tile layer and the tile
    /// grid debug layer to the map, creates the style checkboxes and the zoom
    /// widget and centers the map on the style's center/zoom.
    /// </summary>
    public MainView()
    {
        InitializeComponent();

        //var styleSource = $"file://{_pathOMT}/osm-liberty.json";
        //var styleSource = $"file://{_pathOMT}/osm-liberty-for-mapsforge.json";
        //var styleSource = $"file://{_pathPMT}/protomaps-light-style.json";
        //var styleSource = $"file://{_pathVT}/versatiles-en-style.json";
        var styleSource = $"file://{_pathMLT}/MapLibreTiles-style.json";
        //var styleSource = $"https://api.protomaps.com/styles/v5/light/en.json?key=YOUR_API_KEY";
        //var styleSource = "https://sgx.geodatenzentrum.de/gdz_basemapde_vektor/styles/bm_web_col.json";

        _tileSource = new MLRenderedTileSource(styleSource);
        //_tileSource = new MFRenderedTileSource($"file://{_pathMF}/zuerich.map", new IdentityTagsConverter("mapsforge"), $"file://{_pathMF}/mapsforge/default.xml");
        
        // The style file references the fonts of a glyph server by their
        // fontstack names ("noto_sans_regular"); the name resolver maps them
        // to the installed "Noto Sans" family, whose weights are parsed from
        // the returned names
        _tileLayer = new RenderedTileLayer(_tileSource, fontNameResolver: f => f switch
        {
            "noto_sans_regular" => "Noto Sans Regular",
            "noto_sans_bold" => "Noto Sans Bold",
            _ => f
        }, fontSourceResolver: (f) => f switch
        {
            "Open Sans Semibold" => "https://github.com/googlefonts/opensans/raw/refs/heads/main/fonts/noto-set/ttf/OpenSans-SemiBold.ttf",
            _ => null
        });
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

        if (_tileSource is MLRenderedTileSource renderedTileSource)
        {
            // Create a checkbox for each style of the style file. Initial state is the visibility from the style file.
            foreach (var style in renderedTileSource.Styles)
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

    private void StyleCheckBox_Click(ILayerStyle style, CheckBox checkBox)
    {
        if (style is not LayerStyle layerStyle)
        {
            return;
        }
        
        layerStyle.Layout.Visibility = checkBox.IsChecked == true ? "visible" : "none";

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
