using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Clipt.Data.Migrations;

public sealed class MigrationRunner
{
    private readonly DatabasePathProvider _pathProvider;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(DatabasePathProvider pathProvider, ILogger<MigrationRunner> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var connectionString = $"Data Source={_pathProvider.GetDatabasePath()}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        EnsureSchemaMigrationsTable(connection);

        var migrations = DiscoverMigrations();
        foreach (var (name, sql) in migrations)
        {
            if (IsMigrationApplied(connection, name))
            {
                _logger.LogDebug("Migration {Migration} already applied, skipping.", name);
                continue;
            }

            _logger.LogInformation("Applying migration {Migration}...", name);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            RecordMigration(connection, name);
            _logger.LogInformation("Migration {Migration} applied.", name);
        }
    }

    private static void EnsureSchemaMigrationsTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                name TEXT PRIMARY KEY,
                applied_at INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static bool IsMigrationApplied(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM schema_migrations WHERE name = @name";
        command.Parameters.AddWithValue("@name", name);
        var result = command.ExecuteScalar();
        return result is long count && count > 0;
    }

    private static void RecordMigration(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO schema_migrations (name, applied_at) VALUES (@name, @applied_at)";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@applied_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    private static List<(string Name, string Sql)> DiscoverMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var prefix = typeof(MigrationRunner).Namespace + ".";
        var resourceNames = assembly.GetManifestResourceNames();
        var migrationResources = resourceNames
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var migrations = new List<(string Name, string Sql)>();
        foreach (var resourceName in migrationResources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();
            var name = resourceName[prefix.Length..^4]; // Strip prefix and ".sql"
            migrations.Add((name, sql));
        }

        return migrations;
    }
}
