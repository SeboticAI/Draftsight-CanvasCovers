# Project Status

Snapshot of where the codebase is right now. Update this file whenever the
working state changes meaningfully (new feature lands, refactor commits,
gating issue identified). Skim [ROADMAP.md](ROADMAP.md) for what's queued
next.

---

## Headline

The add-in loads cleanly in DraftSight 2026, surfaces a branded ribbon
button, presents a product picker, drives a Lift Blanket dialog with
project metadata + 3 walls + options + configurable layers, and emits a
layered drawing ready for the cutting machine. Ctrl+Z reverts the whole
generation in one step.

The architecture is ready for additional products — caravan annexe slot
exists in the picker as a disabled tile pending the spec from the client.

## Last verified runtime

- DraftSight 2026 Premium trial, Windows 11 Pro 26200
- Operator can: open picker → Lift Blanket tile → fill form (defaults
  prepopulated) → Generate → see three walls with COP cutouts + title
  block on four named coloured layers
- `Ctrl+Z` after generation reverts cleanly
- Layer Manager (`LAYER` command) lists CC-Outline / CC-COP /
  CC-Annotation / CC-Titleblock with their ACI colours set

## What works end-to-end

- COM add-in load (CLSID `aa497758-3d06-46b7-9f75-7a8f2fffed7c`)
- Ribbon registration on the active workspace
- Orphan-tab cleanup at startup (handles crash-recovery)
- Product picker UI with Lift Blanket tile available, Caravan Annexe
  tile disabled with explanatory tooltip
- Lift Blanket dialog: branded header, project metadata, three wall
  sections (left, right, rear), options (Through Car, Plastic Cover,
  Fixings), configurable LAYERS panel with live colour swatches
- Multi-error validation: all dialog errors shown at once
- Through Car ticking auto-disables Rear Wall section
- LAYERS panel "Reset to Defaults" button
- Generator: 4 named layers (Outline/COP/Annotation/Titleblock) created
  with ACI colours via the activate pattern, geometry undo-grouped via
  `SketchManager.StartUndoRecord` / `StopUndoRecord`
- Diagnostic `_CANVASCOVERSLAYERTEST` command (logs to
  `%LocalAppData%\CanvasCovers\layertest.log`)

## What's deliberately not yet built

- **Caravan annexe flow.** Picker tile exists, dialog and generator do
  not. Gated on receiving a measurement form / sample DXF from Adelaide
  Annexe & Canvas.
- **DXF auto-export.** Operator currently saves the drawing manually.
- **Project save/load.** No JSON serialisation of `LiftBlanketJob` yet.
- **Branded icons.** Currently using the SDK sample's placeholder PNGs.
- **Inno Setup installer.** Deploy/rollback PowerShell scripts only.
- **Code signing.** Add-in shows "Unknown Publisher" on first load.
- **Field-level tooltips / help.** Power-user friendly only right now.
- **Machine-constraint validation** (max sheet width etc). Gated on
  spec from client.

## Known gotchas (don't re-learn these)

All four are documented in detail in [`/CLAUDE.md`](../CLAUDE.md) §9.
The short version:

1. **Do not call `Application.RemoveUserInterface(guid)` preemptively
   on first connect** — corrupts internal state. The orphan-tab cleanup
   in `App.cs` iterates by GUID and removes individual tabs instead.
2. **Toolbar items with empty icon paths crash the host** when
   `dsUIState_Document` activates. Use ribbon (text-only style is
   tolerant of empty icons) or supply real PNG paths.
3. **`EntityHelper.SetLayer` and `EntityHelper.GetLayer` crash on
   freshly-inserted entities.** Use the activate-based pattern
   (`Layer.Activate()` before insert) — that's what `LayerHelper` does.
4. **`MessageBox.Show` from a `Command.ExecuteNotify` handler is
   risky.** Use `Application.GetCommandMessage().PrintLine(...)` for
   command-line feedback; reserve `MessageBox.Show` for catch blocks
   surfacing fatal errors.

## Current commit baseline

- Branch: `main`
- Last verified commit message: "Multi-product refactor + polish pass"
- Build: clean (`dotnet build -c Release` produces `CanvasCovers.dll`
  with zero warnings)

## Quick verification

```powershell
.\scripts\deploy-canvascovers.ps1
```

Then in DraftSight: new drawing → tick CanvasCovers in Add-Ins → ribbon
"CanvasCovers" tab → "Canvas Covers" button → "Lift Blanket" tile in
picker → Generate.
