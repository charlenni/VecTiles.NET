using Newtonsoft.Json;

namespace VecTiles.Styles.Mapbox.Json;

/// <summary>
/// Class holding StoppedFloat data in Json format
/// </summary>
public class JsonStoppedFloat
{
    [JsonProperty("base")]
    public float Base { get; set; } = 1f;

    [JsonProperty("stops")]
    public IList<KeyValuePair<float, float>> Stops { get; set; } = [];
}
