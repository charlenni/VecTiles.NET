using ProtoBuf;

namespace VecTiles.Converters.Mapbox.Pbf;

/// <summary>
/// Represents a Mapbox vector tile containing one or more layers.
/// </summary>
[ProtoContract(Name = "tile")]
public sealed class PbfTile : IExtensible
{
    /// <summary>
    /// Gets the list of layers contained in this tile.
    /// </summary>
    [ProtoMember(3, Name = "layers", DataFormat = DataFormat.Default)]
    public List<PbfLayer> Layers { get; } = new();

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension? IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
