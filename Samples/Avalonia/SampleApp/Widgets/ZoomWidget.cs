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
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
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
