# Nexus Explorer

A modern, cross-platform desktop file manager built with .NET 10 and Avalonia UI, featuring a Windows 11-inspired interface.

## Architecture

The solution follows a clean, layered architecture with strict separation of concerns:

```
NexusExplorer.sln
src/
  NexusExplorer.App/            UI layer (Avalonia, Views, ViewModels)
  NexusExplorer.Core/           Domain layer (models, abstractions, pure logic)
  NexusExplorer.Platform/       Platform layer (OS-specific implementations)
  NexusExplorer.Infrastructure/ Infrastructure layer (services, DI, logging)
```

### NexusExplorer.Core

The foundation layer with zero external dependencies (aside from CommunityToolkit.Mvvm for ObservableObject support). Contains:

- **Models** — `FileSystemItem`, `TabItem`, `NavigationItem`, enums
- **Abstractions** — Interfaces for all services (`IFileSystemService`, `IPlatformService`, `INavigationService`, `ITabService`, `ITerminalService`, `IPreviewService`)
- **Services** — Platform-independent implementations (`NavigationService`, `TabService`)

### NexusExplorer.Platform

Isolates all OS-specific code behind Core abstractions. Organized by platform:

- `Windows/` — Windows platform service, terminal launcher
- `Linux/` — Linux (xdg-open, x-terminal-emulator)
- `MacOS/` — macOS (open, Terminal.app)
- `PlatformDetector` — Runtime OS detection
- `PlatformServiceRegistration` — DI extension that registers the correct implementations

### NexusExplorer.Infrastructure

Bridges Core abstractions with real implementations:

- `FileSystem/FileSystemService` — File system operations using `System.IO`
- `DependencyInjection/ServiceCollectionExtensions` — Composition root

### NexusExplorer.App

The Avalonia UI application:

- **Views** — AXAML views with Windows 11-inspired layout
- **ViewModels** — MVVM ViewModels using CommunityToolkit.Mvvm source generators
- **Converters** — Value converters for display formatting
- **App.axaml** — Application startup, DI container setup

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 |
| UI | Avalonia UI 11 (FluentTheme) |
| Pattern | MVVM |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Microsoft.Extensions.Logging |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build

```bash
dotnet build NexusExplorer.slnx
```

## Run

```bash
dotnet run --project src/NexusExplorer.App
```

## Logging

Nexus Explorer writes logs to disk so issues can be diagnosed after the fact. The
logging infrastructure lives in `NexusExplorer.Infrastructure.Logging` and is built
on top of `Microsoft.Extensions.Logging`.

- **Location** — Logs are written to the user's local application-data folder:
  - Windows: `%LOCALAPPDATA%\NexusExplorer\Logs`
  - Linux: `~/.local/share/NexusExplorer/Logs`
  - macOS: `~/Library/Application Support/NexusExplorer/Logs`
- **Format** — `[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [Category] message`, followed by
  the exception details when one is present.
- **Rotation** — A new file is created each day (`nexus-yyyy-MM-dd.log`). If a file
  exceeds 10 MB it rolls to a numbered file (`.1`, `.2`, …). Logs older than 14 days
  are deleted automatically.
- **Level** — Debug builds log from `Debug`; release builds log from `Information`.
  The default is set in `NexusLog.EnsureInitialized()`; adjust `FileLoggerOptions`
  to change it.
- **Startup diagnostics** — Each launch records elapsed-time markers under the
  `Startup` category, ending with `APPLICATION READY` and the total startup time.

### Adding logging to a service

Inject `ILogger<T>` and log through it — the file provider is registered globally,
so no manual logger construction is required:

```csharp
public sealed class MyService(ILogger<MyService> logger)
{
    public void DoWork()
    {
        logger.LogInformation("Doing work");
        logger.LogError(ex, "Work failed");
    }
}
```

## Platforms

| Platform | Status |
|----------|--------|
| Windows | Supported |
| Linux | Supported |
| macOS | Supported |

## Design Principles

- **Modularity** — Each layer has a single responsibility and can be extended independently.
- **Testability** — All services are accessed through interfaces; platform code is fully isolated.
- **Cross-platform** — Platform-specific code lives exclusively in `NexusExplorer.Platform`.
- **MVVM** — Clean separation between views and business logic via data binding.
- **Dependency Injection** — All services are resolved through DI; no service locator patterns.

## Project Dependency Graph

```
App → Infrastructure → Platform → Core
App → Core
App → Platform
Infrastructure → Core
Infrastructure → Platform
Platform → Core
```

## License

Proprietary. All rights reserved.
