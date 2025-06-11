using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives;

public class ColorTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var color = new Color(10, 20, 30, 40);
        Assert.Equal((byte)10, color.R);
        Assert.Equal((byte)20, color.G);
        Assert.Equal((byte)30, color.B);
        Assert.Equal((byte)40, color.A);
    }

    [Fact]
    public void Empty_IsAllZero()
    {
        var color = Color.Empty;
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)0, color.A);
    }

    [Fact]
    public void Black_IsOpaqueBlack()
    {
        var color = Color.Black;
        Assert.Equal((byte)0, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var color = new Color(1, 2, 3, 4);
        Assert.Equal("Color [R1;G2;B3;A4]", color.ToString());
    }

    [Fact]
    public void Equality_WorksForSameValues()
    {
        var c1 = new Color(1, 2, 3, 4);
        var c2 = new Color(1, 2, 3, 4);
        Assert.Equal(c1, c2);
        Assert.True(c1 == c2);
    }

    [Fact]
    public void Inequality_WorksForDifferentValues()
    {
        var c1 = new Color(1, 2, 3, 4);
        var c2 = new Color(4, 3, 2, 1);
        Assert.NotEqual(c1, c2);
        Assert.True(c1 != c2);
    }
}
