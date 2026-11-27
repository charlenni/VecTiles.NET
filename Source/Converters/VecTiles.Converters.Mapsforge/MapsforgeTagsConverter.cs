using System.Globalization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace VecTiles.Converters.Mapsforge;

/// <summary>
/// Static converter of the raw Mapsforge tags of a feature to the conventions of the MapLibre
/// schema, https://maplibre.org/maplibre-style-spec/layers/: it determines the
/// source layer of the feature from its tags (e.g. a feature tagged 'building=yes' belongs to the
/// source layer 'building') and converts the tags to the attribute names and values expected by
/// styles based on the MapLibre schema (e.g. osm-liberty).
/// </summary>
/// <remarks>
/// The mapping follows the MapLibre layer definitions, restricted to the features that occur
/// in Mapsforge map files: MapLibre enriches its tiles with data from post processing steps
/// (e.g. computed building heights or place ranks) that Mapsforge files do not carry, such
/// attributes are approximated or left out. One feature may map to several source layers, the
/// MapLibre schema lists street names as separate features in the 'transportation_name'
/// layer besides the road in the 'transportation' layer. A feature whose tags do not map to a
/// source layer of the schema is dropped.
/// </remarks>
public static class MapsforgeTagsConverter
{
    /// <summary>
    /// Determines the source layer or layers of a feature from the tags and converts the tags of
    /// the feature to the attribute names and values of the MapLibre schema at those source
    /// layers.
    /// </summary>
    /// <param name="geometry">Geometry of the feature, in the normalized tile space; the geometry
    /// type decides between the point, line and area features of a source layer</param>
    /// <param name="sourceLayer">Provisional source layer of the feature (Mapsforge tile data
    /// has no notion of source layers, so this is normally string.Empty)</param>
    /// <param name="tags">Raw tags of the feature</param>
    /// <returns>Source layers of the feature and its tags converted per source layer</returns>
    public static (string SourceLayer, IAttributesTable Attributes)[] ConvertTags(Geometry geometry, string sourceLayer, IAttributesTable tags)
    {
        if (geometry.IsEmpty)
            return [];

        // Read the tags once into a string to string map, the tag values of Mapsforge files are
        // decodable strings
        var tagMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in tags.GetNames())
        {
            tagMap[name] = tags.GetOptionalValue(name)?.ToString() ?? string.Empty;
        }

