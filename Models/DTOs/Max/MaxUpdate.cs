using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxUpdate
{
    [JsonPropertyName("update_type")] public string UpdateType { get; init; } = "";
    [JsonPropertyName("timestamp")] public long Timestamp { get; init; }
    [JsonPropertyName("message")] public MaxMessage? Message { get; init; }
    [JsonPropertyName("callback")] public MaxCallback? Callback { get; init; }
    [JsonPropertyName("user")] public MaxUser? User { get; init; }
    [JsonPropertyName("chat_id")] public long? ChatId { get; init; }
    [JsonPropertyName("user_locale")] public string? UserLocale { get; init; }
}