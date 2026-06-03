using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxCallback
{
    [JsonPropertyName("callback_id")] public string CallbackId { get; init; } = "";
    [JsonPropertyName("timestamp")] public long Timestamp { get; init; }
    [JsonPropertyName("user")] public MaxUser? User { get; init; }
    [JsonPropertyName("payload")] public string? Payload { get; init; }
    [JsonPropertyName("message")] public MaxMessage? Message { get; init; }
}