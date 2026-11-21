using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// A point symbol (icon and/or label) of the last drawn placement that was
/// hit at a screen position, as a Mapsui feature. The <see cref="PointFeature.Point"/>
/// inherited position is the anchor of the symbol in map (world) coordinates.
/// <see cref="IconRectangle"/> and <see cref="TextRectangle"/> frame the drawn
/// icon/label in screen coordinates of the viewport of the request; they are
/// null when the part is not drawn.
/// </summary>
public sealed class PlacedPointFeature : PointFeature
{
    // Mapsui's BaseFeature.Id is get-only for feature consumers (the id is
    // normally assigned by the data source); the setter is private, so the
    // id of the placed symbol is written through the setter once
    private static readonly System.Reflection.PropertyInfo IdSetter =
        typeof(BaseFeature).GetProperty(nameof(Id), System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;

    public PlacedPointFeature(MPoint point, string styleLayer, PointSymbol symbol,
        string title, MRect? iconRectangle, MRect? textRectangle) : base(point)
    {
        IdSetter.SetValue(this, (long)symbol.Id);
        StyleLayer = styleLayer;
        Symbol = symbol;
        Title = title;
        SourceClass = symbol.SourceClass;
        SourceSubclass = symbol.SourceSubclass;
        IconRectangle = iconRectangle;
        TextRectangle = textRectangle;
    }

    /// <summary>
    /// The name of the style layer the symbol belongs to.
    /// </summary>
    public string StyleLayer { get; }

    /// <summary>
    /// The placed point symbol (icon and/or text). Carries the details of the
    /// source feature (Name, Class, Subclass, Tile).
    /// </summary>
    public PointSymbol Symbol { get; }

    /// <summary>
    /// The title of the symbol: the text of the label, or the name of the
    /// feature when the label carries no text. Stays empty for features
    /// without name; for a fallback display see <see cref="SourceClass"/> and
    /// <see cref="SourceSubclass"/>.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The class attribute of the source vector data (e.g. the "klasse" of
    /// basemap.de data) — independent of the OpenMapTiles schema keys. Lets
    /// the consumer decide what to show for features whose name is empty.
    /// </summary>
    public string SourceClass { get; }

    /// <summary>
    /// The subclass attribute of the source vector data (e.g. the "art" of
    /// basemap.de data); see <see cref="SourceClass"/>.
    /// </summary>
    public string SourceSubclass { get; }

    /// <summary>
    /// The drawn icon in screen coordinates, or null when no icon is drawn.
    /// </summary>
    public MRect? IconRectangle { get; }

    /// <summary>
    /// The drawn label in screen coordinates, or null when no text is drawn.
    /// </summary>
    public MRect? TextRectangle { get; }
}

/// <summary>
/// Hit test for placed point symbols of a placement result: it returns the
/// symbols whose icon or label envelope contains a given screen position,
/// ordered from the visually topmost symbol downwards. The envelopes are the
/// collision boxes of the placement pass, so a hit matches the area over
/// which the symbol keeps its space.
/// </summary>
public static class PlacedPointFeatureTester
{
    /// <summary>
    /// Returns the placed point symbols hit at the given screen position (in
    /// screen pixels of the current viewport), ordered from the visually
    /// topmost symbol downwards.
    /// </summary>
    /// <param name="placement">The placement the symbols were drawn from.</param>
    /// <param name="screenX">The x coordinate of the position in screen pixels.</param>
    /// <param name="screenY">The y coordinate of the position in screen pixels.</param>
    /// <param name="viewport">The current viewport of the map (including its rotation).</param>
    public static IReadOnlyList<PlacedPointFeature> GetPointFeaturesAt(PlacementResult? placement,
        double screenX, double screenY, Viewport viewport)
    {
        if (placement is null || placement.PlacedByLayer.Count == 0)
        {
            return Array.Empty<PlacedPointFeature>();
        }

        // The collision envelopes of the placement live in the unrotated
        // canonical placement space of the zoom the placement was computed
        // for (see RenderedTileLayerRenderer.Draw): the screen positions of
        // the unrotated viewport in pixels, divided by the scale towards that
        // zoom. The click position is mapped into the same space, so the
        // test stays correct for rotated viewports and between zoom steps.
        // On a holdover placement the envelopes were measured in the space of
        // the zoom the layout came from, not the target zoom.
        var frameZoom = placement.IsHoldover ? placement.SourceZoomLevel : placement.ZoomLevel;
        var frameResolution = frameZoom.ToResolution();
        if (frameResolution <= 0 || viewport.Resolution <= 0)
        {
            return Array.Empty<PlacedPointFeature>();
        }

        var placementScale = frameResolution / viewport.Resolution;

        var center = viewport.ScreenToWorld(viewport.Width / 2.0, viewport.Height / 2.0);
        var unrotated = new Viewport(center.X, center.Y, viewport.Resolution, 0, viewport.Width, viewport.Height);
        var world = viewport.ScreenToWorld(screenX, screenY);
        var clickScreen = unrotated.WorldToScreen(world.X, world.Y);
        var clickX = clickScreen.X / placementScale;
        var clickY = clickScreen.Y / placementScale;

        // The style expressions of the placement use the frame zoom
        var context = new EvaluationContext(frameZoom);
        var viewportRotation = viewport.Rotation;

        List<PlacedPointFeature>? hits = null;

        // The symbol lists are drawn bottom to top: walk them from the top,
        // so the symbols drawn last (visually on top) are reported first
        foreach (var (layerName, entries) in placement.PlacedByLayer.Reverse())
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                // Line symbols carry their labels along lines; those runs are
                // not covered by this hit test
                if (entries[i].Run is not null || entries[i].Symbol is not PointSymbol symbol)
                {
                    continue;
                }

                var anchorScreen = unrotated.WorldToScreen(symbol.Point.X, symbol.Point.Y);
                var dx = clickX - anchorScreen.X / placementScale;
                var dy = clickY - anchorScreen.Y / placementScale;

                var iconEnvelope = symbol.IconSymbol?.Envelope;
                var textEnvelope = TextPlacementEnvelope(symbol.TextSymbol, viewportRotation, context);

                var alignmentOfIcon = RotationAlignment(symbol.IconSymbol);
                var alignmentOfText = RotationAlignment(symbol.TextSymbol);

                var iconHit = symbol.DrawIcon && Hits(iconEnvelope, dx, dy, alignmentOfIcon, viewportRotation);
                var textHit = symbol.DrawText && Hits(textEnvelope, dx, dy, alignmentOfText, viewportRotation);

                if (!iconHit && !textHit)
                {
                    continue;
                }

                // Real screen position of the anchor for the returned rectangles
                var anchor = viewport.WorldToScreen(symbol.Point.X, symbol.Point.Y);

                var text = symbol.TextSymbol;
                var title = !string.IsNullOrEmpty(text?.Text) ? text!.Text : symbol.Name;

                (hits ??= new List<PlacedPointFeature>()).Add(new PlacedPointFeature(
                    new MPoint(symbol.Point.X, symbol.Point.Y), layerName, symbol, title,
                    iconHit ? ScreenRectangle(iconEnvelope, alignmentOfIcon, placementScale, viewportRotation, anchor.X, anchor.Y) : null,
                    textHit ? ScreenRectangle(textEnvelope, alignmentOfText, placementScale, viewportRotation, anchor.X, anchor.Y) : null));
            }
        }

