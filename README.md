# Clipt

[![CI](https://github.com/mwilson/clipt/actions/workflows/ci.yml/badge.svg)](https://github.com/mwilson/clipt/actions/workflows/ci.yml)

Polished, native Windows clipboard history with fast capture and rich review modes.

## Phase 1 — Native Capture Foundation

Current status: the app builds into a dark Windows 11-style WPF shell with a borderless window, teal accent theme, capture/work mode toggle, persisted SQLite clipboard history, live FTS-backed search, content-type filters, pinning, rich previews, tray integration, configurable global hotkey, clipboard monitoring, source-app privacy filters, history retention, per-clip size guards, and demo content for screenshot work.

The next near-term polish pass is README/screenshot readiness: capture the hero image, reconcile the feature checklist as V1 closes, then tighten the remaining user-facing settings that are still marked as coming later.

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
- NHotkey.Wpf global hotkeys
- Microsoft.Data.Sqlite with FTS5-backed history
- Markdig and Markdig.Wpf
- AvalonEdit
- xUnit and FluentAssertions

## License

GPL-3.0-only, matching the upstream ClipMon inspiration noted in `Tasks.md`.
