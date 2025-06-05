using Newtonsoft.Json;
public class Item
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }
}