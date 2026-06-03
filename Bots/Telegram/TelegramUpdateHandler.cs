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
        var chatId = message.Chat.Id;

        _logger.LogInformation("Received a '{Text}' message from {FromId} in chat {ChatId}", message.Text, message.From?.Id, chatId);

        var response = await _processor.ProcessAsync(chatId, Platform.Telegram, text, ct);

        await _client.SendResponseAsync(chatId, response, ct);
    }

    private async Task OnCallback(CallbackQuery callback, CancellationToken ct)
    {
        var chatId = callback.Message!.Chat.Id;
        var response = await _processor.ProcessCallbackAsync(chatId, Platform.Telegram, callback.Data!, ct);

        await _client.AnswerCallbackAsync(callback.Id, ct: ct);
        await _client.SendResponseAsync(chatId, response, ct);
    }
}