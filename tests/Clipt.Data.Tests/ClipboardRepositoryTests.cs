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

        // Verify FTS triggers exist.
        using var triggerCommand = connection.CreateCommand();
        triggerCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger' ORDER BY name";
        await using var triggerReader = await triggerCommand.ExecuteReaderAsync();

        var triggers = new List<string>();
        while (await triggerReader.ReadAsync())
        {
            triggers.Add(triggerReader.GetString(0));
        }

        triggers.Should().Contain("trg_clipboard_items_fts_ai");
        triggers.Should().Contain("trg_clipboard_items_fts_ad");
        triggers.Should().Contain("trg_clipboard_items_fts_au");

        // Verify FTS is backfilled: insert a row and it should appear in FTS.
        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO clipboard_items (id, content_hash, primary_format, content_type, title, preview_text, content_text, source_app_name, byte_size, created_at, last_used_at, use_count)
            VALUES (@id, @hash, 'text', 'Text', 'Trigger test', 'trigger preview', 'trigger content', 'Test', 0, @ts, @ts, 0)
            """;
        insertCommand.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("@hash", "trigger-test-hash");
        insertCommand.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await insertCommand.ExecuteNonQueryAsync();

        using var ftsCommand = connection.CreateCommand();
        ftsCommand.CommandText = "SELECT COUNT(1) FROM clipboard_items_fts WHERE clipboard_items_fts MATCH @query";
        ftsCommand.Parameters.AddWithValue("@query", "\"trigger\"*");
        var ftsCount = (long)(await ftsCommand.ExecuteScalarAsync())!;
        ftsCount.Should().BeGreaterThan(0, "the INSERT trigger should populate FTS automatically");
    }

    [Fact]
    public async Task Migrations_AreIdempotent()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);
        await _migrationRunner.RunAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

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

    [Fact]
    public async Task SaveAsync_DuplicateContentHash_KeepsItemSearchable()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var original = CreateTestItem("Searchable duplicate content alpha");
        await _repository.SaveAsync(original, CancellationToken.None);

        // Verify it is searchable.
        var beforeResults = await _repository.SearchAsync("alpha", CancellationToken.None);
        beforeResults.Should().ContainSingle();
        beforeResults[0].Id.Should().Be(original.Id);

        // Save a duplicate — this triggers the UPDATE path.
        var duplicate = CreateTestItem("Searchable duplicate content alpha");
        var result = await _repository.SaveAsync(duplicate, CancellationToken.None);

        result.Id.Should().Be(original.Id);
        result.UseCount.Should().Be(1);

        // After duplicate update, the item should still be searchable.
        var afterResults = await _repository.SearchAsync("alpha", CancellationToken.None);
        afterResults.Should().ContainSingle();
        afterResults[0].Id.Should().Be(original.Id);
        afterResults[0].UseCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_ReturnsAllItemsInNormalOrdering()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("First item"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Second item", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Third item"), CancellationToken.None);

        var results = await _repository.SearchAsync("   ", CancellationToken.None);

        results.Should().HaveCount(3);
        results[0].IsPinned.Should().BeTrue("pinned items should appear first");
    }

    [Fact]
    public async Task SearchAsync_MatchesTitle()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Alpha bravo"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Charlie delta"), CancellationToken.None);

        var results = await _repository.SearchAsync("alpha", CancellationToken.None);
        results.Should().ContainSingle();
        results[0].Title.Should().Be("Alpha bravo");
    }

    [Fact]
    public async Task SearchAsync_MatchesPreviewText()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem(
            content: "UniqueKeyword",
            preview: "this preview contains UniqueKeyword inside");
        await _repository.SaveAsync(item, CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Other stuff"), CancellationToken.None);

        var results = await _repository.SearchAsync("UniqueKeyword", CancellationToken.None);
        results.Should().ContainSingle();
        results[0].PreviewText.Should().Contain("UniqueKeyword");
    }

    [Fact]
    public async Task SearchAsync_MatchesContentText()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem(
            content: "The secret code is ZEBRA42 hidden here",
            preview: "Short title preview");
        await _repository.SaveAsync(item, CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("distraction"), CancellationToken.None);

        var results = await _repository.SearchAsync("ZEBRA42", CancellationToken.None);
        results.Should().ContainSingle();
        results[0].Content.Should().Contain("ZEBRA42");
    }

    [Fact]
    public async Task SearchAsync_MatchesContentType()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var markdownItem = CreateTestItem(
            content: "## Some markdown heading",
            contentType: ContentType.Markdown);
        var jsonItem = CreateTestItem(
            content: "{ \"key\": \"value\" }",
            contentType: ContentType.Json);

        await _repository.SaveAsync(markdownItem, CancellationToken.None);
        await _repository.SaveAsync(jsonItem, CancellationToken.None);

        var markdownResults = await _repository.SearchAsync("Markdown", CancellationToken.None);
        markdownResults.Should().ContainSingle();
        markdownResults[0].ContentType.Should().Be(ContentType.Markdown);

        var jsonResults = await _repository.SearchAsync("Json", CancellationToken.None);
        jsonResults.Should().ContainSingle();
        jsonResults[0].ContentType.Should().Be(ContentType.Json);
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmptyList()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Something"), CancellationToken.None);

        var results = await _repository.SearchAsync("NONEXISTENT_TOKEN_XYZ", CancellationToken.None);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SetPinnedAsync_PinsItem()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Pin me");
        await _repository.SaveAsync(item, CancellationToken.None);

        await _repository.SetPinnedAsync(item.Id, isPinned: true, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].IsPinned.Should().BeTrue();
        items[0].PinOrder.Should().NotBeNull();
    }

    [Fact]
    public async Task SetPinnedAsync_UnpinsItem()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Unpin me", isPinned: true, pinOrder: 1);
        await _repository.SaveAsync(item, CancellationToken.None);

        await _repository.SetPinnedAsync(item.Id, isPinned: false, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].IsPinned.Should().BeFalse();
        items[0].PinOrder.Should().BeNull();
    }

    [Fact]
    public async Task SetPinnedAsync_PinnedItemsOrderFirstInSearch()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinned1 = CreateTestItem("Pinned alpha item");
        var pinned2 = CreateTestItem("Pinned beta item");
        var unpinned = CreateTestItem("Unpinned item");

        await _repository.SaveAsync(unpinned, CancellationToken.None);
        await _repository.SaveAsync(pinned1, CancellationToken.None);
        await _repository.SaveAsync(pinned2, CancellationToken.None);

        await _repository.SetPinnedAsync(pinned1.Id, isPinned: true, CancellationToken.None);
        await _repository.SetPinnedAsync(pinned2.Id, isPinned: true, CancellationToken.None);

        // Search for "item" which matches all three.
        var results = await _repository.SearchAsync("item", CancellationToken.None);
        results.Should().HaveCount(3);
        results[0].IsPinned.Should().BeTrue();
        results[1].IsPinned.Should().BeTrue();
        results[2].IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task SetPinnedAsync_PinStatePersistsAfterRestart()
    {
        // Simulate "restart" by creating a fresh repository instance on the same DB file.
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Persistent pin");
        await _repository.SaveAsync(item, CancellationToken.None);
        await _repository.SetPinnedAsync(item.Id, isPinned: true, CancellationToken.None);

        // Dispose and recreate repository (same DB file).
        _repository.Dispose();

        var freshRepository = new ClipboardRepository(_pathProvider, NullLogger<ClipboardRepository>.Instance);
        try
        {
            var items = await freshRepository.GetItemsAsync(CancellationToken.None);
            items.Should().ContainSingle();
            items[0].IsPinned.Should().BeTrue("pin should persist across repository instances");
            items[0].PinOrder.Should().NotBeNull();
        }
        finally
        {
            freshRepository.Dispose();
        }
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

    private static ClipboardItem CreateTestItem(
        string content,
        bool isPinned = false,
        int? pinOrder = null,
        ContentType contentType = ContentType.Text,
        string? preview = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = Core.Services.ClipboardContentHasher.ComputeHash(content),
            Title = content.Length <= 64 ? content : content[..61] + "...",
            PreviewText = preview ?? (content.Length <= 180 ? content : content[..177] + "..."),
            Content = content,
            ContentType = contentType,
            SourceAppName = "Test",
            CreatedAt = now,
            IsPinned = isPinned,
            PinOrder = isPinned ? (pinOrder ?? 1) : null,
            ByteSize = content.Length * sizeof(char),
            LastUsedAt = now,
            UseCount = 0,
        };
    }
}
