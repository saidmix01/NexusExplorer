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

## Microsoft Store (MSIX)

Requires the Windows 10/11 SDK (for `makeappx`, `makepri`, `signtool` and the App Certification Kit).

| Path | What it is |
|------|------------|
| `packaging/Package.appxmanifest` | MSIX manifest template (identity tokens filled in by the build script) |
| `packaging/Assets/` | All tile / app-list / splash / Store logo assets |
| `packaging/StoreListing/` | Partner Center listing images (screenshots 1920x1080, logos) |
| `packaging/build-msix.ps1` | Publishes the app and builds `.msix` per architecture + `.msixbundle` |
| `packaging/validate-msix.ps1` | Installs a test-signed package and runs WACK (admin) |
| `tools/IconGen/` | Regenerates every icon and asset: `dotnet run --project tools/IconGen` |

**Version:** bump `<Version>` in `Directory.Build.props` for each submission (`1.0.1` becomes MSIX `1.0.1.0`).

**Local test** (admin PowerShell, once per build):

```powershell
./packaging/build-msix.ps1 -Architectures x64 -SignForTesting
./packaging/validate-msix.ps1
```

**Store build** (unsigned, the Store signs it). Use the values from Partner Center > Product identity:

```powershell
./packaging/build-msix.ps1 -IdentityName "<Package/Identity/Name>" `
    -Publisher "<Package/Identity/Publisher>" -PublisherDisplayName "<PublisherDisplayName>"
```

Upload `artifacts/msix/NexusExplorer_<version>.msixbundle`.

Notes:
- Don't enable trimming or single-file for the package: shell COM interop breaks under trimming.
- "Set as default file manager" is hidden in the packaged build (MSIX virtualizes the registry
  writes it depends on). The app can be launched by name with `nexusexplorer` (execution alias).
- Settings/logs live in `%LOCALAPPDATA%\Packages\<package>\LocalCache\Local\NexusExplorer`
  in the packaged build and are removed on uninstall.

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
