using Newtonsoft.Json;

namespace VecTiles.Styles.Mapbox.Json;

/// <summary>
/// Class holding StoppedString data in Json format
/// </summary>
public class JsonStoppedString
{
    [JsonProperty("base")]
    public float Base { get; set; } = 1f;

    [JsonProperty("stops")]
    public IList<KeyValuePair<float, string>> Stops { get; set; } = [];
}
