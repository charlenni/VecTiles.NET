using System.ComponentModel;
using ProtoBuf;

namespace VecTiles.Converters.Mapbox.Pbf;

/// <summary>
/// Represents a Mapbox PBF layer, containing features, keys, and values.
/// </summary>
[ProtoContract(Name = "layer")]
public sealed class PbfLayer : IExtensible
{
    /// <summary>
    /// Gets or sets the version of the layer.
    /// </summary>
    [ProtoMember(15, IsRequired = true, Name = "version", DataFormat = DataFormat.Default)]
    public uint Version { get; set; }

    /// <summary>
    /// Gets or sets the name of the layer.
    /// </summary>
    [ProtoMember(1, IsRequired = true, Name = "name", DataFormat = DataFormat.Default)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of features in the layer.
    /// </summary>
    [ProtoMember(2, Name = "features", DataFormat = DataFormat.Default)]
    public List<PbfFeature> Features { get; } = new();

    /// <summary>
    /// Gets the list of keys used in the layer.
    /// </summary>
    [ProtoMember(3, Name = "keys", DataFormat = DataFormat.Default)]
    public List<string> Keys { get; } = new();

    /// <summary>
    /// Gets the list of values used in the layer.
    /// </summary>
    [ProtoMember(4, Name = "values", DataFormat = DataFormat.Default)]
    public List<PbfValue> Values { get; } = new();

    /// <summary>
    /// Gets or sets the extent of the layer. Default is 4096.
    /// </summary>
    [ProtoMember(5, IsRequired = false, Name = "extent", DataFormat = DataFormat.TwosComplement)]
    [DefaultValue((uint)4096)]
    public uint Extent { get; set; } = 4096;

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension? IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
