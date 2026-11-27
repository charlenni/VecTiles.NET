namespace VecTiles.Tests;

/// <summary>
/// Path to the central data Files folders of the repository.
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
                $"Repository root (VecTiles.NET.sln) not found walking up from '{AppContext.BaseDirectory}' "
                + $"(cwd: '{Directory.GetCurrentDirectory()}').");

        var dataFiles = Path.Combine(repoRoot, "Files", "OpenMapTiles");

        if (!File.Exists(Path.Combine(dataFiles, "osm-liberty.json")))
        {
            throw new InvalidOperationException(
                $"Data folder not found in '{repoRoot}'. "
                + $"Expected '{Path.Combine(dataFiles, "osm-liberty.json")}'.");
        }

        return dataFiles;
    });

    private static readonly Lazy<string> _mapsforgeFiles = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VecTiles.NET.sln")))
        {
            directory = directory.Parent;
        }

        var repoRoot = directory?.FullName
            ?? throw new InvalidOperationException(
                $"Repository root (VecTiles.NET.sln) not found walking up from '{AppContext.BaseDirectory}' "
                + $"(cwd: '{Directory.GetCurrentDirectory()}').");

        var dataFiles = Path.Combine(repoRoot, "Files", "Mapsforge");

        if (!File.Exists(Path.Combine(dataFiles, "mapsforge", "default.xml")))
        {
            throw new InvalidOperationException(
                $"Data folder not found in '{repoRoot}'. "
                + $"Expected '{Path.Combine(dataFiles, "mapsforge", "default.xml")}'.");
        }

        return dataFiles;
    });

    /// <summary>
    /// The OpenMapTiles data Files folder of the repository
    /// (styles, mbtiles, mapsforge maps, fonts, sprites).
    /// </summary>
    public static string DataFiles => _dataFiles.Value;

    /// <summary>
    /// The Mapsforge data Files folder of the repository
    /// (render themes, symbols and patterns).
    /// </summary>
    public static string MapsforgeFiles => _mapsforgeFiles.Value;

    private static readonly Lazy<string> _pmTilesFiles = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VecTiles.NET.sln")))
        {
            directory = directory.Parent;
        }

        var repoRoot = directory?.FullName
            ?? throw new InvalidOperationException(
                $"Repository root (VecTiles.NET.sln) not found walking up from '{AppContext.BaseDirectory}' " +
                $"(cwd: '{Directory.GetCurrentDirectory()}').");

        var dataFiles = Path.Combine(repoRoot, "Files", "Protomaps");

        if (!File.Exists(Path.Combine(dataFiles, "zuerich.pmtiles")))
        {
            throw new InvalidOperationException(
                $"Data folder not found in '{repoRoot}'. " +
                $"Expected '{Path.Combine(dataFiles, "zuerich.pmtiles")}'.");
        }

        return dataFiles;
    });

    /// <summary>
    /// The Protomaps data Files folder of the repository
    /// (pmtiles archives and styles).
    /// </summary>
    public static string ProtomapsFiles => _pmTilesFiles.Value;
}
