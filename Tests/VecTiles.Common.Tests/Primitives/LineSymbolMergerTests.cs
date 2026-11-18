using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using Xunit;

namespace VecTiles.Common.Tests.Primitives;

public class LineSymbolMergerTests
{
    [Fact]
    public void MergeById_MergesSegmentsOfSameFeature()
    {
        var (tileA, tileB, y) = AdjacentTiles();
        var segments = new List<ISymbol>
        {
            CreateLineSymbol(tileA, 1, (tileA.Left - 1000, y), (tileA.Right + 1000, y)),
            UnrelatedSymbol(tileA),
            CreateLineSymbol(tileB, 1, (tileB.Left - 1000, y), (tileB.Right + 1000, y)),
        };

        var result = LineSymbolMerger.MergeById(segments);

        Assert.Equal(2, result.Count);
        Assert.IsType<PointSymbol>(result[1]);

        var merged = Assert.IsType<LineSymbol>(result[0]);
        var line = Assert.IsType<LineString>(merged.Geometry);

        // Buffer overhang of both segments is clipped at the shared border,
        // the remaining parts form one continuous line
        Assert.Equal(3, line.Coordinates.Length);
        Assert.Equal(tileA.Left, line.Coordinates[0].X, 5);
        Assert.Equal(tileB.Right, line.Coordinates[2].X, 5);
        Assert.Equal(y, line.Coordinates[1].Y, 5);
    }

    [Fact]
    public void MergeById_KeepsOrderAndOtherSymbols()
    {
        var (tileA, tileB, y) = AdjacentTiles();

        var other = UnrelatedSymbol(tileA);
        var first = CreateLineSymbol(tileA, 42, (tileA.Left, y), (tileA.Right + 1000, y));
        var second = CreateLineSymbol(tileB, 42, (tileB.Left - 1000, y), (tileB.Right, y));

        var result = LineSymbolMerger.MergeById(new ISymbol[] { first, other, second });

        Assert.Equal(2, result.Count);
        Assert.Equal(42ul, Assert.IsType<LineSymbol>(result[0]).Id);
        Assert.Same(other, result[1]);
    }

    [Fact]
    public void MergeById_PassesThroughSingleLineSymbolsUnchanged()
    {
        var (tileA, _, y) = AdjacentTiles();
        var symbol = CreateLineSymbol(tileA, 7, (tileA.Left, y), (tileA.Right, y));

        var result = LineSymbolMerger.MergeById(new ISymbol[] { symbol });

        var line = Assert.Single(result);
        Assert.Same(symbol, Assert.IsType<LineSymbol>(line));
    }

    [Fact]
    public void Merge_UnconnectedSegmentsBecomeMultiLineString()
    {
        var (tileA, tileB, y) = AdjacentTiles();

        var first = CreateLineSymbol(tileA, 5, (tileA.Left + 1000, y), (tileA.Right - 10000, y));
        var second = CreateLineSymbol(tileB, 5, (tileB.Left + 10000, y), (tileB.Right - 1000, y));

        var merged = LineSymbolMerger.Merge(new[] { first, second });

        Assert.NotNull(merged);
        var multi = Assert.IsType<MultiLineString>(merged!.Geometry);
        Assert.Equal(2, multi.NumGeometries);
    }

    [Fact]
    public void Merge_ClipsBufferOverhangAtTileBorders()
    {
        var (tileA, tileB, y) = AdjacentTiles();
        const double buffer = 1000;

        var first = CreateLineSymbol(tileA, 9,
            (tileA.Left - buffer, y), (tileA.Left + buffer, y), (tileA.Right + buffer, y));
        var second = CreateLineSymbol(tileB, 9,
            (tileB.Left - buffer, y), (tileB.Left + buffer, y), (tileB.Right - buffer, y));

        var merged = LineSymbolMerger.Merge(new[] { first, second });

        Assert.NotNull(merged);
        var line = Assert.IsType<LineString>(merged!.Geometry);

        // No point may lie outside the two tiles; the buffer points beyond the
        // borders are removed, interior vertices are kept, the segments are
        // joined at the shared border without duplicated joint points
        Assert.Equal(5, line.Coordinates.Length);
        Assert.All(line.Coordinates, c => Assert.InRange(c.X, tileA.Left, tileB.Right));
    }

    [Fact]
    public void Merge_CopiesSymbolPropertiesFromFirstSymbol()
    {
        var (tileA, tileB, y) = AdjacentTiles();

        var first = CreateLineSymbol(tileA, 3, (tileA.Left, y), (tileA.Right, y));
        var second = CreateLineSymbol(tileB, 3, (tileB.Left, y), tileB.Right - 500, y);

        var merged = LineSymbolMerger.Merge(new[] { first, second });

        Assert.NotNull(merged);
        Assert.Equal(first.Id, merged!.Id);
        Assert.Equal(first.Tile, merged.Tile);
        Assert.Equal(first.Spacing, merged.Spacing);
        Assert.Equal(first.IconSymbol, merged.IconSymbol);
        Assert.Equal(first.TextSymbol, merged.TextSymbol);
    }

    private static (Tile first, Tile second, double y) AdjacentTiles()
    {
        var tileA = new Tile(1, 1, 6);
        var tileB = new Tile(2, 1, 6);
        var y = (tileA.Bottom + tileA.Top) / 2;
        return (tileA, tileB, y);
    }

    private static LineSymbol CreateLineSymbol(Tile tile, ulong id, params (double x, double y)[] points)
    {
        var geometry = new LineString(points.Select(p => new Coordinate(p.x, p.y)).ToArray());
        return new LineSymbol(tile, id, geometry, null, null);
    }

    private static LineSymbol CreateLineSymbol(Tile tile, ulong id, (double x, double y) start, double endX, double endY)
    {
        return CreateLineSymbol(tile, id, start, (endX, endY));
    }

    private static ISymbol UnrelatedSymbol(Tile tile)
    {
        return new PointSymbol(tile, 99, new Point(tile.Left + 100, tile.Bottom + 100), null, null);
    }
}