        return geometry switch
        {
            Point or MultiPoint => [ConvertPoint(tagMap)],
            Polygon or MultiPolygon => Area(tagMap),
            LineString or MultiLineString or GeometryCollection => Line(tagMap),
            _ => [],
        };
    }

    /// <summary>
    /// Convert the tags of a point feature (POIs and place labels).
    /// </summary>
    private static (string SourceLayer, IAttributesTable Attributes) ConvertPoint(Dictionary<string, string> tags)
    {
        // Places are labels of administrative entities in their own source layer; the MapLibre
        // schema requires a name for places
        if (tags.TryGetValue("place", out var place))
        {
            if (!tags.TryGetValue("name", out var placeName) || placeName == string.Empty)
                return (string.Empty, NewAttributes());

            var attributes = NewAttributes("class", place);
            attributes.Add("name", placeName);
            if (place == "country")
                attributes.Add("rank", 3);

            return ("place", attributes);
        }

        // Mountain peaks, volcanos and saddles are labels in their own source layer
        if (tags.TryGetValue("natural", out var peakNatural) && peakNatural is "peak" or "volcano" or "saddle")
        {
            var peakAttributes = NewAttributes();
            AddIfPresent(peakAttributes, tags, "name");
            AddIfPresent(peakAttributes, tags, "ele");
            return ("mountain_peak", peakAttributes);
        }

        // Springs are points of the water layer
        if (tags.TryGetValue("natural", out var springNatural) && springNatural == "spring")
            return ("water", NewAttributes("class", "spring"));

        // Housenumbers are features of their own layer (points and, see below, areas)
        if (tags.TryGetValue("addr:housenumber", out var housenumber))
        {
            var housenumberAttributes = NewAttributes("housenumber", housenumber);
            AddIfPresent(housenumberAttributes, tags, "addr:street");
            return ("housenumber", housenumberAttributes);
        }

        // All other points are POIs
        var poiClass = PoiClass(tags, out var subclass);
        if (poiClass == null)
            return (string.Empty, NewAttributes());

        var poiAttributes = NewAttributes(
            ("class", poiClass),
            ("subclass", subclass),
            ("rank", PoiRank(poiClass)));
        AddIfPresent(poiAttributes, tags, "name");

        return ("poi", poiAttributes);
    }

    /// <summary>
    /// Class and subclass of a POI from its tags; null, when the tags do not declare a POI.
    /// </summary>
    private static string? PoiClass(Dictionary<string, string> tags, out string subclass)
    {
        // Transit stops and stations form classes of their own, the MapLibre styles render
        // them with transit symbols (class in bus, rail, airport)
        if (tags.TryGetValue("highway", out var highway) && highway == "bus_stop")
        {
            subclass = highway;
            return "bus";
        }
        if (tags.TryGetValue("railway", out var railway) &&
            railway is "station" or "tram_stop" or "halt" or "crossing")
        {
            subclass = railway;
            return "rail";
        }
        if (tags.TryGetValue("station", out var station) && station != "no")
        {
            subclass = station;
            return "rail";
        }
        if (tags.TryGetValue("aeroway", out var aeroway) && aeroway is "aerodrome" or "terminal" or "gate")
        {
            subclass = aeroway;
            return "airport";
        }

        foreach (var key in PoiTagKeys)
        {
            if (tags.TryGetValue(key, out var value) && value != "no")
            {
                subclass = value;
                return value;
            }
        }

        subclass = string.Empty;
        return null;
    }

    /// <summary>
    /// Source layer priority of the POI tag keys: the first present key declares the POI. The
    /// list follows the mapping of the MapLibre poi layer (poi/mapping.yaml).
    /// </summary>
    private static readonly string[] PoiTagKeys =
    [
        "amenity",
        "shop",
        "tourism",
        "historic",
        "leisure",
        "office",
        "sport",
        "barrier",
        "landuse",
        "waterway",
    ];

    /// <summary>
    /// Rank of a POI, a low rank is more important. MapLibre computes the rank in a post
    /// processing step; this simple mapping keeps the well-known POI classes of the MapLibre
    /// styles in the top rank band.
    /// </summary>
    private static int PoiRank(string poiClass)
    {
        return poiClass is "hospital" or "police" or "fire_station" or "townhall" or "place_of_worship"
            or "museum" or "bus" or "rail" or "airport"
            ? 1
            : 10;
    }

    /// <summary>
    /// Convert the tags of an area feature. The source layers and their order follow the polygon
    /// mappings of the MapLibre layers (their mapping.yaml definitions).
    /// </summary>
    private static (string SourceLayer, IAttributesTable Attributes)[] Area(Dictionary<string, string> tags)
    {
        // Water areas: natural water and bays, river banks, docks, reservoirs, swimming pools,
        // fountains (see water/mapping.yaml)
        if (tags.TryGetValue("natural", out var natural) && natural == "bay")
            return [("water", NewAttributes("class", "bay"))];
        if (tags.TryGetValue("natural", out var waterNatural) && waterNatural == "water")
            return [("water", NewAttributes("class", tags.TryGetValue("water", out var water) && water != string.Empty ? water : "lake"))];
        if (tags.TryGetValue("waterway", out var waterway) && waterway == "dock")
            return [("water", NewAttributes("class", "dock"))];
        if (tags.TryGetValue("landuse", out var waterLanduse) && waterLanduse is "reservoir" or "basin" or "salt_pond")
            return [("water", NewAttributes("class", waterLanduse))];
        if (tags.TryGetValue("leisure", out var poolLeisure) && poolLeisure == "swimming_pool")
            return [("water", NewAttributes("class", "swimming_pool"))];
        if (tags.TryGetValue("amenity", out var fountain) && fountain == "fountain")
            return [("water", NewAttributes("class", "fountain"))];

        // Land cover (see landcover/mapping.yaml with the class table of landcover.yaml; the
        // class is derived from the subclass, the class 'park' of the MapLibre styles is
        // not assigned here because the schema derives it to 'grass')
        if (LandcoverOf(tags) is { } landcover)
            return [("landcover", NewAttributes(("class", landcover.Class), ("subclass", landcover.Subclass)))];

        // Land use of built and administratively organized areas (see landuse/mapping.yaml;
        // the class is the raw declaring tag value, like the COALESCE in landuse.sql)
        if (LanduseOf(tags) is { } landuse)
            return [("landuse", NewAttributes("class", landuse))];

        // Parks and protected areas (see park/mapping.yaml)
        if (ParkClass(tags) is { } parkClass)
        {
            var parkAttributes = NewAttributes("class", parkClass);
            AddIfPresent(parkAttributes, tags, "name");
            return [("park", parkAttributes)];
        }

        // Airport areas like aprons
        if (tags.TryGetValue("aeroway", out var areaAeroway) &&
            areaAeroway is "apron" or "helipad" or "terminal" or "gate")
            return [("aeroway", NewAttributes("class", areaAeroway))];

        // Administrative boundary areas: like the boundary lines below, only that OMT derives
        // them from the multipolygon rings; the styles stroke the outline of the polygon
        if (tags.TryGetValue("boundary", out var areaBoundary) && areaBoundary == "administrative")
        {
            AttributesTable boundaryAttributes = NewAttributes();
            if (tags.TryGetValue("admin_level", out var boundaryLevel) &&
                int.TryParse(boundaryLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var levelValue))
                boundaryAttributes.Add("admin_level", levelValue);
            if (tags.TryGetValue("disputed", out var areaDisputed) && areaDisputed == "yes")
                boundaryAttributes.Add("disputed", 1);
            if (tags.TryGetValue("maritime", out var areaMaritime) && areaMaritime == "yes")
                boundaryAttributes.Add("maritime", 1);

            return [("boundary", boundaryAttributes)];
        }

        // Buildings and building parts; a building with a housenumber also feeds the
        // housenumber layer
        if (tags.ContainsKey("building") || tags.ContainsKey("building:part"))
        {
            if (tags.TryGetValue("addr:housenumber", out var housenumber))
            {
                var housenumberAttributes = NewAttributes("housenumber", housenumber);
                AddIfPresent(housenumberAttributes, tags, "addr:street");
                return [("building", NewAttributes()), ("housenumber", housenumberAttributes)];
            }

            return [("building", NewAttributes())];
        }

        // Pedestrian paths and plazas as well as platform and pier areas are transportation
        // polygons (see the highway_polygon mapping of transportation/mapping.yaml)
        if (tags.TryGetValue("highway", out var polygonHighway) &&
            polygonHighway is "path" or "cycleway" or "bridleway" or "footway" or "corridor" or
                "pedestrian" or "steps")
            return [("transportation", NewAttributes(("class", "path"), ("subclass", polygonHighway)))];
        if (tags.TryGetValue("public_transport", out var polygonPt) && polygonPt == "platform")
            return [("transportation", NewAttributes(("class", "path"), ("subclass", "platform")))];
        if (tags.TryGetValue("man_made", out var polygonManMade) && polygonManMade is "bridge" or "pier")
            return [("transportation", NewAttributes("class", polygonManMade))];

        // The tags do not map to a source layer of the schema
        return [];
    }

    /// <summary>
    /// Class and subclass of a land cover area from the 'natural', 'landuse', 'leisure' and
    /// 'wetland' tags (see landcover/mapping.yaml and the class field table of
    /// landcover/landcover.yaml); null, when the tags do not declare a land cover.
    /// </summary>
    private static (string Class, string Subclass)? LandcoverOf(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("natural", out var natural) && LandcoverNaturalSubclasses.Contains(natural))
        {
            // The subclass of a wetland polygon is the raw value of the 'wetland' tag, if present
            var subclass = natural == "wetland" &&
                tags.TryGetValue("wetland", out var wetland) && wetland != string.Empty
                ? wetland
                : natural;
            return (LandcoverClassOf(subclass), subclass);
        }

        foreach (var key in new[] { "landuse", "leisure" })
        {
            if (tags.TryGetValue(key, out var value) && LandcoverKeyedSubclasses[key].Contains(value))
                return (LandcoverClassOf(value), value);
        }

        return null;
    }

    /// <summary>
    /// Subclasses of a land cover area by declaring key, following landcover/mapping.yaml.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> LandcoverKeyedSubclasses = new()
    {
        ["landuse"] = new HashSet<string>
        {
            "allotments", "farm", "farmland", "orchard", "flowerbed", "plant_nursery", "vineyard",
            "grass", "grassland", "meadow", "forest", "village_green", "recreation_ground",
        },
        ["leisure"] = new HashSet<string> { "park", "garden", "golf_course" },
    };

    /// <summary>
    /// Subclasses of a land cover area declared by the 'natural' key, following
    /// landcover/mapping.yaml.
    /// </summary>
    private static readonly HashSet<string> LandcoverNaturalSubclasses =
    [
        "wood", "wetland", "fell", "grassland", "heath", "scrub", "shrubbery", "tundra",
        "glacier", "bare_rock", "scree", "beach", "sand", "dune",
    ];

    /// <summary>
    /// Class of a land cover subclass, following the class field table of
    /// landcover/landcover.yaml.
    /// </summary>
    private static string LandcoverClassOf(string subclass)
    {
        return subclass switch
        {
            "farmland" or "farm" or "orchard" or "vineyard" or "plant_nursery" => "farmland",
            "glacier" or "ice_shelf" => "ice",
            "wood" or "forest" => "wood",
            "bare_rock" or "scree" => "rock",
            "wetland" or "bog" or "swamp" or "wet_meadow" or "marsh" or "reedbed" or "saltern" or
                "tidalflat" or "saltmarsh" or "mangrove" => "wetland",
            "beach" or "sand" or "dune" => "sand",
            _ => "grass",
        };
    }

    /// <summary>
    /// Land use class of an area from the 'landuse', 'amenity', 'leisure', 'tourism' and
    /// 'waterway' tags (see landuse/mapping.yaml; the class follows the COALESCE of
    /// landuse.sql); null, when the tags do not declare a land use.
    /// </summary>
    private static string? LanduseOf(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("landuse", out var landuse) &&
            landuse is "railway" or "cemetery" or "military" or "quarry" or "residential" or
                "commercial" or "industrial" or "garages" or "retail")
            return landuse;

        if (tags.TryGetValue("amenity", out var amenity) &&
            amenity is "bus_station" or "school" or "university" or "kindergarten" or "college" or
                "library" or "hospital" or "grave_yard" or "parking")
            return amenity;

        if (tags.TryGetValue("leisure", out var leisure) &&
            leisure is "stadium" or "pitch" or "playground" or "track")
            return leisure;

        if (tags.TryGetValue("tourism", out var tourism) && tourism is "theme_park" or "zoo")
            return tourism;

        return tags.TryGetValue("waterway", out var waterway) && waterway == "dam" ? "dam" : null;
    }

    /// <summary>
    /// Park class of a protected area (see park/mapping.yaml); null, when the tags do not
    /// declare a park.
    /// </summary>
    private static string? ParkClass(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("leisure", out var leisure) && leisure == "nature_reserve")
            return leisure;
        if (tags.TryGetValue("boundary", out var boundary) &&
            boundary is "national_park" or "protected_area" or "aboriginal_lands")
            return boundary;
        if (tags.TryGetValue("historic", out var historic) &&
            historic is "archaeological_site" or "battlefield" or "district")
            return historic;

        return null;
    }

    /// <summary>
    /// Convert the tags of a line feature (roads, railways, waterways, boundaries, ...).
    /// </summary>
    private static (string SourceLayer, IAttributesTable Attributes)[] Line(Dictionary<string, string> tags)
    {
        // Roads; named roads are also road labels in their own source layer
        if (tags.TryGetValue("highway", out var highway) && highway != "no")
        {
            var transportationClass = TransportationClassOf(highway);
            var brunnel = Brunnel(tags);

            var attributes = NewAttributes(
                ("class", transportationClass),
                ("subclass", highway));
            if (brunnel != null)
                attributes.Add("brunnel", brunnel);
            if (highway.EndsWith("_link", StringComparison.Ordinal))
                attributes.Add("ramp", 1);
            AddOneway(attributes, tags);

            AttributesTable? nameAttributes = null;
            if (tags.TryGetValue("name", out var name) && name != string.Empty || tags.ContainsKey("ref"))
            {
                nameAttributes = NewAttributes(
                    ("class", transportationClass),
                    ("subclass", highway));
                if (brunnel != null)
                    nameAttributes.Add("brunnel", brunnel);
                if (tags.TryGetValue("name", out var nameValue) && nameValue != string.Empty)
                    nameAttributes.Add("name", nameValue);
                if (tags.TryGetValue("ref", out var refs) && refs != string.Empty)
                {
                    nameAttributes.Add("ref", refs);
                    nameAttributes.Add("ref_length", refs.Length);
                }
            }

            return nameAttributes != null
                ? [("transportation", attributes), ("transportation_name", nameAttributes)]
                : [("transportation", attributes)];
        }

        // Platform lines of railways and public transport (see the class field table of
        // transportation/transportation.yaml: public_transport platforms belong to class 'path')
        if (tags.TryGetValue("railway", out var platformRailway) && platformRailway == "platform" ||
            tags.TryGetValue("public_transport", out var platformPt) && platformPt == "platform")
            return [("transportation", NewAttributes(("class", "path"), ("subclass", "platform")))];

        // Railways: physical railways and transit systems
        if (tags.TryGetValue("railway", out var railway) && railway != "no")
        {
            var railwayClass = railway switch
            {
                "subway" or "light_rail" or "tram" or "monorail" => "transit",
                _ => "rail",
            };

            var attributes = NewAttributes(
                ("class", railwayClass),
                ("subclass", railway));
            if (Brunnel(tags) is { } brunnel)
                attributes.Add("brunnel", brunnel);

            return [("transportation", attributes)];
        }

        // Aerialways
        if (tags.TryGetValue("aerialway", out var aerialway) && aerialway != "no")
        {
            var attributes = NewAttributes(
                ("class", "aerialway"),
                ("subclass", aerialway));
            if (Brunnel(tags) is { } brunnel)
                attributes.Add("brunnel", brunnel);

            return [("transportation", attributes)];
        }

        // Waterways with a class of their own in the schema
        if (tags.TryGetValue("waterway", out var waterway) &&
            waterway is "stream" or "river" or "canal" or "ditch" or "drain")
        {
            var attributes = NewAttributes(("class", waterway));
            if (Brunnel(tags) is { } brunnel)
                attributes.Add("brunnel", brunnel);
            AddIfPresent(attributes, tags, "name");

            return [("waterway", attributes)];
        }

        // Airport lines like runways and taxiways
        if (tags.TryGetValue("aeroway", out var lineAeroway) && lineAeroway is "runway" or "taxiway")
            return [("aeroway", NewAttributes(("class", lineAeroway)))];

        // Ferry routes are shipways (see the shipway_linestring table of
        // transportation/mapping.yaml)
        if (tags.TryGetValue("route", out var route) && route == "ferry")
        {
            var ferryAttributes = NewAttributes("class", "ferry");
            AddIfPresent(ferryAttributes, tags, "name");

            return [("transportation", ferryAttributes)];
        }

        // Administrative boundaries; the schema does not require an admin level, styles filter
        // on it (a boundary without one is mapped, but usually not drawn). Mapsforge files also
        // carry boundary segments that only have the admin level, without the boundary key.
        var adminLevel = tags.TryGetValue("admin_level", out var adminLevelTag) ? adminLevelTag : null;
        if (tags.TryGetValue("boundary", out var boundary) && boundary == "administrative" ||
            adminLevel != null)
        {
            AttributesTable attributes = NewAttributes();
            if (adminLevel != null &&
                int.TryParse(adminLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var adminLevelValue))
                attributes.Add("admin_level", adminLevelValue);
            if (tags.TryGetValue("disputed", out var disputed) && disputed == "yes")
                attributes.Add("disputed", 1);
            if (tags.TryGetValue("maritime", out var maritime) && maritime == "yes")
                attributes.Add("maritime", 1);

            return [("boundary", attributes)];
        }

        // The tags do not map to a source layer of the schema
        return [];
    }

    /// <summary>
    /// Class of a road in the schema, the raw highway value is kept as subclass.
    /// </summary>
    private static string TransportationClassOf(string highway)
    {
        return highway.Replace("_link", string.Empty, StringComparison.Ordinal) switch
        {
            "motorway" => "motorway",
            "trunk" => "trunk",
            "primary" => "primary",
            "secondary" => "secondary",
            "tertiary" => "tertiary",
            "unclassified" or "residential" or "living_street" or "road" => "minor",
            "service" => "service",
            "track" => "track",
            "footway" or "path" or "cycleway" or "steps" or "pedestrian" or "bridleway" or "corridor" => "path",
            _ => highway,
        };
    }

    /// <summary>
    /// Brunnel attribute of a feature: 'tunnel' or 'bridge', null for normal features.
    /// </summary>
    private static string? Brunnel(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("tunnel", out var tunnel) && tunnel == "yes")
            return "tunnel";
        if (tags.TryGetValue("bridge", out var bridge) && bridge == "yes")
            return "bridge";

        return null;
    }

    /// <summary>
    /// Oneway attribute of a road as used by the MapLibre schema: 1 for a oneway road, -1
    /// for a reversed oneway road, absent for a bidirectional road.
    /// </summary>
    private static void AddOneway(IAttributesTable attributes, Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("oneway", out var oneway))
        {
            if (oneway is "yes" or "true")
                attributes.Add("oneway", 1);
            else if (oneway == "-1")
                attributes.Add("oneway", -1);
        }
    }

    /// <summary>
    /// Add a further attribute from the given tags map to the attributes of the feature, when
    /// the value is present.
    /// </summary>
    private static void AddIfPresent(IAttributesTable attributes, Dictionary<string, string>? tags, string key, string? value = null)
    {
        if (value != null || tags != null && tags.TryGetValue(key, out value))
        {
            if (value != null)
                attributes.Add(key, value);
        }
    }

    /// <summary>
    /// Create an attributes table with one attribute value.
    /// </summary>
    private static AttributesTable NewAttributes(string key, object value)
    {
        var table = new AttributesTable();
        table.Add(key, value);

        return table;
    }

    /// <summary>
    /// Create an attributes table with the given attribute values.
    /// </summary>
    private static AttributesTable NewAttributes(params (string Key, object Value)[] attributes)
    {
        var table = new AttributesTable();
        foreach (var (key, value) in attributes)
        {
            table.Add(key, value);
        }

        return table;
    }
}
