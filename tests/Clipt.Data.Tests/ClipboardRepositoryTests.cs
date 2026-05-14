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
        tables.Should().Contain("clipboard_formats");
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
    public async Task SaveAsync_DuplicateContentHash_MovesExistingItemToTop()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var original = CreateTestItem("Duplicate recency") with { CreatedAt = now.AddHours(-2), LastUsedAt = now.AddHours(-2) };
        var newer = CreateTestItem("Newer item") with { CreatedAt = now.AddHours(-1), LastUsedAt = now.AddHours(-1) };

        await _repository.SaveAsync(original, CancellationToken.None);
        await _repository.SaveAsync(newer, CancellationToken.None);

        var before = await _repository.GetItemsAsync(CancellationToken.None);
        before[0].Id.Should().Be(newer.Id);

        var duplicate = CreateTestItem("Duplicate recency");
        var savedDuplicate = await _repository.SaveAsync(duplicate, CancellationToken.None);

        savedDuplicate.Id.Should().Be(original.Id);
        savedDuplicate.CreatedAt.Should().BeAfter(original.CreatedAt);

        var after = await _repository.GetItemsAsync(CancellationToken.None);
        after.Should().HaveCount(2);
        after[0].Id.Should().Be(original.Id, "recapturing existing content should make it the most recent unpinned item");
        after[1].Id.Should().Be(newer.Id);
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
    public async Task SearchAsync_ShortQuery_UsesLikeFallbackForSubstringMatch()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("API client with LP token"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unrelated item"), CancellationToken.None);

        var results = await _repository.SearchAsync("LP", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Title.Should().Be("API client with LP token");
    }

    [Fact]
    public async Task SearchAsync_SymbolHeavyQuery_UsesLikeFallback()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("#14B8A6", contentType: ContentType.Color), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("#0F172A", contentType: ContentType.Color), CancellationToken.None);

        var results = await _repository.SearchAsync("#14B8A6", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Be("#14B8A6");
    }

    [Fact]
    public async Task SearchAsync_LikeFallback_EscapesWildcardCharacters()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Discount is 50% today"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("A normal unrelated row"), CancellationToken.None);

        var literalPercentResults = await _repository.SearchAsync("%", CancellationToken.None);

        literalPercentResults.Should().ContainSingle();
        literalPercentResults[0].Content.Should().Be("Discount is 50% today");
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

    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Delete me");
        await _repository.SaveAsync(item, CancellationToken.None);

        await _repository.DeleteAsync(item.Id, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesSearchableFtsRow()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("FTS delete target content");
        await _repository.SaveAsync(item, CancellationToken.None);

        // Verify searchable before delete.
        var before = await _repository.SearchAsync("delete", CancellationToken.None);
        before.Should().ContainSingle();

        await _repository.DeleteAsync(item.Id, CancellationToken.None);

        // After delete, FTS should be cleaned via trigger.
        var after = await _repository.SearchAsync("delete", CancellationToken.None);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownIdIsSafeNoOp()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Keep me"), CancellationToken.None);

        var unknownId = Guid.NewGuid();
        await _repository.DeleteAsync(unknownId, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
    }

    [Fact]
    public async Task ClearUnpinnedAsync_RemovesOnlyUnpinnedRows()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Pinned A", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned A"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned B"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Pinned B", isPinned: true), CancellationToken.None);

        var result = await _repository.ClearUnpinnedAsync(CancellationToken.None);
        result.Count.Should().Be(2);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(i => i.IsPinned.Should().BeTrue());
    }

    [Fact]
    public async Task ClearUnpinnedAsync_KeepsPinnedRows()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinned = CreateTestItem("Must stay", isPinned: true);
        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Will go"), CancellationToken.None);

        await _repository.ClearUnpinnedAsync(CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].Id.Should().Be(pinned.Id);
        items[0].Title.Should().Be("Must stay");
    }

    [Fact]
    public async Task ClearUnpinnedAsync_RemovesFtsEntriesForDeletedRows()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Pinned search term", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned search term"), CancellationToken.None);

        // Verify both are searchable.
        var before = await _repository.SearchAsync("search", CancellationToken.None);
        before.Should().HaveCount(2);

        await _repository.ClearUnpinnedAsync(CancellationToken.None);

        // After clearing unpinned, only the pinned item should remain searchable.
        var after = await _repository.SearchAsync("search", CancellationToken.None);
        after.Should().ContainSingle();
        after[0].IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task GetItemsAsync_OrderingRemainsSensibleAfterDeletes()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinned = CreateTestItem("Pinned ordered", isPinned: true);
        var newer = CreateTestItem("Newer ordered");
        var older = CreateTestItem("Older ordered");

        await _repository.SaveAsync(older, CancellationToken.None);
        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(newer, CancellationToken.None);

        // Delete the newer unpinned item.
        await _repository.DeleteAsync(newer.Id, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(2);
        items[0].IsPinned.Should().BeTrue();
        items[0].Id.Should().Be(pinned.Id);
        items[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task SearchAsync_OrderingRemainsSensibleAfterDeletes()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinnedA = CreateTestItem("Pinned alpha after delete", isPinned: true);
        var unpinnedA = CreateTestItem("Unpinned bravo after delete");
        var unpinnedB = CreateTestItem("Unpinned charlie after delete");

        await _repository.SaveAsync(unpinnedA, CancellationToken.None);
        await _repository.SaveAsync(pinnedA, CancellationToken.None);
        await _repository.SaveAsync(unpinnedB, CancellationToken.None);

        // Delete the first unpinned item.
        await _repository.DeleteAsync(unpinnedA.Id, CancellationToken.None);

        var results = await _repository.SearchAsync("after delete", CancellationToken.None);
        results.Should().HaveCount(2);
        results[0].IsPinned.Should().BeTrue();
        results[0].Id.Should().Be(pinnedA.Id);
        results[1].Id.Should().Be(unpinnedB.Id);
    }

    [Fact]
    public async Task ClearUnpinnedAsync_EmptyIsNoOp()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var result = await _repository.ClearUnpinnedAsync(CancellationToken.None);
        result.Count.Should().Be(0);
        result.ImageUris.Should().BeEmpty();
    }

    // ── Format persistence ──────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_PersistsFormats()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItemWithFormats("Formatted content",
            formats: [("UnicodeText", "Formatted content"),
                      ("HTML Format", "<html><b>Formatted content</b></html>")]);

        var saved = await _repository.SaveAsync(item, CancellationToken.None);

        saved.Formats.Should().HaveCount(2);
        saved.Formats[0].Name.Should().Be("UnicodeText");
        saved.Formats[1].Name.Should().Be("HTML Format");
    }

    [Fact]
    public async Task GetItemsAsync_ReturnsFormats()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItemWithFormats("Item A",
            formats: [("UnicodeText", "Item A")]), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItemWithFormats("Item B",
            formats: [("UnicodeText", "Item B"),
                      ("HTML Format", "<p>Item B</p>")]), CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(2);

        var itemA = items.Should().ContainSingle(i => i.Title == "Item A").Subject;
        itemA.Formats.Should().HaveCount(1);

        var itemB = items.Should().ContainSingle(i => i.Title == "Item B").Subject;
        itemB.Formats.Should().HaveCount(2);
        itemB.Formats.Should().Contain(f => f.Name == "HTML Format");
    }

    [Fact]
    public async Task SearchAsync_ReturnsFormats()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItemWithFormats("Searchable with formats",
            formats: [("UnicodeText", "Searchable with formats"),
                      ("Rich Text Format", @"{\rtf1 Rich content}")]), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Plain item"), CancellationToken.None);

        var results = await _repository.SearchAsync("Searchable", CancellationToken.None);
        results.Should().ContainSingle();
        results[0].Formats.Should().HaveCount(2);
        results[0].Formats.Should().Contain(f => f.Name == "Rich Text Format");
    }

    [Fact]
    public async Task SaveAsync_FileDrop_ReturnsFilePathsFromFormatPayload()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var paths = new[]
        {
            @"C:\Users\Matt\Documents\phase-one-notes.md",
            @"C:\Users\Matt\Pictures\clipt-mockup.png",
        };

        var item = CreateFileDropItem(paths);

        var saved = await _repository.SaveAsync(item, CancellationToken.None);

        saved.ContentType.Should().Be(ContentType.File);
        saved.FilePaths.Should().Equal(paths);
        saved.Formats.Should().Contain(f => f.Name == ClipboardFormatNames.FileDrop);
    }

    [Fact]
    public async Task GetItemsAsync_FileDrop_RehydratesFilePaths()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var paths = new[]
        {
            @"C:\Users\Matt\Documents\phase-one-notes.md",
            @"C:\Users\Matt\Pictures\clipt-mockup.png",
        };

        await _repository.SaveAsync(CreateFileDropItem(paths), CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);

        items.Should().ContainSingle();
        items[0].ContentType.Should().Be(ContentType.File);
        items[0].FilePaths.Should().Equal(paths);
    }

    [Fact]
    public async Task SearchAsync_FileDrop_ReturnsRehydratedFilePaths()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var paths = new[]
        {
            @"C:\Users\Matt\Documents\phase-one-notes.md",
            @"C:\Users\Matt\Pictures\clipt-mockup.png",
        };

        await _repository.SaveAsync(CreateFileDropItem(paths), CancellationToken.None);

        var results = await _repository.SearchAsync("mockup", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].ContentType.Should().Be(ContentType.File);
        results[0].FilePaths.Should().Equal(paths);
    }

    [Fact]
    public async Task DeleteAsync_DeletesAssociatedFormats()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItemWithFormats("To be deleted",
            formats: [("UnicodeText", "To be deleted"),
                      ("HTML Format", "<b>To be deleted</b>")]);

        await _repository.SaveAsync(item, CancellationToken.None);
        await _repository.DeleteAsync(item.Id, CancellationToken.None);

        // Verify no orphaned format rows.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM clipboard_formats WHERE item_id = @id";
        cmd.Parameters.AddWithValue("@id", item.Id.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0, "format rows should be cascade-deleted with the parent item");
    }

    [Fact]
    public async Task ClearUnpinnedAsync_DeletesFormatsForUnpinnedAndKeepsForPinned()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinned = CreateTestItemWithFormats("Pinned formatted", isPinned: true,
            formats: [("UnicodeText", "Pinned formatted"),
                      ("HTML Format", "<i>Pinned</i>")]);
        var unpinned = CreateTestItemWithFormats("Unpinned formatted",
            formats: [("UnicodeText", "Unpinned formatted"),
                      ("Rich Text Format", @"{\rtf1 Unpinned}")]);

        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(unpinned, CancellationToken.None);

        await _repository.ClearUnpinnedAsync(CancellationToken.None);

        // Pinned item and its formats should survive.
        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].Id.Should().Be(pinned.Id);
        items[0].Formats.Should().HaveCount(2);

        // Verify no orphaned format rows for the deleted unpinned item.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM clipboard_formats WHERE item_id = @id";
        cmd.Parameters.AddWithValue("@id", unpinned.Id.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0, "unpinned format rows should be cascade-deleted");
    }

    [Fact]
    public async Task ExistingItems_WithNoFormatRows_LoadWithEmptyFormats()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        // Insert a legacy item directly without any format rows.
        var legacyId = Guid.NewGuid().ToString();
        await using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO clipboard_items (id, content_hash, primary_format, content_type, title,
                    preview_text, content_text, source_app_name, byte_size, created_at, last_used_at, use_count)
                VALUES (@id, @hash, 'text', 'Text', 'Legacy item', 'Legacy', 'legacy content',
                    'LegacyApp', 0, @ts, @ts, 0)
                """;
            cmd.Parameters.AddWithValue("@id", legacyId);
            cmd.Parameters.AddWithValue("@hash", "legacy-hash-001");
            cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync();
        }

        // The legacy item should load with an empty Formats list, not throw.
        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].Formats.Should().BeEmpty("legacy items with no format rows should load fine");
    }

    [Fact]
    public async Task DuplicateSaveAsync_DoesNotCreateDuplicateFormatRows()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var original = CreateTestItemWithFormats("Duplicate content",
            formats: [("UnicodeText", "Duplicate content"),
                      ("HTML Format", "<html>Duplicate</html>")]);

        await _repository.SaveAsync(original, CancellationToken.None);

        var duplicate = CreateTestItemWithFormats("Duplicate content",
            formats: [("UnicodeText", "Duplicate content"),
                      ("HTML Format", "<html>Duplicate</html>")]);

        await _repository.SaveAsync(duplicate, CancellationToken.None);

        // Verify exactly one set of formats exists for this item.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM clipboard_formats WHERE item_id = @id";
        cmd.Parameters.AddWithValue("@id", original.Id.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(2, "duplicate save should not create additional format rows");
    }

    [Fact]
    public async Task ClearUnpinnedAsync_AllPinnedRemovesNone()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Pinned 1", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Pinned 2", isPinned: true), CancellationToken.None);

        var result = await _repository.ClearUnpinnedAsync(CancellationToken.None);
        result.Count.Should().Be(0);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(2);
    }

    // ── History pruning ─────────────────────────────────────────────

    [Fact]
    public async Task PruneUnpinnedAsync_ExcessUnpinned_KeepsAtMostNUnpinned()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Unpinned {i}"), CancellationToken.None);
        }

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 3, CancellationToken.None);
        deleted.Count.Should().Be(7);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(3);
        items.Should().AllSatisfy(i => i.IsPinned.Should().BeFalse());
    }

    [Fact]
    public async Task PruneUnpinnedAsync_PreservesPinnedItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Pinned keep", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned 1"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned 2"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Unpinned 3"), CancellationToken.None);

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 2, CancellationToken.None);
        deleted.Count.Should().Be(1);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        // 1 pinned + 2 unpinned kept = 3 total
        items.Should().HaveCount(3);
        items.Should().ContainSingle(i => i.IsPinned && i.Title == "Pinned keep");
        items.Where(i => !i.IsPinned).Should().HaveCount(2);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_ManyPinned_UnpinnedKept_TotalExceedsMax()
    {
        // 20 pinned + 5 unpinned with max 3 unpinned → total rows = 23, unpinned = 3
        await _migrationRunner.RunAsync(CancellationToken.None);

        for (var i = 0; i < 20; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Pinned {i}", isPinned: true), CancellationToken.None);
        }

        for (var i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Unpinned {i}"), CancellationToken.None);
        }

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 3, CancellationToken.None);
        deleted.Count.Should().Be(2);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(23);
        items.Count(i => i.IsPinned).Should().Be(20);
        items.Count(i => !i.IsPinned).Should().Be(3);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_RemovesOldestUnpinnedItemsFirst()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var baseTime = DateTimeOffset.UtcNow;

        // Save items with staggered timestamps so we can verify oldest-first deletion.
        var oldest = CreateTestItem("Oldest unpinned") with { CreatedAt = baseTime.AddHours(-10) };
        var middle = CreateTestItem("Middle unpinned") with { CreatedAt = baseTime.AddHours(-5) };
        var newest = CreateTestItem("Newest unpinned") with { CreatedAt = baseTime };

        await _repository.SaveAsync(oldest, CancellationToken.None);
        await _repository.SaveAsync(middle, CancellationToken.None);
        await _repository.SaveAsync(newest, CancellationToken.None);

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);
        deleted.Count.Should().Be(2);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].Title.Should().Be("Newest unpinned", "the newest unpinned item should survive pruning");
    }

    [Fact]
    public async Task PruneUnpinnedAsync_ReturnsDeletedRowCount()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Item {i}"), CancellationToken.None);
        }

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 2, CancellationToken.None);
        deleted.Count.Should().Be(3);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_CountBelowMax_RemovesNone()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Only item"), CancellationToken.None);

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 5, CancellationToken.None);
        deleted.Count.Should().Be(0);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
    }

    [Fact]
    public async Task PruneUnpinnedAsync_ZeroMaxItems_DisablesPruning()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Item {i}"), CancellationToken.None);
        }

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 0, CancellationToken.None);
        deleted.Count.Should().Be(0);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_NegativeMaxItems_DisablesPruning()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(CreateTestItem($"Item {i}"), CancellationToken.None);
        }

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: -1, CancellationToken.None);
        deleted.Count.Should().Be(0);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_RemovesFtsRowsForDeletedItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Will be pruned first text"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Will be pruned second text"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Will survive prune text"), CancellationToken.None);

        // Verify all three are searchable before pruning.
        var before = await _repository.SearchAsync("prune text", CancellationToken.None);
        before.Should().HaveCount(3);

        await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);

        // After pruning, only the newest unpinned item should remain searchable.
        var after = await _repository.SearchAsync("prune text", CancellationToken.None);
        after.Should().ContainSingle();
        after[0].Title.Should().Be("Will survive prune text");
    }

    [Fact]
    public async Task PruneUnpinnedAsync_DeletesFormatRowsForRemovedItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var toPrune = CreateTestItemWithFormats("Pruned formatted",
            formats: [("UnicodeText", "Pruned formatted"),
                      ("HTML Format", "<b>Pruned</b>")]);
        var kept = CreateTestItemWithFormats("Kept formatted",
            formats: [("UnicodeText", "Kept formatted")]);

        await _repository.SaveAsync(toPrune, CancellationToken.None);
        await _repository.SaveAsync(kept, CancellationToken.None);

        await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);

        // Verify no orphaned format rows for the pruned item.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM clipboard_formats WHERE item_id = @id";
        cmd.Parameters.AddWithValue("@id", toPrune.Id.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0, "format rows should be cascade-deleted with the pruned item");
    }

    [Fact]
    public async Task PruneUnpinnedAsync_EmptyTableIsNoOp()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 100, CancellationToken.None);
        deleted.Count.Should().Be(0);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_AllPinnedRemovesNone()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Pinned A", isPinned: true), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Pinned B", isPinned: true), CancellationToken.None);

        var deleted = await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);
        deleted.Count.Should().Be(0);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(i => i.IsPinned.Should().BeTrue());
    }

    [Fact]
    public async Task SaveAsync_ImageItem_PersistsImageUri()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Image capture test") with
        {
            ContentType = ContentType.Image,
            ImageUri = "file:///C:/test/preview-cache/img_abc123.png",
        };

        var saved = await _repository.SaveAsync(item, CancellationToken.None);
        saved.ImageUri.Should().Be("file:///C:/test/preview-cache/img_abc123.png");

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].ImageUri.Should().Be("file:///C:/test/preview-cache/img_abc123.png");
    }

    [Fact]
    public async Task SaveAsync_ItemWithoutImageUri_PersistsNullImageUri()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var item = CreateTestItem("Text without image");

        await _repository.SaveAsync(item, CancellationToken.None);

        var items = await _repository.GetItemsAsync(CancellationToken.None);
        items.Should().ContainSingle();
        items[0].ImageUri.Should().BeNull();
    }

    // ── Image URI cleanup ────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ImageItem_ReturnsImageUri_ViaCallerKnowsUri()
    {
        // The repository does not change its DeleteAsync return type; the ViewModel
        // already holds the URI from the ClipboardItemViewModel. This test verifies
        // that an image item's ImageUri round-trips through Save -> GetItems correctly
        // so the caller can use it for cache cleanup.
        await _migrationRunner.RunAsync(CancellationToken.None);

        const string uri = "file:///C:/cache/preview-cache/img_del001.png";
        var item = CreateTestItem("Delete image") with
        {
            ContentType = ContentType.Image,
            ImageUri = uri,
        };

        await _repository.SaveAsync(item, CancellationToken.None);

        var before = await _repository.GetItemsAsync(CancellationToken.None);
        before.Should().ContainSingle();
        before[0].ImageUri.Should().Be(uri, "URI must survive the save/load round-trip");

        await _repository.DeleteAsync(item.Id, CancellationToken.None);

        var after = await _repository.GetItemsAsync(CancellationToken.None);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearUnpinnedAsync_ReturnsImageUrisOfDeletedImageItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        const string unpinnedUri1 = "file:///C:/cache/preview-cache/img_clear001.png";
        const string unpinnedUri2 = "file:///C:/cache/preview-cache/img_clear002.png";
        const string pinnedUri = "file:///C:/cache/preview-cache/img_pinned.png";

        var unpinned1 = CreateTestItem("Unpinned image 1") with
        {
            ContentType = ContentType.Image,
            ImageUri = unpinnedUri1,
        };
        var unpinned2 = CreateTestItem("Unpinned image 2") with
        {
            ContentType = ContentType.Image,
            ImageUri = unpinnedUri2,
        };
        var pinnedImg = CreateTestItem("Pinned image", isPinned: true) with
        {
            ContentType = ContentType.Image,
            ImageUri = pinnedUri,
        };
        var textItem = CreateTestItem("Plain text, no URI");

        await _repository.SaveAsync(unpinned1, CancellationToken.None);
        await _repository.SaveAsync(unpinned2, CancellationToken.None);
        await _repository.SaveAsync(pinnedImg, CancellationToken.None);
        await _repository.SaveAsync(textItem, CancellationToken.None);

        var result = await _repository.ClearUnpinnedAsync(CancellationToken.None);

        result.Count.Should().Be(3, "two unpinned images + one plain text item were deleted");
        result.ImageUris.Should().HaveCount(2, "only items with a non-null image_uri are included");
        result.ImageUris.Should().Contain(unpinnedUri1);
        result.ImageUris.Should().Contain(unpinnedUri2);
        result.ImageUris.Should().NotContain(pinnedUri, "pinned image must not be cleared");

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
        remaining[0].Id.Should().Be(pinnedImg.Id);
        remaining[0].ImageUri.Should().Be(pinnedUri);
    }

    [Fact]
    public async Task ClearUnpinnedAsync_NoImageItems_ReturnsEmptyImageUriList()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await _repository.SaveAsync(CreateTestItem("Text A"), CancellationToken.None);
        await _repository.SaveAsync(CreateTestItem("Text B"), CancellationToken.None);

        var result = await _repository.ClearUnpinnedAsync(CancellationToken.None);

        result.Count.Should().Be(2);
        result.ImageUris.Should().BeEmpty("no image URIs among deleted text items");
    }

    [Fact]
    public async Task PruneUnpinnedAsync_ReturnsImageUrisOfPrunedImageItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var baseTime = DateTimeOffset.UtcNow;

        const string oldUri = "file:///C:/cache/preview-cache/img_prune_old.png";
        const string newUri = "file:///C:/cache/preview-cache/img_prune_new.png";

        var oldest = CreateTestItem("Oldest image") with
        {
            CreatedAt = baseTime.AddHours(-10),
            ContentType = ContentType.Image,
            ImageUri = oldUri,
        };
        var newest = CreateTestItem("Newest image") with
        {
            CreatedAt = baseTime,
            ContentType = ContentType.Image,
            ImageUri = newUri,
        };

        await _repository.SaveAsync(oldest, CancellationToken.None);
        await _repository.SaveAsync(newest, CancellationToken.None);

        var result = await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().ContainSingle().Which.Should().Be(oldUri,
            "only the oldest item is pruned");
        result.ImageUris.Should().NotContain(newUri, "newest item survives pruning");

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
        remaining[0].ImageUri.Should().Be(newUri);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_PinnedImageItems_NeverIncludedInPrunedUris()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var baseTime = DateTimeOffset.UtcNow;
        const string pinnedUri = "file:///C:/cache/preview-cache/img_pinned_safe.png";
        const string prunedUri = "file:///C:/cache/preview-cache/img_unpinned_prune.png";
        const string keptUri = "file:///C:/cache/preview-cache/img_unpinned_keep.png";

        var pinned = CreateTestItem("Pinned image", isPinned: true) with
        {
            CreatedAt = baseTime.AddHours(-20),
            ContentType = ContentType.Image,
            ImageUri = pinnedUri,
        };
        var pruned = CreateTestItem("Pruned unpinned image") with
        {
            CreatedAt = baseTime.AddHours(-10),
            ContentType = ContentType.Image,
            ImageUri = prunedUri,
        };
        var kept = CreateTestItem("Kept unpinned image") with
        {
            CreatedAt = baseTime,
            ContentType = ContentType.Image,
            ImageUri = keptUri,
        };

        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(pruned, CancellationToken.None);
        await _repository.SaveAsync(kept, CancellationToken.None);

        var result = await _repository.PruneUnpinnedAsync(maxItems: 1, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().ContainSingle().Which.Should().Be(prunedUri);
        result.ImageUris.Should().NotContain(pinnedUri, "pinned images are never pruned");

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(i => i.Id == pinned.Id);
        remaining.Should().Contain(i => i.Id == kept.Id);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_MixedItems_OnlyPrunedImageUrisReturned()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var baseTime = DateTimeOffset.UtcNow;

        const string pruneUri = "file:///C:/cache/preview-cache/img_prune_mixed.png";

        var oldImage = CreateTestItem("Old image item") with
        {
            CreatedAt = baseTime.AddHours(-5),
            ContentType = ContentType.Image,
            ImageUri = pruneUri,
        };
        var oldText = CreateTestItem("Old text item") with
        {
            CreatedAt = baseTime.AddHours(-4),
        };
        var newText = CreateTestItem("New text item") with
        {
            CreatedAt = baseTime,
        };

        await _repository.SaveAsync(oldImage, CancellationToken.None);
        await _repository.SaveAsync(oldText, CancellationToken.None);
        await _repository.SaveAsync(newText, CancellationToken.None);

        // Keep 2 unpinned → prune 1 (the oldest).
        var result = await _repository.PruneUnpinnedAsync(maxItems: 2, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().ContainSingle().Which.Should().Be(pruneUri);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(i => i.Id == oldImage.Id);
    }

    [Fact]
    public async Task PruneUnpinnedAsync_TextRowPrunedBeforeImage_DoesNotReturnSurvivingImageUri()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var baseTime = DateTimeOffset.UtcNow;
        const string survivingImageUri = "file:///C:/cache/preview-cache/img_survives_text_prune.png";

        var oldText = CreateTestItem("Old text item") with
        {
            CreatedAt = baseTime.AddHours(-5),
        };
        var middleImage = CreateTestItem("Middle image item") with
        {
            CreatedAt = baseTime.AddHours(-4),
            ContentType = ContentType.Image,
            ImageUri = survivingImageUri,
        };
        var newText = CreateTestItem("New text item") with
        {
            CreatedAt = baseTime,
        };

        await _repository.SaveAsync(oldText, CancellationToken.None);
        await _repository.SaveAsync(middleImage, CancellationToken.None);
        await _repository.SaveAsync(newText, CancellationToken.None);

        // Keep 2 unpinned: the oldest text item is pruned, while the image survives.
        var result = await _repository.PruneUnpinnedAsync(maxItems: 2, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().BeEmpty("the pruned row did not have an image URI");

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(i => i.Id == middleImage.Id && i.ImageUri == survivingImageUri);
    }

    // ── Age-based pruning ────────────────────────────────────────────

    [Fact]
    public async Task PruneOlderThanAsync_DeletesOnlyItemsOlderThanCutoff()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;

        var ancient = CreateTestItem("Ancient") with { CreatedAt = now.AddDays(-30) };
        var recent = CreateTestItem("Recent") with { CreatedAt = now.AddDays(-3) };
        var fresh = CreateTestItem("Fresh") with { CreatedAt = now };

        await _repository.SaveAsync(ancient, CancellationToken.None);
        await _repository.SaveAsync(recent, CancellationToken.None);
        await _repository.SaveAsync(fresh, CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);
        result.Count.Should().Be(1);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(i => i.Title == "Ancient");
    }

    [Fact]
    public async Task PruneOlderThanAsync_PreservesPinnedItemsEvenWhenOld()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;

        var oldPinned = CreateTestItem("Old pinned", isPinned: true) with { CreatedAt = now.AddDays(-100) };
        var oldUnpinned = CreateTestItem("Old unpinned") with { CreatedAt = now.AddDays(-100) };

        await _repository.SaveAsync(oldPinned, CancellationToken.None);
        await _repository.SaveAsync(oldUnpinned, CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);
        result.Count.Should().Be(1);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
        remaining[0].IsPinned.Should().BeTrue();
        remaining[0].Title.Should().Be("Old pinned");
    }

    [Fact]
    public async Task PruneOlderThanAsync_ZeroOrNegativeDays_DisablesPruning()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(
            CreateTestItem("Ancient") with { CreatedAt = now.AddDays(-365) },
            CancellationToken.None);

        var zero = await _repository.PruneOlderThanAsync(maxAgeDays: 0, CancellationToken.None);
        zero.Count.Should().Be(0);

        var negative = await _repository.PruneOlderThanAsync(maxAgeDays: -10, CancellationToken.None);
        negative.Count.Should().Be(0);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
    }

    [Fact]
    public async Task PruneOlderThanAsync_NothingOldEnough_RemovesNone()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(
            CreateTestItem("Recent") with { CreatedAt = now.AddDays(-2) },
            CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 30, CancellationToken.None);
        result.Count.Should().Be(0);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
    }

    [Fact]
    public async Task PruneOlderThanAsync_EmptyTable_IsNoOp()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);
        result.Count.Should().Be(0);
        result.ImageUris.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneOlderThanAsync_RemovesFtsRowsForDeletedItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(
            CreateTestItem("Will be aged out unique-marker") with { CreatedAt = now.AddDays(-30) },
            CancellationToken.None);
        await _repository.SaveAsync(
            CreateTestItem("Will survive unique-marker") with { CreatedAt = now },
            CancellationToken.None);

        var before = await _repository.SearchAsync("unique-marker", CancellationToken.None);
        before.Should().HaveCount(2);

        await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);

        var after = await _repository.SearchAsync("unique-marker", CancellationToken.None);
        after.Should().ContainSingle();
        after[0].Title.Should().Be("Will survive unique-marker");
    }

    [Fact]
    public async Task PruneOlderThanAsync_DeletesFormatRowsForRemovedItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var aged = CreateTestItemWithFormats(
            "Aged formatted",
            formats: [("UnicodeText", "Aged formatted"), ("HTML Format", "<b>Aged</b>")]) with
        {
            CreatedAt = now.AddDays(-30),
        };
        var kept = CreateTestItemWithFormats(
            "Kept formatted",
            formats: [("UnicodeText", "Kept formatted")]) with
        {
            CreatedAt = now,
        };

        await _repository.SaveAsync(aged, CancellationToken.None);
        await _repository.SaveAsync(kept, CancellationToken.None);

        await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM clipboard_formats WHERE item_id = @id";
        cmd.Parameters.AddWithValue("@id", aged.Id.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0, "format rows should be cascade-deleted with the pruned item");
    }

    [Fact]
    public async Task PruneOlderThanAsync_ReturnsImageUrisOfPrunedImageItems()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        const string agedUri = "file:///C:/cache/preview-cache/img_age_old.png";
        const string keptUri = "file:///C:/cache/preview-cache/img_age_new.png";

        var aged = CreateTestItem("Aged image") with
        {
            CreatedAt = now.AddDays(-30),
            ContentType = ContentType.Image,
            ImageUri = agedUri,
        };
        var kept = CreateTestItem("Kept image") with
        {
            CreatedAt = now,
            ContentType = ContentType.Image,
            ImageUri = keptUri,
        };

        await _repository.SaveAsync(aged, CancellationToken.None);
        await _repository.SaveAsync(kept, CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().ContainSingle().Which.Should().Be(agedUri);
        result.ImageUris.Should().NotContain(keptUri);

        var remaining = await _repository.GetItemsAsync(CancellationToken.None);
        remaining.Should().ContainSingle();
        remaining[0].ImageUri.Should().Be(keptUri);
    }

    [Fact]
    public async Task PruneOlderThanAsync_PinnedImageItems_NeverIncludedInPrunedUris()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        const string pinnedUri = "file:///C:/cache/preview-cache/img_age_pinned.png";
        const string prunedUri = "file:///C:/cache/preview-cache/img_age_pruned.png";

        var pinnedOld = CreateTestItem("Pinned old image", isPinned: true) with
        {
            CreatedAt = now.AddDays(-100),
            ContentType = ContentType.Image,
            ImageUri = pinnedUri,
        };
        var unpinnedOld = CreateTestItem("Unpinned old image") with
        {
            CreatedAt = now.AddDays(-100),
            ContentType = ContentType.Image,
            ImageUri = prunedUri,
        };

        await _repository.SaveAsync(pinnedOld, CancellationToken.None);
        await _repository.SaveAsync(unpinnedOld, CancellationToken.None);

        var result = await _repository.PruneOlderThanAsync(maxAgeDays: 7, CancellationToken.None);

        result.Count.Should().Be(1);
        result.ImageUris.Should().ContainSingle().Which.Should().Be(prunedUri);
        result.ImageUris.Should().NotContain(pinnedUri, "pinned images are never pruned");
    }

    // ── GetImageUrisAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetImageUrisAsync_ReturnsPersistedImageUris()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        const string uri1 = "file:///C:/cache/preview-cache/img_test_a.png";
        const string uri2 = "file:///C:/cache/preview-cache/img_test_b.png";

        var item1 = CreateTestItem("Image A") with
        {
            ContentType = ContentType.Image,
            ImageUri = uri1,
        };
        var item2 = CreateTestItem("Image B", isPinned: true) with
        {
            ContentType = ContentType.Image,
            ImageUri = uri2,
        };

        await _repository.SaveAsync(item1, CancellationToken.None);
        await _repository.SaveAsync(item2, CancellationToken.None);

        var uris = await _repository.GetImageUrisAsync(CancellationToken.None);

        uris.Should().HaveCount(2);
        uris.Should().Contain(uri1);
        uris.Should().Contain(uri2);
    }

    [Fact]
    public async Task GetImageUrisAsync_ExcludesNullImageUri()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var textItem = CreateTestItem("Text only");
        var imageItem = CreateTestItem("Image") with
        {
            ContentType = ContentType.Image,
            ImageUri = "file:///C:/cache/preview-cache/img_test_c.png",
        };

        await _repository.SaveAsync(textItem, CancellationToken.None);
        await _repository.SaveAsync(imageItem, CancellationToken.None);

        var uris = await _repository.GetImageUrisAsync(CancellationToken.None);

        uris.Should().ContainSingle();
    }

    [Fact]
    public async Task GetImageUrisAsync_EmptyTable_ReturnsEmptyList()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var uris = await _repository.GetImageUrisAsync(CancellationToken.None);

        uris.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImageUrisAsync_IncludesPinnedAndUnpinned()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var pinned = CreateTestItem("Pinned image", isPinned: true) with
        {
            ContentType = ContentType.Image,
            ImageUri = "file:///C:/cache/preview-cache/img_pinned.png",
        };
        var unpinned = CreateTestItem("Unpinned image") with
        {
            ContentType = ContentType.Image,
            ImageUri = "file:///C:/cache/preview-cache/img_unpinned.png",
        };

        await _repository.SaveAsync(pinned, CancellationToken.None);
        await _repository.SaveAsync(unpinned, CancellationToken.None);

        var uris = await _repository.GetImageUrisAsync(CancellationToken.None);

        uris.Should().HaveCount(2);
        uris.Should().Contain("file:///C:/cache/preview-cache/img_pinned.png");
        uris.Should().Contain("file:///C:/cache/preview-cache/img_unpinned.png");
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

    private static ClipboardItem CreateTestItemWithFormats(
        string content,
        bool isPinned = false,
        (string Name, string Text)[]? formats = null)
    {
        var item = CreateTestItem(content, isPinned: isPinned);
        var formatList = formats is not null
            ? formats.Select(f => new ClipboardFormat(f.Name, f.Text)).ToList()
            : [];
        return item with { Formats = formatList };
    }

    private static ClipboardItem CreateFileDropItem(IReadOnlyList<string> paths)
    {
        var content = string.Join(Environment.NewLine, paths);
        var item = CreateTestItem(
            content,
            contentType: ContentType.File,
            preview: paths.Count == 1 ? paths[0] : $"{paths.Count} files and folders");

        return item with
        {
            Title = paths.Count == 1 ? Path.GetFileName(paths[0]) : $"{paths.Count} files",
            FilePaths = paths,
            Formats =
            [
                new ClipboardFormat(ClipboardFormatNames.FileDrop, content),
                new ClipboardFormat(ClipboardFormatNames.UnicodeText, content),
            ],
        };
    }
}
