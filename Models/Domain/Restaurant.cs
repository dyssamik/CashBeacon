namespace CashBeacon;

public class Restaurant
{
    public int RestaurantId { get; init; }
    public string Name { get; init; } = "";
    public string Token { get; init; } = "";
    public bool IsSelected { get; init; }
    public DateTime AddedAt { get; init; }
    public DateTime ActiveUntil { get; init; }
}