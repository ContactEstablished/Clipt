using Clipt.Core.Models;
using Clipt.Data;
using Clipt.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clipt.Data.Tests;

public sealed class SettingsRepositoryTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly DatabasePathProvider _pathProvider;
    private readonly MigrationRunner _migrationRunner;
    private readonly SettingsRepository _repository;

    public SettingsRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"clipt_settings_test_{Guid.NewGuid():N}.db");
        _pathProvider = new DatabasePathProvider(_dbPath);
        _migrationRunner = new MigrationRunner(_pathProvider, NullLogger<MigrationRunner>.Instance);
        _repository = new SettingsRepository(_pathProvider, NullLogger<SettingsRepository>.Instance);
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaultsWhenNoSettingsRowExists()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var settings = await _repository.GetAsync(CancellationToken.None);

        settings.Should().NotBeNull();
        settings.IsWorkMode.Should().BeFalse();
        settings.Opacity.Should().Be(1.0);
        settings.CaptureModeWidth.Should().Be(380);
        settings.WorkModeWidth.Should().Be(880);
        settings.Height.Should().Be(640);
        settings.Left.Should().BeNull();
        settings.Top.Should().BeNull();
        settings.AlwaysOnTop.Should().BeFalse();
        settings.OpenHotkey.Should().Be("Ctrl+Shift+V");
        settings.AutoPasteOnEnter.Should().BeTrue();
        settings.RestorePreviousClipboardAfterPaste.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsAllSupportedProperties()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var original = new AppSettings
        {
            IsWorkMode = true,
            Opacity = 0.75,
            CaptureModeWidth = 400,
            WorkModeWidth = 900,
            Height = 700,
            Left = 100,
            Top = 200,
            AlwaysOnTop = true,
            OpenHotkey = "Ctrl+Alt+V",
            Theme = "Light",
            AccentColor = "#FF5722",
            AutoPasteOnEnter = false,
            RestorePreviousClipboardAfterPaste = true,
        };

        await _repository.SaveAsync(original, CancellationToken.None);

        var loaded = await _repository.GetAsync(CancellationToken.None);

        loaded.IsWorkMode.Should().BeTrue();
        loaded.Opacity.Should().Be(0.75);
        loaded.CaptureModeWidth.Should().Be(400);
        loaded.WorkModeWidth.Should().Be(900);
        loaded.Height.Should().Be(700);
        loaded.Left.Should().Be(100);
        loaded.Top.Should().Be(200);
        loaded.AlwaysOnTop.Should().BeTrue();
        loaded.OpenHotkey.Should().Be("Ctrl+Alt+V");
        loaded.Theme.Should().Be("Light");
        loaded.AccentColor.Should().Be("#FF5722");
        loaded.AutoPasteOnEnter.Should().BeFalse();
        loaded.RestorePreviousClipboardAfterPaste.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ClampsOpacityOutOfRange()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var tooLow = new AppSettings { Opacity = 0.01 };
        await _repository.SaveAsync(tooLow, CancellationToken.None);
        var loadedLow = await _repository.GetAsync(CancellationToken.None);
        loadedLow.Opacity.Should().Be(0.1);

        var tooHigh = new AppSettings { Opacity = 1.5 };
        await _repository.SaveAsync(tooHigh, CancellationToken.None);
        var loadedHigh = await _repository.GetAsync(CancellationToken.None);
        loadedHigh.Opacity.Should().Be(1.0);
    }

    [Fact]
    public async Task SaveAsync_RoundsOpacityToTwoDecimals()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var withExtraDecimals = new AppSettings { Opacity = 0.755555 };
        await _repository.SaveAsync(withExtraDecimals, CancellationToken.None);

        var loaded = await _repository.GetAsync(CancellationToken.None);
        loaded.Opacity.Should().Be(0.76);
    }

    [Fact]
    public async Task CorruptStoredSettings_ReturnsDefaultsWithoutThrowing()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        // Write corrupt JSON directly into the settings table.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO settings (key, value) VALUES ('app_settings', @value)";
        insertCommand.Parameters.AddWithValue("@value", "{{not valid json!!!");
        await insertCommand.ExecuteNonQueryAsync();

        // Should not throw; returns defaults.
        var settings = await _repository.GetAsync(CancellationToken.None);

        settings.Should().NotBeNull();
        settings.IsWorkMode.Should().BeFalse();
        settings.Opacity.Should().Be(1.0);
    }

    [Fact]
    public async Task NullJsonValue_ReturnsDefaultsWithoutThrowing()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        // Write JSON that deserializes to null (e.g. "null").
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO settings (key, value) VALUES ('app_settings', @value)";
        insertCommand.Parameters.AddWithValue("@value", "null");
        await insertCommand.ExecuteNonQueryAsync();

        var settings = await _repository.GetAsync(CancellationToken.None);

        settings.Should().NotBeNull();
        settings.Opacity.Should().Be(1.0);
    }

    [Fact]
    public async Task MultipleSaves_UpdateExistingSettings_NotCreateDuplicateRows()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var first = new AppSettings { IsWorkMode = true, CaptureModeWidth = 400 };
        await _repository.SaveAsync(first, CancellationToken.None);

        var second = new AppSettings { IsWorkMode = false, CaptureModeWidth = 500 };
        await _repository.SaveAsync(second, CancellationToken.None);

        var loaded = await _repository.GetAsync(CancellationToken.None);
        loaded.IsWorkMode.Should().BeFalse();
        loaded.CaptureModeWidth.Should().Be(500);

        // Verify only one row exists.
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(1) FROM settings";
        var rowCount = (long)(await countCommand.ExecuteScalarAsync())!;
        rowCount.Should().Be(1, "INSERT OR REPLACE should keep exactly one row for the settings key");
    }

    [Fact]
    public async Task Migration_CreatesSettingsTable()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'settings'";
        var result = (string?)(await command.ExecuteScalarAsync());
        result.Should().Be("settings");
    }

    [Fact]
    public async Task Migration_IsIdempotent_ForSettingsTable()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);
        await _migrationRunner.RunAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Should still have the table and no duplicate migration entries for it.
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'settings'";
        var result = (string?)(await command.ExecuteScalarAsync());
        result.Should().Be("settings");
    }

    [Fact]
    public async Task SaveAsync_Defaults_PersistAndRoundTrip()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var defaults = new AppSettings();
        await _repository.SaveAsync(defaults, CancellationToken.None);

        var loaded = await _repository.GetAsync(CancellationToken.None);

        loaded.IsWorkMode.Should().BeFalse();
        loaded.Opacity.Should().Be(1.0);
        loaded.CaptureModeWidth.Should().Be(380);
        loaded.WorkModeWidth.Should().Be(880);
        loaded.Height.Should().Be(640);
        loaded.Left.Should().BeNull();
        loaded.Top.Should().BeNull();
        loaded.AutoPasteOnEnter.Should().BeTrue();
        loaded.RestorePreviousClipboardAfterPaste.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_AllowsNewRepositoryOnSameFile()
    {
        await _migrationRunner.RunAsync(CancellationToken.None);

        var settings = new AppSettings { IsWorkMode = true };
        await _repository.SaveAsync(settings, CancellationToken.None);

        _repository.Dispose();

        var freshRepository = new SettingsRepository(_pathProvider, NullLogger<SettingsRepository>.Instance);
        try
        {
            var loaded = await freshRepository.GetAsync(CancellationToken.None);
            loaded.IsWorkMode.Should().BeTrue();
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
}
