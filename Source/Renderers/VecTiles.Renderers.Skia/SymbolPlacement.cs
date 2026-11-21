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
    /// Gets the canonical integer zoom level the placement was computed for.
    /// A result of another (previous) zoom level that is still handed out
    /// while the placement of the new zoom runs is only a holdover: the
    /// drawing evaluates the style sizes at the current zoom, and the line
    /// runs are rescaled into the current placement space
    /// (<see cref="Rescaled"/>).
    /// </summary>
    public int ZoomLevel { get; init; }

    /// <summary>
    /// Gets the zoom level whose canonical space the layout measurements of
    /// the entries (glyph spacing, along-path run distances) were made in.
    /// Normally this is <see cref="ZoomLevel"/>; only on a holdover copy
    /// (<see cref="Rescaled"/>) the two differ: the copy reports the target
    /// zoom as its <see cref="ZoomLevel"/>, but the layout was measured in
    /// the space of the source zoom, so the drawing must scale it with the
    /// font size ratio towards that zoom.
    /// </summary>
    public int SourceZoomLevel { get; init; }

    /// <summary>
    /// True when this result is the holdover of the previous zoom (see
    /// <see cref="ZoomLevel"/>): the drawing fades it out and the finished
    /// placement of the current zoom in, so no label set pops between the
    /// two states.
    /// </summary>
    public bool IsHoldover { get; init; }

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

    /// <summary>
    /// Returns a shallow copy whose line runs are rescaled from the
    /// placement space of this result's zoom level into the placement space
    /// of another zoom level. The along-path distances of the runs (anchor
    /// position, draw start, path length) are proportional to
    /// 1 / resolution(zoom), so they scale by 2^(sourceZoom - targetZoom);
    /// shared symbols and point entries are carried over unchanged (point
    /// anchors are world coordinates and the style sizes are evaluated
    /// again at drawing time). Used when a placement of the previous zoom
    /// keeps the labels on the screen while the placement of the new zoom
    /// runs. The copy carries <paramref name="targetZoom"/> as its
    /// <see cref="ZoomLevel"/> and this result's zoom level as its
    /// <see cref="SourceZoomLevel"/> (the zoom the layout was measured in).
    /// </summary>
    /// <param name="targetZoom">The canonical zoom level to rescale into.</param>
    public PlacementResult Rescaled(int targetZoom)
    {
        var factor = Math.Pow(2, ZoomLevel - targetZoom);
        var rescaled = new Dictionary<string, List<PlacementEntry>>(PlacedByLayer.Count);

        foreach (var (name, entries) in PlacedByLayer)
        {
            var list = new List<PlacementEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.Run is not { } run)
                {
                    list.Add(entry);
                    continue;
                }

                list.Add(entry with { Run = RescaleRun(run, factor) });
            }

            rescaled.Add(name, list);
        }

        return this with
        {
            PlacedByLayer = rescaled,
            ZoomLevel = targetZoom,
            // The layout measurements of the entries stay in the canonical
            // space of this (source) zoom; the drawing reads them for the
            // font size ratio
            SourceZoomLevel = ZoomLevel,
        };
    }

    private static LineSymbolPlacementRun RescaleRun(LineSymbolPlacementRun run, double factor)
    {
        var contours = new List<IReadOnlyList<LineSymbolAnchorPlacement>>(run.Contours.Count);
        foreach (var contour in run.Contours)
        {
            var anchors = new List<LineSymbolAnchorPlacement>(contour.Count);
            foreach (var anchor in contour)
            {
                var placement = anchor.Placement is null
                    ? null
                    : anchor.Placement with
                    {
                        DrawStart = (float)(anchor.Placement.DrawStart * factor),
                        PathLength = (float)(anchor.Placement.PathLength * factor),
                    };
                anchors.Add(new LineSymbolAnchorPlacement((float)(anchor.Position * factor),
                    anchor.Icon, anchor.Text, placement));
            }

            contours.Add(anchors);
        }

        return new LineSymbolPlacementRun(contours);
    }
}

/// <summary>
/// The persistent placement decisions of one zoom level across passes. The
/// collision tree only grows (a space once occupied stays occupied) and
/// every examined symbol keeps its decision (placed entry or rejection), so
/// labels never change their position when the tile set of the viewport
/// changes — the placement is attached to the feature (its id) or, without
/// a feature id, to the symbol instance of the (cached) tile, not to the
/// current tile set.
/// </summary>
public sealed class IncrementalPlacementState
{
    /// <summary>
    /// The maximum number of decisions kept per zoom level; beyond that the
    /// state is reset (labels are placed again, a rare event after very
    /// long panning sessions).
    /// </summary>
    public const int MaxDecisions = 100_000;

