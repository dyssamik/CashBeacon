using System.Text.Json.Serialization;

namespace CashBeacon;

public class MaxUser
{
    [JsonPropertyName("user_id")] public long UserId { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("first_name")] public string? FirstName { get; init; }
    [JsonPropertyName("last_name")] public string? LastName { get; init; }
    [JsonPropertyName("is_bot")] public bool IsBot { get; init; }
}