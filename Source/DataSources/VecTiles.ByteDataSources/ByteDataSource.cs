// Copyright (c) The Mapsui authors.
// The Mapsui authors licensed this file under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using VecTiles.DataSources.Extensions;

namespace VecTiles.DataSources;

/// <summary>
/// Get binary data, that isn't organized in tiles, from different sources as byte array
/// </summary>
public static class ByteDataSource
{
    public const string EmbeddedScheme = "embedded";
    public const string FileScheme = "file";
    public const string HttpScheme = "http";
    public const string HttpsScheme = "https";
    public const string SvgContentScheme = "svg-content";
    public const string Base64ContentScheme = "base64-content";

    private static ConcurrentDictionary<string, Assembly>? _embeddedResources;

    // Shared client: a new HttpClient per call exhausts sockets under load.
    // Timeout guards against hanging requests to unresponsive servers.
    private static readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Get binary data asynchron from a source in URL notation
    /// </summary>
    /// <param name="source">Source as URL string</param>
    /// <returns>Binary data as byte array</returns>
    /// <exception cref="ArgumentException"></exception>
    public static async Task<byte[]> GetBytesAsync(string source)
    {
        // Uri has a limitation of ~2000 bytes for URLs, so extract the scheme by hand
        var scheme = source.Substring(0, source.IndexOf(':'));

        return scheme switch
        {
            EmbeddedScheme => LoadEmbeddedResourceFromPath(new Uri(source)),
            FileScheme => LoadFromFileSystem(source),
            HttpScheme or HttpsScheme => await LoadFromUrlAsync(new Uri(source)).ConfigureAwait(false),
            SvgContentScheme => LoadFromSvg(source.Substring(SvgContentScheme.Length + 3)),
            Base64ContentScheme => LoadFromBase64(source.Substring(Base64ContentScheme.Length + 3)),
            _ => throw new ArgumentException($"Scheme '{scheme}' of '{source}' is not supported"),
        };
    }

    private static byte[] LoadEmbeddedResourceFromPath(Uri source)
    {
        var sourceName = source.AbsoluteUri.Substring(EmbeddedScheme.Length + 3).Replace("/", ".");

        try
        {
            if (_embeddedResources is null)
                _embeddedResources = LoadEmbeddedResourcePaths();

            Assembly? assembly = null;

            foreach (var resource in _embeddedResources)
            {
                if (resource.Key.EndsWith(sourceName, StringComparison.CurrentCulture))
                {
                    assembly = resource.Value;
                    string[] resourceNames = assembly.GetManifestResourceNames();
                    var matchingResourceName = resourceNames.FirstOrDefault(r => r.EndsWith(sourceName, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingResourceName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(matchingResourceName)
                            ?? throw new Exception($"The resource name was found but GetManifestResourceStream returned null: '{source}'");
                        return stream.ToBytes();
                    }
                }
            }

            if (assembly != null || _embeddedResources.TryGetValue(source.Host, out assembly))
            {
                string[] resourceNames = assembly.GetManifestResourceNames();
                var matchingResourceName = resourceNames.FirstOrDefault(r => r.Equals(source.Host, StringComparison.InvariantCultureIgnoreCase));
                if (matchingResourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(matchingResourceName)
                        ?? throw new Exception($"The resource name was found but GetManifestResourceStream returned null: '{source}'");
                    return stream.ToBytes();
                }
            }

            var allResourceNames = _embeddedResources.Keys.ToList();
            string listOfEmbeddedResources = string.Concat(allResourceNames.Select(n => '\n' + n)); // All resources should be on a new line.
            throw new Exception($"Could not find the embedded resource in the current assemblies. ImageSource: '{source}'. Other embedded resources in matching assemblies: {listOfEmbeddedResources}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not load embedded resource '{source}' : '{ex.Message}'", ex);
        }
    }

    private static ConcurrentDictionary<string, Assembly> LoadEmbeddedResourcePaths()
    {
        var result = new ConcurrentDictionary<string, Assembly>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);

        foreach (var assembly in assemblies)
        {
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                if (!resourceName.EndsWith(".resources", StringComparison.InvariantCulture))
                    result.AddOrUpdate(resourceName.ToLower(CultureInfo.CurrentCulture), (r) => assembly, (r, a) => assembly);
            }
        }
        return result;
    }

    private async static Task<byte[]> LoadFromUrlAsync(Uri imageSource)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(imageSource, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is unsuccessful
            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not load resource from url '{imageSource}' : '{ex.Message}'", ex);
        }
    }

    private static byte[] LoadFromFileSystem(string imageSource)
    {
        if (!imageSource.StartsWith("file://", StringComparison.InvariantCulture))
        {
            throw new ArgumentException($"Invalid image path '{imageSource}'", nameof(imageSource));    
        }
        
        try
        {
            return File.ReadAllBytes(imageSource.Substring(7));
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not load resource from file '{imageSource}' : '{ex.Message}'", ex);
        }
    }

    private static byte[] LoadFromSvg(string imageSource)
    {
        try
        {
            return Encoding.UTF8.GetBytes(imageSource);
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not load svg from string '{imageSource}' : '{ex.Message}'", ex);
        }
    }

    private static byte[] LoadFromBase64(string imageSource)
    {
        try
        {
            return Convert.FromBase64String(imageSource);
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not load resource from base64 encoded string '{imageSource}' : '{ex.Message}'", ex);
        }
    }
}
