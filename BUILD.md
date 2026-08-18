# Nexus Explorer — Build & Run

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Development (Debug)

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/NexusExplorer.App
```

## Production Build (Release)

### Windows x64 — Self-contained single file (no .NET required on target)

```bash
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64
```

Output: `publish/win-x64/NexusExplorer.App.exe` (~105 MB)

### Windows x64 — Trimmed (smaller, ~50-60 MB)

```bash
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64-trimmed
```

### Windows ARM64

```bash
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj \
  -c Release \
  -r win-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/win-arm64
```

### Linux x64

```bash
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/linux-x64
```

### macOS (Apple Silicon)

```bash
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/osx-arm64
```

## Tests

```bash
dotnet test
```

## PowerShell (Windows) — one-liner

```powershell
# Build + Run
dotnet build; dotnet run --project src/NexusExplorer.App

# Publish release
dotnet publish src/NexusExplorer.App/NexusExplorer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/win-x64
```

## Notes

- The published `.exe` is fully self-contained — no .NET runtime needed on the target machine.
- Delete `.pdb` files from the publish folder if you don't need crash debugging symbols.
- The app minimizes to the system tray when closed (use tray menu → Exit to quit completely).
- Session state is saved to `%LOCALAPPDATA%/NexusExplorer/session-state.json`.
- Folder colors are saved to `%LOCALAPPDATA%/NexusExplorer/folder-colors.json`.
