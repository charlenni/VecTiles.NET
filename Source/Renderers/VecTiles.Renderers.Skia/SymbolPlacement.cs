using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// A symbol processed by the placement pass. For line symbols the run holds
/// the recorded anchor placements per contour, so the drawing phase consumes
/// them without running the collision checks again.
/// </summary>
/// <param name="Symbol">The placed symbol.</param>
/// <param name="Run">The placement run for line symbols; null for point symbols.</param>
public sealed record PlacementEntry(ISymbol Symbol, LineSymbolPlacementRun? Run);

/// <summary>
/// Result of the placement pass over a set of tiles. The symbols are grouped
/// by style layer name; the dictionary preserves the ascending style order of
/// the layers, so drawing in dictionary order draws the layers from bottom
/// (first style layer) to top (last style layer).
/// </summary>
public sealed record PlacementResult
{
    /// <summary>
    /// Gets the placed symbols per style layer in ascending style order.
    /// </summary>
    public required Dictionary<string, List<PlacementEntry>> PlacedByLayer { get; init; }

    /// <summary>
    /// Gets the symbols that found no space (for debug borders only).
    /// </summary>
    public IReadOnlyList<ISymbol> Rejected { get; init; } = Array.Empty<ISymbol>();

    /// <summary>
    /// Gets the collision quadtree as it stood after the placement pass; it
    /// only grows during the pass (nothing is removed). Kept for tests and
    /// debugging of placement decisions.
    /// </summary>
    public Quadtree<ISymbol> Tree { get; init; } = new();

    /// <summary>
    /// True when at least one tile could not be fetched, so the placement may
    /// be missing symbols; the result should be recomputed when the tiles
    /// become available.
    /// </summary>
    public bool Incomplete { get; init; }

    /// <summary>
    /// An empty result without placed symbols.
    /// </summary>
    public static PlacementResult Empty() => new()
    {
        PlacedByLayer = new Dictionary<string, List<PlacementEntry>>(),
    };
}

/// <summary>
/// The placement pass: computes for all visible tiles which symbols are drawn
/// where, as a background task before the next drawing. The collision check
/// runs in the unrotated canonical screen space of the integer zoom level and
/// processes the style layers in reverse (highest layer first), so an
/// overlapping symbol of a higher style layer wins; the quadtree only grows
/// (a symbol that finds its space occupied is dropped, nothing is removed).
/// </summary>
public static class SymbolPlacementEngine
{
    // Half extent of the offscreen canvas used for the collision checks. The
    // placement runs in canonical screen space (real pixels divided by the
    // placement scale), so this covers every real-world screen size; the
    // canvas is only used for debug borders and keeps no rendered content.
    private const float TestingCanvasHalfExtent = 32768f;

    /// <summary>
    /// Computes the placement for the given tiles and zoom level.
    /// </summary>
    /// <param name="tiles">The rendered tiles visible on screen.</param>
    /// <param name="zoomLevel">The canonical integer zoom level.</param>
    /// <param name="worldToScreen">
    /// Converts world coordinates into the unrotated canonical placement space.
    /// </param>
    /// <param name="token">
    /// Cancels the pass; the exception is thrown between two symbols or
    /// layers, so a partially computed result is discarded as a whole.
    /// </param>
    /// <returns>The placement result for all tiles.</returns>
    public static PlacementResult Compute(IReadOnlyList<IRenderedTile> tiles, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, CancellationToken token = default)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(
            new SKRect(-TestingCanvasHalfExtent, -TestingCanvasHalfExtent,
                TestingCanvasHalfExtent, TestingCanvasHalfExtent));

        var tree = new Quadtree<ISymbol>();
        var context = new EvaluationContext(zoomLevel);

        var placedByLayer = new Dictionary<string, List<PlacementEntry>>();

        // The layers are placed top first (see below); the lists are collected
        // in this working dictionary and copied over afterwards in the
        // ascending style order, which the renderer needs for drawing.
        var placesTopFirst = new Dictionary<string, List<PlacementEntry>>();

        var rejected = new List<ISymbol>();

        // Only the first occurrence of a symbol can be placed, so track all
        // examined ids of this pass and discard following occurrences. Id 0
        // means "no id" (data sources without feature ids): such symbols are
        // always examined; duplicate placement is prevented by the collision
        // detection instead.
        var examinedIds = new HashSet<ulong>();

        // The tiles are processed in tile id order (zoom, x, y), independent
        // of the order they were fetched in: symbols with equal SortOrder and
        // Rank then always win their collision in the same tile order, so the
        // result does not change when the fetch order or the tile set of the
        // viewport changes.
        var orderedTiles = tiles.OrderBy(t => t.Tile.Id);

        // Merge the symbol layers of all tiles into a single dictionary.
        // Not every tile carries an entry for every style layer (styles
        // without data in the tile produce no entry), so the style file order
        // is taken from the StyleIndex of the entries and not from the first
        // occurrence across the tiles. The minzoom/maxzoom of the style layer
        // is kept alongside (every tile carries the same values).
        var layers = new Dictionary<string, ISymbolLayer>();
        var layerZooms = new Dictionary<string, (float MinZoom, float MaxZoom)>();
        var layerOrder = new Dictionary<string, int>();
        // First occurrence order (the position at which a layer name appeared
        // first while merging); used as tie breaker for entries without a
        // meaningful StyleIndex (later in the list = higher layer)
        var layerPositions = new List<string>();
        var seenNames = new HashSet<string>();

