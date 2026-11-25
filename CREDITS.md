# Credits

VecTiles.NET would not exist without the great open source projects listed below.
A big thank you to everyone who contributed to them.

## Libraries used at runtime

These libraries are direct NuGet dependencies of VecTiles:

| Project | License | Used by |
| --- | --- | --- |
| [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) | BSD-3-Clause | Geometry handling shared by all packages |
| [NetTopologySuite.Features](https://github.com/NetTopologySuite/NetTopologySuite.Features) | BSD-3-Clause | Feature model of converted vector tiles |
| [NetTopologySuite.IO.VectorTiles](https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles) | MIT | Origin of the vector tile primitives |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Skia renderer (`VecTiles.Skia`) |
| [SkiaSharp.HarfBuzz](https://github.com/mono/SkiaSharp) | MIT | Complex text shaping for symbol rendering |
| [Topten.RichTextKit](https://github.com/toptensoftware/RichTextKit) | Apache-2.0 | Rich text layout and rendering |
| [Earcut.NET](https://github.com/MichaCo/Earcut) | MIT | Polygon triangulation (a port of Mapbox's [Earcut](https://github.com/mapbox/earcut), ISC) |
| [protobuf-net](https://github.com/protobuf-net/protobuf-net) | Apache-2.0 | Parsing of the Mapbox/OpenMapTiles PBF format |
| [BruTile](https://github.com/BruTile/BruTile) | Apache-2.0 | Tile math and predefined tile schemes |
| [Mapsui](https://github.com/Mapsui/Mapsui) | MIT | Mapsui control (`VecTiles.Mapsui`) |
| [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) | MIT | MBTiles (SQLite) tile data source |

## Build, test and sample tooling

| Project | License | Used by |
| --- | --- | --- |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT | Avalonia sample app |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | Sample app view models |
| [System.Reactive](https://github.com/dotnet/reactive) | MIT | Sample app |
| [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) | MIT | Benchmarks |
| [xUnit](https://github.com/xunit/xunit) | Apache-2.0 | Tests |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | MIT | Test coverage |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | MIT | Test platform |

The [Inter](https://rsms.me/inter/) typeface, shipped with `Avalonia.Fonts.Inter` for the
sample app, is licensed under the [SIL Open Font License 1.1](https://openfontlicense.org/).

## Code adapted from other projects

Parts of the VecTiles source are adapted from the following projects; the
corresponding license texts are included in [LICENSE.md](LICENSE.md):

- [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) (BSD-3-Clause) and
  [NetTopologySuite.IO.VectorTiles](https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles) (MIT) —
  the vector tile primitives in `Source/VecTiles.Common/Primitives/`.
- [Mapsui](https://github.com/Mapsui/Mapsui) (MIT) —
  `ByteDataSource` and stream extensions in `Source/DataSources/VecTiles.ByteDataSources/`.
- [BruTile](https://github.com/BruTile/BruTile) (Apache-2.0) —
  MBTiles support in `Source/DataSources/VecTiles.TileDataSources/MBTilesTileDataSource/`.

VecTiles also implements the open [Mapbox style specification](https://docs.mapbox.com/mapbox-gl-js/style-spec/)
and reads tiles in the open [Mapbox Vector Tile](https://github.com/mapbox/vector-tile-spec),
[OpenMapTiles](https://github.com/openmaptiles/openmaptiles) and
[Mapsforge binary map file](https://github.com/mapsforge/mapsforge/blob/master/docs/Specification-Binary-Map-File.md)
schemas, and styles them with [Mapsforge render themes](https://github.com/mapsforge/mapsforge/blob/master/docs/Rendertheme-V5.md).

## Sample content

The sample application ships the Mapsforge render themes (`osmarender.xml`, `default.xml`,
`biker.xml`, `motorider.xml`, `dark.xml`, `indigo.xml`, `hillshading.xml`) together with the
symbols and patterns they reference in [`Files/Mapsforge/`](Files/Mapsforge); they are part of
the [mapsforge](https://github.com/mapsforge/mapsforge) project and are licensed under
[LGPL-3.0](https://www.gnu.org/licenses/lgpl-3.0.html) (see [LICENSE.md](LICENSE.md)).

The sample styles and maps build on open data and open projects:

- [OSM Liberty](https://github.com/maputnik/osm-liberty) — the sample style `osm-liberty.json`
  and its Mapsforge variant `osm-liberty-for-mapsforge.json`; license see
  [`Files/OpenMapTiles/LICENSE.md`](Files/OpenMapTiles/LICENSE.md).
- The sample map files `zuerich.map` and `zurich.mbtiles` are derived from OpenStreetMap data
  (© [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, ODbL); `zuerich.map`
  was written with the mapsforge `map-writer`.
- The shaded relief raster sample
  [`natural_earth_2_shaded_relief.raster.mbtiles`](Files/OpenMapTiles) is based on
  [Natural Earth](https://www.naturalearthdata.com/) (public domain).
