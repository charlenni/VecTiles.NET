using Newtonsoft.Json;

namespace VecTiles.Styles.OpenMapTiles.Json;

/// <summary>
/// Class holding StoppedBoolean data in Json format
/// </summary>
public class JsonStoppedBoolean
{
    [JsonProperty("stops")]
    public IList<KeyValuePair<float, bool>> Stops { get; set; } = [];
}
