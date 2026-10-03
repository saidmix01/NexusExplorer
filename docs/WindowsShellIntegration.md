# Windows Shell Integration

This document describes how Nexus Explorer integrates with the Windows shell for dynamic context
menus, and the roadmap for extending that integration. It reflects the current implementation of the
**"New"** submenu and the planned architecture for **Open With**, **Send To**, and the full
**Windows Shell Context Menu**.

## Design goals

- *If Windows already knows how to handle it, Nexus should reuse that instead of duplicating the
  logic.* Nothing about specific applications (Office, LibreOffice, 7-Zip, VS Code, PowerToys, …)
  is hard-coded; everything is discovered from the system at runtime.
- Keep OS-specific code out of ViewModels and Views. The flow is always:

  ```
  UI (AXAML)
    → MainWindowViewModel
      → Core abstraction (interface)
        → Platform/Windows implementation
          → Registry / Shell / Win32 APIs
  ```

- Fail safe. A single corrupt or unreadable registry entry is skipped and logged; it never breaks
  the rest of the menu.
- Don't block the UI. Discovery runs off the UI thread and results are cached.

## Layering summary (implemented for "New")

| Concern | Type | Project |
|--------|------|---------|
| Describe a "New" option | `NewItemDefinition`, `NewItemKind`, `NewItemSource` | `Core/Models` |
| Discovery abstraction | `IShellNewTemplateService` | `Core/Abstractions` |
| Windows discovery | `WindowsShellNewTemplateService` | `Platform/Windows` |
| Registry parsing (pure, testable) | `ShellNewParser` + `IRegistryReader` | `Platform/Windows/ShellNew` |
| Real registry access | `WindowsRegistryReader` | `Platform/Windows/ShellNew` |
| Cross-platform fallback | `DefaultShellNewTemplateService` | `Platform` |
| File creation strategy | `IFileOperationService.CreateFromDefinitionAsync` | `Infrastructure/FileOperations` |
| Menu item + resolved icon | `NewMenuItemViewModel` | `App/ViewModels` |
| Native type icon | `WindowsShellIconExtractor.ExtractIconForExtensionAsync` | `App/Services/Thumbnails` |

### How "New" discovery works

1. `WindowsShellNewTemplateService` enumerates extension subkeys under `HKEY_CLASSES_ROOT`.
2. For each extension, `ShellNewParser` looks for a `ShellNew` key either directly
   (`HKCR\.ext\ShellNew`) or under the extension's default ProgId (`HKCR\.ext\<ProgId>\ShellNew`).
3. It interprets the ShellNew value set into a `NewItemKind`:
   - `FileName` → `TemplateFile` (copy a template from the user's Templates folder; falls back to an
     empty file if the template is missing).
   - `Data` → `DataFile` (write inline bytes).
   - `NullFile` (or an empty ShellNew key) → `EmptyFile`.
   - `Command` → **ignored** (see Security).
4. A friendly type name is resolved from the ProgId (`FriendlyTypeName`, then the ProgId default
   value, then an `"<EXT> File"` fallback).
5. Built-in options (**Folder**, **Text Document**) are always prepended, so the menu works even
   when no associations exist and on non-Windows platforms.

Because this reads live associations, Office entries appear only when Office is installed and
disappear when it is uninstalled; LibreOffice entries appear if it registers `ShellNew`. No app is
assumed.

### Caching / performance

`WindowsShellNewTemplateService` caches the discovered list and exposes `InvalidateCache()`.
`GetNewItemsAsync` runs the registry walk on a background thread. The ViewModel builds the submenu
once per session (`_newMenuItemsLoaded`) and resolves native icons lazily in the background, so
opening the context menu is immediate. To refresh after an association change, call
`InvalidateCache()` (future: hook `SHChangeNotify(SHCNE_ASSOCCHANGED)`).

### Security & stability

- Registry-declared `Command` entries are never executed. Only empty-file, template-copy, and
  inline-data creation are performed.
- Templates are validated (`File.Exists`) before copying; a missing template degrades to an empty
  file rather than failing.
- Extension keys are validated (single leading dot, no path/invalid characters).
- Every registry read is wrapped so failures return a neutral value; discovery as a whole never
  throws and degrades to the built-ins.

---

## Roadmap: Open With (FASE 9)

The same shape as "New". Add:

- `Core/Abstractions/IOpenWithService` with
  `Task<IReadOnlyList<OpenWithApp>> GetAppsForExtensionAsync(string extension)`.
- `Core/Models/OpenWithApp` (DisplayName, ExecutablePath/launch verb, icon hint) — a description,
  not an executor.
- `Platform/Windows/WindowsOpenWithService` reading, in priority order:
  - `HKCR\.ext\OpenWithProgids`
  - `HKCR\.ext\OpenWithList`
  - `HKCR\Applications\<app>.exe\shell\open\command`
  - the per-user `...\FileExts\.ext\OpenWithList` MRU.
- Reuse `WindowsShellIconExtractor` for per-app icons, and the existing
  `WindowsPlatformService.OpenWithDialogAsync` (`SHOpenWithDialog`) as the "More apps…" fallback.
- Surface it as another data-driven submenu next to the existing **Open With…** item (do not remove
  the current item). Launch via a new `OpenWithAppCommand` that starts the chosen executable with
  the selected path as an argument (quoted; never a shell string).

This plugs in without touching the discovered-items pattern already established for "New".

## Roadmap: Send To (FASE 10)

Most of this already exists: `ISendToService` / `WindowsSendToService` read
`%APPDATA%\Microsoft\Windows\SendTo`. To expose it in the context menu:

- Add a `SendToTargets` `ObservableCollection<SendToTarget>` on the ViewModel, populated on
  right-click (mirroring `NewMenuItems`), and a `SendToCommand(SendToTarget)` that calls
  `ISendToService.SendToAsync` with the selected paths.
- Add a **Send To >** `MenuItem` with `ItemsSource="{Binding SendToTargets}"` using the same
  `MenuItem > MenuItem` style the "New"/"Project" submenus use.

No new platform work is required for the common cases (folders, Compressed folder).

## Roadmap: Windows Shell Context Menu (FASE 11)

This is the complex one and is intentionally **not implemented** yet. Third-party shell extensions
expose their entries through COM, primarily:

- `IShellItem` / `IShellFolder` → `IContextMenu`, `IContextMenu2`, `IContextMenu3`
- `IExplorerCommand` (modern, used by Windows 11's menu and tools like PowerToys)
- In-process COM handlers registered under `shellex\ContextMenuHandlers`

### Recommended architecture

1. **Isolate it in a separate host process.** The empty `NexusExplorer.ShellMenu` project is the
   intended home. Loading arbitrary third-party COM handlers in-process risks crashing or hanging
   the whole app; a dedicated host can be sandboxed and killed if a handler misbehaves. The main app
   talks to it over a simple IPC channel (named pipe) and renders the returned items in its own
   Avalonia menu.
2. **Model the result OS-neutrally.** The host returns a tree of lightweight items
   (id, display text, icon bytes, has-submenu) — never COM pointers across the boundary.
3. **Invoke by id.** When the user clicks an item, the main app asks the host to invoke that id
   (`IContextMenu::InvokeCommand` / `IExplorerCommand::Invoke`) on the selected paths.
4. **Expose it behind a Core abstraction** (`IShellContextMenuService`) and wire it as an *additional*
   section of the existing context menu, exactly like "New" — never replacing the current menu.

### Why not now

A partial in-process implementation would be fragile against misbehaving third-party extensions and
could destabilize Nexus. The host-process design above is the safe path and should be its own
focused effort.
