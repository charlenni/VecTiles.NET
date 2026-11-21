using System;
using Mapsui;
using Mapsui.Styles;
using Mapsui.Widgets.BoxWidgets;
using VecTiles.Controls.Mapsui.Extensions;
using HAlign = Mapsui.Widgets.HorizontalAlignment;
using VAlign = Mapsui.Widgets.VerticalAlignment;

namespace SampleApp.Widgets;

/// <summary>
/// Position of the ZoomWidget on the map control.
/// </summary>
public enum ZoomWidgetPosition
{
    /// <summary>Top-left corner of the map control.</summary>
    TopLeft,
    /// <summary>Center of the top edge.</summary>
    TopCenter,
    /// <summary>Top-right corner of the map control.</summary>
    TopRight,
    /// <summary>Middle of the left edge.</summary>
    MiddleLeft,
    /// <summary>Center of the map control.</summary>
    Center,
    /// <summary>Middle of the right edge.</summary>
    MiddleRight,
    /// <summary>Bottom-left corner of the map control.</summary>
    BottomLeft,
    /// <summary>Center of the bottom edge.</summary>
    BottomCenter,
    /// <summary>Bottom-right corner of the map control.</summary>
    BottomRight
}

/// <summary>
/// Widget that displays the current zoom level of the map with two decimal places.
/// Default position is top right. Use <see cref="ZoomPosition"/> to place it
/// in one of the four corners, in the middle of one of the four sides or in the center.
/// </summary>
public class ZoomWidget : TextBoxWidget
{
    private readonly Map _map;

    /// <summary>
    /// Creates a zoom widget that displays the current zoom level of the map
    /// and updates itself whenever the map's viewport changes.
    /// </summary>
    /// <param name="map">The map whose navigator's viewport is observed.</param>
    public ZoomWidget(Map map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));

        TextSize = 12;
        TextColor = Color.FromRgba(255, 255, 255, 255);
        BackColor = Color.FromRgba(0, 0, 0, 160);
        Margin = new MRect(0, 0, 8, 8);

        ZoomPosition = ZoomWidgetPosition.TopRight;

        _map.Navigator.ViewportChanged += (s, e) => UpdateText();
        UpdateText();
    }

    private ZoomWidgetPosition _zoomPosition = ZoomWidgetPosition.TopRight;

    /// <summary>
    /// Gets or sets the position of the widget on the map control. Setting
    /// this also updates the widget's horizontal and vertical alignment.
    /// </summary>
    public ZoomWidgetPosition ZoomPosition
    {
        get => _zoomPosition;
        set
        {
            _zoomPosition = value;
            (HorizontalAlignment, VerticalAlignment) = _zoomPosition switch
            {
                ZoomWidgetPosition.TopLeft => (HAlign.Left, VAlign.Top),
                ZoomWidgetPosition.TopCenter => (HAlign.Center, VAlign.Top),
                ZoomWidgetPosition.TopRight => (HAlign.Right, VAlign.Top),
                ZoomWidgetPosition.MiddleLeft => (HAlign.Left, VAlign.Center),
                ZoomWidgetPosition.Center => (HAlign.Center, VAlign.Center),
                ZoomWidgetPosition.MiddleRight => (HAlign.Right, VAlign.Center),
                ZoomWidgetPosition.BottomLeft => (HAlign.Left, VAlign.Bottom),
                ZoomWidgetPosition.BottomCenter => (HAlign.Center, VAlign.Bottom),
                ZoomWidgetPosition.BottomRight => (HAlign.Right, VAlign.Bottom),
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
    }

    private void UpdateText()
    {
        var resolution = _map.Navigator.Viewport.Resolution;
        if (double.IsNaN(resolution) || double.IsInfinity(resolution))
            return;

        Text = $"Zoom {resolution.ToZoomLevel():0.00}";
    }
}
