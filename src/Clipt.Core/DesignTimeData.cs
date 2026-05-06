using Clipt.Core.Models;
using Clipt.Core.Services;

namespace Clipt.Core;

public static class DesignTimeData
{
    public static IReadOnlyList<ClipboardItem> GetSampleItems()
    {
        var now = DateTimeOffset.Now;

        return
        [
            // ── Pinned ──────────────────────────────────────────
            Create(
                "Phase 1 release notes",
                "Markdown checklist for the visual foundation pass with milestones, code fences, and links.",
                """
                # Phase 1 Visual Foundation

                Build the showroom before the engine.

                ## Milestones
                - [x] Mica-backed floating window
                - [x] Compact capture mode
                - [x] Work mode with rich previews
                - [x] Tray-first lifecycle
                - [x] FTS5 search with highlights
                - [ ] Image capture support

                ## Tech Stack
                ```csharp
                builder.Services.AddHostedService<AppLifecycleService>();
                builder.Services.AddSingleton<IHistoryService, ClipboardRepository>();
                builder.Services.AddHostedService<ClipboardMonitorHostedService>();
                ```

                > Ship the visual foundation before adding new capture formats.

                [Project plan](https://github.com/example/clipt) · [Figma mockups](https://figma.com/example)
                """,
                ContentType.Markdown,
                "Obsidian",
                now.AddMinutes(-2),
                isPinned: true),
            Create(
                "Accent color",
                "#14B8A6 teal, rgb(20 184 166), hsl(174 80% 40%). Primary accent for the Clipt design system.",
                """
                #14B8A6
                #0D9488
                #0F766E
                #115E59
                #134E4A
                """,
                ContentType.Color,
                "Figma",
                now.AddMinutes(-5),
                isPinned: true),

            // ── Code snippets ──────────────────────────────────
            Create(
                "MainViewModel.cs",
                "CommunityToolkit MVVM partial property and relay command wiring for the shell.",
                """
                public sealed partial class MainViewModel : ObservableObject
                {
                    private readonly IHistoryService _historyService;

                    [ObservableProperty]
                    public partial string SearchText { get; set; } = string.Empty;

                    [ObservableProperty]
                    public partial ClipboardItemViewModel? SelectedItem { get; set; }

                    [RelayCommand]
                    private async Task TogglePin(ClipboardItemViewModel? item)
                    {
                        if (item is null) return;
                        var newState = !item.IsPinned;
                        await _historyService.SetPinnedAsync(item.Id, newState, CancellationToken.None);
                    }
                }
                """,
                ContentType.Code,
                "Rider",
                now.AddMinutes(-12),
                language: "C#"),
            Create(
                "useHistorySearch.ts",
                "Custom React hook with SWR for debounced full-text search against the Clipt API.",
                """
                import useSWR from "swr";
                import { useDebounce } from "use-debounce";

                export function useHistorySearch(query: string) {
                  const [debounced] = useDebounce(query, 250);
                  const { data, error, isLoading } = useSWR(
                    debounced ? `/api/clips/search?q=${encodeURIComponent(debounced)}` : null,
                    (url: string) => fetch(url).then((r) => r.json()),
                    { keepPreviousData: true },
                  );
                  return { results: data ?? [], error, isLoading };
                }
                """,
                ContentType.Code,
                "VS Code",
                now.AddMinutes(-20),
                language: "TypeScript"),

            // ── SQL ────────────────────────────────────────────
            Create(
                "Dashboard query",
                "Monthly clip volume by content type with FTS5 search join.",
                """
                SELECT
                  c.content_type,
                  strftime('%Y-%m', c.created_at / 1000, 'unixepoch') AS month,
                  COUNT(1) AS clips,
                  ROUND(AVG(c.byte_size)) AS avg_bytes
                FROM clipboard_items c
                WHERE c.created_at > @since
                  AND c.id IN (
                    SELECT item_id FROM clipboard_items_fts
                    WHERE clipboard_items_fts MATCH @query
                  )
                GROUP BY c.content_type, month
                ORDER BY month DESC, clips DESC;
                """,
                ContentType.Code,
                "DataGrip",
                now.AddMinutes(-30),
                language: "SQL"),

            // ── JSON ───────────────────────────────────────────
            Create(
                "GitHub workflow payload",
                "CI status response from the GitHub Actions API for the main branch.",
                """
                {
                  "repository": {
                    "full_name": "mwilson/clipt",
                    "default_branch": "main"
                  },
                  "workflow_runs": [
                    {
                      "id": 9876543210,
                      "name": "ci",
                      "status": "completed",
                      "conclusion": "success",
                      "head_branch": "main",
                      "run_number": 42,
                      "created_at": "2026-05-06T12:30:00Z"
                    }
                  ],
                  "total_count": 1
                }
                """,
                ContentType.Json,
                "Windows Terminal",
                now.AddMinutes(-38),
                language: "JSON"),
            Create(
                "AppSettings.json",
                "Clipt configuration snapshot showing privacy filters and UI preferences.",
                """
                {
                  "autoPasteOnEnter": true,
                  "maxHistoryItems": 500,
                  "windowOpacity": 0.92,
                  "isWorkMode": false,
                  "openHotkey": "Ctrl+Shift+V",
                  "ignoredAppNames": ["Windows Security", "Task Manager"],
                  "ignoredPatterns": ["regex:PIN\\d{6}"]
                }
                """,
                ContentType.Json,
                "VS Code",
                now.AddMinutes(-45),
                language: "JSON"),

            // ── URLs ───────────────────────────────────────────
            Create(
                "Design system links",
                "Primary design and documentation references for the Clipt visual system.",
                """
                https://github.com/mwilson/clipt
                https://m3.material.io/theme-builder
                https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/mica
                https://www.sqlite.org/fts5.html
                """,
                ContentType.Url,
                "Microsoft Edge",
                now.AddHours(-1)),
            Create(
                "NuGet packages",
                "https://www.nuget.org/packages/Microsoft.Data.Sqlite/",
                "https://www.nuget.org/packages/Microsoft.Data.Sqlite/",
                ContentType.Url,
                "Chrome",
                now.AddHours(-2)),

            // ── Prose / Notes ──────────────────────────────────
            Create(
                "Release checklist",
                "Run FTS rebuild migration, bump patch version, tag release on main, and update the changelog before publishing.",
                """
                Before tagging v0.2.0:

                1. Run FTS rebuild migration on the release config
                2. Bump patch version in Directory.Build.props
                3. Push signed tag to origin
                4. Draft GitHub release with auto-generated notes
                5. Update CHANGELOG.md with new features and fixes
                6. Verify the MSIX package installs cleanly on a fresh VM
                """,
                ContentType.Text,
                "Notion",
                now.AddHours(-4)),
            Create(
                "Morning stand-up notes",
                "Demo items working; need to wire the seeder button in settings. Image capture blocked on WPF DPI scaling.",
                "Demo items working; need to wire the seeder button in settings. Image capture blocked on WPF DPI scaling.",
                ContentType.Text,
                "Slack",
                now.AddHours(-6)),

            // ── File paths ─────────────────────────────────────
            Create(
                "Design references",
                "C:\\Users\\Matt\\Pictures\\clipt-mockup.png; C:\\Users\\Matt\\Documents\\phase-one-notes.md",
                "C:\\Users\\Matt\\Pictures\\clipt-mockup.png\r\nC:\\Users\\Matt\\Documents\\phase-one-notes.md",
                ContentType.File,
                "File Explorer",
                now.AddDays(-1),
                filePaths:
                [
                    "C:\\Users\\Matt\\Pictures\\clipt-mockup.png",
                    "C:\\Users\\Matt\\Documents\\phase-one-notes.md",
                ]),

            // ── Image ──────────────────────────────────────────
            Create(
                "Screenshot placeholder",
                "Demo bitmap for image preview layout testing.",
                "pack://application:,,,/Clipt.App;component/Resources/Images/PreviewPlaceholder.png",
                ContentType.Image,
                "ShareX",
                now.AddDays(-2),
                imageUri: "pack://application:,,,/Clipt.App;component/Resources/Images/PreviewPlaceholder.png"),

            // ── More code ──────────────────────────────────────
            Create(
                "ClipboardRepository.SaveAsync",
                "Upsert path for clipboard items with content-hash deduplication and FTS sync.",
                """
                public async Task<ClipboardItem> SaveAsync(ClipboardItem item, CancellationToken ct)
                {
                    await _connectionLock.WaitAsync(ct);
                    try
                    {
                        var connection = await GetConnectionAsync(ct);

                        // Check for existing item by content hash
                        using var check = connection.CreateCommand();
                        check.CommandText = "SELECT ... FROM clipboard_items WHERE content_hash = @hash";
                        check.Parameters.AddWithValue("@hash", item.ContentHash);

                        await using var reader = await check.ExecuteReaderAsync(ct);
                        if (await reader.ReadAsync(ct))
                        {
                            var existing = MapRow(reader);
                            await reader.DisposeAsync();
                            // Update last_used_at and use_count on duplicate
                            await BumpDuplicateAsync(connection, item.ContentHash, ct);
                            return existing with { UseCount = existing.UseCount + 1 };
                        }

                        // Insert new item with format rows
                        await InsertItemAsync(connection, item, ct);
                        return item;
                    }
                    finally { _connectionLock.Release(); }
                }
                """,
                ContentType.Code,
                "Rider",
                now.AddDays(-3),
                language: "C#"),
            Create(
                "clipboard_schema.sql",
                "Core clipboard_items and clipboard_items_fts DDL with triggers.",
                """
                CREATE TABLE clipboard_items (
                  id            TEXT PRIMARY KEY,
                  content_hash  TEXT NOT NULL UNIQUE,
                  content_type  TEXT NOT NULL DEFAULT 'Text',
                  title         TEXT NOT NULL,
                  preview_text  TEXT NOT NULL DEFAULT '',
                  content_text  TEXT NOT NULL,
                  source_app_name TEXT NOT NULL DEFAULT '',
                  source_app_path TEXT,
                  byte_size     INTEGER NOT NULL DEFAULT 0,
                  is_pinned     INTEGER NOT NULL DEFAULT 0,
                  pin_order     INTEGER,
                  is_favorite   INTEGER NOT NULL DEFAULT 0,
                  created_at    INTEGER NOT NULL,
                  last_used_at  INTEGER NOT NULL,
                  use_count     INTEGER NOT NULL DEFAULT 0
                );

                CREATE VIRTUAL TABLE clipboard_items_fts USING fts5(
                  title, preview_text, content_text,
                  content=clipboard_items,
                  content_rowid=rowid
                );
                """,
                ContentType.Code,
                "DataGrip",
                now.AddDays(-4),
                language: "SQL"),
        ];
    }

