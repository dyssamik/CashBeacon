using Telegram.Bot.Types;

namespace CashBeacon
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            SQLitePCL.Batteries.Init();

            var builder = WebApplication.CreateBuilder(args);

            var appFolder = AppContext.BaseDirectory;
            var dbPath = Path.Combine(appFolder, "CashBeacon.db");

            bool isNewDatabase = !File.Exists(dbPath);

            builder.Services.AddSingleton(provider =>
                new Database(dbPath,
                    provider.GetRequiredService<ILogger<Database>>()));

            var whiteServerBaseUrl = builder.Configuration["WhiteServer:BaseUrl"];
            var whiteServerEnabled = !string.IsNullOrWhiteSpace(whiteServerBaseUrl);

            if (whiteServerEnabled)
            {
                builder.Services.AddSingleton<IWhiteServerFactory>(
                    _ => new WhiteServerFactory(whiteServerBaseUrl!));
            }

            builder.Services.AddSingleton(provider =>
                new Processor(provider.GetRequiredService<ILogger<Processor>>(),
                    provider.GetRequiredService<Database>(),
                    provider.GetService<IWhiteServerFactory>()));

            var tgBots = builder.Configuration.GetSection("Telegram:Bots").GetChildren();
            var tgSecretToTokenMap = new Dictionary<string, string>();

            foreach (var tgBot in tgBots)
            {
                var name = tgBot["Name"];
                var token = tgBot["Token"];

                if (string.IsNullOrWhiteSpace(token))
                    continue;

                var transportSection = tgBot.GetSection("Transport");
                var mode = transportSection["Mode"] ?? "Polling";
                var webhookUrl = transportSection["WebhookUrl"];
                var webhookSecret = transportSection["WebhookSecret"];
                var certPath = transportSection.GetSection("Certificate")["Path"];

                string botKey = token;

                builder.Services.AddKeyedSingleton(botKey, (_, _) => new TelegramClient(token));
                builder.Services.AddKeyedSingleton(botKey, (provider, _) =>
                    new TelegramUpdateHandler(
                        provider.GetRequiredKeyedService<TelegramClient>(botKey),
                        provider.GetRequiredService<Processor>(),
                        provider.GetRequiredService<ILogger<TelegramUpdateHandler>>()));

                if (mode.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(webhookSecret))
                        continue;

                    tgSecretToTokenMap[webhookSecret] = botKey;

                    builder.Services.AddHostedService(provider =>
                    {
                        var handler = provider.GetRequiredKeyedService<TelegramUpdateHandler>(botKey);
                        return new TelegramWebhookService(
                            provider.GetRequiredKeyedService<TelegramClient>(botKey),
                            webhookUrl!,
                            webhookSecret!,
                            certPath,
                            provider.GetRequiredService<ILogger<TelegramWebhookService>>());
                    });
                }
                else
                {
                    builder.Services.AddHostedService(provider =>
                    {
                        var handler = provider.GetRequiredKeyedService<TelegramUpdateHandler>(botKey);
                        return new TelegramPollingService(
                            provider.GetRequiredService<ILogger<TelegramPollingService>>(),
                            provider.GetRequiredKeyedService<TelegramClient>(botKey),
                            handler);
                    });
                }
            }

            var maxBots = builder.Configuration.GetSection("Max:Bots").GetChildren();
            var maxSecretToTokenMap = new Dictionary<string, string>();

            foreach (var maxBot in maxBots)
            {
                var name = maxBot["Name"];
                var token = maxBot["Token"];

                if (string.IsNullOrWhiteSpace(token))
                    continue;

                var transportSection = maxBot.GetSection("Transport");
                var mode = transportSection["Mode"] ?? "Polling";
                var webhookUrl = transportSection["WebhookUrl"];
                var webhookSecret = transportSection["WebhookSecret"];
                var certPath = transportSection.GetSection("Certificate")["Path"];

                string botKey = token;

                builder.Services.AddKeyedSingleton(botKey, (_, _) => new MaxClient(token));
                builder.Services.AddKeyedSingleton(botKey, (provider, _) =>
                    new MaxUpdateHandler(
                        provider.GetRequiredKeyedService<MaxClient>(botKey),
                        provider.GetRequiredService<Processor>(),
                        provider.GetRequiredService<ILogger<MaxUpdateHandler>>()));

                if (mode.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(webhookSecret))
                        continue;

                    maxSecretToTokenMap[webhookSecret] = botKey;

                    builder.Services.AddHostedService(provider =>
                    {
                        return new MaxWebhookService(
                            provider.GetRequiredKeyedService<MaxClient>(botKey),
                            webhookUrl!,
                            webhookSecret!,
                            certPath,
                            provider.GetRequiredService<ILogger<MaxWebhookService>>());
                    });
                }
                else
                {
                    builder.Services.AddHostedService(provider =>
                    {
                        return new MaxPollingService(
                            provider.GetRequiredService<ILogger<MaxPollingService>>(),
                            provider.GetRequiredKeyedService<MaxClient>(botKey),
                            provider.GetRequiredKeyedService<MaxUpdateHandler>(botKey));
                    });
                }
            }

            var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            if (tgBots.Count() == 0)
                logger.LogWarning("Telegram listener disabled: no bot tokens provided");

            app.MapPost("/webhook/telegram", async (HttpContext ctx, CancellationToken ct) =>
            {
                var secret = ctx.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
                if (!tgSecretToTokenMap.TryGetValue(secret, out var botKey)) return Results.Unauthorized();

                var handler = ctx.RequestServices.GetKeyedService<TelegramUpdateHandler>(botKey);
                if (handler is null) return Results.Unauthorized();

                var update = await ctx.Request.ReadFromJsonAsync<Update>(ct);
                if (update is not null) await handler.HandleAsync(update, ct);
                return Results.Ok();
            });

            if (maxBots.Count() == 0)
                logger.LogWarning("Max listener disabled: no bot tokens provided");

            app.MapPost("/webhook/max", async (HttpContext ctx, CancellationToken ct) =>
            {
                var secret = ctx.Request.Headers["X-Max-Bot-Api-Secret"].ToString();
                if (!maxSecretToTokenMap.TryGetValue(secret, out var botKey)) return Results.Unauthorized();

                var handler = ctx.RequestServices.GetKeyedService<MaxUpdateHandler>(botKey);
                if (handler is null) return Results.Unauthorized();

                var update = await ctx.Request.ReadFromJsonAsync<MaxUpdate>(ct);
                if (update is not null) await handler.HandleAsync(update, ct);
                return Results.Ok();
            });

            if (!whiteServerEnabled)
                logger.LogWarning("WhiteServer integration disabled: no base URL provided");

            var db = app.Services.GetRequiredService<Database>();

            await db.InitializeAsync();

            if (isNewDatabase)
            {
                logger.LogInformation("A new database has been created. Skipping integrity check");
            }
            else
            {
                if (!await db.CheckIntegrityAsync())
                {
                    logger.LogCritical("Database integrity check failed. The application will not start");
                    return;
                }
            }

            await app.RunAsync();
        }
    }
}