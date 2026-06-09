# CanvasCovers - the add-in DLL project

The actual COM-visible DraftSight add-in. Builds to `CanvasCovers.dll`, gets
RegAsm-registered by the installer, and loads into DraftSight when ticked in
the Add-Ins manager.

For a code tour see [`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md). For
the current behaviour, see [`../docs/STATUS.md`](../docs/STATUS.md).

## Build

From the repo root:

```powershell
dotnet build CanvasCovers/CanvasCovers.csproj -c Release
```

Produces `bin/Release/net48/CanvasCovers.dll` plus the icons under
`bin/Release/net48/Resources/`.

## Package / Deploy

From the repo root with DraftSight closed:

```powershell
.\Installer\build.ps1
```

This builds Release and creates
`Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe`. Run that EXE as
administrator to install or upgrade; it handles RegAsm and the ProgramData XML.

## What's In Here

| Folder | Purpose |
|---|---|
| `App.cs` | COM entry point. CLSID `aa497758-3d06-46b7-9f75-7a8f2fffed7c`. |
| `CanvasCovers.xml` | DraftSight add-in config; deployed into ProgramData. |
| `CanvasCovers.csproj` | net48, x64, WPF, references SDK interop DLLs. |
| `Commands/` | Command classes. `OpenCanvasCoversCommand` is the ribbon entry; `LayerTestCommand` is a diagnostic. |
| `Geometry/` | `LayerHelper` and product-specific calculators/generators. |
| `IO/` | DXF filename and export helpers. |
| `Models/` | Shared and product-specific POCO models. |
| `Properties/AssemblyInfo.cs` | Version, GUID, company. |
| `Resources/` | Ribbon icons. |
| `UI/` | WPF windows and UserControls. |

## Hard Rules

These are in [`../CLAUDE.md`](../CLAUDE.md) section 9 in detail:

- Do not call `EntityHelper.SetLayer` or `GetLayer` on freshly inserted
  entities. Both crash DraftSight. Use `LayerHelper`.
- Do not call `Application.RemoveUserInterface(guid)` preemptively on first
  connect. `App.cs` does targeted orphan-tab cleanup instead.
- Toolbar items need real icon paths. The add-in uses the ribbon path.
- `MessageBox.Show` from `Command.ExecuteNotify` is risky; reserve it for
  controlled error paths.
- Parent WPF dialogs to DraftSight's main HWND.
- Use `InsertAlignedDimension`, not `InsertLinearDimension`.
- `InsertSimpleNote` angles are radians at the SDK boundary.

## Development Safety

- `CanvasCovers.xml` ships with `startup="0"`.
- DraftSight must be closed before build/package because the DLL is locked
  while the host is alive.
- Keep `.\scripts\rollback-canvascovers.ps1 -StopDraftSight` ready before
  load testing.
