using System.Text.Json;
using System.Text.Json.Serialization;

namespace CashBeacon;

public class WhiteServerEvent
{
    [JsonPropertyName("responseEventCommon")]
    public WhiteServerEventCommon Common { get; init; } = new();

    [JsonPropertyName("response")]
    public JsonElement Response { get; init; }
}

public class WhiteServerEventCommon
{
    [JsonPropertyName("objectId")]
    public int RestaurantId { get; init; }

    [JsonPropertyName("eventGuid")]
    public string EventGuid { get; init; } = "";

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = "";

    [JsonPropertyName("dateTimeServerReceiveEventFromAgent")]
    public string ReceivedAt { get; init; } = "";
}