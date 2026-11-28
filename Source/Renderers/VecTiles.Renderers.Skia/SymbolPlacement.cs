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
/// Sets the placement semantics of a placement pass and restores the previous
/// one when disposed. Using the declaration form, the disposal keeps the
/// semantics bound to the <see cref="SymbolPlacementEngine.Compute"/> call
/// (which runs single-threaded on one thread).
/// </summary>
internal readonly struct PlacementSemanticsScope : IDisposable
{
    public PlacementSemanticsScope(Func<ISymbol, int>? displayClassAccessor)
    {
        Previous = SymbolPlacementEngine.MapsforgeDisplayClassAccessor;
        SymbolPlacementEngine.MapsforgeDisplayClassAccessor = displayClassAccessor;
    }

    private Func<ISymbol, int>? Previous { get; }

    public void Dispose()
    {
        SymbolPlacementEngine.MapsforgeDisplayClassAccessor = Previous;
    }
}

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
    /// True when the placement was computed with the mapsforge semantics (a
    /// Mapsforge render theme): the drawing then lifts the line-symbol icons
    /// (e.g. the oneway arrows) out of the per-style-layer draw order — like
    /// the Mapsforge renderer they are drawn with the ways, below the placed
    /// symbols and labels of every style layer, even when their rule comes
    /// after the caption rules of the theme (dark.xml).
    /// </summary>
    public bool MapsforgeDrawOrder { get; init; }

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
/// The persistent placement decisions of one zoom level across passes. Every
/// examined symbol keeps its placed decision, so labels never change their
/// position when the tile set of the viewport changes — the placement is
/// attached to the feature (its id) or, without a feature id, to the anchor
/// of the symbol (point symbols; stable across re-renders) or to the symbol
/// instance of the (cached) tile, not to the current tile set. Rejections
/// are not kept: a rejected symbol is re-examined in every pass, so it can
/// appear when the space was freed by other symbols vanishing (see
/// <see cref="Decide"/>). The collision tree is rebuilt per pass: every
/// still-existing placed symbol occupies its space again, the boxes of
/// vanished symbols are gone (the old grow-only tree kept them forever and
/// blocked other symbols that took the space meanwhile — the reported case
/// of the canoe_polo icons remaining invisible at zoom 17).
/// </summary>
public sealed class IncrementalPlacementState
{
    /// <summary>
    /// The maximum number of decisions kept per zoom level; beyond that the
    /// state is reset (labels are placed again, a rare event after very
    /// long panning sessions).
    /// </summary>
    public const int MaxDecisions = 100_000;

    // Decisions by feature id (value non-null = the placed entry that is
    // kept in the following passes). The key includes the style layer name:
    // feature ids are only unique per layer (MVT/MLT convention), the same
    // id in another layer is a different feature. For symbols without
    // feature id by the symbol instance (stable while the tile is in the
    // tile cache), or — for point symbols — by their anchor (stable across
    // re-renders, see <see cref="AnchorKey"/>)
    private readonly Dictionary<(string Layer, ulong Id), PlacementEntry?> _decidedById = new();
    private readonly Dictionary<ISymbol, PlacementEntry?> _decidedByReference = new();
    private readonly Dictionary<(ulong TileId, double X, double Y, string? Text), PlacementEntry?> _decidedByAnchor = new();

    /// <summary>
    /// Gets the decision for the given symbol: true when the symbol was
    /// placed in an earlier pass. Rejections are not kept (see Decide): the
    /// symbol is examined again in every later pass, so it can appear when
    /// the space was freed by other symbols leaving the view.
    /// </summary>
    internal bool TryGetDecision(ISymbol symbol, string layer, out PlacementEntry? entry)
    {
        if (symbol.Id != 0)
        {
            return _decidedById.TryGetValue((layer, symbol.Id), out entry);
        }

        if (symbol is PointSymbol point)
        {
            // Point symbols without a feature id (MVT data sources often
            // carry no id attribute): their anchor is stable across re-renders
            // of the same tile, so a fresh instance finds the decision of the
            // first pass. Without this every return to a visited view would
            // re-examine the new instances against the collision boxes of
            // the first pass — they collide with the boxes of their own
            // earlier placement and are rejected ("labels disappear when you
            // come back")
            return _decidedByAnchor.TryGetValue(AnchorKey(point), out entry);
        }

        return _decidedByReference.TryGetValue(symbol, out entry);
    }

