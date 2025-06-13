using ProtoBuf;

namespace VecTiles.Converters.Mapbox.Pbf;

/// <summary>
/// Represents the geometry type for Mapbox PBF features.
/// </summary>
[ProtoContract(Name = "GeomType")]
public enum PbfGeomType
{
    /// <summary>
    /// Unknown geometry type.
    /// </summary>
    [ProtoEnum(Name = "Unknown")]
    Unknown = 0,

    /// <summary>
    /// Point geometry type.
    /// </summary>
    [ProtoEnum(Name = "Point")]
    Point = 1,

    /// <summary>
    /// LineString geometry type.
    /// </summary>
    [ProtoEnum(Name = "LineString")]
    LineString = 2,

    /// <summary>
    /// Polygon geometry type.
    /// </summary>
    [ProtoEnum(Name = "Polygon")]
    Polygon = 3
}
