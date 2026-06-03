using Microsoft.Extensions.Logging;

namespace CashBeacon;

public class Processor
{
    private readonly ILogger<Processor> _logger;
    private readonly Database _db;
    private readonly IWhiteServerFactory? _wsFactory;

    public Processor(ILogger<Processor> logger, Database db, IWhiteServerFactory? wsFactory = null)
    {
        _logger = logger;
        _db = db;
        _wsFactory = wsFactory;
    }

    public async Task<BotResponse> ProcessAsync(long chatId, Platform platform, string text, CancellationToken cancellationToken = default)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if ( parts.Length == 0 ) return new BotResponse("Пустая команда");

        const string commandsList = """
        Доступные команды:
        /help - Показать это сообщение
        /register <id> [название] <токен> - Зарегистрировать ресторан
        /unregister <id> - Удалить ресторан
        /restaurants - Показать подключенные рестораны
        /report <код> - Получить отчёт по коду макета

        В угловых скобках <> указаны обязательные параметры, в квадратных [] - необязательные
        """;

        return parts[0].ToLowerInvariant() switch
        {
            "/start" or "/help" => new BotResponse(commandsList),
            "/register" when parts.Length >= 3 && int.TryParse(parts[1], out var registerId) =>
                new BotResponse(await RegisterRestaurantAsync(
                    chatId, platform, registerId,
                    restaurantName: parts.Length > 3 ? string.Join(" ", parts[2..^1]) : parts[1],
                    token: parts[^1])),

            "/register" => new BotResponse("Использование: /register <id> [название] <токен>"),
            "/unregister" when parts.Length >= 2 && int.TryParse(parts[1], out var unregisterId) =>
                new BotResponse(await UnregisterRestaurantAsync(chatId, platform, unregisterId)),
            "/unregister" => new BotResponse("Использование: /unregister <id>"),
            "/restaurants" => await GetRestaurantsAsync(chatId, platform),
            "/report" when parts.Length >= 2 && int.TryParse(parts[1], out var layoutCode) =>
                await GetReportAsync(chatId, platform, layoutCode, cancellationToken),
            "/report" => new BotResponse("Использование: /report <код>"),
            _ => new BotResponse("Неизвестная команда")
        };
    }

    public async Task<BotResponse> ProcessCallbackAsync(long chatId, Platform platform, string callbackData, CancellationToken cancellationToken = default)
    {
        var parts = callbackData.Split(':');

        return parts[0] switch
        {
            "select" when int.TryParse(parts[1], out var restaurantId) =>
                await SelectRestaurantAsync(chatId, platform, restaurantId),
            _ => new BotResponse("Неизвестное действие")
        };
    }

    private async Task<(WhiteServerClient? Client, BotResponse? Error)> GetWhiteServerClientAsync(long chatId, Platform platform)
    {
        if (_wsFactory is null)
            return (null, new BotResponse("❌ WhiteServer отключён: BaseUrl не настроен."));

        var restaurant = await _db.GetSelectedRestaurantAsync(chatId, platform);
        if (restaurant is null)
            return (null, new BotResponse("❌ Не выбран ресторан. Используйте /restaurants для выбора."));

		return (_wsFactory.Create(restaurant.Token, restaurant.RestaurantId), null);
    }

    private async Task<BotResponse> CallWhiteServerAsync(
        long chatId,
        Platform platform,
        bool isMonospace,
        Func<WhiteServerClient, Task<string>> action,
        CancellationToken cancellationToken)
    {
		var (client, error) = await GetWhiteServerClientAsync(chatId, platform);
		if (error is not null) return error;
		try
		{
			var result = await action(client!);
			return new BotResponse(result, IsMonospace: isMonospace);
		}
        catch (WhiteServerOfflineException exception)
        {
			_logger.LogWarning(exception, "Agent offline for chat {ChatId}", chatId);
			return new BotResponse("⚠️ Агент WS не отвечает. Проверьте работу устройства и интернета");
		}
        catch (WhiteServerException exception)
        {
			_logger.LogError(exception, "WhiteServer error for chat {ChatId}: {Message}", chatId, exception.Message);
			return new BotResponse($"⚠️ Ошибка WhiteServer: {exception.Message}");
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Unexpected error in WhiteServer call for chat {ChatId}: {Message}", chatId, exception.Message);
			return new BotResponse("⚠️ Произошла внутренняя ошибка. Попробуйте позже");
		}
	}

    private async Task<string> RegisterRestaurantAsync(long chatId, Platform platform, int restaurantId, string restaurantName, string token)
    {
        var result = await _db.RegisterRestaurantAsync(chatId, platform, restaurantId, restaurantName, token);
        return result switch
        {
            RegistrationResult.Added => "Ресторан успешно зарегистрирован",
            RegistrationResult.Updated => "Информация о ресторане обновлена",
            RegistrationResult.Failed => "Произошла ошибка при регистрации ресторана",
            _ => "Неизвестный результат регистрации"
        };
    }

    private async Task<string> UnregisterRestaurantAsync(long chatId, Platform platform, int restaurantId)
    {
        var success = await _db.UnregisterRestaurantAsync(chatId, platform, restaurantId);
        return success ? "Ресторан успешно удалён" : "Не удалось удалить ресторан";
    }

    private async Task<BotResponse> GetRestaurantsAsync(long chatId, Platform platform)
    {
        var restaurants = (await _db.GetRestaurantsAsync(chatId, platform)).ToList();

        if (restaurants.Count == 0)
            return new BotResponse("Ни один ресторан не подключен. Введите /register для регистрации");

        var text = "Подключенные рестораны:\n" + string.Join("\n", restaurants.Select(r =>
            $"{(r.IsSelected ? "✅" : "•")} {r.Name} (до {r.ActiveUntil:dd.MM.yyyy})"));

        var buttons = restaurants
            .Select(r => new InlineButton(
                $"{(r.IsSelected ? "✅ " : "")}{r.Name}",
                $"select:{r.RestaurantId}"))
            .ToList();

        return new BotResponse(text, buttons);
    }

    private async Task<BotResponse> SelectRestaurantAsync(long chatId, Platform platform, int restaurantId)
    {
        var restaurant = await _db.SetSelectedAsync(chatId, platform, restaurantId);
        return restaurant is null
            ? new BotResponse("❌ Не удалось выбрать ресторан")
            : new BotResponse($"✅ Выбран ресторан: {restaurant.Name}");
    }

    private async Task<BotResponse> GetReportAsync(long chatId, Platform platform, int layoutCode, CancellationToken cancellationToken)
    {
        return await CallWhiteServerAsync(chatId, platform, true,
			async client => await client.GetLayoutAsync(layoutCode, cancellationToken), cancellationToken);
	}
}