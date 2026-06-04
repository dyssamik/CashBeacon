namespace CashBeacon;

public class PlatformClientFactory : IPlatformClientFactory
{
    private readonly Dictionary<PlatformClientKey, IPlatformClient> _clients;

    public PlatformClientFactory(IEnumerable<IPlatformClient> clients)
    {
        _clients = clients.ToDictionary(c => new PlatformClientKey(c.Platform, c.BotKey));
    }

    public IPlatformClient? GetClient(Platform platform, string botKey)
    {
        _clients.TryGetValue(new PlatformClientKey(platform, botKey), out var client);

        return client;
    }
}