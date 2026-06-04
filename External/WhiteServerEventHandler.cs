using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CashBeacon;

public class WhiteServerEventHandler
{
    public readonly Database _db;
    public readonly ILogger<WhiteServerEventHandler> _logger;

    private readonly IPlatformClientFactory _factory;

    public WhiteServerEventHandler(Database db, ILogger<WhiteServerEventHandler> logger, IPlatformClientFactory factory)
    {
        _db = db;
        _logger = logger;
        _factory = factory;
    }

    public bool VerifySignature(byte[] body, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(body);
            var received = Convert.FromBase64String(signature);

            return CryptographicOperations.FixedTimeEquals(hash, received);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task HandleAsync(WhiteServerEvent wsEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Received WS event: {Json}",
            JsonSerializer.Serialize(wsEvent, new JsonSerializerOptions { WriteIndented = true }));

        var chats = await _db.GetChatsByRestaurantAsync(wsEvent.Common.RestaurantId);

        foreach (var chat in chats)
        {
            var client = _factory.GetClient(chat.Platform, chat.BotKey);

            if (client != null)
            {
                var response = new BotResponse($"📢 Событие от ресторана {wsEvent.Common.RestaurantId}:\n{wsEvent.Common.EventType}");
                await client.SendResponseAsync(chat.ChatId, response, ct);
            }
        }
    }
}