    /// <summary>
    /// The collision boxes of every placed symbol of this zoom level; only
    /// grows. New symbols are placed against it, so the result of a pass
    /// depends no longer on the tile set of the current viewport.
    /// </summary>
    internal Quadtree<ISymbol> Tree { get; private set; } = new();

    // Decisions by feature id (value null = symbol found no space and is
    // skipped in the later passes as well); for symbols without feature id
    // by the symbol instance (stable while the tile is in the tile cache)
    private readonly Dictionary<ulong, PlacementEntry?> _decidedById = new();
    private readonly Dictionary<ISymbol, PlacementEntry?> _decidedByReference = new();

    /// <summary>
    /// Gets the decision for the given symbol: true when the symbol was
    /// examined in an earlier pass (as placed entry, or null when rejected).
    /// </summary>
    internal bool TryGetDecision(ISymbol symbol, out PlacementEntry? entry)
    {
        if (symbol.Id != 0)
        {
            return _decidedById.TryGetValue(symbol.Id, out entry);
        }

        return _decidedByReference.TryGetValue(symbol, out entry);
    }

    /// <summary>
    /// Records the decision for the symbol: the placed entry, or null when
    /// it found no space (the rejection also stands: the space the other
    /// symbols took is not given back).
    /// </summary>
    internal void Decide(ISymbol symbol, PlacementEntry? entry)
    {
        if (symbol.Id != 0)
        {
            _decidedById[symbol.Id] = entry;
        }
        else
        {
            _decidedByReference[symbol] = entry;
        }
    }

    /// <summary>
    /// Resets the state when the maximum number of decisions is reached, at
    /// the beginning of the next pass (labels are placed again, a rare event
    /// after very long panning sessions; the memory stays bounded).
    /// </summary>
    internal void ResetIfFull()
    {
        if (_decidedById.Count + _decidedByReference.Count < MaxDecisions)
        {
            return;
        }

        _decidedById.Clear();
        _decidedByReference.Clear();
        Tree = new Quadtree<ISymbol>();
    }
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
    /// Gets the placement for the given tiles and zoom level.
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
        Func<double, double, (double, double)> worldToScreen, CancellationToken token = default,
        IncrementalPlacementState? incremental = null)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(
            new SKRect(-TestingCanvasHalfExtent, -TestingCanvasHalfExtent,
                TestingCanvasHalfExtent, TestingCanvasHalfExtent));

        // With a persistent state the collision tree survives the pass: every
        // envelope ever placed keeps its space, so the placement decisions of
        // earlier passes remain valid and labels never move when the tile set
        // of the viewport changes
        // Very long panning session at one zoom level: start the next pass
        // with a clean state, so the memory stays bounded. The reset happens
        // BEFORE the tree is taken: it replaces the tree instance, so the
        // pass works against an empty collision tree
        incremental?.ResetIfFull();

        var tree = incremental?.Tree ?? new Quadtree<ISymbol>();
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

        // With a persistent state every decision is one per pass; see the
        // first occurrence rule in the loop below
        var seenSymbols = incremental is null ? null : new HashSet<ISymbol>();

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

                if (incremental is not null &&
                    !seenSymbols!.Add(symbol))
                {
                    // A later occurrence of the same feature: decided or
                    // rejected already in this very pass
                    continue;
                }

                if (symbol.Id != 0 && !examinedIds.Add(symbol.Id))
                {
                    continue;
                }

                // A persistent state keeps every decision of an earlier pass:
                // the symbol is neither placed again nor re-examined, its
                // placed (or rejected) outcome stands — the label stays at
                // the position of the pass that placed it, also when tiles
                // appear or disappear
                if (incremental is not null &&
                    incremental.TryGetDecision(symbol, out var decided))
                {
                    if (decided is not null)
                    {
                        placements.Add(decided);
                    }

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
                        incremental?.Decide(symbol, null);
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
                        incremental?.Decide(symbol, null);
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
                incremental?.Decide(symbol, placements[^1]);
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
            ZoomLevel = zoomLevel,
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
