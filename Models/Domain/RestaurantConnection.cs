namespace CashBeacon;

public class RestaurantConnection
{
    public int Id { get; init; }
    public int RestaurantId { get; init; }
    public long ChatId { get; init; }
    public string Token { get; init; } = "";
    public bool IsSelected { get; init; }
}