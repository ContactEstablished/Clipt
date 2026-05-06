# TASKS.md — Clipt (WPF Clipboard Manager)

## Project Overview

Clipt is a polished Windows clipboard manager inspired by ClipMon for macOS (`C9-Labs/clipmon`), but built native to Windows with WPF. Lives in the system tray, opens via global hotkey, and provides searchable clipboard history with rich previews, markdown rendering, pinning, transparency control, and respect for password manager privacy flags. Distributed as a signed installer via GitHub Releases with auto-update support.

## Goals

This is a **portfolio piece first, useful tool second.** The intent is for a hiring manager to land on the GitHub repo, glance at a screenshot, and immediately think "this developer has skills." Every decision should be filtered through that lens.

1. **Make the screenshot stunning.** The README's hero image needs to convey craft, polish, and modern design at a glance. This drives the whole UI direction.
2. **Demonstrate professional WPF/.NET desktop development** — clean MVVM, proper DI, async/await done right, sensible project structure, real tests.
3. **Match ClipMon's core feature set** (history, search, markdown, pins, previews) and exceed it where Windows enables it.
4. **Feel native to Windows** — proper system tray, configurable hotkey, Fluent-inspired styling, Win11 Mica/Acrylic backdrop where supported.
5. **Ship a polished GitHub release** — installer, auto-updates, signed binaries (or documented SmartScreen workaround), great README.

## Visual Polish & Screenshot Strategy

The README hero screenshot is the single most important deliverable on this project. Most visitors won't run the app — they'll glance at the image and form an opinion in 3 seconds. Design with that in mind.