        if (hits == null)
        {
            return Array.Empty<PlacedPointFeature>();
        }

        return hits;
    }

    private static MapAlignment RotationAlignment(IconPointSymbol? symbol) =>
        symbol?.RotationAlignment ?? MapAlignment.Map;

    private static MapAlignment RotationAlignment(TextPointSymbol? symbol) =>
        symbol?.RotationAlignment ?? MapAlignment.Map;

    /// <summary>
    /// The label envelope in the unrotated placement frame, relative to the
    /// anchor: the unrotated envelope of the style, turned by the rotation the
    /// label is drawn with and moved by its translate. Unlike the icon, the
    /// label envelope is stored unrotated and the translate is part of the
    /// drawn position, not of the envelope.
    /// </summary>
    private static Envelope? TextPlacementEnvelope(TextPointSymbol? text, double viewportRotation, EvaluationContext context)
    {
        if (text?.Envelope is null)
        {
            return null;
        }

        var envelope = text.Envelope.Copy();

        // The rotation the label is drawn with (see CalcRotation): Map
        // alignment turns with the map, so in the placement frame the label
        // is drawn at its style rotation; Viewport alignment keeps its
        // screen orientation, which in the placement frame appears rotated
        // back by the viewport rotation
        var rotation = text.RotationAlignment == MapAlignment.Map
            ? (double)text.Rotation
            : (double)text.Rotation - viewportRotation;

        if (Math.Abs(rotation % 360.0) > 1e-6)
        {
            envelope = RotatedAabbAroundOrigin(envelope, rotation);
        }

        var translate = text.Translate?.Invoke(context) ?? new Point(0, 0);
        if (translate.X != 0 || translate.Y != 0)
        {
            // Map-anchored translate follows the map (raw in the placement
            // frame); viewport-anchored translate is fixed to the screen and
            // appears rotated back
            if (text.TranslateAnchor?.Invoke(context) == MapAlignment.Viewport)
            {
                (translate.X, translate.Y) = Turn(translate.X, translate.Y, -viewportRotation);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }

    /// <summary>
    /// True when the position relative to the anchor (in placement pixels)
    /// lies inside the envelope of the part. Viewport-oriented symbols rotate
    /// with the screen: their envelope in the placement frame is only exact
    /// without map rotation, so for those the conservative square around the
    /// anchor (the fallback the placement uses) is checked too.
    /// </summary>
    private static bool Hits(Envelope? envelope, double dx, double dy, MapAlignment alignment, double viewportRotation)
    {
        if (envelope is null)
        {
            return false;
        }

        if (envelope.Contains(dx, dy))
        {
            return true;
        }

        return alignment == MapAlignment.Viewport && Math.Abs(viewportRotation % 360.0) > 1e-6 &&
            IsInSquare(envelope, dx, dy);
    }

    private static bool IsInSquare(Envelope envelope, double dx, double dy)
    {
        var halfSide = Math.Max(
            Math.Max(Math.Abs(envelope.MinX), Math.Abs(envelope.MaxX)),
            Math.Max(Math.Abs(envelope.MinY), Math.Abs(envelope.MaxY)));

        return Math.Abs(dx) <= halfSide && Math.Abs(dy) <= halfSide;
    }

    /// <summary>
    /// The drawn rectangle of an icon or label part in screen coordinates:
    /// the envelope in placement pixels, scaled into real screen pixels,
    /// turned with the map on a rotated viewport (map-aligned parts follow
    /// the map, viewport-aligned parts stay screen-oriented) and shifted to
    /// the anchor. A rectangle cannot carry a rotation, so the axis-aligned
    /// bounding box of the turned rectangle is returned.
    /// </summary>
    private static MRect? ScreenRectangle(Envelope? envelope, MapAlignment alignment,
        double placementScale, double viewportRotation, double anchorX, double anchorY)
    {
        if (envelope is null)
        {
            return null;
        }

        var minX = envelope.MinX * placementScale;
        var minY = envelope.MinY * placementScale;
        var maxX = envelope.MaxX * placementScale;
        var maxY = envelope.MaxY * placementScale;

        if (alignment == MapAlignment.Map && Math.Abs(viewportRotation % 360.0) > 1e-6)
        {
            // The map content turns by the viewport rotation around the
            // anchor: account for it when the rectangle is built
            var turned = Turn(minX, maxX, minY, maxY, viewportRotation);
            (minX, minY, maxX, maxY) = turned;
        }

        return new MRect(anchorX + minX, anchorY + minY,
            anchorX + maxX, anchorY + maxY);
    }

    /// <summary>
    /// Axis-aligned bounding box of the envelope rotated around the anchor at
    /// (0, 0). Screen space grows downwards, so a positive angle turns
    /// clockwise, like the canvas rotations of the drawing.
    /// </summary>
    private static Envelope RotatedAabbAroundOrigin(Envelope envelope, double degrees)
    {
        var (minX, minY, maxX, maxY) = Turn(envelope.MinX, envelope.MaxX, envelope.MinY, envelope.MaxY, degrees);
        return new Envelope(minX, maxX, minY, maxY);
    }

    /// <summary>
    /// Turns the four corners of the box given by [minX..maxX, minY..maxY]
    /// around (0, 0) and returns the bounding box of the turned corners.
    /// </summary>
    private static (double MinX, double MinY, double MaxX, double MaxY) Turn(
        double minX, double maxX, double minY, double maxY, double degrees)
    {
        var rad = degrees / 180.0 * Math.PI;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        var x1 = minX * cos - minY * sin;
        var y1 = minX * sin + minY * cos;
        var x2 = maxX * cos - minY * sin;
        var y2 = maxX * sin + minY * cos;
        var x3 = maxX * cos - maxY * sin;
        var y3 = maxX * sin + maxY * cos;
        var x4 = minX * cos - maxY * sin;
        var y4 = minX * sin + maxY * cos;

        return (Math.Min(Math.Min(x1, x2), Math.Min(x3, x4)),
                Math.Min(Math.Min(y1, y2), Math.Min(y3, y4)),
                Math.Max(Math.Max(x1, x2), Math.Max(x3, x4)),
                Math.Max(Math.Max(y1, y2), Math.Max(y3, y4)));
    }

    /// <summary>
    /// Turns the point around (0, 0) clockwise in screen space (y downwards).
    /// </summary>
    private static (double X, double Y) Turn(double x, double y, double degrees)
    {
        var rad = degrees / 180.0 * Math.PI;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        return (x * cos - y * sin, x * sin + y * cos);
    }
}
