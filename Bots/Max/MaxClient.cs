using System.Text.Json;

namespace CashBeacon;

public class MaxClient : IPlatformClient
{
    private readonly HttpClient _http = new();
    private readonly string _token;
    private const string BaseUrl = "https://platform-api.max.ru";

    public string BotKey { get; }
    public Platform Platform => Platform.Max;

    public MaxClient(string token, string botKey)
    {
        _token = token;
        BotKey = botKey;

        _http.DefaultRequestHeaders.Add("Authorization", _token);
    }

    public async Task<MaxUser> GetMeAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/me", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MaxUser>(json)! ?? throw new Exception("Failed to parse MAX response");
    }

    public async Task<MaxUpdates> GetUpdatesAsync(long? marker, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/updates?timeout=30" + (marker.HasValue ? $"&marker={marker.Value}" : "");
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MaxUpdates>(json)! ?? throw new Exception("Failed to parse MAX response");
    }

    public async Task SetWebhookAsync(string url, string secret, CancellationToken ct = default)
    {
        var payload = new
        {
            url = url,
            update_types = new[] { "message_created", "message_callback" },
            secret = secret
        };

        var response = await _http.PostAsJsonAsync($"{BaseUrl}/subscriptions", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<MaxSubscription>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/subscriptions", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<MaxSubscription>>(json) ?? new List<MaxSubscription>();
    }

    public async Task DeleteWebhookAsync(CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{BaseUrl}/subscriptions", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendResponseAsync(long chatId, BotResponse response, CancellationToken ct = default)
    {
        object payload;

        if (response.Buttons is { Count: > 0 })
        {
            var buttons = response.Buttons
                .Select(b => new[]
                {
                new { type = "callback", text = b.Label, payload = b.CallbackData }
                })
                .ToArray();

            payload = new
            {
                text = response.Text,
                attachments = new[]
                {
                new
                {
                    type    = "inline_keyboard",
                    payload = new { buttons }
                }
            }
            };
        }
        else
        {
            payload = response.IsMonospace
                ? new { text = response.Text, format = "html" }
                : (object)new { text = response.Text };
        }

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var httpResponse = await _http.PostAsync(
            $"{BaseUrl}/messages?user_id={chatId}", content, ct);

        httpResponse.EnsureSuccessStatusCode();
    }
}