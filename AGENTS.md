# AGENTS.md

Instructions for AI coding agents (Rider AI Assistant and others) working in this repository.
Follow these rules for every task. When this file conflicts with explicit user instructions in a
session, the user instructions win.

## What this project is

VecTiles.NET is a .NET library suite that makes vector map data (vector tiles) from different
services usable in C# applications. It covers the full pipeline: loading tile bytes, converting
them to vector tile objects, styling them with map styles, and rendering the styled map to
bitmaps. A Avalonia sample app and a Mapsui control demonstrate the usage. Parts of the suite are
published as NuGet packages (VecTiles.Common, VecTiles.MapLibre, VecTiles.Skia,
VecTiles.Mapsui).

## Repository layout

- `Source/` — all library projects (folders per area, described below)
- `Tests/` — xUnit test projects, one per area (`*.Tests`) plus renderer test apps
- `Samples/Avalonia/` — `SampleApp` (shared Avalonia UI) and `SampleApp.Desktop` (desktop head)
- `Benchmarks/` — BenchmarkDotNet benchmarks for the MapLibre code
- `Source/Packages/` — thin NuGet packaging projects (`*.Package`); one per published package
- `Logos/`, `Files/OpenMapTiles` — assets used by the packages and samples
- `Directory.Build.props` — shared package metadata (version, author, repository)
- `pack-nuget.sh` — builds/pushes all NuGet packages in dependency order
- `NuGet.config` — nuget.org plus a local feed at `artifacts/nugets` (created by `pack-nuget.sh`)

Build and test from the repository root:

```sh
dotnet build VecTiles.NET.sln
dotnet test VecTiles.NET.sln
```

All projects target `net10.0`.

## Architecture and data flow

The core pipeline is: **TileDataSource -> TileConverter -> VectorTileDataSource -> Style -> Renderer**.

1. **Data sources** (`Source/DataSources/`)
   - `VecTiles.ByteDataSources` — provide raw tile bytes for a tile coordinate (x, y, zoom), e.g.
     from HTTP or from MBTiles (SQLite) files. All implement `ITileDataSource`
     (`Source/VecTiles.Common/Interfaces/`).
2. **Converters** (`Source/Converters/`)
   - `VecTiles.Converters.MVT` — converts Mapbox/OpenMapTiles vector tiles (MVT, in PBF
     or zipped PBF form, protobuf via `protobuf-net`) into `VectorTile` objects (NetTopologySuite
     features). `VecTiles.Converters.SdfFont` converts fonts to SDF glyphs for symbol rendering.
3. **VectorTileDataSource**
   - Combines an `ITileDataSource` with an `ITileConverter`. Returns a `VectorTile` for a tile
     coordinate. If a tile is unavailable, a tile from a lower zoom level is fetched and clipped.
   - A `VectorTile` consists of named layers; each layer holds features with geometries and
     attributes.
4. **Styles** (`Source/Styles/`)
   - `VecTiles.Styles.MapLibre` implements the Mapbox style specification
     (https://docs.mapbox.com/mapbox-gl-js/style-spec/). A style selects features per layer via
     expressions (the old simple `Filter` concept was removed; use `IExpression` from
     `VecTiles.Common`). Layout properties are fixed per zoom level; paint properties are
     zoom-dependent.
5. **Renderers** (`Source/Renderers/`)
   - `VecTiles.Renderers.Common` — platform-independent rendering of a styled map for a bounding
     box and zoom level, using `ISymbol`, `IPaint`, `IBitmap` and related abstractions.
   - `VecTiles.Renderers.Skia` — SkiaSharp-based implementation of those interfaces.
   - Point symbols (sprite and/or text with anchors and collision handling), line symbols (text
     along lines, merged across tiles via `LineSymbolMerger`) and polygon symbols are supported.
6. **Controls** (`Source/Controls/`)
   - `VecTiles.Controls.Mapsui` — a Mapsui layer/control that plugs the renderer into Mapsui maps.

Key interfaces in `Source/VecTiles.Common/Interfaces/`: `ITileDataSource`, `ITileConverter`,
`IVectorTileDataSource`, `ILayerStyle`, `IExpression`, `ISymbol`, `ISymbolFactory`, `IPaint`,
`IPaintFactory`, `IBitmap`, `IDataSource`. New implementations should fit into these seams.

## Conventions

- C# / .NET 10; follow the existing code style (file-scoped conventions, folder-per-concern:
  `Enums/`, `Interfaces/`, `Primitives/`, `Extensions/`, `Exceptions/` inside `VecTiles.Common`).
- Tests are xUnit; every new feature should come with tests in the matching `Tests/*.Tests`
  project.
- NuGet package versions are managed centrally in `Directory.Build.props` (`Version`). Bump it
  there before releasing; package IDs, titles, descriptions and tags live in the
  `Source/Packages/*.Package` projects.
- `pack-nuget.sh` must be run in the documented order (Common, MapLibre, Skia, Mapsui) since
  later packages depend on earlier ones. Switch on packaging with `-p:CreateNuGets=true`.
- Commits use concise imperative English messages; pull requests target the `master` branch.
- Do not commit build outputs (`bin/`, `obj/`, `artifacts/`) or `*.user` file changes unrelated to
  the task.
