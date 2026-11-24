namespace VecTiles.Tests;

/// <summary>
/// Path to the central OpenMapTiles data Files folder of the repository.
/// All test input data (styles, mbtiles, mapsforge maps, fonts, sprites)
/// lives there and is read directly from the repository, so inputs are
/// kept in a single place instead of being copied into the test output.
/// Test-only data (golden images, results, fixtures) stays in the test
/// projects themselves.
/// </summary>
public static class RepoData
{
    private static readonly Lazy<string> _dataFiles = new(() =>
    {
        // The test host runs with the bin/<configuration>/<tfm> folder as its
        // working directory. Walk up to the repository root instead.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VecTiles.NET.sln")))
        {
            directory = directory.Parent;
        }

        var repoRoot = directory?.FullName
            ?? throw new InvalidOperationException(
                $"Repository root (VecTiles.NET.sln) not found walking up from '{AppContext.BaseDirectory}' " +
                $"(cwd: '{Directory.GetCurrentDirectory()}').");

        var dataFiles = Path.Combine(repoRoot, "Files", "OpenMapTiles");

        if (!File.Exists(Path.Combine(dataFiles, "osm-liberty.json")))
        {
            throw new InvalidOperationException(
                $"Data folder not found in '{repoRoot}'. " +
                $"Expected '{Path.Combine(dataFiles, "osm-liberty.json")}'.");
        }

        return dataFiles;
    });

    /// <summary>
    /// The OpenMapTiles data Files folder of the repository
    /// (styles, mbtiles, mapsforge maps, fonts, sprites).
    /// </summary>
    public static string DataFiles => _dataFiles.Value;
}
