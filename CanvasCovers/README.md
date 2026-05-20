# CanvasCovers — the add-in DLL project

The actual COM-visible DraftSight add-in. Builds to `CanvasCovers.dll`,
gets RegAsm-registered, loads into DraftSight when ticked in the
Add-Ins manager.

For a code tour see [`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md).
For the current behaviour, see [`../docs/STATUS.md`](../docs/STATUS.md).

## Build

From the repo root:

```powershell
dotnet build CanvasCovers/CanvasCovers.csproj -c Release
```

Produces `bin/Release/net48/CanvasCovers.dll` plus the icons under
`bin/Release/net48/Resources/`.

## Deploy

```powershell
.\scripts\deploy-canvascovers.ps1
```

(Admin PowerShell required — runs RegAsm and writes to ProgramData.)

## What's in here

| Folder | Purpose |
|---|---|
| `App.cs` | COM entry point. CLSID `aa497758-3d06-46b7-9f75-7a8f2fffed7c`. |
| `CanvasCovers.xml` | DraftSight add-in config; deployed into ProgramData. |
| `CanvasCovers.csproj` | net48, x64, WPF, references SDK interop DLLs. |
| `Commands/` | Command classes. `OpenCanvasCoversCommand` is the ribbon entry; `LayerTestCommand` is a diagnostic. |
| `Geometry/` | `LayerHelper` (shared) and product-specific generators under `Products/`. |
| `Models/` | POCO models. Shared types at top level, product-specific under `Products/`. |
| `Properties/AssemblyInfo.cs` | Version, GUID, company. |
| `Resources/` | Ribbon icons (placeholder PNGs from SDK sample). |
| `UI/` | WPF windows and UserControls. `ProductPickerWindow` at top, shared UCs in `Controls/`, product dialogs under `Products/`. |

## Hard rules

These are in [`../CLAUDE.md`](../CLAUDE.md) §9 in detail — short version:

- **Do not call `EntityHelper.SetLayer` or `GetLayer`** on a
  freshly-inserted entity. Both crash DraftSight. Use the activate
  pattern via `LayerHelper` instead.
- **Do not call `Application.RemoveUserInterface(guid)`** preemptively
  on first connect. The orphan-tab cleanup in `App.cs` handles
  recovery without it.
- **Toolbar items need real icon paths.** Empty paths crash the host
  when `dsUIState_Document` activates. We use ribbon buttons with
  text-only style (which tolerates empty icons).
- **`MessageBox.Show` from inside `Command.ExecuteNotify`** is risky.
  Use it only in catch blocks; for command-line feedback use
  `Application.GetCommandMessage().PrintLine(...)`.

## Development safety

- `CanvasCovers.xml` ships with `startup="0"`. The add-in must be
  ticked manually after every DraftSight launch.
- Build / deploy / DraftSight cycle is locked: must close DraftSight
  before building (the DLL is locked while the host is alive).
- Always have `.\scripts\rollback-canvascovers.ps1 -StopDraftSight`
  ready before any load test.
