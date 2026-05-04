# Clipt

[![CI](https://github.com/mwilson/clipt/actions/workflows/ci.yml/badge.svg)](https://github.com/mwilson/clipt/actions/workflows/ci.yml)

Polished, native Windows clipboard history with fast capture and rich review modes.

## Phase 1 — Visual Foundation

Current status: the app builds into a dark Windows 11-style WPF shell with a borderless rounded window, teal accent theme, demo clipboard history, capture/work mode toggle, preview pane, sidebar placeholders, tray icon, and test/CI foundation. Clipboard monitoring, SQLite persistence, settings, and hotkeys are intentionally stubbed for later phases.

## Screenshots

Screenshots will land in `docs/screenshots/` after the first visual QA pass.

## Build

Requirements:

- Visual Studio 2022 or Rider on Windows
- .NET 9 SDK

Commands:

```powershell
dotnet restore Clipt.sln --configfile NuGet.Config -p:NuGetAudit=false
dotnet build Clipt.sln --configuration Release --no-restore
dotnet test Clipt.sln --configuration Release --no-build
```

## Tech Stack

- .NET 9, C# 13 preview, WPF
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting and DependencyInjection
- Microsoft.Extensions.Logging with Serilog file/debug sinks
- H.NotifyIcon.Wpf
- NHotkey.Wpf stubbed for Phase 2
- Microsoft.Data.Sqlite stubbed for Phase 2
- Markdig and Markdig.Wpf
- AvalonEdit
- xUnit and FluentAssertions

## License

GPL-3.0-only, matching the upstream ClipMon inspiration noted in `Tasks.md`.
