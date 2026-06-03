using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxUpdates
{
    [JsonPropertyName("updates")] public List<MaxUpdate> Updates { get; set; } = [];
    [JsonPropertyName("marker")] public long Marker { get; set; }
}