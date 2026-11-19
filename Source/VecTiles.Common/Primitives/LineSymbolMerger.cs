using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Merges <see cref="LineSymbol"/>s of the same feature that are split across
/// multiple tiles into a single continuous line, so that symbols (icons / text)
/// are placed uniformly along the whole feature instead of only along the
/// segment of the first tile that was examined.
/// </summary>
public static class LineSymbolMerger
{
    private static readonly GeometryFactory Factory = new();

    /// <summary>
    /// Replaces all groups of <see cref="LineSymbol"/>s sharing the same id
    /// with a single merged symbol carrying the combined geometry. Symbols of
    /// other types and LineSymbols without duplicates are passed through
    /// unchanged. The original ordering is preserved.
    /// </summary>
    /// <param name="symbols">Symbols coming from all visible tiles.</param>
    /// <returns>Symbols with merged line geometries.</returns>
    public static List<ISymbol> MergeById(IEnumerable<ISymbol> symbols)
    {
        var result = new List<ISymbol>();
        var groups = new Dictionary<ulong, List<LineSymbol>>();

        foreach (var symbol in symbols)
        {
            if (symbol is not LineSymbol line || line.Id == 0)
            {
                // Id 0 means "no id" (data sources without feature ids, e.g.
                // basemap.de). Grouping everything without an id together
                // would merge unrelated features carrying the text of the
                // first one, so pass them through unchanged.
                result.Add(symbol);
                continue;
            }

            if (!groups.TryGetValue(line.Id, out var group))
            {
                group = new List<LineSymbol>();
                groups[line.Id] = group;
                // Placeholder, may be replaced by the merged symbol below
                result.Add(line);
            }

            group.Add(line);
        }

        for (var i = 0; i < result.Count; i++)
        {
            if (result[i] is LineSymbol line &&
                groups.TryGetValue(line.Id, out var group) &&
                group.Count > 1 &&
                Merge(group) is { } merged)
            {
                result[i] = merged;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges several <see cref="LineSymbol"/>s of the same feature into one
    /// symbol with a continuous geometry. Each segment is first clipped to its
    /// tile borders to remove the buffer overhang that vector tiles keep
    /// beyond the tile edge, so adjacent segments meet exactly at the shared
    /// border.
    /// </summary>
    /// <param name="symbols">Symbols of one feature, coming from different tiles.</param>
    /// <returns>Merged symbol, or null if no usable geometry remains.</returns>
    public static LineSymbol? Merge(IReadOnlyList<LineSymbol> symbols)
    {
        if (symbols.Count == 1)
        {
            return symbols[0];
        }

        // Two world pixels: covers quantization differences of the border
        // crossing points between neighbouring tiles
        var tolerance = 2 * (symbols[0].Tile.Right - symbols[0].Tile.Left) / 512.0;

        var segments = new List<List<Coordinate>>();

        foreach (var symbol in symbols)
        {
            foreach (var part in ClipToTile(symbol))
            {
                segments.Add(new List<Coordinate>(part.Coordinates));
            }
        }

        // Chain in a deterministic order: the chained geometry (and with it the
        // anchor positions of the labels) must not depend on the order in which
        // the tiles were enumerated
        segments.Sort(CompareSegments);

        var chains = Chain(segments, tolerance);

        chains.Sort(CompareSegments);

        Geometry geometry = chains.Count == 1
            ? new LineString(chains[0].ToArray())
            : new MultiLineString(chains
                .Select(c => new LineString(c.ToArray()))
                .ToArray());

        var first = symbols[0];

        return new LineSymbol(first.Tile, first.Id, geometry, first.IconSymbol, first.TextSymbol)
        {
            Name = first.Name,
            SortOrder = first.SortOrder,
            Class = first.Class,
            Subclass = first.Subclass,
            Rank = first.Rank,
            AllowOthers = first.AllowOthers,
            Spacing = first.Spacing,
        };
    }

    /// <summary>
    /// Clips the geometry of a symbol to the exact bounds of its tile,
    /// removing the buffer overhang beyond the tile border.
    /// </summary>
    private static IEnumerable<LineString> ClipToTile(LineSymbol symbol)
    {
        var tile = symbol.Tile;
        var envelope = new Envelope(tile.Left, tile.Right, tile.Bottom, tile.Top);

        var clipped = symbol.Geometry.Intersection(Factory.ToGeometry(envelope));

        return ExtractLineStrings(clipped)
            .Where(l => l.Coordinates.Length >= 2 && l.Length > 0);
    }

    /// <summary>
    /// Recursively collects all line strings of a (multi-)geometry.
    /// </summary>
    private static IEnumerable<LineString> ExtractLineStrings(Geometry geometry)
    {
        return geometry.OgcGeometryType switch
        {
            OgcGeometryType.LineString => [(LineString)geometry],
            OgcGeometryType.MultiLineString => ((MultiLineString)geometry).Geometries.OfType<LineString>(),
            OgcGeometryType.GeometryCollection => ((GeometryCollection)geometry).Geometries
                .SelectMany(ExtractLineStrings),
            _ => [],
        };
    }

    /// <summary>
    /// Chains segments that touch at their endpoints (within a tolerance)
    /// into maximal continuous polylines. Unconnected segments stay separate.
    /// </summary>
    private static List<List<Coordinate>> Chain(List<List<Coordinate>> segments, double tolerance)
    {
        var pool = new LinkedList<List<Coordinate>>(segments);
        var chains = new List<List<Coordinate>>();

        while (pool.Count > 0)
        {
            var chain = pool.First!.Value;
            pool.RemoveFirst();

            var extended = true;
            while (extended)
            {
                extended = false;

                var node = pool.First;
                while (node != null)
                {
                    var next = node.Next;
                    var segment = node.Value;

                    if (Touch(chain[^1], segment[0], tolerance))
                    {
                        Append(chain, segment, reversed: false);
                        pool.Remove(node);
                        extended = true;
                    }
                    else if (Touch(chain[^1], segment[^1], tolerance))
                    {
                        Append(chain, segment, reversed: true);
                        pool.Remove(node);
                        extended = true;
                    }
                    else if (Touch(chain[0], segment[^1], tolerance))
                    {
                        Prepend(chain, segment, reversed: false);
                        pool.Remove(node);
                        extended = true;
                    }
                    else if (Touch(chain[0], segment[0], tolerance))
                    {
                        Prepend(chain, segment, reversed: true);
                        pool.Remove(node);
                        extended = true;
                    }

                    node = next;
                }
            }

            chains.Add(chain);
        }

        return chains.Where(c => c.Count >= 2).ToList();
    }

    private static bool Touch(Coordinate a, Coordinate b, double tolerance) => a.Distance(b) <= tolerance;

    /// <summary>
    /// Compares two polylines by their first coordinate (and the last one on
    /// equality), so that chaining and geometry order are deterministic.
    /// </summary>
    private static int CompareSegments(List<Coordinate> a, List<Coordinate> b)
    {
        var compare = FirstCompare(a[0], b[0]);

        return compare != 0 ? compare : FirstCompare(a[^1], b[^1]);
    }

    private static int FirstCompare(Coordinate a, Coordinate b)
    {
        var compare = a.X.CompareTo(b.X);

        return compare != 0 ? compare : a.Y.CompareTo(b.Y);
    }

    /// <summary>
    /// Adds a segment to the end of a chain, skipping the duplicated joint point.
    /// </summary>
    private static void Append(List<Coordinate> chain, List<Coordinate> segment, bool reversed)
    {
        if (reversed)
        {
            segment.Reverse();
        }

        chain.AddRange(segment.Skip(1));
    }

    /// <summary>
    /// Adds a segment to the start of a chain, skipping the duplicated joint point.
    /// </summary>
    private static void Prepend(List<Coordinate> chain, List<Coordinate> segment, bool reversed)
    {
        if (reversed)
        {
            segment.Reverse();
        }

        chain.InsertRange(0, segment.Take(segment.Count - 1));
    }
}
