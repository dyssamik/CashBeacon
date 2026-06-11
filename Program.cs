using System.Text.Json;
using Telegram.Bot.Types;

namespace CashBeacon
{
    internal class Program
    {
        private static readonly JsonSerializerOptions CaseInsensitive = new()
        {
            PropertyNameCaseInsensitive = true
        };

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

            var wsBaseUrl = builder.Configuration["WhiteServer:BaseUrl"];

            var whiteServerEnabled = !string.IsNullOrWhiteSpace(wsBaseUrl);

            if (whiteServerEnabled)
            {
                builder.Services.AddSingleton<IWhiteServerFactory>(_ => new WhiteServerFactory(wsBaseUrl!));
                builder.Services.AddSingleton<WhiteServerEventHandler>();
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

                var botKey = token;

                builder.Services.AddKeyedSingleton<TelegramClient>(botKey, (_, _) => new TelegramClient(token, botKey));
                builder.Services.AddSingleton<IPlatformClient>(provider => provider.GetRequiredKeyedService<TelegramClient>(botKey));
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

                var botKey = token;

                builder.Services.AddKeyedSingleton<MaxClient>(botKey, (_, _) => new MaxClient(token, botKey));
                builder.Services.AddSingleton<IPlatformClient>(provider => provider.GetRequiredKeyedService<MaxClient>(botKey));
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

            builder.Services.AddSingleton<IPlatformClientFactory, PlatformClientFactory>();

            var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            if (!tgBots.Any())
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

            if (!maxBots.Any())
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

            if (whiteServerEnabled)
            {
                app.MapPost("/webhook/whiteserver", async (HttpContext ctx, WhiteServerEventHandler handler, CancellationToken ct) =>
                {
                    var signature = ctx.Request.Headers["Signature"].ToString();

                    ctx.Request.EnableBuffering();
                    using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync(ct);
                    ctx.Request.Body.Position = 0;

                    if (!handler.VerifySignature(body, signature))
                    {
                        logger.LogWarning("Invalid WhiteServer signature");
                        return Results.Unauthorized();
                    }

                    var wsEvent = JsonSerializer.Deserialize<WhiteServerEvent>(
                        body,
                        CaseInsensitive);

                    if (wsEvent is not null)
                        await handler.HandleAsync(wsEvent, ct);

                    return Results.Ok();
                });
            }

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