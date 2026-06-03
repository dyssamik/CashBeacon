using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxMessage
{
    [JsonPropertyName("recipient")] public MaxRecipient Recipient { get; init; } = new();
    [JsonPropertyName("sender")] public MaxUser? Sender { get; init; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; init; }
    [JsonPropertyName("body")] public MaxMessageBody Body { get; init; } = new();
}