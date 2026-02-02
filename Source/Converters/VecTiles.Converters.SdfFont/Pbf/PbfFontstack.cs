using ProtoBuf;
using System.ComponentModel;

namespace VecTiles.Converters.SdfFont.Pbf;

[ProtoContract(Name = "fontstack")]
public sealed class PbfFontstack : IExtensible
{
    /// <summary>
    /// Gets or sets the unique name for the fontstack.
    /// </summary>
    [ProtoMember(1, IsRequired = true, Name = "name")]
    [DefaultValue(default(string))]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique range for the fontstack.
    /// </summary>
    [ProtoMember(2, IsRequired = true, Name = "range")]
    [DefaultValue(default(string))]
    public string Range { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique name for the fontstack.
    /// </summary>
    [ProtoMember(3, IsRequired = true, Name = "glyph")]
    public List<PbfGlyph> Glyph { get; set; } = new();

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension? IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
