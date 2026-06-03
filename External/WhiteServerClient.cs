using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CashBeacon;

public class WhiteServerClient
{
    private static readonly HttpClient _http = new();

    private readonly string _token;
    private readonly int _restaurantId;
    private readonly string _baseUrl;

    public WhiteServerClient(string token, int restaurantId, string baseUrl)
    {
        _token = token;
        _restaurantId = restaurantId;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    private async Task<JsonElement?> SendAsync(object body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
            request.Headers.Add("AggregatorAuthentication", _token);
            request.Content = JsonContent.Create(body);

            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement?>(cancellationToken: cancellationToken);

            if (json is not JsonElement element || element.ValueKind == JsonValueKind.Undefined)
                return null;

            if (element.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                var errorText = error.ToString();

                if (errorText.Contains("Agent is offline", StringComparison.OrdinalIgnoreCase))
                    throw new WhiteServerOfflineException(errorText);

                throw new WhiteServerException(errorText);
            }

            return json;
        }
        catch (WhiteServerException)
		{
			throw;
		}
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WhiteServerException("WhiteServer error", exception);
        }
    }

    public async Task<JsonElement?> GetAgentInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendAsync(
            new { taskType = "GetAgentInfo", @params = new { sync = new { objectId = _restaurantId } } },
            cancellationToken);

            return response?.GetProperty("taskResponse");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> ExecuteRk7QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var xmlBytes = Encoding.UTF8.GetBytes(query);
        var base64 = Convert.ToBase64String(xmlBytes);

        var payload = new
        {
            taskType = "ExecuteRk7Query",
            @params = new
            {
                sync = new
                {
                    objectId = _restaurantId
                },
                base64
            }
        };

        var response = await SendAsync(payload, cancellationToken);

        if (response is null)
            throw new InvalidOperationException("Failed to execute RK7 query.");

        var base64Response = response.Value.GetProperty("taskResponse").GetProperty("base64").GetString()!;

        if (string.IsNullOrEmpty(base64Response))
            throw new InvalidOperationException("RK7 query returned empty response.");

        var decodedBytes = Convert.FromBase64String(base64Response);
        return Encoding.UTF8.GetString(decodedBytes);
    }

    public async Task<string> GetLayoutAsync(int layoutCode, CancellationToken cancellationToken = default)
    {
        var xml = await ExecuteRk7QueryAsync(Rk7Queries.GetPrintLayout(layoutCode), cancellationToken);
        return Formatter.FormatDocument(xml);
    }
}