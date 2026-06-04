using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CashBeacon;

public class TelegramUpdateHandler : IPlatformUpdateHandler<Update>
{
    private readonly TelegramClient _client;
    private readonly Processor _processor;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(TelegramClient client, Processor processor, ILogger<TelegramUpdateHandler> logger)
    {
        _client = client;
        _processor = processor;
        _logger = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken ct = default)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message when update.Message is { Text: { } text } message:
                    await OnMessage(message, text, ct);
                    break;
                case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
                    await OnCallback(callback, ct);
                    break;
                default:
                    _logger.LogWarning("Received unsupported update type: {UpdateType}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task OnMessage(Message message, string text, CancellationToken ct)
    {
        var ctx = new BotContext(message.Chat.Id, Platform.Telegram, _client.BotKey);

        _logger.LogInformation("Received a '{Text}' message from {FromId} in chat {ChatId}", message.Text, message.From?.Id, ctx.ChatId);

        var response = await _processor.ProcessAsync(ctx, text, ct);

        await _client.SendResponseAsync(ctx.ChatId, response, ct);
    }

    private async Task OnCallback(CallbackQuery callback, CancellationToken ct)
    {
        var ctx = new BotContext(callback.Message!.Chat.Id, Platform.Telegram, _client.BotKey);
        var response = await _processor.ProcessCallbackAsync(ctx, callback.Data!, ct);

        await _client.AnswerCallbackAsync(callback.Id, ct: ct);
        await _client.SendResponseAsync(ctx.ChatId, response, ct);
    }
}