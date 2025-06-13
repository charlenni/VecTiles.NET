using ProtoBuf;
using System.ComponentModel;

namespace VecTiles.Converters.Mapbox.Pbf;

/// <summary>
/// Represents a Mapbox PBF feature, containing geometry, tags, and an identifier.
/// </summary>
[ProtoContract(Name = "feature")]
public sealed class PbfFeature : IExtensible
{
    /// <summary>
    /// Gets or sets the unique identifier for the feature.
    /// </summary>
    [ProtoMember(1, IsRequired = false, Name = "id", DataFormat = DataFormat.TwosComplement)]
    [DefaultValue(default(ulong))]
    public ulong Id { get; set; }

    /// <summary>
    /// Gets the list of tag key-value indices for the feature.
    /// </summary>
    [ProtoMember(2, Name = "tags", DataFormat = DataFormat.TwosComplement, Options = MemberSerializationOptions.Packed)]
    public List<uint> Tags { get; } = new();

    /// <summary>
    /// Gets or sets the geometry type of the feature.
    /// </summary>
    [ProtoMember(3, IsRequired = false, Name = "type", DataFormat = DataFormat.TwosComplement)]
    [DefaultValue(PbfGeomType.Unknown)]
    public PbfGeomType Type { get; set; } = PbfGeomType.Unknown;

    /// <summary>
    /// Gets the encoded geometry data for the feature.
    /// </summary>
    [ProtoMember(4, Name = "geometry", DataFormat = DataFormat.TwosComplement, Options = MemberSerializationOptions.Packed)]
    public List<uint> Geometry { get; } = new();

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
