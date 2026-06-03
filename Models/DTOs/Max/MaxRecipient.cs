using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxRecipient
{
    [JsonPropertyName("chat_id")] public long ChatId { get; init; }
    [JsonPropertyName("chat_type")] public string ChatType { get; init; } = "";
    [JsonPropertyName("user_id")] public long? UserId { get; init; }
}