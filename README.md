##VectTiles.NET

VecTiles.NET is another try to bring vector map data from different services to C#.

###Details

####Sources

In the beginning each tile comes as a byte array. This could be read from different data sources. 
For tiles VecTiles.NET has two possible sources: HTTP and MBTiles files (SQLite files in Mapbox format). It should be no 
problem to add later data files in a folder structure.

All of them implement the ITileDataSource interface. With this it is possible to get a byte array with vector data for a 
given tile.

####Converters

Converters take a given tile as byte array and convert this to a VectorTile (see NTS for this). In the moment there is 
only one converter implemented. It could convert Mapbox Vector Tiles (MVT) in PBF or zipped PBF format to VectorTile 
objects. 

It is possible to add other converters for other vector tile formats later, e.g. Mapsforge.

A VectorTile consits of different layers. Each layer has a name and contains a list of features. Each feature has 
one or more geometries and some attributes.

####VectorTileDataSource

A VectorTileDataSource combines a TileDataSource (which delivers tile data) with a Converter (converts the delivered 
tile data to a VectorTile). 

With this it is possible to get a VectorTile for a given tile coordinate (x, y, zoom). If a tile is not available, 
a tile of a lower zoom level is retrived and clipped to the requested tile.

####Styles

There are different styles for styling vector maps out there. In the moment there is only one style implemented:
Mapbox style format (https://docs.mapbox.com/mapbox-gl-js/style-spec/).

####Mapbox Style

A style file contains the sources to use (background, raster and vector), additionally files (sprites and glyphs) and 
a list of style layers. Each layer has a name, a source layer (the vector tile layer it is based on) and a filter to 
select the features that should be styled with this style layer. The style contains a name, type (fill, line or symbol), 
visibility and min/max zoom for this layer. The layout array is fixed for a given zoom level, while the paint part is 
zoom dependent.

####Renderer

The renderer now should be able to render a map with vector tiles. It needs a VectorTileDataSource (to get the vector 
tiles) and a Style (to style the features). The renderer can render a map for a given bounding box and zoom level to a 
bitmap. The rendering is zoom dependent.

####Symbols

There are different types of symbols: point, line and polygon symbols. 

A point symbol (e.g. city name) consits of a sprite and a text (one or both). Both could be placed around the given 
point with different anchor points. There are more than one possible place to show text or sprite, depending on, if the 
place on the map is already occupied by another symbol.

Line symbols are placed along a line (e.g. street names). Polygon symbols are placed in the center of the polygon.