### Design Direction
- **Modern dark theme by default** — saturated blacks (#0E0E10 / #1A1A1D range), not flat #000. Subtle vertical gradient on the window background gives depth.
- **Single accent color** chosen deliberately and used sparingly — recommend a teal-to-cyan (your favorite color is teal — lean into it) or a soft electric blue. Used for selection state, pin indicators, focused search box border, and the active-format pill.
- **Typography matters more than people think.** Use `Inter` or `Geist` for UI text (ship the font with the app — both are open source and free to embed), and `JetBrains Mono` or `Geist Mono` for code/preview content. Default Segoe UI looks dated in screenshots compared to these.
- **Generous whitespace.** Cramped UIs photograph badly. Err on the side of more padding than feels necessary — this is a screenshot-driven design.
- **Subtle depth, not heavy shadows.** A 1px inner border with a slight lighter tone, plus a soft 24px drop shadow on the window itself when composited.
- **Win11 Mica/Acrylic backdrop** on Windows 11 (`DwmSetWindowAttribute` with `DWMWA_SYSTEMBACKDROP_TYPE`) — this single feature makes screenshots look instantly modern. Promote this from stretch goal to V1.
- **Rounded corners** on the window (16-20px radius) and on internal elements (8-12px). Sharp corners read as 2010-era WPF.

### The Hero Screenshot Composition
Plan the screenshot before designing the UI. Sketch out exactly what it should show:
- Main window in foreground with realistic, varied clipboard content visible in the list (a markdown snippet, a code block with syntax highlighting, an image thumbnail, a hex color, a URL with preview)
- Right-side preview pane showing the rendered markdown of a *visually interesting* item — a snippet with headings, a fenced code block, a bulleted list, maybe an inline link
- Search box with real text in it that matches highlighted results in the list
- Pinned items at top, with the pin icon visible in your accent color
- Tray icon visible in the system tray with a tooltip
- Composited over a tasteful Windows 11 desktop background with a real-looking app behind (VS Code, browser) showing where the paste would land
- Subtle device-frame mockup if used in the README — but not too gimmicky

### Side-by-Side Mode Screenshot
The dual-mode design (capture vs. work) is a strong differentiator and lends itself to a great second README image: capture mode and work mode side by side, captioned "Compact for capture, expanded for review." This immediately communicates a thoughtful product decision rather than just "another clipboard manager." Worth investing time in.

### Demo Content Strategy
Ship a "Load demo content" feature behind a hidden setting or first-run option that populates the history with curated, visually interesting items for screenshots and demo videos. This lets you reproduce the marketing screenshot consistently and lets reviewers see Clipt at its best on first launch. Demo items might include:
- A markdown blog post excerpt
- A C# code snippet with syntax highlighting
- A SQL query
- A JSON response
- A hex color palette
- An image (a stock screenshot)
- A multi-line URL list
- A short Lorem ipsum

### README Structure
- Hero screenshot at the very top, before the title
- One-line tagline below the project name
- 3-4 animated GIFs or short MP4s showing key interactions: opening with hotkey, fuzzy search, pinning, markdown preview toggle
- Feature list with small icons (use emoji or Lucide SVGs)
- Installation section with a single prominent download button image linking to the latest GitHub release
- Tech stack section with shield.io badges (.NET, WPF, SQLite, License)
- Screenshots gallery showing settings, dark/light themes, etc.
- "Why Clipt" section explaining the privacy-first, local-first philosophy
- Contributing section
- License

### Tools to Use for the README Assets
- **ScreenToGif** for short demo GIFs (free, Windows native, excellent quality control)
- **ShareX** for the static screenshots with proper window capture and shadow
- **Carbon** (carbon.now.sh) only for code blocks in the README, not for app screenshots
- **Figma** for any composite hero images that combine app screenshot + desktop background + device frame

### Calibration Test
Before considering the project "done," do this exercise: take the hero screenshot, send it to a developer friend with no context other than "what's your gut reaction to this app?" If they don't immediately say something positive about the design, iterate on the UI before iterating on more features. This is the single most valuable feedback loop for a portfolio project.

## Tech Stack

- **.NET 9** with C# 13 (preview features for partial property support)
- **WPF** with MVVM via **CommunityToolkit.Mvvm 8.4+** (partial properties, source generators)
- **Microsoft.Extensions.DependencyInjection** for IoC
- **Microsoft.Extensions.Hosting** for lifecycle management and background services
- **Microsoft.Extensions.Logging** + **Serilog** with rolling file sink
- **SQLite** via `Microsoft.Data.Sqlite` with **FTS5** enabled for full-text search (no EF Core — overkill for this)
- **Markdig** for markdown parsing
- **Markdig.Wpf** (`Markdig.Wpf` NuGet package) for rendering markdown to a native WPF `FlowDocument` — lightweight, no browser runtime, full WPF theming integration
- **H.NotifyIcon.Wpf** for system tray
- **NHotkey.Wpf** for global hotkey registration (wraps `RegisterHotKey` Win32 API)
- **TextCopy** or direct Win32 P/Invoke for clipboard operations (Win32 gives us format-level control we need)
- **Velopack** for installer + auto-update (modern Squirrel successor — single command release publishing)
- **GitHub Actions** for CI/CD (build, test, release with signed binaries)
- **xUnit** + **FluentAssertions** for testing
- **WPF-UI** library (optional) for Fluent Design controls and theming

## Project Structure

```
Clipt/
├── src/
│   ├── Clipt.App/                          # WPF application
│   │   ├── App.xaml / App.xaml.cs            # Bootstrapper, DI host setup
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml               # Main floating clipboard window
│   │   │   ├── SettingsWindow.xaml           # Settings dialog
│   │   │   ├── PreviewPane.xaml              # Right-side preview UserControl
│   │   │   └── TrayContextMenu.xaml          # System tray right-click menu
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs              # Main window VM (history, search, selection)
│   │   │   ├── ClipboardItemViewModel.cs     # Per-item VM
│   │   │   ├── SettingsViewModel.cs          # Settings dialog VM
│   │   │   └── PreviewViewModel.cs           # Preview pane VM
│   │   ├── Controls/
│   │   │   ├── MarkdownPreviewControl.xaml   # FlowDocument-based markdown renderer (Markdig.Wpf)
│   │   │   ├── CodePreviewControl.xaml       # Syntax-highlighted code preview
│   │   │   ├── ImagePreviewControl.xaml      # Image preview
│   │   │   └── TransparencySlider.xaml       # Custom slider with live binding
│   │   ├── Converters/
│   │   │   ├── ContentTypeIconConverter.cs
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   └── TimestampConverter.cs
│   │   ├── Themes/
│   │   │   ├── Dark.xaml
│   │   │   ├── Light.xaml
│   │   │   └── Generic.xaml                  # Default styles
│   │   ├── Behaviors/
│   │   │   └── WindowDragBehavior.cs         # Drag borderless window
│   │   ├── Resources/
│   │   │   ├── Icons/                        # App icons, tray icons
│   │   │   └── Strings.resx                  # Localizable strings
│   │   └── Clipt.App.csproj
│   │
│   ├── Clipt.Core/                         # Business logic (testable, no WPF deps)
│   │   ├── Services/
│   │   │   ├── IClipboardMonitor.cs
│   │   │   ├── ClipboardMonitor.cs           # Win32 clipboard event listener
│   │   │   ├── IClipboardWriter.cs
│   │   │   ├── ClipboardWriter.cs            # Sets clipboard with format preservation
│   │   │   ├── IHistoryService.cs
│   │   │   ├── HistoryService.cs             # CRUD over clipboard items
│   │   │   ├── ISearchService.cs
│   │   │   ├── SearchService.cs              # FTS5 fuzzy search
│   │   │   ├── IContentTypeDetector.cs
│   │   │   ├── ContentTypeDetector.cs        # Detect markdown, code, URL, color, etc.
│   │   │   ├── IPrivacyFilter.cs
│   │   │   └── PrivacyFilter.cs              # Honor CF_CLIPBOARD_VIEWER_IGNORE + ignore list
│   │   ├── Models/
│   │   │   ├── ClipboardItem.cs              # Core entity
│   │   │   ├── ClipboardFormat.cs            # Per-format payload
│   │   │   ├── ContentType.cs                # Enum: Text, Markdown, Code, Url, Image, File, Color
│   │   │   └── AppSettings.cs                # User settings model
│   │   └── Clipt.Core.csproj
│   │
│   ├── Clipt.Data/                         # SQLite persistence
│   │   ├── ClipboardRepository.cs
│   │   ├── SettingsRepository.cs
│   │   ├── Migrations/
│   │   │   ├── 001_InitialSchema.sql
│   │   │   ├── 002_AddFts5.sql
│   │   │   └── MigrationRunner.cs
│   │   └── Clipt.Data.csproj
│   │
│   ├── Clipt.Portable/                     # Encrypted export / import (V2)
│   │   ├── IExportService.cs
│   │   ├── ExportService.cs                  # Build encrypted .clipt file from history
│   │   ├── IImportService.cs
│   │   ├── ImportService.cs                  # Decrypt + merge .clipt file
│   │   ├── PortableFileFormat.cs             # JSON DTOs + version handling
│   │   ├── Crypto/
│   │   │   ├── PortableCrypto.cs             # AES-256-GCM encrypt/decrypt
│   │   │   └── PassphraseKdf.cs              # PBKDF2/Argon2id key derivation
│   │   └── Clipt.Portable.csproj
│   │
│   ├── Clipt.AI/                           # AI-assisted actions (V3)
│   │   ├── IAIService.cs
│   │   ├── AnthropicService.cs               # Claude API wrapper
│   │   ├── Actions/
│   │   │   ├── SummarizeAction.cs
│   │   │   ├── TranslateAction.cs
│   │   │   ├── ConvertToMarkdownAction.cs
│   │   │   ├── ExplainCodeAction.cs
│   │   │   └── RewriteAction.cs
│   │   └── Clipt.AI.csproj
│   │
│   └── Clipt.Interop/                      # Win32 P/Invoke wrappers
│       ├── NativeMethods.cs                  # User32, Kernel32 imports
│       ├── ClipboardNative.cs                # Direct clipboard format access
│       ├── HotkeyNative.cs                   # RegisterHotKey wrappers
│       └── Clipt.Interop.csproj
│
├── tests/
│   ├── Clipt.Core.Tests/
│   │   ├── ContentTypeDetectorTests.cs
│   │   ├── PrivacyFilterTests.cs
│   │   ├── SearchServiceTests.cs
│   │   └── HistoryServiceTests.cs
│   ├── Clipt.Data.Tests/
│   │   └── ClipboardRepositoryTests.cs
│   └── Clipt.Portable.Tests/
│       ├── PortableCryptoTests.cs
│       ├── ExportImportRoundTripTests.cs
│       └── CrossVersionCompatibilityTests.cs
│
├── installer/
│   ├── velopack.config                       # Velopack configuration
│   └── icons/                                # Installer branding
│
├── .github/
│   └── workflows/
│       ├── ci.yml                            # Build + test on PR
│       └── release.yml                       # Build, sign, publish to Releases
│
├── docs/
│   ├── screenshots/                          # README screenshots
│   ├── ARCHITECTURE.md
│   └── PRIVACY.md
│
├── Clipt.sln
├── Directory.Build.props                     # Shared MSBuild properties (LangVersion=preview)
├── Directory.Packages.props                  # Central package management
├── README.md
├── LICENSE                                   # GPL-3.0 to match upstream Clipt
└── .gitignore
```

## Database Schema

**clipboard_items**
- `id` (INTEGER PRIMARY KEY)
- `content_hash` (TEXT) — SHA256 of primary format, for dedup
- `primary_format` (TEXT) — text, html, rtf, image, files
- `content_type` (TEXT) — text, markdown, code, url, image, files, color, json
- `preview_text` (TEXT) — first ~500 chars for list display
- `source_app_name` (TEXT, nullable) — app that originated the copy
- `source_app_path` (TEXT, nullable)
- `byte_size` (INTEGER)
- `is_pinned` (INTEGER) — boolean
- `pin_order` (INTEGER, nullable)
- `is_favorite` (INTEGER) — boolean
- `created_at` (INTEGER) — unix timestamp
- `last_used_at` (INTEGER) — unix timestamp
- `use_count` (INTEGER) DEFAULT 0

**clipboard_formats** (one item can have many formats)
- `id` (INTEGER PRIMARY KEY)
- `item_id` (INTEGER FK)
- `format_name` (TEXT) — CF_UNICODETEXT, CF_HTML, CF_RTF, CF_BITMAP, CF_HDROP, custom
- `payload` (BLOB) — raw format data
- `payload_text` (TEXT, nullable) — text representation if applicable

**clipboard_items_fts** (FTS5 virtual table)
- Mirrors `preview_text` and `content_type` for fast fuzzy search

**tags**
- `id`, `name`, `color`

**item_tags**
- `item_id`, `tag_id`

**ignored_apps**
- `id`, `app_name`, `app_path`

**ignored_patterns** (regex patterns to never capture)
- `id`, `pattern`, `description`

**settings** (key/value)
- `key`, `value`

## Core Features (V1)

### Clipboard Monitoring
- [ ] Win32 clipboard listener via `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE`
- [ ] Capture all available formats per copy event (not just text)
- [ ] Detect and skip clipboard content with `CF_CLIPBOARD_VIEWER_IGNORE` flag (password managers)
- [ ] Identify source application via `GetForegroundWindow` + process info
- [ ] Hash-based duplicate detection (move existing entry to top instead of duplicating)
- [ ] Configurable max history size with auto-pruning of oldest unpinned items
- [ ] Configurable per-format size limits (e.g., skip images > 50MB)

### Content Type Detection
- [ ] Plain text (default fallback)
- [ ] Markdown (heuristic: presence of `#`, `**`, `[](`, fenced code blocks, etc.)
- [ ] Code (heuristic + extension detection if from a file path)
- [ ] URL (regex match on full content)
- [ ] Color (hex `#RGB` / `#RRGGBB`, `rgb()`, `hsl()`)
- [ ] JSON (attempt parse)
- [ ] Image (from format)
- [ ] Files (from `CF_HDROP`)

### Main Window UI
- [ ] Borderless, rounded-corner window
- [ ] Two-pane layout: left = item list, right = preview pane
- [ ] Search box at top with live fuzzy filtering (FTS5 backed)
- [ ] List items show: icon (by content type), preview text, source app, timestamp, pin indicator
- [ ] Keyboard navigation (arrow keys, Enter to paste, Esc to close)
- [ ] Mouse + keyboard friendly
- [ ] Always-on-top toggle
- [ ] **Transparency slider** (10% to 100%) bound to `Window.Opacity`, persists in settings
- [x] Prominent paused-capture visual cue in the main window (e.g., flag/banner/status pill) so users can immediately tell clipboard capture is paused
- [ ] Drag-to-move on title area
- [ ] Remembers last position and size
- [ ] Drop shadow / acrylic backdrop on Windows 11 (via `DwmSetWindowAttribute`)

### Dual-Mode View (Capture vs. Work)

The window has two distinct modes that match the two main use cases for a clipboard manager:

- **Capture mode (collapsed, single column ~380px wide)** — the default for fast summon-scan-paste-dismiss workflows. Single-column item list, denser rows, minimal metadata (icon + title + relative time only), no preview pane. Optimized for "I just want to find that thing I copied 3 minutes ago and paste it."
- **Work mode (expanded, two-pane ~880px wide)** — for organizing, reviewing, or curating clips. List on the left, full preview pane on the right with markdown rendering, format toggles, and metadata.

#### Behavior
- [ ] Window opens in last-used mode (persisted per user)
- [ ] **Tab** or **Ctrl+E** toggles between modes
- [ ] Smooth animated resize when toggling — `Storyboard` animating `Width` over 180ms with cubic ease
- [ ] Window anchors to its current screen position during resize (don't jump to center)
- [ ] **Auto-expand on item single-click** is an opt-in setting — when on, clicking an item in capture mode slides the window into work mode and selects that item
- [ ] Each mode remembers its own preferred size — user can resize work mode wider without affecting capture mode
- [ ] Capture mode: items are 32px tall, 12px font, single-line title with relative time on the right
- [ ] Work mode: items are 56px tall with two-line layout (title + preview text + source app metadata)
- [ ] Mode indicator in the title bar (small icon button) so users discover the toggle even without knowing the hotkey
- [ ] Settings → Appearance: default mode dropdown, default sizes for each mode, toggle for auto-expand-on-click

### Preview Pane
- [ ] Text: monospace for code, proportional for prose
- [ ] **Markdown: native WPF FlowDocument via Markdig.Wpf with toggle between rendered and raw views** (lightweight, theme-aware, no browser runtime)
- [ ] Code: syntax highlighting via `ColorCode.Wpf` or `AvalonEdit` (both render to native WPF — no WebView2 needed)
- [ ] Image: scaled preview with dimensions and size
- [ ] URL: show URL + attempt to fetch og:image and og:title (cached, optional, disabled by default for privacy)
- [ ] Files: list with icons and paths
- [ ] Color: visual swatch + hex/rgb/hsl conversions
- [ ] JSON: pretty-printed with collapsible nodes (use AvalonEdit with JSON folding)

### Search
- [ ] FTS5 full-text search over preview_text
- [ ] Fuzzy matching (FTS5 doesn't do fuzzy natively — use trigram extension or fallback to LIKE for short queries)
- [ ] Filter chips: All, Text, Markdown, Code, URLs, Images, Files, Pinned
- [ ] Search history within the search box

### Pinning & Organization
- [ ] Pin/unpin via keyboard shortcut, context menu, or pin icon
- [ ] Pinned items always appear at top of list
- [ ] Drag-to-reorder pins
- [ ] Tags with custom colors
- [ ] Tag filtering in search

### System Tray
- [ ] Tray icon with state indicators (idle, capturing, paused)
- [ ] Left-click: open main window
- [ ] Right-click context menu:
  - Open Clipt
  - Pause/Resume capturing
  - Clear history (with confirmation)
  - Settings
  - About
  - Quit
- [ ] Notification on first launch explaining the app is running

### Global Hotkey
- [ ] Default: `Ctrl+Shift+V` (Win+V is reserved by Windows)
- [ ] Configurable in settings
- [ ] Conflict detection on registration

### Settings Window
- [ ] General tab:
  - Start with Windows (registry HKCU\Software\Microsoft\Windows\CurrentVersion\Run)
  - Theme: System / Light / Dark
  - Language
  - **Auto-paste on Enter** (checkbox; default enabled) — when off, Enter only copies to clipboard
  - **Restore previous clipboard after paste** (checkbox; default off)
- [ ] Hotkeys tab:
  - Open main window (with **"Set your own hotkey"** capture control — listens for any key combination, validates conflicts, allows custom binding)
  - Toggle pause/resume
  - Quick paste 1-9 (Ctrl+Shift+1 through 9 for top items)
  - Reset to defaults button
- [ ] History tab:
  - Max items to keep
  - Auto-prune after N days
  - Skip clipboard data larger than N MB
- [ ] Privacy tab:
  - Ignored applications list (with picker)
  - Ignored regex patterns
  - Honor password manager flag (default on)
  - Pause when specified apps are focused
- [ ] Appearance tab:
  - Default opacity
  - Window size and position behavior
  - Show source app name
  - Compact / comfortable list density
- [ ] Backup & Transfer tab (V2 — see Phase 2 section):
  - Export history button (opens Export wizard)
  - Import history button (opens Import wizard)
  - Last export timestamp
  - Optional: scheduled auto-export (off / daily / weekly) with target folder picker
- [ ] About tab:
  - Version, license, GitHub link, check for updates button

### Pasting
- [ ] **Auto-paste toggle in settings** — when enabled (default), pressing Enter copies to clipboard AND simulates Ctrl+V into the previous foreground window via `SendInput`. When disabled, Enter only copies to clipboard and the user pastes manually with Ctrl+V.
- [ ] Track previous foreground window via `GetForegroundWindow` before main window opens, so we know where to send the paste
- [ ] Single Enter on a list item: behavior depends on auto-paste toggle (above)
- [ ] Shift-Enter: paste as plain text (strip formatting)
- [ ] Ctrl-Enter: paste as markdown source (raw, even if rendered preview is shown)
- [ ] Restore previous clipboard contents after paste (optional, configurable — defaults to off because it surprises some users)
- [ ] Quick paste hotkeys (Ctrl+Shift+1 through 9): paste the Nth most recent item directly without opening the window

### Privacy & Security
- [ ] All data stored locally in `%LOCALAPPDATA%\Clipt\clipt.db`
- [ ] Database optionally encrypted via SQLCipher (stretch goal — adds complexity)
- [ ] Auto-clear sensitive content (numbers matching credit card / SSN regex)
- [ ] Pause-on-focus for specified apps (banking sites, password managers)
- [ ] No telemetry, no network calls except optional update check + opt-in URL preview fetch

### Logging & Diagnostics
- [ ] Serilog rolling file logs in `%LOCALAPPDATA%\Clipt\logs\`
- [ ] Log levels configurable (default: Warning in release, Debug in development)
- [ ] "Open log folder" menu item in tray
- [ ] Crash reporter writes minidump to logs folder

### Distribution & Updates
- [ ] Velopack package generation
- [ ] Auto-update check on startup (configurable interval)
- [ ] In-app "Check for updates now" button
- [ ] Code-signing pipeline (use a self-signed cert initially; document path to real cert)
- [ ] GitHub Actions workflow that builds, signs, and publishes Velopack release on tag push
- [ ] Installer with proper Add/Remove Programs entry, Start Menu shortcut, optional desktop shortcut

## Setup Tasks

- [ ] `dotnet new sln -n Clipt`
- [ ] Create projects per structure above
- [ ] `Directory.Build.props` with `<LangVersion>preview</LangVersion>`, nullable reference types enabled, treat warnings as errors
- [ ] `Directory.Packages.props` for central package management
- [ ] `.gitignore` for .NET, VS, Rider, build outputs
- [ ] Initial WPF window with placeholder content
- [ ] Wire up `Microsoft.Extensions.Hosting` in `App.xaml.cs` for DI + lifetime
- [ ] Set up Serilog with file + debug sinks
- [ ] Initial SQLite migration with empty schema
- [ ] Add xUnit test projects with sample passing tests
- [ ] GitHub Actions CI workflow (build + test on push/PR)
- [ ] Initial README with project description, screenshots placeholder, build instructions

## Phase 2 — Encrypted Export & Import

Instead of automatic cloud sync, Clipt offers a manual portable backup format — a single encrypted file that the user can save anywhere (Drive, Dropbox, OneDrive, USB stick, email to themselves) and import on another machine. This is the same model Postman, KeePass, Bitwarden, Authy, and many other apps use successfully.

### Why this approach
- **Universal portability** — works with any storage medium, not tied to a specific cloud provider
- **No OAuth complexity** — no Google verification, no API quotas, no client secrets to manage
- **Privacy is simple** — encryption happens in Clipt, the file is opaque to whatever stores it
- **No background service needed** — no sync engine, no conflict resolution, no quota tracking
- **User stays in control** — they decide when to export, what to include, and where to store it
- **Easier to test and ship** — a contained feature with clear boundaries

### File Format
- **Extension:** `.clipt` (or `.clipt.json.enc` to be more explicit)
- **Container:** JSON wrapped in AES-256-GCM encryption
- **Structure (decrypted):**
  ```json
  {
    "version": 1,
    "exportedAt": "2026-05-04T...",
    "exportedFrom": "Matthew's-PC",
    "itemCount": 142,
    "items": [
      {
        "id": "uuid",
        "createdAt": "...",
        "contentType": "markdown",
        "primaryFormat": "text",
        "previewText": "...",
        "isPinned": true,
        "tags": ["work"],
        "formats": [
          { "name": "CF_UNICODETEXT", "payload": "base64..." },
          { "name": "CF_HTML", "payload": "base64..." }
        ]
      }
    ],
    "settings": { ... }
  }
  ```
- **Encryption:** AES-256-GCM with key derived from user passphrase via PBKDF2 (200,000+ iterations) or Argon2id
- **Header includes:** salt, IV, KDF parameters, format version — so future versions can decrypt old files

### Tasks
- [ ] New `Clipt.Portable` project (instead of `Clipt.Sync`) — handles export/import
- [ ] Export wizard (modal dialog):
  - Step 1: Choose what to include — All items / Pinned only / Filtered by tag / Filtered by date range / Selected items only
  - Step 2: Include settings? (checkbox — useful when migrating to new machine)
  - Step 3: Set passphrase with confirmation field and strength indicator
  - Step 4: Choose save location (standard Save File dialog)
  - Step 5: Confirmation screen with file size and item count
- [ ] Import wizard (modal dialog):
  - Step 1: Choose file (Open File dialog filtered to `.clipt`)
  - Step 2: Enter passphrase (3 attempts before clearing fields)
  - Step 3: Preview contents — shows item count, date range, source machine name, included settings
  - Step 4: Choose merge strategy:
    - **Add as new items** (default) — duplicates allowed if content differs
    - **Skip duplicates** — match by content hash
    - **Replace all** — wipe local history first (with very loud confirmation)
  - Step 5: Choose what to import: items / settings / both
  - Step 6: Progress bar and result summary
- [ ] Settings tab "Backup & Transfer":
  - Export button (opens wizard)
  - Import button (opens wizard)
  - Last export timestamp display
  - Optional: scheduled auto-export to a chosen folder (daily/weekly) — handy for users who want a passive backup, can drop the file in their Drive sync folder themselves
- [ ] Drag-and-drop a `.clipt` file onto the main window to trigger import wizard
- [ ] Command-line import for power users: `Clipt.exe --import path\to\file.clipt`
- [ ] Format versioning so future Clipt versions can read old export files
- [ ] Compression before encryption (gzip) — clipboard history with images can be large
- [ ] Size warnings: if export is over 100MB, warn the user
- [ ] Excluded by default: items matching `ignored_patterns` (sensitive content stays local)

### Notes
- **Passphrase forgotten = data unrecoverable.** Make this very clear in the export wizard. Suggest saving the passphrase in a password manager.
- **No telemetry on this feature.** Export/import never touches the network.
- **Test cross-version compatibility** as part of CI: every release must successfully import files exported by every prior release.
- **The "save anywhere" pitch in the README** should explicitly mention Google Drive, Dropbox, OneDrive, iCloud Drive, USB drive, and email-to-self as valid storage options. This matches user mental models from Postman/KeePass/Bitwarden.

---

## Phase 3 — AI-Assisted Actions

Once core features and sync are stable, add AI features that make this manager genuinely smarter than competitors. Built on the Anthropic API (which Matt already has subscriptions and familiarity with).

### Tasks
- [ ] New `Clipt.AI` project with Anthropic client wrapper
- [ ] Settings tab for AI:
  - Enable AI features (checkbox, default off — opt-in for privacy)
  - API key input (stored in Windows Credential Manager)
  - Model selector (Claude Sonnet 4.5 default; allow Haiku for cost-conscious users)
  - Per-action toggle: enable/disable individual AI actions
- [ ] AI Actions accessible via right-click context menu on any clipboard item:
  - **Summarize** — for long text, generate a 2-3 sentence summary; replace clipboard or save as new item
  - **Translate** — to user-selected target language
  - **Convert to Markdown** — for HTML or plain text
  - **Convert to Plain Text** — strip all formatting intelligently
  - **Format JSON / XML / SQL** — pretty-print with proper indentation
  - **Explain Code** — for code snippets, generate plain-English explanation
  - **Extract Key Points** — bullet-list of main ideas
  - **Fix Grammar** — proofread and correct
  - **Generate Title** — create a short label for the item (auto-applied as a tag)
- [ ] Smart auto-tagging:
  - When AI is enabled, every new clipboard item gets a 1-3 word tag generated by Claude Haiku (cheap, fast)
  - User can disable per-item or globally
- [ ] Smart search:
  - Beyond text match — semantic search via embeddings stored alongside FTS5
  - "Find that thing about authentication" works even if "authentication" isn't in the original text
  - Use Anthropic embeddings or fall back to local sentence-transformers model for offline
- [ ] Privacy & cost controls:
  - Show estimated cost before running AI action on large items
  - Monthly spend cap with warning
  - Usage log showing which actions were run, when, and approximate token cost
  - "Never send to AI" flag per ignored app pattern (e.g., never send banking content)

### Notes
- AI features must be entirely opt-in and clearly marked with a "Sends data to Anthropic" indicator
- Keep AI calls async and non-blocking — show progress in the preview pane
- Cache results: if the user runs "Summarize" on the same item twice, return the cached summary

---

## Stretch Goals (Discuss Before Starting)

- **OCR on copied images** via `Windows.Media.Ocr` (built into Windows, no API key needed)
- **Snippet templates** with placeholder substitution (`{{date}}`, `{{cursor}}`, `{{clipboard}}`)
- **Encrypted local database** via SQLCipher
- **Multi-monitor awareness** — open on the monitor with cursor focus
- **Custom theme support for markdown preview** — user-editable XAML resource dictionary for markdown styles
- **Portable mode** (run from USB without installation, settings + DB in app folder)
- **Microsoft Store distribution** as MSIX package alongside the standalone installer
- **Plugin system** for custom content type handlers
- **Folder watch import** — point Clipt at a folder (e.g., your Drive sync folder) and have it auto-import any `.clipt` files that appear there. Lightweight pseudo-sync without OAuth.

## Notes for Claude Code

- **Use C# 13 / .NET 9 with `<LangVersion>preview</LangVersion>`** to leverage partial properties for `[ObservableProperty]`
- **MVVM Toolkit 8.4+** — use partial properties pattern, not the older field-based pattern
- **Win32 P/Invoke** isolated to `Clipt.Interop` project — keep the rest of the code platform-agnostic where possible
- **No EF Core** — overkill for this. Use raw SQL with `Microsoft.Data.Sqlite` and Dapper if needed for mapping
- **FTS5 must be enabled** at SQLite connection level (`SQLitePCLRaw.bundle_e_sqlite3` includes it)
- **WebView2 not needed** — we're rendering markdown natively via Markdig.Wpf into a `FlowDocument`. This eliminates a heavy runtime dependency, improves startup time, and gives us full WPF theming (FlowDocument respects DynamicResource bindings)
- **Markdown rendering**: use `Markdig` with `MarkdownPipelineBuilder().UseAdvancedExtensions()` (tables, auto-links, task lists, footnotes); pass to `Markdig.Wpf.Markdown.ToFlowDocument()`; bind the result to a `FlowDocumentScrollViewer`. Style headings, code blocks, blockquotes via `FlowDocument` resource dictionary so theme switching just works
- **Code blocks inside markdown**: Markdig.Wpf supports custom renderers — wire up `ColorCode.Wpf` for syntax highlighting on fenced code blocks
- **Threading**: clipboard events fire on the message-loop thread; offload all DB work to background tasks via `Task.Run` or a `Channel<T>` queue. Never block the UI thread on clipboard operations
- **Test what matters**: ContentTypeDetector, PrivacyFilter, SearchService logic. Don't try to unit-test WPF views or clipboard interop — that's integration territory
- **Clipboard format preservation**: when copying an item back to the clipboard, restore ALL captured formats, not just text. Apps that paste rich content (Word, Outlook, browsers) depend on this
- **Reference Maccy and Ditto** (Ditto is a long-running Windows clipboard manager — open source, decent reference for the format preservation problem)
- **Serilog config** in `appsettings.json` so users can adjust log level without rebuilding
- **Code-signing**: for a real release, document the use of `signtool` and an EV cert; for development, generate a self-signed cert and document the SmartScreen warning users will see
- **GPL-3.0** to match upstream Clipt's license — be deliberate about this, since it has implications if this code is ever reused commercially
