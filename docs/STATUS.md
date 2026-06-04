# Project Status

Snapshot of where the codebase is right now. Update this file whenever the
working state changes meaningfully (new feature lands, refactor commits,
gating issue identified). Skim [ROADMAP.md](ROADMAP.md) for what's queued
next.

---

## Headline

The add-in loads cleanly in DraftSight 2026, surfaces a branded ribbon
button, presents a product picker, drives a **non-modal** Lift Blanket
dialog (DraftSight stays interactive — pan/zoom while the form is open)
with project metadata + 3 walls + options + configurable layers + a
free-text NOTES field, and emits a layered drawing that mirrors the
client's reference DXF visual convention: blue cut path, white free-
floating project + worksheet text, proper DIMENSION entities. Ctrl+Z
reverts the whole generation in one step.

As of **v1.2.0** the wall model was rebuilt from the client's real
reference DXFs (see [`reference/DXF_FINDINGS.md`](../reference/DXF_FINDINGS.md)):
each wall is now a plain rectangle whose **cut height is auto-derived**
(`(measuredHeight − fixingAllowance) × 2`) and width is the sum of the
five measurement-sheet segment boxes + 10 mm. COP is per-wall, placed
from the bottom/left and validated to stay below the fold midline. A
headless `LiftBlanketCalculator` holds all this geometry math and is
covered by 21 MSTest unit tests asserting against the real DXF
coordinates; the generator is a thin SDK translator over its output.
After Generate, an optional **native DXF export** (Save-As dialog,
filename defaulting to the network number) writes the drawing as
R2018 ASCII DXF. Quilting remains deliberately deferred (no input on
the measurement sheet; spacing rule unconfirmed) behind an off-by-
default `QuiltingEnabled` flag.

The architecture is ready for additional products — caravan annexe slot
exists in the picker as a disabled tile pending the spec from the
client.

## Last verified runtime

- DraftSight 2026 Premium trial, Windows 11 Pro 26200
- Operator can: open picker → Lift Blanket tile → fill form (defaults
  prepopulated, cursor lands in Main Width with `1400` selected) →
  Generate → see walls with COP cutouts + DIMENSION-entity dims + a
  right-side info column with project metadata, FIXINGS lookup table,
  WIDTH/HEIGHT formula reminder, vertical-quilting-spacing lookup, and
  optional NOTES — all on the layer convention the client's machine
  expects (`1 Rotary Blade` for cuts, `0` for white text)
- `Ctrl+Z` after generation reverts cleanly
- Layer Manager (`LAYER` command) lists `1 Rotary Blade`, `5 Draw and
  Text`, `0` with their ACI colours set
- While the dialog is open: DraftSight pan / zoom / select all work;
  closing DraftSight does not affect the dialog (and vice versa);
  clicking the ribbon button while a dialog is already open just
  brings the existing dialog forward

## What works end-to-end

- COM add-in load (CLSID `aa497758-3d06-46b7-9f75-7a8f2fffed7c`)
- Ribbon registration on the active workspace
- Orphan-tab cleanup at startup (handles crash-recovery)
- Product picker UI with Lift Blanket tile available, Caravan Annexe
  tile disabled with explanatory tooltip
- Lift Blanket dialog:
  - Branded header, project metadata (incl. multi-line NOTES), three
    wall sections, options, configurable LAYERS panel with live colour
    swatches, plus a focus-driven wall diagram side panel
  - Non-modal — DraftSight stays interactive; dialog parented to the
    DraftSight HWND so alt-tab cycles them together
  - Auto-focus + select-all on `LeftMainWidth` when opened
  - Diagram retargets on `MouseEnter` of each wall section (not just
    on field click) and highlights the matching part of the diagram
    when a dim field gains focus
  - Multi-error validation: all errors shown at once
  - **Generate-fails-keeps-dialog-open**: if the generator throws
    (e.g. no active drawing), the error is shown and the dialog stays
    open so the operator can retry (`GenerateRequestedEventArgs.Cancel`)
  - Through Car ticking auto-disables Rear Wall section
  - LAYERS panel "Reset to Defaults" button
  - Esc closes the dialog (`ApplicationCommands.Close` key binding)
- Generator:
  - Four named layers (Outline/COP/Annotation/Titleblock) created
    with ACI colours via the activate pattern
  - Geometry undo-grouped via `SketchManager.StartUndoRecord` /
    `StopUndoRecord`
  - Wall cut rectangles on `1 Rotary Blade` (cuts, blue); COP
    rectangles + wall labels on `5 Draw and Text` (draw/score,
    magenta — NOT cut)
  - Proper `SketchManager.InsertAlignedDimension` entities for cut
    width (per wall) and leftmost-wall cut height
  - Free-floating worksheet text on layer `0` (white): top legend,
    project metadata column, FIXINGS lookup, FIXING ALLOWANCE line,
    WIDTH/HEIGHT formula, and NOTES block when populated
  - `DIMSCALE` auto-bumped to 30 via `Application.RunCommand` at the
    start of Generate so dim text + arrows are legible at lift-
    blanket scale
- Diagnostic `_CANVASCOVERSLAYERTEST` command (logs to
  `%LocalAppData%\CanvasCovers\layertest.log`)
- **Inno Setup installer.** `Installer\CanvasCovers.iss` + `build.ps1`
  produce a per-machine, admin-elevated EXE that lays down the payload,
  drops the addin XML in ProgramData, and runs RegAsm /codebase.
  Uninstall via Settings → Apps reverses all of it.

