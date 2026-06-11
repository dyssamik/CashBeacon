namespace CashBeacon;

public class MaxPollingService : BackgroundService
{
    private readonly ILogger<MaxPollingService> _logger;
    private readonly MaxClient _client;
    private readonly MaxUpdateHandler _handler;

    public MaxPollingService(ILogger<MaxPollingService> logger, MaxClient client, MaxUpdateHandler handler)
    {
        _logger = logger;
        _client = client;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        long? marker = null;
        var me = await _client.GetMeAsync(ct);
        _logger.LogInformation("Started polling: \"{Name}\" ({Id})", me.Name, me.UserId) ;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await _client.GetUpdatesAsync(marker, ct);
                marker = updates.Marker;
                foreach (var update in updates.Updates)
                {
                    await _handler.HandleAsync(update, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while polling MAX updates");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}