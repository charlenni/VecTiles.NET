using ProtoBuf;

namespace VecTiles.Converters.MVT.Pbf;

/// <summary>
/// Represents a MVT vector tile containing one or more layers.
/// </summary>
[ProtoContract(Name = "tile")]
public sealed class PbfTile
{
    /// <summary>
    /// Gets the list of layers contained in this tile.
    /// </summary>
    [ProtoMember(3, Name = "layers", DataFormat = DataFormat.Default)]
    public List<PbfLayer> Layers { get; } = new(16);
}
