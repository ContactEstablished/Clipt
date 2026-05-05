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
            Create(
                "Phase 1 release notes",
                "Markdown checklist for the visual foundation pass.",
                """
                # Phase 1 Visual Foundation

                Build the showroom before the engine:

                - Mica-backed floating window
                - Compact capture mode
                - Work mode with rich previews
                - Tray-first lifecycle

                ```csharp
                builder.Services.AddHostedService<AppLifecycleService>();
                ```

                [Project plan](https://github.com/example/clipt)
                """,
                ContentType.Markdown,
                "Obsidian",
                now.AddMinutes(-2),
                isPinned: true),
            Create(
                "MainWindowViewModel.cs",
                "CommunityToolkit partial property sample for the shell state.",
                """
                public sealed partial class MainWindowViewModel : ObservableObject
                {
                    [ObservableProperty]
                    public partial string SearchText { get; set; } = string.Empty;
                }
                """,
                ContentType.Code,
                "Rider",
                now.AddMinutes(-8),
                language: "C#"),
            Create(
                "GitHub API response",
                "JSON payload with repository metadata and CI status.",
                """
                {
                  "repository": "mwilson/clipt",
                  "workflow": "ci",
                  "status": "passing",
                  "runner": "windows-latest"
                }
                """,
                ContentType.Json,
                "Windows Terminal",
                now.AddMinutes(-16),
                language: "JSON"),
            Create(
                "Accent color",
                "#14B8A6 teal, rgb(20 184 166), hsl(174 80% 40%).",
                "#14B8A6",
                ContentType.Color,
                "Figma",
                now.AddMinutes(-25),
                isPinned: true),
            Create(
                "Clipt repository",
                "https://github.com/mwilson/clipt",
                "https://github.com/mwilson/clipt",
                ContentType.Url,
                "Microsoft Edge",
                now.AddHours(-1)),
            Create(
                "Screenshot placeholder",
                "Demo bitmap for image preview layout.",
                "pack://application:,,,/Clipt.App;component/Resources/Images/PreviewPlaceholder.png",
                ContentType.Image,
                "ShareX",
                now.AddHours(-3),
                imageUri: "pack://application:,,,/Clipt.App;component/Resources/Images/PreviewPlaceholder.png"),
            Create(
                "Meeting follow-up",
                "Send the CI badge and Phase 1 screenshot after the first green build.",
                "Send the CI badge and Phase 1 screenshot after the first green build.",
                ContentType.Text,
                "Teams",
                now.AddHours(-5)),
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
            Create(
                "SQL search prototype",
                "FTS5 query shape for Phase 2.",
                """
                SELECT item_id, highlight(clipboard_items_fts, 0, '<mark>', '</mark>')
                FROM clipboard_items_fts
                WHERE clipboard_items_fts MATCH @query
                ORDER BY rank;
                """,
                ContentType.Code,
                "DataGrip",
                now.AddDays(-2),
                language: "SQL"),
            Create(
                "Plain text snippet",
                "Privacy-first local clipboard history, searchable and fast.",
                "Privacy-first local clipboard history, searchable and fast.",
                ContentType.Text,
                "Notepad",
                now.AddDays(-3)),
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
            Formats =
            [
                new ClipboardFormat("CF_UNICODETEXT", content),
            ],
        };
    }
}