        foreach (var tile in orderedTiles)
        {
            foreach (var entry in tile.Layers)
            {
                if (entry is not RenderedSymbolLayer symbolEntry)
                {
                    continue;
                }

                if (!layers.TryGetValue(entry.Name, out var symbolLayer))
                {
                    symbolLayer = new SymbolLayer(symbolEntry.Symbols.Symbols, symbolEntry.Symbols.MinZoom, symbolEntry.Symbols.MaxZoom);
                    layers.Add(entry.Name, symbolLayer);
                    layerZooms.Add(entry.Name, (symbolEntry.MinZoom, symbolEntry.MaxZoom));
                    layerOrder.Add(entry.Name, symbolEntry.StyleIndex);
                }
                else
                {
                    symbolLayer.Symbols.AddRange(symbolEntry.Symbols.Symbols);
                }

                if (seenNames.Add(entry.Name))
                {
                    layerPositions.Add(entry.Name);
                }
            }
        }

        // The highest style layer wins: process the layers top first (highest
        // style index; equal indices order by list position, later entry
        // first) so its symbols occupy the quadtree first; symbols of lower
        // layers that collide are dropped and nothing is removed later
        var merged = layerPositions.Select((name, position) => (name, position));
        var topFirst = merged
            .OrderByDescending(pair => layerOrder[pair.name])
            .ThenByDescending(pair => pair.position)
            .Select(pair => pair.name)
            .ToList();

        foreach (var name in topFirst)
        {
            token.ThrowIfCancellationRequested();

            if (IsVisibleAtZoom(layerZooms[name], zoomLevel) == false)
            {
                // Invisible at this zoom level, but keep the layer order
                placesTopFirst.Add(name, new List<PlacementEntry>());
                continue;
            }

            // Names in the same style layer are ordered by SortOrder and then
            // by Rank; line symbols of the same feature split across tiles are
            // merged into one continuous symbol
            var symbols = LineSymbolMerger.MergeById(layers[name].Symbols
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Rank));

            var placements = new List<PlacementEntry>();

            foreach (var symbol in symbols)
            {
                token.ThrowIfCancellationRequested();

                if (symbol.Id != 0 && !examinedIds.Add(symbol.Id))
                {
                    continue;
                }

                if (symbol is PointSymbol pointSymbol)
                {
                    var status = PointSymbolRenderer.CheckForSpace(canvas, context, pointSymbol, tree,
                        worldToScreen, 0, showUnvalidBorders: false);

                    if (status == SymbolPlacementStatus.Success)
                    {
                        placedByLayerAdd(symbol, null);
                    }
                    else
                    {
                        rejected.Add(symbol);
                    }
                }
                else if (symbol is LineSymbol lineSymbol)
                {
                    var run = LineSymbolRenderer.Place(canvas, context, lineSymbol, tree, worldToScreen,
                        showUnvalidBorders: false);

                    if (run.HasPlacements)
                    {
                        placedByLayerAdd(symbol, run);
                    }
                    else
                    {
                        rejected.Add(symbol);
                    }
                }
            }

            placesTopFirst.Add(name, placements);

            void placedByLayerAdd(ISymbol symbol, LineSymbolPlacementRun? run)
            {
                if (symbol is PointSymbol point)
                {
                    // The check set the draw flags and screen envelopes;
                    // insert the collision boxes like the drawing pass did
                    if (point.IconSymbol is not null && point.DrawIcon)
                    {
                        tree.Insert(point.IconSymbol.ScreenEnvelope!, point.IconSymbol.Copy());
                    }

                    if (point.TextSymbol is not null && point.DrawText)
                    {
                        tree.Insert(point.TextSymbol.ScreenEnvelope!, point.TextSymbol.Copy());
                    }
                }

                placements.Add(new PlacementEntry(symbol, run));
            }
        }

        // Copy the placements into the result dictionary in the ascending
        // style order, so drawing in dictionary order draws the layers from
        // bottom (first style layer) to top (last style layer)
        foreach (var (name, _) in layerPositions
            .Select((name, position) => (name, position))
            .OrderBy(pair => layerOrder[pair.name])
            .ThenBy(pair => pair.position))
        {
            placedByLayer.Add(name, placesTopFirst[name]);
        }

        return new PlacementResult
        {
            PlacedByLayer = placedByLayer,
            Rejected = rejected,
            Tree = tree,
        };
    }

    /// <summary>
    /// Style layer visibility semantics (see Renderer.IsVisible): a layer is
    /// visible when the zoom lies within [minzoom, maxzoom); -1 marks an
    /// unset zoom value in the style file.
    /// </summary>
    public static bool IsVisibleAtZoom((float MinZoom, float MaxZoom) zooms, float zoom)
    {
        if (zooms.MinZoom > -1 && zooms.MinZoom > zoom)
        {
            return false;
        }

        if (zooms.MaxZoom > -1 && zooms.MaxZoom <= zoom)
        {
            return false;
        }

        return true;
    }
}
