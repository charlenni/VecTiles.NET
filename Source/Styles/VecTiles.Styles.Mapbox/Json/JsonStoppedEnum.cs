using Newtonsoft.Json;

namespace VecTiles.Styles.Mapbox.Json;

/// <summary>
/// Class holding StoppedEnum data in Json format
/// </summary>
public class JsonStoppedEnum<T> where T : struct, Enum
{
    [JsonProperty("base")]
    public float Base { get; set; } = 1f;

    [JsonProperty("stops")]
    public IList<KeyValuePair<float, T>> Stops { get; set; } = [];
}
