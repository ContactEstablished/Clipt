using Clipt.Core.Models;
using Clipt.Data;
using Clipt.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clipt.Data.Tests;

public sealed class ClipboardRepositoryTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly DatabasePathProvider _pathProvider;
    private readonly MigrationRunner _migrationRunner;
    private readonly ClipboardRepository _repository;

    public ClipboardRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"clipt_test_{Guid.NewGuid():N}.db");
        _pathProvider = new DatabasePathProvider(_dbPath);
        _migrationRunner = new MigrationRunner(_pathProvider, NullLogger<MigrationRunner>.Instance);
        _repository = new ClipboardRepository(_pathProvider, NullLogger<ClipboardRepository>.Instance);
    }

    [Fact]
    public async Task Migrations_CreateExpectedTables()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
        await using var tableReader = await tableCommand.ExecuteReaderAsync();

        var tables = new List<string>();
        while (await tableReader.ReadAsync())
        {
            tables.Add(tableReader.GetString(0));
        }

        tables.Should().Contain("clipboard_items");
        tables.Should().Contain("clipboard_items_fts");
        tables.Should().Contain("schema_migrations");

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' ORDER BY name";
        await using var indexReader = await indexCommand.ExecuteReaderAsync();

        var indexes = new List<string>();
        while (await indexReader.ReadAsync())
        {
            indexes.Add(indexReader.GetString(0));
        }

        indexes.Should().Contain("ix_clipboard_items_created_at");
        indexes.Should().Contain("ix_clipboard_items_is_pinned");
    }

    [Fact]
    public async Task Migrations_AreIdempotent()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);
        await _migrationRunner.RunAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Applying migrations twice should not fail.
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM clipboard_items";
        var count = (long)(await command.ExecuteScalarAsync())!;
        count.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_InsertsItem()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Hello World");

        var saved = await _repository.SaveAsync(item, CancellationToken.None);

        saved.Id.Should().Be(item.Id);
        saved.Title.Should().Be("Hello World");

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task SaveAsync_DuplicateContentHash_UpdatesExistingRow()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Duplicate content");
        await _repository.SaveAsync(item, CancellationToken.None);

        var duplicate = CreateTestItem("Duplicate content");
        var result = await _repository.SaveAsync(duplicate, CancellationToken.None);

        // Should return the original item's ID (deduplication)
        result.Id.Should().Be(item.Id);
        result.UseCount.Should().Be(1);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].UseCount.Should().Be(1); // original use_count incremented
    }

    [Fact]
    public async Task GetItemsAsync_ReturnsItemsOrderedByPinnedThenCreatedAt()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var older = CreateTestItem("Older item");
        var pinned = CreateTestItem("Pinned item", isPinned: true);
        var newer = CreateTestItem("Newer item");

        await _repository.SaveAsync(older, CancellationToken.None);
        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(newer, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(3);
        items[0].IsPinned.Should().BeTrue();
        items[0].Title.Should().Be("Pinned item");
    }

    public async ValueTask DisposeAsync()
    {
        _repository.Dispose();

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        await Task.CompletedTask;
    }

    private static ClipboardItem CreateTestItem(string content, bool isPinned = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = Core.Services.ClipboardContentHasher.ComputeHash(content),
            Title = content.Length <= 64 ? content : content[..61] + "...",
            PreviewText = content.Length <= 180 ? content : content[..177] + "...",
            Content = content,
            ContentType = ContentType.Text,
            SourceAppName = "Test",
            CreatedAt = now,
            IsPinned = isPinned,
            ByteSize = content.Length * sizeof(char),
            LastUsedAt = now,
            UseCount = 0,
        };
    }
}
