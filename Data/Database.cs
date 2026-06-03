using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CashBeacon;

public class Database
{
    private readonly string _connectionString;
    private readonly ILogger<Database> _logger;

    public Database(string dbPath, ILogger<Database> logger)
    {
		_connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true
			}.ToString();

        _logger = logger;
    }

	private async Task<SqliteConnection> ConnectAsync()
	{
		var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync();
		return connection;
	}

	public async Task InitializeAsync()
    {
        using var connection = await ConnectAsync();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Restaurants (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RestaurantId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                AddedAt DATETIME NOT NULL,
                ActiveUntil DATETIME NOT NULL,
                UNIQUE(RestaurantId)
            );

            CREATE TABLE IF NOT EXISTS Chats (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                Platform INTEGER NOT NULL,
                UNIQUE(Platform, ChatId)
            );

            CREATE TABLE IF NOT EXISTS RestaurantConnections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                RestaurantId INTEGER NOT NULL,
                Token TEXT NOT NULL,
                IsSelected INTEGER NOT NULL DEFAULT 0,

                FOREIGN KEY(ChatId) REFERENCES Chats(Id),
                FOREIGN KEY(RestaurantId) REFERENCES Restaurants(Id),

                UNIQUE(ChatId, RestaurantId)
            );
        """);
    }

    public async Task<bool> CheckIntegrityAsync()
    {
        try
        {
            using var connection = await ConnectAsync();
            var integrityResult = await connection.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
            if (integrityResult != "ok")
            {
                _logger.LogError("Database integrity check failed: {Result}", integrityResult);
                return false;
            }

            var foreignKeysResult = await connection.QueryAsync("PRAGMA foreign_key_check;");
            if (foreignKeysResult.Any())
            {
                var foreignKeysErrors = foreignKeysResult.ToList();
                _logger.LogError("Foreign key violations found: {@Violations}", foreignKeysErrors);
                return false;
            }
            
            _logger.LogInformation("Database integrity check passed: no errors found");
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception during database integrity check");
            return false;
        }
    }

    public async Task<RegistrationResult> RegisterRestaurantAsync(long chatId, Platform platform, int restaurantId, string restaurantName, string token)
    {
        using var connection = await ConnectAsync();

        var existingConnection = await connection.QuerySingleOrDefaultAsync<int?>("""
            SELECT rc.Id
            FROM RestaurantConnections rc
            JOIN Chats c ON rc.ChatId = c.Id
            JOIN Restaurants r ON rc.RestaurantId = r.Id
            WHERE c.ChatId = @ChatId AND c.Platform = @Platform
              AND r.RestaurantId = @RestaurantId
            """,
            new { ChatId = chatId, Platform = (int)platform, RestaurantId = restaurantId });

        var isNew = existingConnection is null;

        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("""
                INSERT INTO Restaurants (RestaurantId, Name, AddedAt, ActiveUntil)
                VALUES (@RestaurantId, @Name, @AddedAt, @ActiveUntil)
                ON CONFLICT(RestaurantId) DO UPDATE SET
                    Name = excluded.Name,
                    ActiveUntil = excluded.ActiveUntil;
            """,
            new
            {
                RestaurantId = restaurantId,
                Name = restaurantName,
                AddedAt = DateTime.UtcNow,
                ActiveUntil = DateTime.UtcNow.AddMonths(1)
            },
            transaction);

            await connection.ExecuteAsync("""
                INSERT INTO Chats (ChatId, Platform)
                VALUES (@ChatId, @Platform)
                ON CONFLICT(Platform, ChatId) DO NOTHING;
            """,
            new
            {
                ChatId = chatId,
                Platform = (int)platform
            },
            transaction);

            await connection.ExecuteAsync("""
                INSERT INTO RestaurantConnections (ChatId, RestaurantId, Token)
                VALUES (
                    (SELECT Id FROM Chats WHERE ChatId = @ChatId AND Platform = @Platform),
                    (SELECT Id FROM Restaurants WHERE RestaurantId = @RestaurantId),
                    @Token
                )
                ON CONFLICT(ChatId, RestaurantId) DO UPDATE SET
                Token = excluded.Token;
            """,
            new
            {
                ChatId = chatId,
                Platform = (int)platform,
                RestaurantId = restaurantId,
                Token = token
            },
            transaction);

            await transaction.CommitAsync();
            return isNew ? RegistrationResult.Added : RegistrationResult.Updated;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();

            _logger.LogError(exception,
                "Failed to register restaurant {RestaurantId} for chat {ChatId} on platform {Platform}",
                restaurantId, chatId, platform);

            return RegistrationResult.Failed;
        }
    }

    public async Task<IEnumerable<Restaurant>> GetRestaurantsAsync(long chatId, Platform platform)
    {
        using var connection = await ConnectAsync();
        return await connection.QueryAsync<Restaurant>("""
            SELECT r.*, rc.Token, rc.IsSelected
            FROM RestaurantConnections rc
            JOIN Restaurants r ON rc.RestaurantId = r.Id
            JOIN Chats c ON rc.ChatId = c.Id
            WHERE c.ChatId = @ChatId AND c.Platform = @Platform
            ORDER BY r.Name
        """,
        new { ChatId = chatId, Platform = (int)platform } );
    }

    public async Task<Restaurant?> GetSelectedRestaurantAsync(long chatId, Platform platform)
    {
        using var connection = await ConnectAsync();
        return await connection.QuerySingleOrDefaultAsync<Restaurant>("""
            SELECT r.RestaurantId, r.Name, r.AddedAt, r.ActiveUntil, rc.Token, rc.IsSelected
            FROM RestaurantConnections rc
            JOIN Restaurants r ON rc.RestaurantId = r.Id
            JOIN Chats c ON rc.ChatId = c.Id
            WHERE c.ChatId = @ChatId AND c.Platform = @Platform AND rc.IsSelected = 1
        """,
        new { ChatId = chatId, Platform = (int)platform } );
    }

    public async Task<Restaurant?> SetSelectedAsync(long chatId, Platform platform, int restaurantId)
    {
        using var connection = await ConnectAsync();
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("""
                UPDATE RestaurantConnections SET IsSelected = 0
                WHERE ChatId = (SELECT Id FROM Chats WHERE ChatId = @ChatId AND Platform = @Platform)
            """,
            new { ChatId = chatId, Platform = (int)platform }, transaction);

            await connection.ExecuteAsync("""
                UPDATE RestaurantConnections SET IsSelected = 1
                WHERE ChatId = (SELECT Id FROM Chats WHERE ChatId = @ChatId AND Platform = @Platform)
                    AND RestaurantId = (SELECT Id FROM Restaurants WHERE RestaurantId = @RestaurantId)
            """,
            new { ChatId = chatId, Platform = (int)platform, RestaurantId = restaurantId }, transaction);

            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();

            _logger.LogError(exception,
                "Failed to set selected restaurant {RestaurantId} for chat {ChatId} on platform {Platform}",
				restaurantId, chatId, platform);

			return null;
        }

        return await GetSelectedRestaurantAsync(chatId, platform);
    }

    public async Task<bool> UnregisterRestaurantAsync(long chatId, Platform platform, int restaurantId)
    {
        using var connection = await ConnectAsync();
        return await connection.ExecuteAsync("""
            DELETE FROM RestaurantConnections
            WHERE ChatId = (SELECT Id FROM Chats WHERE ChatId = @ChatId AND Platform = @Platform)
            AND RestaurantId = (SELECT Id FROM Restaurants WHERE RestaurantId = @RestaurantId)
        """,
        new
        {
            ChatId = chatId,
            Platform = (int)platform,
            RestaurantId = restaurantId
        }) > 0;
    }
}