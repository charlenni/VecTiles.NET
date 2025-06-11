using System.Reflection;
using System.Text;

namespace VecTiles.DataSources.Tests;

public class ByteDataSourceTests
{
    [Fact]
    public async Task LoadFromSvgContentScheme_ReturnsUtf8Bytes()
    {
        // Arrange
        var svg = "<svg></svg>";
        var source = $"{ByteDataSource.SvgContentScheme}://{svg}";

        // Act
        var result = await ByteDataSource.GetBytesAsync(source);

        // Assert
        Assert.Equal(Encoding.UTF8.GetBytes(svg), result);
    }

    [Fact]
    public async Task LoadFromBase64ContentScheme_ReturnsDecodedBytes()
    {
        // Arrange
        var data = "Hello, world!";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
        var source = $"{ByteDataSource.Base64ContentScheme}://{base64}";

        // Act
        var result = await ByteDataSource.GetBytesAsync(source);

        // Assert
        Assert.Equal(Encoding.UTF8.GetBytes(data), result);
    }

    [Fact]
    public async Task LoadFromFileScheme_ReturnsFileBytes()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(tempFile, content);
        var source = $"{ByteDataSource.FileScheme}://{tempFile.Replace("\\", "/")}";

        try
        {
            // Act
            var result = await ByteDataSource.GetBytesAsync(source);

            // Assert
            Assert.Equal(content, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetBytesAsync_UnsupportedScheme_ThrowsArgumentException()
    {
        // Arrange
        var source = @"unsupported://data";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => ByteDataSource.GetBytesAsync(source));
    }

    [Fact]
    public void LoadFromEmbeddedScheme_ResourceNotFound_ThrowsException()
    {
        // Arrange
        var source = $"{ByteDataSource.EmbeddedScheme}://nonexistent.resource";

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() => typeof(ByteDataSource)
                .GetMethod("LoadEmbeddedResourceFromPath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { new Uri(source) })
        );
        Assert.Contains("Could not load embedded resource", ex.InnerException?.Message);
    }
}
