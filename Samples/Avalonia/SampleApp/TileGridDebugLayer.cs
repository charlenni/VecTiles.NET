using System.Collections.Generic;
using BruTile;
using BruTile.Predefined;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Styles;

namespace SampleApp;

public class TileGridDebugLayer : MemoryLayer
{
    private readonly ITileSchema _schema;

    /// <summary>
    /// Creates an automatic debug layer that renders tile boundaries and TileIndex values.
    /// </summary>
    /// <param name="schema">The ITileSchema to use (defaults to GlobalSphericalMercator for OSM/WMTS).</param>
    public TileGridDebugLayer(ITileSchema? schema = null) : base("TileGridDebugLayer")
    {
        _schema = schema ?? new GlobalSphericalMercator();

        // Default style: Red border without fill
        Style = new VectorStyle
        {
            Fill = null,
            Outline = new Pen(Color.Red, 2)
        };
    }

    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        if (box == null || resolution <= 0)
            yield break;

        // 1. Determine the matching zoom level for the current map resolution
        var level = BruTile.Utilities.GetNearestLevel(_schema.Resolutions, resolution);

        // 2. Convert the visible Mapsui viewport box into a BruTile extent
        var extent = new Extent(box.MinX, box.MinY, box.MaxX, box.MaxY);

        // 3. Query all visible tiles from the schema
        var tileInfos = _schema.GetTileInfos(extent, level);

        foreach (var tile in tileInfos)
        {
            // Create a rectangle feature from the tile's world bounds
            var rect = new MRect(tile.Extent.MinX, tile.Extent.MinY, tile.Extent.MaxX, tile.Extent.MaxY);
            var feature = new GeometryFeature(rect.ToPolygon());

            // Center a label displaying Level, Col, and Row inside the tile
            feature.Styles.Add(new LabelStyle
            {
                Text = $"{tile.Index.Level}/{tile.Index.Col}/{tile.Index.Row}",
                ForeColor = Color.Red,
                BackColor = new Brush(Color.FromArgb(190, 255, 255, 255)),
                Font = new Font { Size = 14, Bold = true },
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center
            });

            yield return feature;
        }
    }
}
