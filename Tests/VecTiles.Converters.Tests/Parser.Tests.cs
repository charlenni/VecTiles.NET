using NetTopologySuite.Geometries;
using ProtoBuf;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles.Pbf;
using OmtParser = VecTiles.Converters.OpenMapTiles.Parser.Parser;

namespace VecTiles.Converters.Tests;

/// <summary>
/// Unit tests for <see cref="OmtParser.Parse" />, focused on the layer extent handling.
/// </summary>
public class ParserTests
{
    /// <summary>
    /// Encodes a value as signed zigzag varint as used in MVT geometry commands.
    /// </summary>
    private static uint ZigZag(long value) => (uint)((value << 1) ^ (value >> 31));

    /// <summary>
    /// Serializes a pbf tile to its binary MVT representation.
    /// </summary>
    private static byte[] Serialize(PbfTile pbfTile)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, pbfTile);
        return stream.ToArray();
    }

    /// <summary>
    /// Builds a tile with a single point feature at the given layer coordinates and extent.
    /// </summary>
    private static PbfTile BuildPointTile(uint extent, long x, long y)
    {
        var layer = new PbfLayer { Name = "test", Version = 2, Extent = extent };
        layer.Features.Add(new PbfFeature
        {
            Type = PbfGeomType.Point,

            // MoveTo (command 1, count 1) followed by zigzag encoded deltas
            Geometry = { 9, ZigZag(x), ZigZag(y) },
        });

        var tile = new PbfTile();
        tile.Layers.Add(layer);
        return tile;
    }

    /// <summary>
    /// A point in a standard 4096 extent layer is scaled to the 512 tile space.
    /// </summary>
    [Fact]
    public void ParsesPointWithDefaultExtent()
    {
        var tile = BuildPointTile(4096, 2048, 2048);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(0, 0, 14), new Tile(0, 0, 14));

        var point = (Point)vectorTile.Layers[0].Features[0].Geometry;
        Assert.Equal(256, point.Coordinate.X, 6);
        Assert.Equal(256, point.Coordinate.Y, 6);
    }

    /// <summary>
    /// A point in a layer with a smaller extent is scaled with the layer's own extent.
    /// </summary>
    [Fact]
    public void ParsesPointWithSmallerExtent()
    {
        var tile = BuildPointTile(2048, 1024, 1024);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(0, 0, 14), new Tile(0, 0, 14));

        var point = (Point)vectorTile.Layers[0].Features[0].Geometry;
        Assert.Equal(256, point.Coordinate.X, 6);
        Assert.Equal(256, point.Coordinate.Y, 6);
    }

    /// <summary>
    /// A point in a layer with a larger extent is scaled with the layer's own extent.
    /// </summary>
    [Fact]
    public void ParsesPointWithLargerExtent()
    {
        var tile = BuildPointTile(65536, 32768, 49152);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(0, 0, 14), new Tile(0, 0, 14));

        var point = (Point)vectorTile.Layers[0].Features[0].Geometry;
        Assert.Equal(256, point.Coordinate.X, 6);
        Assert.Equal(384, point.Coordinate.Y, 6);
    }

    /// <summary>
    /// Each layer of a tile is scaled with its own extent, even when adjacent layers differ.
    /// </summary>
    [Fact]
    public void ParsesLayersWithDifferentExtents()
    {
        var firstLayer = new PbfLayer { Name = "small", Version = 2, Extent = 1024 };
        firstLayer.Features.Add(new PbfFeature { Type = PbfGeomType.Point, Geometry = { 9, ZigZag(512), ZigZag(512) } });

        var secondLayer = new PbfLayer { Name = "large", Version = 2, Extent = 8192 };
        secondLayer.Features.Add(new PbfFeature
        {
            Type = PbfGeomType.Point,

            // ZigZag(4096) = 8192
            Geometry = { 9, 8192, 8192 },
        });

        var tile = new PbfTile();
        tile.Layers.Add(firstLayer);
        tile.Layers.Add(secondLayer);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(0, 0, 14), new Tile(0, 0, 14));

        var firstPoint = (Point)vectorTile.Layers[0].Features[0].Geometry;
        var secondPoint = (Point)vectorTile.Layers[1].Features[0].Geometry;
        Assert.Equal(256, firstPoint.Coordinate.X, 6);
        Assert.Equal(256, firstPoint.Coordinate.Y, 6);
        Assert.Equal(256, secondPoint.Coordinate.X, 6);
        Assert.Equal(256, secondPoint.Coordinate.Y, 6);
    }

    /// <summary>
    /// An invalid (zero) extent falls back to the default extent of 4096.
    /// </summary>
    [Fact]
    public void FallsBackToDefaultExtentForZeroExtent()
    {
        var tile = BuildPointTile(0, 2048, 2048);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(0, 0, 14), new Tile(0, 0, 14));

        var point = (Point)vectorTile.Layers[0].Features[0].Geometry;
        Assert.Equal(256, point.Coordinate.X, 6);
        Assert.Equal(256, point.Coordinate.Y, 6);
    }

    /// <summary>
    /// Overzoom: a point in a provided parent tile with a non-default extent is
    /// correctly transformed into the requested child tile.
    /// </summary>
    [Fact]
    public void ParsesPointWithSmallerExtentAndOverzoom()
    {
        // Top-left quarter (child 0,0) of the provided tile
        var tile = BuildPointTile(2048, 512, 512);
        var vectorTile = OmtParser.Parse(Serialize(tile), new Tile(4, 2, 15), new Tile(2, 1, 14));

        var point = (Point)vectorTile.Layers[0].Features[0].Geometry;

        // 512/2048 * 512 = 128 in the provided tile's 512-space, times overzoom scale 2
        Assert.Equal(256, point.Coordinate.X, 6);
        Assert.Equal(256, point.Coordinate.Y, 6);
    }
}
