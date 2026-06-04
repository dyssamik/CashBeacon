namespace CashBeacon;

public interface IPlatformClientFactory
{
    IPlatformClient? GetClient(Platform platform, string botKey);
}