    /// <summary>
    /// Gets the decision for the given symbol (the placed entry). Rejections
    /// are NOT recorded: a symbol that found no space is examined again in
    /// every later pass against the fresh collision tree of the pass, so it
    /// appears when the space was freed — a pinned rejection would hide it
    /// for the whole session at this zoom level (reported with the
    /// canoe_polo icons: rejected once at zoom 17, never drawn again). A
    /// re-examined symbol that still collides is rejected again
    /// deterministically, so nothing flaps.
    /// </summary>
    internal void Decide(ISymbol symbol, string layer, PlacementEntry entry)
    {
        if (symbol.Id != 0)
        {
            _decidedById[(layer, symbol.Id)] = entry;
        }
        else if (symbol is PointSymbol point)
        {
            _decidedByAnchor[AnchorKey(point)] = entry;
        }
        else
        {
            _decidedByReference[symbol] = entry;
        }
    }

    /// <summary>
    /// The anchor key of a point symbol: the tile the feature came from, the
    /// exact tile coordinates of the anchor and the label text. Two re-renders
    /// of the same tile produce identical values (the pipeline is
    /// deterministic), so this identifies "the same label" without a feature
    /// id. Line symbols are not covered: their merged geometry changes with
    /// the tile set, there the instance reference stays the key.
    /// </summary>
    private static (ulong TileId, double X, double Y, string? Text) AnchorKey(PointSymbol point)
    {
        return (point.Tile.Id, point.Point.X, point.Point.Y, point.TextSymbol?.Text);
    }

    /// <summary>
    /// Resets the state when the maximum number of decisions is reached, at
    /// the beginning of the next pass (labels are placed again, a rare event
    /// after very long panning sessions; the memory stays bounded).
    /// </summary>
    internal void ResetIfFull()
    {
        if (_decidedById.Count + _decidedByReference.Count + _decidedByAnchor.Count < MaxDecisions)
        {
            return;
        }

        _decidedById.Clear();
        _decidedByReference.Clear();
        _decidedByAnchor.Clear();
    }
}

