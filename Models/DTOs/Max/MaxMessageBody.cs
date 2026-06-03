using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxMessageBody
{
    [JsonPropertyName("mid")] public string Mid { get; init; } = "";
    [JsonPropertyName("seq")] public long Seq { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
}