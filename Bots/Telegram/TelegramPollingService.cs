using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace CashBeacon;

public class TelegramPollingService : IHostedService
{
    private readonly ILogger<TelegramPollingService> _logger;
    private readonly TelegramClient _client;
    private readonly TelegramUpdateHandler _handler;

    private CancellationTokenSource _cts = new();

    public TelegramPollingService(ILogger<TelegramPollingService> logger, TelegramClient client, TelegramUpdateHandler handler)
    {
        _logger = logger;
        _client = client;
        _handler = handler;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _client.GetMeAsync(cancellationToken);
        _logger.LogInformation("Started polling: \"{Username}\" ({Id})", me.Username, me.Id);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _client.StartReceiving(
            updateHandler: (update, ct) => _handler.HandleAsync(update, ct),
            errorHandler: (ex, ct) =>
            {
                _logger.LogError(ex, "Error while polling Telegram updates");
                return Task.CompletedTask;
            },
            options: new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true
            },
            ct: _cts.Token);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }
}