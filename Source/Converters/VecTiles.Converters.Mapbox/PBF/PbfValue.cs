using ProtoBuf;

namespace VecTiles.Converters.Mapbox.Pbf;

/// <summary>
/// Represents a value in the Mapbox PBF format, supporting multiple types.
/// </summary>
[ProtoContract(Name = @"value")]
public sealed class PbfValue : IExtensible
{
    /// <summary>
    /// Gets or sets the string value.
    /// </summary>
    [ProtoMember(1, IsRequired = false, Name = @"string_value", DataFormat = DataFormat.Default)]
    [System.ComponentModel.DefaultValue("")]
    public string StringValue
    {
        get => _stringValue;
        set
        {
            _stringValue = value;
            HasStringValue = true;
        }
    }

    private string _stringValue = string.Empty;

    /// <summary>
    /// Gets a bool indicating whether the string value has been set.
    /// </summary>
    public bool HasStringValue { get; private set; }

    /// <summary>
    /// Gets or sets the float value.
    /// </summary>
    [ProtoMember(2, IsRequired = false, Name = @"float_value", DataFormat = DataFormat.FixedSize)]
    [System.ComponentModel.DefaultValue(default(float))]
    public float FloatValue
    {
        get => _floatValue;
        set
        {
            _floatValue = value;
            HasFloatValue = true;
        }
    }

    private float _floatValue;

    /// <summary>
    /// Gets a bool indicating whether the float value has been set.
    /// </summary>
    public bool HasFloatValue { get; private set; }

    /// <summary>
    /// Gets or sets the double value.
    /// </summary>
    [ProtoMember(3, IsRequired = false, Name = @"double_value", DataFormat = DataFormat.TwosComplement)]
    [System.ComponentModel.DefaultValue(default(double))]
    public double DoubleValue
    {
        get => _doubleValue;
        set
        {
            _doubleValue = value;
            HasDoubleValue = true;
        }
    }

    private double _doubleValue;

    /// <summary>
    /// Gets a bool indicating whether the double value has been set.
    /// </summary>
    public bool HasDoubleValue { get; private set; }

    /// <summary>
    /// Gets or sets the int value.
    /// </summary>
    [ProtoMember(4, IsRequired = false, Name = @"int_value", DataFormat = DataFormat.TwosComplement)]
    [System.ComponentModel.DefaultValue(default(long))]
    public long IntValue
    {
        get => _intValue;
        set
        {
            _intValue = value;
            HasIntValue = true;
        }
    }

    private long _intValue;

    /// <summary>
    /// Gets a bool indicating whether the int value has been set.
    /// </summary>
    public bool HasIntValue { get; private set; }

    /// <summary>
    /// Gets or sets the unsigned int value.
    /// </summary>
    [ProtoMember(5, IsRequired = false, Name = @"uint_value", DataFormat = DataFormat.TwosComplement)]
    [System.ComponentModel.DefaultValue(default(ulong))]
    public ulong UintValue
    {
        get => _uintValue;
        set
        {
            _uintValue = value;
            HasUIntValue = true;
        }
    }

    private ulong _uintValue;

    /// <summary>
    /// Gets a bool indicating whether the unsigned int value has been set.
    /// </summary>
    public bool HasUIntValue { get; private set; }

    /// <summary>
    /// Gets or sets the signed int value.
    /// </summary>
    [ProtoMember(6, IsRequired = false, Name = @"sint_value", DataFormat = DataFormat.ZigZag)]
    [System.ComponentModel.DefaultValue(default(long))]
    public long SintValue
    {
        get => _sintValue;
        set
        {
            _sintValue = value;
            HasSIntValue = true;
        }
    }

    private long _sintValue;

    /// <summary>
    /// Gets a bool indicating whether the signed int value has been set.
    /// </summary>
    public bool HasSIntValue { get; private set; }

    /// <summary>
    /// Gets or sets the boolean value.
    /// </summary>
    [ProtoMember(7, IsRequired = false, Name = @"bool_value", DataFormat = DataFormat.Default)]
    [System.ComponentModel.DefaultValue(default(bool))]
    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            _boolValue = value;
            HasBoolValue = true;
        }
    }

    private bool _boolValue;

    /// <summary>
    /// Gets a bool indicating whether the boolean value has been set.
    /// </summary>
    public bool HasBoolValue { get; private set; }

    /// <summary>
    /// Determines whether the string value should be serialized.
    /// </summary>
    public bool ShouldSerializeStringValue() => HasStringValue;

    /// <summary>
    /// Determines whether the float value should be serialized.
    /// </summary>
    public bool ShouldSerializeFloatValue() => HasFloatValue;

    /// <summary>
    /// Determines whether the double value should be serialized.
    /// </summary>
    public bool ShouldSerializeDoubleValue() => HasDoubleValue;

    /// <summary>
    /// Determines whether the int value should be serialized.
    /// </summary>
    public bool ShouldSerializeIntValue() => HasIntValue;

    /// <summary>
    /// Determines whether the unsigned int value should be serialized.
    /// </summary>
    public bool ShouldSerializeUIntValue() => HasUIntValue;

    /// <summary>
    /// Determines whether the signed int value should be serialized.
    /// </summary>
    public bool ShouldSerializeSIntValue() => HasSIntValue;

    /// <summary>
    /// Determines whether the boolean value should be serialized.
    /// </summary>
    public bool ShouldSerializeBoolValue() => HasBoolValue;

    private IExtension? _extensionObject;

    /// <summary>
    /// Gets the extension object for protobuf extensibility.
    /// </summary>
    /// <param name="createIfMissing">Whether to create the extension object if it does not exist.</param>
    /// <returns>The extension object.</returns>
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref _extensionObject, createIfMissing);
}
