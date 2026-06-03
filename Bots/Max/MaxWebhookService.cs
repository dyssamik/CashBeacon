namespace CashBeacon;

public class MaxWebhookService : IHostedService
{
    private readonly MaxClient _client;
    private readonly string _webhookUrl;
    private readonly string _secret;
    private readonly string? _certPath;
    private readonly ILogger<MaxWebhookService> _logger;

    public MaxWebhookService(MaxClient client, string webhookUrl, string secret, string? certPath, ILogger<MaxWebhookService> logger)
    {
        _client = client;
        _webhookUrl = webhookUrl;
        _secret = secret;
        _certPath = certPath;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var subscriptions = await _client.GetSubscriptionsAsync(ct);
        bool alreadySubscribed = subscriptions.Any(s => s.Url == _webhookUrl);

        if (alreadySubscribed)
        {
            _logger.LogInformation("Webhook already set → {Url}", _webhookUrl);
            return;
        }

        await _client.SetWebhookAsync(_webhookUrl, _secret, ct);
        _logger.LogInformation("Webhook set → {Url}", _webhookUrl);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _client.DeleteWebhookAsync(ct);
        _logger.LogInformation("Webhook deleted");
    }
}