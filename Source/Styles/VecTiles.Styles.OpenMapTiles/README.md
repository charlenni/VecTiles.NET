### OpenMapTiles style file

A vector style file, which contains the information to style the vector data in a VectorTile (see https://openmaptiles.org/schema/). 
OpenMapTiles style file has the same format as the Mapbox style file. The format for the Mapbox style file is described here: https://docs.mapbox.com/style-spec.
Not all parts are implemented now (e.g. Expressions are missing).

The style file contains different style layers, each selecting a part of geometries from one of the layers 
of the VectorTile. This geometries than styled with the given data, e.g. line width and color.

