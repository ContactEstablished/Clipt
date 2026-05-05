using System.Text.Json;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Clipt.Data;

public sealed class SettingsRepository : ISettingsService, IDisposable
{
    private const string SettingsKey = "app_settings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DatabasePathProvider _pathProvider;
    private readonly ILogger<SettingsRepository> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _isDisposed;

    public SettingsRepository(DatabasePathProvider pathProvider, ILogger<SettingsRepository> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = @key";
            command.Parameters.AddWithValue("@key", SettingsKey);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                _logger.LogDebug("No settings row found, returning defaults.");
                return new AppSettings();
            }

            var json = result.ToString()!;
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is null)
                {
                    _logger.LogWarning("Settings JSON deserialized to null, returning defaults.");
                    return new AppSettings();
                }

                return settings.Normalize();
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Corrupt settings JSON, returning defaults.");
                return new AppSettings();
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            var normalized = settings.Normalize();
            var json = JsonSerializer.Serialize(normalized, JsonOptions);

            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR REPLACE INTO settings (key, value)
                VALUES (@key, @value)
                """;
            command.Parameters.AddWithValue("@key", SettingsKey);
            command.Parameters.AddWithValue("@value", json);

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogDebug("Settings saved.");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _connection?.Dispose();
        _connection = null;
        _connectionLock.Dispose();
        _isDisposed = true;
    }

    private async ValueTask<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        var connectionString = $"Data Source={_pathProvider.GetDatabasePath()}";
        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync(cancellationToken);
        return _connection;
    }
}
