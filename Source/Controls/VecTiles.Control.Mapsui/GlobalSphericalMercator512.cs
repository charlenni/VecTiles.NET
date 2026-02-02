using BruTile;

namespace VecTiles.Controls.Mapsui;

public class GlobalSphericalMercator512 : TileSchema
{
    public GlobalSphericalMercator512(YAxis yAxis = YAxis.OSM, int minZoom = 0, int maxZoom = 24)
    {
        Name = "GlobalSphericalMercator-512";
        Srs = "EPSG:3857";
        Format = "pbf";
        YAxis = yAxis;
        const int tileWidth = 512;
        const int tileHeight = 512;

        const double initialResolution = 2 * Math.PI * 6378137 / tileWidth; // 512 instead of 256
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var unitsPerPixel = initialResolution / Math.Pow(2, z);
            Resolutions[z] = new Resolution(z, unitsPerPixel, tileWidth, tileHeight, scaledenominator: unitsPerPixel * 39.37 / 0.00028);
        }

        OriginX = -20037508.3427892;
        OriginY = 20037508.3427892 * (YAxis == YAxis.TMS ? -1 : 1);  // TMS starts bottom to top, XYZ starts top to bottom

        Extent = new Extent(
            -20037508.3427892,
            -20037508.3427892,
             20037508.3427892,
             20037508.3427892);
    }
}