    private static ClipboardItem Create(
        string title,
        string preview,
        string content,
        ContentType contentType,
        string sourceApp,
        DateTimeOffset createdAt,
        bool isPinned = false,
        string? language = null,
        string? imageUri = null,
        IReadOnlyList<string>? filePaths = null)
    {
        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = ClipboardContentHasher.ComputeHash(content),
            Title = title,
            PreviewText = preview,
            Content = content,
            ContentType = contentType,
            SourceAppName = sourceApp,
            CreatedAt = createdAt,
            IsPinned = isPinned,
            Language = language,
            ImageUri = imageUri,
            FilePaths = filePaths ?? [],
            ByteSize = content.Length * sizeof(char),
            LastUsedAt = createdAt,
            UseCount = 0,
            Formats = CreateFormats(content, filePaths),
        };
    }

    private static IReadOnlyList<ClipboardFormat> CreateFormats(string content, IReadOnlyList<string>? filePaths)
    {
        var formats = new List<ClipboardFormat>
        {
            new(ClipboardFormatNames.UnicodeText, content),
        };

        if (filePaths is { Count: > 0 })
        {
            formats.Insert(0, new ClipboardFormat(
                ClipboardFormatNames.FileDrop,
                string.Join(Environment.NewLine, filePaths)));
        }

        return formats;
    }
}
