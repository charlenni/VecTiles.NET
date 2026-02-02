using ProtoBuf;

namespace VecTiles.Converters.SdfFont.Pbf;

[ProtoContract(Name = "glyphs")]
public sealed class PbfGlyphs : IExtensible
{
    /// <summary>
    /// Gets the list of fontstacks for this glyphs.
    /// </summary>
    [ProtoMember(1, IsRequired = true, Name = "stacks")]
    public List<PbfFontstack> Stacks { get; set; } = new();

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