/// <summary>
/// The placement pass: computes for all visible tiles which symbols are drawn
/// where, as a background task before the next drawing. The collision check
/// runs in the unrotated canonical screen space of the integer zoom level and
/// processes the style layers in reverse (highest layer first), so an
/// overlapping symbol of a higher style layer wins; during a pass the
/// quadtree only grows (a symbol that finds its space occupied is dropped,
/// nothing is removed), while across passes it is rebuilt from the pinned
/// decisions of the <see cref="IncrementalPlacementState"/> (see there).
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
        IncrementalPlacementState? incremental = null, bool orderByPriority = false)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(
            new SKRect(-TestingCanvasHalfExtent, -TestingCanvasHalfExtent,
                TestingCanvasHalfExtent, TestingCanvasHalfExtent));

        // The collision tree is always fresh (the decisions of the previous
        // pass are pinned by value, not by the tree): during the pass it
        // only grows (nothing is removed), so a symbol of a lower style
        // layer never takes the space of an already placed one
        // Very long panning session at one zoom level: start the next pass
        // with a clean state, so the memory stays bounded
        incremental?.ResetIfFull();

        var tree = new Quadtree<ISymbol>();
        var context = new EvaluationContext(zoomLevel);

        // The mapsforge placement semantics (display classes and the clash
        // rules of the mapsforge reference labeller); see the pair checks of
        // the renderers and the ordering below. A using declaration restores
        // the previous semantics also when the pass is cancelled.
        using var placementSemantics = new PlacementSemanticsScope(
            orderByPriority ? GetMapsforgeDisplayClass : null);

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
        var examinedIds = new HashSet<(string Layer, ulong Id)>();

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

        // The mapsforge placement orders the candidates of all layers by the
        // render-theme priority (attribute priority, descending), then by the
        // display class ("always" before "order" before "ifspace"), then by
        // position (x descending, y ascending, matching the arithmetic of the
        // reference labeller), independent of the style layers.
        // The default (MapLibre) semantics: the highest style layer wins;
        // the layers are processed top first (highest style index; equal
        // indices order by list position, later entry first) so its symbols
        // occupy the quadtree first; symbols of lower layers that collide are
        // dropped and nothing is removed later
        var merged = layerPositions.Select((name, position) => (name, position));
        var topFirst = merged
            .OrderByDescending(pair => layerOrder[pair.name])
            .ThenByDescending(pair => pair.position)
            .Select(pair => pair.name)
            .ToList();

        // Merge the line symbol tile copies per layer: both placement modes
        // need the merged lists; the default mode processes layer by layer,
        // the mapsforge mode interleaves the layers by priority
        var mergedByLayer = new Dictionary<string, List<ISymbol>>();

        foreach (var name in topFirst)
        {
            // Every layer keeps its own list of placements (the dictionary is
            // copied over in the ascending style order after the pass)
            placesTopFirst.Add(name, new List<PlacementEntry>());

            if (IsVisibleAtZoom(layerZooms[name], zoomLevel) == false)
            {
                // Invisible at this zoom level
                continue;
            }

            // Names in the same style layer are ordered by SortOrder and then
            // by Rank; line symbols of the same feature split across tiles are
            // merged into one continuous symbol
            mergedByLayer.Add(name, LineSymbolMerger.MergeById(layers[name].Symbols
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Rank)));
        }

        // In the default mode the merged lists are in the top-first layer
        // order (the dictionary keeps the insertion order), so the symbols
        // are placed layer by layer like before; the mapsforge mode sorts the
        // candidates of all layers into a single placement order in three
        // groups, like the label placement of the mapsforge reference: first
        // the way labels (pathText — applied with the ways), then the point
        // labels (the nodes), finally the icon-only line symbols (e.g. the
        // oneway arrows), which are drawn with the ways and claim no label
        // space at all (they neither ask for space nor reserve it, they only
        // avoid their own kind over a separate tree). Within each group:
        // priority ascending (the SortOrder), then the display class
        // ("always" before "order" before "ifspace"), then position
        // (x descending, y ascending, matching the arithmetic of the
        // reference labeller), then the later theme rule wins (higher style
        // index) as the last tie breaker.
        var candidateList = mergedByLayer
            .SelectMany(pair => pair.Value.Select(s => (Symbol: s, Layer: pair.Key)))
            .ToList();

        if (orderByPriority)
        {
            static bool IsWayTextLine(ISymbol s) => s is LineSymbol { TextSymbol: not null };

            static bool IsIconOnlyLine(ISymbol s) =>
                s is LineSymbol { IconSymbol: not null } line && !line.HasText;

            static int GroupOf(ISymbol s) => IsWayTextLine(s) ? 0 : (IsIconOnlyLine(s) ? 2 : 1);

            var candidates = candidateList;
            candidateList = candidates
                .OrderBy(c => GroupOf(c.Symbol))
                .ThenBy(c => c.Symbol.SortOrder)
                .ThenByDescending(c => GetMapsforgeDisplayClass(c.Symbol))
                .ThenByDescending(c => SymbolPoint(c.Symbol).X)
                .ThenBy(c => SymbolPoint(c.Symbol).Y)
                .ThenByDescending(c => layerOrder[c.Layer])
                .ToList();
        }

        // The icon-only line symbols (oneway arrows in the mapsforge mode)
        // check their space only against each other over this separate tree:
        // in the mapsforge semantics they are drawn with the ways, below the
        // labels of every style layer, and block nothing
        Quadtree<ISymbol>? lineIconTree = orderByPriority ? new Quadtree<ISymbol>() : null;

        foreach (var (symbol, layer) in candidateList)
        {
            token.ThrowIfCancellationRequested();

            if (incremental is not null &&
                !seenSymbols!.Add(symbol))
            {
                // A later occurrence of the same feature: decided or
                // rejected already in this very pass
                continue;
            }

            if (symbol.Id != 0 && !examinedIds.Add((layer, symbol.Id)))
            {
                continue;
            }

            var placements = placesTopFirst[layer];

            // A persistent state keeps the decision of an earlier pass:
            // the label stays at the position of the pass that placed
            // it, also when tiles appear or disappear. The collision
            // check still runs again (the tree is fresh), so the space
            // of the pinned symbol is re-affirmed in the current pass
            // and the boxes of vanished symbols free their space
            if (incremental is not null &&
                incremental.TryGetDecision(symbol, layer, out var decided) &&
                decided is not null)
            {
                if (symbol is PointSymbol pinnedPoint)
                {
                    var status = PointSymbolRenderer.CheckForSpace(canvas, context, pinnedPoint, tree,
                        worldToScreen, 0, showUnvalidBorders: false);

                    if (status == SymbolPlacementStatus.Success)
                    {
                        insertCollisionBoxes(pinnedPoint);
                    }
                }
                else if (symbol is LineSymbol pinnedLine)
                {
                    // The icon-only line symbols claim no space (see below);
                    // the pinned decision is re-affirmed without touching the
                    // collision tree
                    if (pinnedLine.IconSymbol is null || pinnedLine.HasText)
                    {
                        LineSymbolRenderer.Place(canvas, context, pinnedLine, tree, worldToScreen,
                            showUnvalidBorders: false);
                    }
                }

                placements.Add(decided);
                continue;
            }

            if (symbol is PointSymbol pointSymbol)
            {
                var status = PointSymbolRenderer.CheckForSpace(canvas, context, pointSymbol, tree,
                    worldToScreen, 0, showUnvalidBorders: false);

                if (status == SymbolPlacementStatus.Success)
                {
                    placedByLayerAdd(symbol, layer, null, placements);
                }
                else
                {
                    // No persistent record: the symbol is re-examined in
                    // the next pass (see Decide)
                    rejected.Add(symbol);
                }
            }
            else if (symbol is LineSymbol lineSymbol)
            {
                if (lineIconTree is { } icons && lineSymbol.IconSymbol is not null && !lineSymbol.HasText)
                {
                    // The oneway arrows (icon-only line symbols) run against
                    // their own kind only; the label collision tree is not
                    // touched
                    var run = LineSymbolRenderer.Place(canvas, context, lineSymbol, icons, worldToScreen,
                        showUnvalidBorders: false);

                    if (run.HasPlacements)
                    {
                        placedByLayerAdd(symbol, layer, run, placements);
                    }
                    else
                    {
                        rejected.Add(symbol);
                    }
                }
                else
                {
                    var run = LineSymbolRenderer.Place(canvas, context, lineSymbol, tree, worldToScreen,
                        showUnvalidBorders: false);

                    if (run.HasPlacements)
                    {
                        placedByLayerAdd(symbol, layer, run, placements);
                    }
                    else
                    {
                        // No persistent record: the symbol's merged run is
                        // re-examined in the next pass (see Decide)
                        rejected.Add(symbol);
                    }
                }
            }
        }

        void insertCollisionBoxes(PointSymbol point)
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

        void placedByLayerAdd(ISymbol symbol, string layer, LineSymbolPlacementRun? run, List<PlacementEntry> placements)
        {
            if (symbol is PointSymbol point)
            {
                insertCollisionBoxes(point);
            }

            placements.Add(new PlacementEntry(symbol, run));
            incremental?.Decide(symbol, layer, placements[^1]);
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
    /// Gets or sets the mapsforge display class accessor of the running
    /// placement pass (2 = "always", 1 = "order", 0 = "ifspace"), or null for
    /// the default MapLibre semantics. Set by
    /// <see cref="PlacementSemanticsScope"/>; a pass runs single-threaded on
    /// one thread, so the accessor is thread-static.
    /// </summary>
    [ThreadStatic]
    internal static Func<ISymbol, int>? MapsforgeDisplayClassAccessor;

    /// <summary>
    /// Gets the position of a symbol for the mapsforge placement order
    /// (PointSymbols sit on their point; LineSymbols on the center of the
    /// envelope of their geometry).
    /// </summary>
    private static (double X, double Y) SymbolPoint(ISymbol symbol)
    {
        return symbol switch
        {
            PointSymbol point => (point.Point.X, point.Point.Y),
            LineSymbol line => ((line.Geometry.EnvelopeInternal.MinX + line.Geometry.EnvelopeInternal.MaxX) / 2,
                (line.Geometry.EnvelopeInternal.MinY + line.Geometry.EnvelopeInternal.MaxY) / 2),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// Gets the mapsforge display class of a symbol: the highest class of its
    /// inner symbols (see <see cref="ClassOf"/>; the Point/LineSymbol wrappers
    /// of the mapsforge symbol factory only carry AllowOthers of the inners).
    /// </summary>
    private static int GetMapsforgeDisplayClass(ISymbol symbol)
    {
        return symbol switch
        {
            PointSymbol point => Math.Max(Display(point.IconSymbol), Display(point.TextSymbol)),
            LineSymbol line => Math.Max(Display(line.IconSymbol), Display(line.TextSymbol)),
            IconPointSymbol inner => ClassOf(inner),
            TextPointSymbol inner => ClassOf(inner),
            IconLineSymbol inner => ClassOf(inner),
            TextLineSymbol inner => ClassOf(inner),
            _ => 0
        };

        static int ClassOf<TSymbol>(TSymbol? inner) where TSymbol : ISymbol =>
            inner switch
            {
                IconPointSymbol innerSymbol => innerSymbol.AllowOverlap ? (innerSymbol.AllowOthers ? 2 : 1) : 0,
                TextPointSymbol innerSymbol => innerSymbol.AllowOverlap ? (innerSymbol.AllowOthers ? 2 : 1) : 0,
                IconLineSymbol innerSymbol => innerSymbol.AllowOverlap ? (innerSymbol.AllowOthers ? 2 : 1) : 0,
                TextLineSymbol innerSymbol => innerSymbol.AllowOverlap ? (innerSymbol.AllowOthers ? 2 : 1) : 0,
                _ => 0
            };

        // Mapsforge display mapping (see the symbol factory): "always" allows
        // overlap in both directions, "order" overlaps other symbols but is
        // removed when overlapping an "always" symbol, "ifspace" only takes
        // space that is free
        static int Display<TSymbol>(TSymbol? inner) where TSymbol : ISymbol =>
            ClassOf(inner);
    }

    /// <summary>
    /// Runs the pairwise clash decision of a placement check: returns true
    /// when the two symbols clash under the active semantics (the rectangle
    /// intersection is already checked by the caller). Without an accessor
    /// (default MapLibre semantics) two symbols clash unless both allow
    /// others; with the mapsforge accessor (see
    /// <see cref="MapsforgeDisplayClassAccessor"/>) symbols of display
    /// "always" clash with nothing and a symbol of display "order" clashes
    /// only with other "order" symbols, so the classified self class is
    /// compared against the class of the other symbol.
    /// </summary>
    internal static bool SymbolsClash(ISymbol symbol, Symbol otherSymbol, int? selfClass)
    {
        if (MapsforgeDisplayClassAccessor is not null)
        {
            var otherClass = MapsforgeDisplayClassAccessor(otherSymbol);

            return selfClass != 2 && otherClass != 2 && selfClass == otherClass;
        }

        return !(symbol.AllowOthers && otherSymbol.AllowOthers);
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
