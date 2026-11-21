using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;

namespace VecTiles.Controls.Mapsui;

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
    /// <returns>The placement result for all tiles.</returns>
    public static PlacementResult Compute(IReadOnlyList<RenderedTileFeature> tiles, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen)
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

        // Merge the symbol layers of all tiles into a single dictionary.
        // Every tile lists its layers in ascending style order (see
        // IRenderedTile.Layers), so the merged dictionary iteration order is
        // the ascending style order as well.
        var layers = new Dictionary<string, ISymbolLayer>();

        foreach (var tile in tiles)
        {
            if (tile.RenderedTile is null)
            {
                continue;
            }

            foreach (var entry in tile.RenderedTile.Layers)
            {
                if (entry is not RenderedSymbolLayer symbolEntry)
                {
                    continue;
                }

                if (!layers.TryGetValue(entry.Name, out var symbolLayer))
                {
                    symbolLayer = new SymbolLayer(symbolEntry.Symbols.Symbols, symbolEntry.Symbols.MinZoom, symbolEntry.Symbols.MaxZoom);
                    layers.Add(entry.Name, symbolLayer);
                }
                else
                {
                    symbolLayer.Symbols.AddRange(symbolEntry.Symbols.Symbols);
                }
            }
        }

        // The highest style layer wins: process the layers in reverse (top
        // layer first) so its symbols occupy the quadtree first; symbols of
        // lower layers that collide are dropped and nothing is removed later.
        foreach (var layerEntry in layers.Reverse())
        {
            if (layerEntry.Value.MinZoom > zoomLevel || layerEntry.Value.MaxZoom < zoomLevel)
            {
                // Invisible at this zoom level, but keep the layer order
                placesTopFirst.Add(layerEntry.Key, new List<PlacementEntry>());
                continue;
            }

            // Names in the same style layer are ordered by SortOrder and then
            // by Rank; line symbols of the same feature split across tiles are
            // merged into one continuous symbol
            var symbols = LineSymbolMerger.MergeById(layerEntry.Value.Symbols
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Rank));

            var placements = new List<PlacementEntry>();

            foreach (var symbol in symbols)
            {
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

            placesTopFirst.Add(layerEntry.Key, placements);

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
        foreach (var layerEntry in layers)
        {
            placedByLayer.Add(layerEntry.Key, placesTopFirst[layerEntry.Key]);
        }

        return new PlacementResult
        {
            PlacedByLayer = placedByLayer,
            Rejected = rejected,
            Tree = tree,
        };
    }
}
