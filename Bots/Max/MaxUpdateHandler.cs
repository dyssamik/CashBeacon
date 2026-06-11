namespace CashBeacon;

public class MaxUpdateHandler
{
    private readonly MaxClient _client;
    private readonly Processor _processor;
    private readonly ILogger<MaxUpdateHandler> _logger;

    public MaxUpdateHandler(MaxClient client, Processor processor, ILogger<MaxUpdateHandler> logger)
    {
        _client = client;
        _processor = processor;
        _logger = logger;
    }

    public async Task HandleAsync(MaxUpdate update, CancellationToken ct = default)
    {
        try
        {
            switch (update.UpdateType)
            {
                case "message_created" when update.Message is { Body.Text: { } text, Sender.UserId: { } chatId }:
                    await OnMessage(chatId, text, ct);
                    break;
                case "message_callback" when update.Callback is { Payload: { } payload, User.UserId: { } callbackChatId }:
                    await OnCallback(callbackChatId, payload, ct);
                    break;
                default:
                    _logger.LogWarning("Received unsupported update type: {UpdateType}", update.UpdateType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MAX update");
        }
    }

    private async Task OnMessage(long chatId, string text, CancellationToken ct)
    {
        var ctx = new BotContext(chatId, Platform.Max, _client.BotKey);

        _logger.LogInformation("Received a {Text} message from {FromId} in chat {ChatId}", text, chatId, chatId);

        var response = await _processor.ProcessAsync(ctx, text, ct);

        await _client.SendResponseAsync(chatId, response, ct);
    }

    private async Task OnCallback(long chatId, string payload, CancellationToken ct)
    {
        var ctx = new BotContext(chatId, Platform.Max, _client.BotKey);

        _logger.LogInformation("Received callback from {ChatId}: {Payload}", chatId, payload);

        var response = await _processor.ProcessCallbackAsync(ctx, payload, ct);

        await _client.SendResponseAsync(chatId, response, ct);
    }
}