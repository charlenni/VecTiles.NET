### MapLibre style file

A vector style file, which contains the information to style the vector data in a VectorTile (see https://maplibre.org/maplibre-style-spec/). 
MapLibre style file has nearly the same format as the Mapbox style file. The format for the Mapbox style file is described here: https://docs.mapbox.com/style-spec.
Not all parts are implemented now.

The style file contains different style layers, each selecting a part of geometries from one of the layers 
of the VectorTile. These geometries than styled with the given data, e.g. line width and color.

