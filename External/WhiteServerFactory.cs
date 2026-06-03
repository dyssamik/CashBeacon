namespace CashBeacon;

public interface IWhiteServerFactory
{
	WhiteServerClient Create(string token, int restaurantId);
}

public class WhiteServerFactory : IWhiteServerFactory
{
	private readonly string _baseUrl;

	public WhiteServerFactory(string baseUrl) => _baseUrl = baseUrl;

	public WhiteServerClient Create(string token, int restaurantId) => new(token, restaurantId, _baseUrl);
}