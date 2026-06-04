using CashBeacon;

public interface IPlatformClient
{
    Platform Platform { get; }
    string BotKey { get; }
    Task SendResponseAsync(long chatId, BotResponse response, CancellationToken ct = default);
}