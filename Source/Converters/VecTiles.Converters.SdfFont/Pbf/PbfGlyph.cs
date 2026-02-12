using ProtoBuf;

namespace VecTiles.Converters.SdfFont.Pbf;

[ProtoContract(Name = "glyph")]
public sealed class PbfGlyph : IExtensible
{
    /// <summary>
    /// Gets or sets the unique identifier for the glyph.
    /// </summary>
    [ProtoMember(1, IsRequired = true, Name = "id")]
    public uint Id { get; set; }

    /// <summary>
    /// Gets or sets the bitmap for the glyph.
    /// </summary>
    /// <remarks>
    /// A signed distance field of the glyph with a border of 3 pixels.
    /// </remarks>
    [ProtoMember(2, IsRequired = false, Name = "bitmap")]
    public byte[] Bitmap { get; set; } = { };

    /// <summary>
    /// Gets or sets the width for the glyph.
    /// </summary>
    [ProtoMember(3, IsRequired = true, Name = "width")]
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the height for the glyph.
    /// </summary>
    [ProtoMember(4, IsRequired = true, Name = "height")]
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the left position for the glyph as signed value.
    /// </summary>
    [ProtoMember(5, IsRequired = true, Name = "left")]
    public int Left { get; set; }

    /// <summary>
    /// Gets or sets the top position for the glyph as signed value.
    /// </summary>
    [ProtoMember(6, IsRequired = true, Name = "top")]
    public int Top { get; set; }

    /// <summary>
    /// Gets or sets the advance (distance to next glyph) for the glyph.
    /// </summary>
    [ProtoMember(7, IsRequired = true, Name = "advance")]
    public int Advance { get; set; }

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);

}
