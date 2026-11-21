using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives;

/// <summary>
/// Unit tests for the <see cref="Color" /> primitive and the <see cref="Colors" /> constants.
/// </summary>
public class ColorTests
{
    /// <summary>
    /// Verifies that the RGBA constructor stores the component values unchanged.
    /// </summary>
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var color = new Color(10, 20, 30, 40);
        Assert.Equal((byte)10, color.R);
        Assert.Equal((byte)20, color.G);
        Assert.Equal((byte)30, color.B);
        Assert.Equal((byte)40, color.A);
    }

    /// <summary>
    /// Verifies that <see cref="Colors.Empty" /> has all components set to zero.
    /// </summary>
    [Fact]
    public void Empty_IsAllZero()
    {
        var color = Colors.Empty;
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)0, color.A);
    }

    /// <summary>
    /// Verifies that <see cref="Colors.Black" /> is opaque black (RGB 0, alpha 255).
    /// </summary>
    [Fact]
    public void Black_IsOpaqueBlack()
    {
        var color = Colors.Black;
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)255, color.A);
    }

    /// <summary>
    /// Verifies that <see cref="Color.ToString" /> returns the "Color [R..;G..;B..;A..]" format.
    /// </summary>
    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var color = new Color(1, 2, 3, 4);
        Assert.Equal("Color [R1;G2;B3;A4]", color.ToString());
    }

    /// <summary>
    /// Verifies that colors with identical component values compare equal.
    /// </summary>
    [Fact]
    public void Equality_WorksForSameValues()
    {
        var c1 = new Color(1, 2, 3, 4);
        var c2 = new Color(1, 2, 3, 4);
        Assert.Equal(c1, c2);
        Assert.True(c1 == c2);
    }

    /// <summary>
    /// Verifies that colors with different component values compare unequal.
    /// </summary>
    [Fact]
    public void Inequality_WorksForDifferentValues()
    {
        var c1 = new Color(1, 2, 3, 4);
        var c2 = new Color(4, 3, 2, 1);
        Assert.NotEqual(c1, c2);
        Assert.True(c1 != c2);
    }
}
