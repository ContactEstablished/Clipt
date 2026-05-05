using System.Text.RegularExpressions;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Clipt.Data;

public sealed partial class ClipboardRepository : IHistoryService, IDisposable
{
    private readonly DatabasePathProvider _pathProvider;
    private readonly ILogger<ClipboardRepository> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _isDisposed;

    public ClipboardRepository(DatabasePathProvider pathProvider, ILogger<ClipboardRepository> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    id,
                    content_hash,
                    content_type,
                    title,
                    preview_text,
                    content_text,
                    source_app_name,
                    source_app_path,
                    byte_size,
                    is_pinned,
                    pin_order,
                    is_favorite,
                    created_at,
                    last_used_at,
                    use_count
                FROM clipboard_items
                ORDER BY is_pinned DESC, pin_order, created_at DESC
                """;

            var items = new List<ClipboardItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapRow(reader));
            }

            return items;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetItemsAsync(cancellationToken);
        }

        var ftsQuery = BuildFtsQuery(query);
        if (string.IsNullOrEmpty(ftsQuery))
        {
            return await GetItemsAsync(cancellationToken);
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    id,
                    content_hash,
                    content_type,
                    title,
                    preview_text,
                    content_text,
                    source_app_name,
                    source_app_path,
                    byte_size,
                    is_pinned,
                    pin_order,
                    is_favorite,
                    created_at,
                    last_used_at,
                    use_count
                FROM clipboard_items
                WHERE id IN (
                    SELECT item_id FROM clipboard_items_fts
                    WHERE clipboard_items_fts MATCH @query
                )
                ORDER BY is_pinned DESC, pin_order, created_at DESC
                """;
            command.Parameters.AddWithValue("@query", ftsQuery);

            var items = new List<ClipboardItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapRow(reader));
            }

            return items;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<ClipboardItem> SaveAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            // Check for existing item by content hash
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = """
                SELECT
                    id,
                    content_hash,
                    content_type,
                    title,
                    preview_text,
                    content_text,
                    source_app_name,
                    source_app_path,
                    byte_size,
                    is_pinned,
                    pin_order,
                    is_favorite,
                    created_at,
                    last_used_at,
                    use_count
                FROM clipboard_items
                WHERE content_hash = @hash
                """;
            checkCommand.Parameters.AddWithValue("@hash", item.ContentHash);

            await using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
            if (await checkReader.ReadAsync(cancellationToken))
            {
                // Duplicate found: update last_used_at and use_count, return existing item.
                // The UPDATE trigger (trg_clipboard_items_fts_au) keeps FTS in sync automatically.
                var existingItem = MapRow(checkReader);
                await checkReader.DisposeAsync();

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE clipboard_items
                    SET last_used_at = @last_used_at,
                        use_count = use_count + 1
                    WHERE content_hash = @hash
                    """;
                updateCommand.Parameters.AddWithValue("@last_used_at", now);
                updateCommand.Parameters.AddWithValue("@hash", item.ContentHash);
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Updated duplicate clipboard item {Hash}, use count incremented.", item.ContentHash);
                return existingItem with
                {
                    LastUsedAt = DateTimeOffset.FromUnixTimeMilliseconds(now),
                    UseCount = existingItem.UseCount + 1,
                };
            }

            await checkReader.DisposeAsync();

            // Insert new item.
            // The INSERT trigger (trg_clipboard_items_fts_ai) populates FTS automatically.
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO clipboard_items (
                    id,
                    content_hash,
                    primary_format,
                    content_type,
                    title,
                    preview_text,
                    content_text,
                    source_app_name,
                    source_app_path,
                    byte_size,
                    is_pinned,
                    pin_order,
                    is_favorite,
                    created_at,
                    last_used_at,
                    use_count
                ) VALUES (
                    @id,
                    @hash,
                    @primary_format,
                    @content_type,
                    @title,
                    @preview_text,
                    @content_text,
                    @source_app_name,
                    @source_app_path,
                    @byte_size,
                    @is_pinned,
                    @pin_order,
                    @is_favorite,
                    @created_at,
                    @last_used_at,
                    @use_count
                )
                """;

            insertCommand.Parameters.AddWithValue("@id", item.Id.ToString());
            insertCommand.Parameters.AddWithValue("@hash", item.ContentHash);
            insertCommand.Parameters.AddWithValue("@primary_format", "text");
            insertCommand.Parameters.AddWithValue("@content_type", item.ContentType.ToString());
            insertCommand.Parameters.AddWithValue("@title", item.Title);
            insertCommand.Parameters.AddWithValue("@preview_text", item.PreviewText);
            insertCommand.Parameters.AddWithValue("@content_text", item.Content);
            insertCommand.Parameters.AddWithValue("@source_app_name", (object?)item.SourceAppName ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@source_app_path", (object?)item.SourceAppPath ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@byte_size", item.ByteSize);
            insertCommand.Parameters.AddWithValue("@is_pinned", item.IsPinned ? 1L : 0L);
            insertCommand.Parameters.AddWithValue("@pin_order", (object?)item.PinOrder ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@is_favorite", item.IsFavorite ? 1L : 0L);
            insertCommand.Parameters.AddWithValue("@created_at", item.CreatedAt.ToUnixTimeMilliseconds());
            insertCommand.Parameters.AddWithValue("@last_used_at", item.LastUsedAt.ToUnixTimeMilliseconds());
            insertCommand.Parameters.AddWithValue("@use_count", item.UseCount);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Inserted clipboard item {Id} with hash {Hash}.", item.Id, item.ContentHash);
            return item;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);

            if (isPinned)
            {
                // Pin: assign a pin_order above existing pins (or 1 if none).
                using var maxCommand = connection.CreateCommand();
                maxCommand.CommandText = "SELECT COALESCE(MAX(pin_order), 0) FROM clipboard_items WHERE is_pinned = 1";
                var maxPinOrder = (long)(await maxCommand.ExecuteScalarAsync(cancellationToken))!;

                using var pinCommand = connection.CreateCommand();
                pinCommand.CommandText = """
                    UPDATE clipboard_items
                    SET is_pinned = 1,
                        pin_order = @pin_order
                    WHERE id = @id
                    """;
                pinCommand.Parameters.AddWithValue("@pin_order", (int)(maxPinOrder + 1));
                pinCommand.Parameters.AddWithValue("@id", id.ToString());
                await pinCommand.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Pinned clipboard item {Id} at pin_order {PinOrder}.", id, maxPinOrder + 1);
            }
            else
            {
                // Unpin: clear pin_order.
                using var unpinCommand = connection.CreateCommand();
                unpinCommand.CommandText = """
                    UPDATE clipboard_items
                    SET is_pinned = 0,
                        pin_order = NULL
                    WHERE id = @id
                    """;
                unpinCommand.Parameters.AddWithValue("@id", id.ToString());
                await unpinCommand.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Unpinned clipboard item {Id}.", id);
            }
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

    /// <summary>
    /// Sanitises user input into an FTS5 MATCH expression.
    /// Strips special FTS5 characters, splits into tokens, and applies prefix matching (*) to each token.
    /// </summary>
    private static string BuildFtsQuery(string raw)
    {
        // Remove FTS5 special characters except alphanumeric, whitespace, and underscore.
        var sanitised = FtsSpecialCharsRegex().Replace(raw, " ").Trim();
        if (sanitised.Length == 0)
        {
            return string.Empty;
        }

        var tokens = sanitised.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        // Quote each token and append * for prefix matching, then join with space (implicit AND).
        return string.Join(" ", tokens.Select(t => $"\"{t}\"*"));
    }

    [GeneratedRegex(@"[^\w\s]", RegexOptions.Compiled)]
    private static partial Regex FtsSpecialCharsRegex();

    private static ClipboardItem MapRow(SqliteDataReader reader)
    {
        var contentTypeString = reader.GetString(2);
        var contentType = Enum.TryParse<ContentType>(contentTypeString, out var parsed)
            ? parsed
            : ContentType.Text;

        return new ClipboardItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            ContentHash = reader.GetString(1),
            Title = reader.GetString(3),
            PreviewText = reader.GetString(4),
            Content = reader.GetString(5),
            ContentType = contentType,
            SourceAppName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            SourceAppPath = reader.IsDBNull(7) ? null : reader.GetString(7),
            ByteSize = reader.GetInt64(8),
            IsPinned = reader.GetInt64(9) != 0,
            PinOrder = reader.IsDBNull(10) ? null : (int)reader.GetInt64(10),
            IsFavorite = reader.GetInt64(11) != 0,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(12)),
            LastUsedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(13)),
            UseCount = (int)reader.GetInt64(14),
            Formats = [],
            FilePaths = [],
        };
    }
}