## Recently completed (v1.2.0)

These were "deliberately not built" before v1.2.0 and now work — built
by reverse-engineering the client's reference DXFs rather than waiting
on a client walkthrough:

- **Wall geometry redesign.** Replaced the wrong-shaped
  3-door-return-on-one-side model with the real one: each wall is a
  plain rectangle, width = sum of the five segment boxes
  (DrLeft / Seg1 / Seg2 / Seg3 / DrRight) + 10 mm, walls enumerated
  L / R / B(ack).
- **Fixing-allowance + ×2 height math.** The operator now types the
  **measured** height; the tool applies `(measured − allowance) × 2`.
  The allowance defaults per fixing type (Hooks 50, Press Studs 40,
  Velcro 0) and is operator-editable per job.
- **DXF export.** A native Save-As dialog (filename defaulting to the
  network number) writes R2018 ASCII DXF after Generate, gated on a
  dialog checkbox.

## What's deliberately not yet built

- **Caravan annexe flow.** Picker tile exists, dialog and generator do
  not. Gated on receiving a measurement form / sample DXF from
  Adelaide Annexe & Canvas.
- **Quilting geometry.** Deferred behind an off-by-default
  `QuiltingEnabled` flag. The measurement sheet has no quilt input and
  the spacing rule is unconfirmed (the on-DXF "VERTICAL QUILTING
  SPACING" lookup didn't reconcile cleanly with measured line spacing —
  see `reference/DXF_FINDINGS.md`). Gated on a client confirmation of
  the rule (CLIENT_QUESTIONS §3).
- **Project save/load.** No JSON serialisation of `LiftBlanketJob`
  yet.
- **Branded icons.** Currently using the SDK sample's placeholder
  PNGs.
- **Code signing.** Add-in shows "Unknown Publisher" on first load.

## Known gotchas (don't re-learn these)

All documented in detail in [`/CLAUDE.md`](../CLAUDE.md) §9. The short
version:

1. **`Application.RemoveUserInterface(guid)` preemptively on first
   connect** — corrupts internal state. Iterate tabs and remove by
   GUID instead (App.cs).
2. **Toolbar items with empty icon paths crash the host** when
   `dsUIState_Document` activates. Use ribbon (text-only style is
   tolerant of empty icons) or supply real PNG paths.
3. **`EntityHelper.SetLayer` / `GetLayer` crash on freshly-inserted
   entities.** Use the activate-based pattern — see `LayerHelper`.
4. **`InsertLinearDimension` is horizontal-only.** Its dim rotation
   defaults to 0, so two ext points sharing an X coordinate measure
   as zero length. Use `InsertAlignedDimension` for cases where
   either orientation can occur — it aligns the dim along the line
   between the two ext points and works for both axes.
5. **`InsertNoteWithParameters` silently produced no visible output**
   in our testing (returned without error but no entity appeared on
   screen). `InsertSimpleNote(x, y, z, height, angle, text)` is the
   reliable single-line text API. Set `Justify` after creation if you
   need centre or middle alignment.
6. **`MessageBox.Show` from a `Command.ExecuteNotify` handler is
   risky.** Use `Application.GetCommandMessage().PrintLine(...)` for
   command-line feedback; reserve `MessageBox.Show` for catch blocks
   surfacing fatal errors.
7. **Non-modal dialogs from a COM addin** need their owner HWND set
   via `WindowInteropHelper` so they alt-tab with the host and stay
   above DraftSight when appropriate. `Process.GetCurrentProcess()
   .MainWindowHandle` is the DraftSight main window (since the addin
   runs in-process). Setting `DialogResult` on a non-modal window
   throws, so don't set `IsCancel="True"` on the Cancel button — use
   `KeyBinding` for Esc instead.

## Current commit baseline

- Branch: `main`
- Version: **v1.2.0** (`MyAppVersion` in `CanvasCovers.iss`;
  `AssemblyVersion`/`AssemblyFileVersion` 1.2.0.0). Installer EXE not
  yet rebuilt for this version — run `.\Installer\build.ps1` to produce
  `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.2.0.exe`.
- Build: clean (`dotnet build -c Release` → 0 warnings, 0 errors)
- Tests: **21 passing** (`dotnet test CanvasCovers.Tests`) — headless
  unit tests over `LiftBlanketCalculator`, `FixingAllowance`, the wall
  model, and the DXF filename rule. The SDK-emission layer
  (generator + exporter) is verified live in DraftSight, not by tests.
- Layer defaults match the client's machine convention:
  `1 Rotary Blade` (ACI 5 blue) for the **Outline/cut** layer,
  `5 Draw and Text` (ACI 6 magenta) for **Cop + Annotation** (COP rect,
  wall labels — draw/score, NOT cut), `0` (ACI 7 white) for the
  Titleblock layer (project info + dims + worksheet reference text).
  NB: COP moved from the cut layer to the draw layer in v1.2.0 to match
  the reference DXFs.

## Quick verification

```powershell
# Close DraftSight first.
.\Installer\build.ps1
# Then double-click the EXE in Installer\Output\ (UAC prompts for admin).
```

In DraftSight after install: new drawing → tick CanvasCovers in
Add-Ins → ribbon "CanvasCovers" tab → "Canvas Covers" button → "Lift
Blanket" tile in picker → Generate.

To remove: Settings → Apps → *BesiaCAD Canvas Covers* → Uninstall.
Upgrades auto-uninstall the previous version via the installer's
`PrepareToInstall`.
