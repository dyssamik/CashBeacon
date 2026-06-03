namespace CashBeacon;

public class TelegramWebhookService : IHostedService
{
    private readonly TelegramClient _client;
    private readonly string _webhookUrl;
    private readonly string _secret;
    private readonly string? _certPath;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        TelegramClient client,
        string webhookUrl,
        string secret,
        string? certPath,
        ILogger<TelegramWebhookService> logger)
    {
        _client = client;
        _webhookUrl = webhookUrl;
        _secret = secret;
        _certPath = certPath;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var info = await _client.GetWebhookInfoAsync(ct);
        if (info.Url == _webhookUrl)
        {
            _logger.LogInformation("Webhook already set → {Url}", _webhookUrl);
            return;
        }

        await _client.SetWebhookAsync(_webhookUrl, _secret, _certPath, ct);
        _logger.LogInformation("Webhook set → {Url}", _webhookUrl);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _client.DeleteWebhookAsync(ct);
        _logger.LogInformation("Webhook deleted");
    }
}