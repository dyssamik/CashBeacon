using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CashBeacon;

public class WhiteServerEventHandler(Database db, ILogger<WhiteServerEventHandler> logger, IPlatformClientFactory factory)
{
    private const string WsSalt = "19eb62c0-42bb-413c-8e14-298ca54fdb6d";

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private readonly Database _db = db;
    private readonly ILogger<WhiteServerEventHandler> _logger = logger;
    private readonly IPlatformClientFactory _factory = factory;

    public bool VerifySignature(string body, string signature)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body + WsSalt);
            var hash = SHA256.HashData(bytes);
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
        var isNew = await _db.RegisterWhiteServerEventAsync(wsEvent);

        if (!isNew)
        {
            _logger.LogInformation("Duplicate WS event: {Json}",
                JsonSerializer.Serialize(wsEvent, IndentedOptions));
            return;
        }

        _logger.LogInformation("Received WS event: {Json}",
            JsonSerializer.Serialize(wsEvent, IndentedOptions));

        var chats = await _db.GetChatsByRestaurantAsync(wsEvent.Common.RestaurantId);

        foreach (var chat in chats)
        {
            var client = _factory.GetClient(chat.Platform, chat.BotKey);

            if (client is null)
            {
                _logger.LogWarning("No client for platform {Platform} botKey {BotKey}",
                    chat.Platform, chat.BotKey);
                continue;
            }

            var response = new BotResponse($"📢 Событие от ресторана {wsEvent.Common.RestaurantId}:\n{wsEvent.Common.EventType}");
            await client.SendResponseAsync(chat.ChatId, response, ct);
        }
    }
}