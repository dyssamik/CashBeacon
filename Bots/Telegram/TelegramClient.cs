using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace CashBeacon;

public class TelegramClient : IPlatformClient
{
    private readonly ITelegramBotClient _bot;

    public string BotKey { get; }

    public Platform Platform => Platform.Telegram;

    public TelegramClient(string token, string botKey)
    {
        _bot = new TelegramBotClient(token);
        BotKey = botKey;
    }

    public void StartReceiving(
        Func<Update, CancellationToken, Task> updateHandler,
        Func<Exception, CancellationToken, Task> errorHandler,
        ReceiverOptions options,
        CancellationToken ct)
        => _bot.StartReceiving(
            (_, update, ct) => updateHandler(update, ct),
            (_, ex, ct) => errorHandler(ex, ct),
            options,
            ct);

    public Task<WebhookInfo> GetWebhookInfoAsync(CancellationToken ct = default)
    => _bot.GetWebhookInfo(ct);

    public async Task SetWebhookAsync(string url, string secret, string? certPath, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
        {
            await using var stream = File.OpenRead(certPath);
            var cert = new InputFileStream(stream, "certificate.pem");
            await _bot.SetWebhook(url, certificate: cert, secretToken: secret, cancellationToken: ct);
        }
        else
        {
            await _bot.SetWebhook(url, secretToken: secret, cancellationToken: ct);
        }
    }

    public Task DeleteWebhookAsync(CancellationToken ct = default)
    => _bot.DeleteWebhook(cancellationToken: ct);

    public Task<User> GetMeAsync(CancellationToken ct = default)  => _bot.GetMe(ct);

    public Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default)
        => _bot.AnswerCallbackQuery(callbackId, cancellationToken: ct);

    public async Task SendResponseAsync(long chatId, BotResponse response, CancellationToken ct = default)
    {
        var parseMode = response.IsMonospace ? ParseMode.Html : ParseMode.None;

        if (response.Buttons is { Count: > 0 })
        {
            var keyboard = new InlineKeyboardMarkup(
                response.Buttons.Select(b =>
                    new[] { InlineKeyboardButton.WithCallbackData(b.Label, b.CallbackData) }));

            await _bot.SendMessage(chatId, response.Text, parseMode: parseMode, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId, response.Text, parseMode: parseMode, cancellationToken: ct);
        }
    }
}