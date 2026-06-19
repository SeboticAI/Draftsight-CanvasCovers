# Project Status

Snapshot of where the codebase is right now. Update this file whenever the
working state changes meaningfully. Skim [ROADMAP.md](ROADMAP.md) for what is
queued next.

## Current Release State

**Current version: v1.7.0** on `main`. Customer installer:
`Installer\Output\BesiaCAD-CanvasCovers-Setup-1.7.0.exe`. Contains Martin's
round-2 change list (see [CHANGE_REQUESTS_ROUND2.md](CHANGE_REQUESTS_ROUND2.md))
plus the round-3 notes carry-over (see
[CHANGE_REQUESTS_ROUND3.md](CHANGE_REQUESTS_ROUND3.md)) and a production-readiness
review pass, implemented + unit-tested + simplify-pass reviewed; the in-DraftSight
live-test checklist (plan task 9, step 3) is still to be run by the operator:

- **Previous-job memory**: every successful Generate caches the raw form
  state to `%AppData%\BesiaCAD\CanvasCovers\last-job.json`; a "Load Previous
  Job's Measurements" button (top of the form) restores walls, options and
  the project notes (company, initials, network number, order number, project
  name, date). The operator edits the per-job fields (order/network number)
  after loading. (Round-3 change: Martin asked for the notes to carry over
  too, reversing the original v1.6.0 "project fields stay blank" decision.)
- **Fixings**: Eyelet split into TG7/TG9 (same -30 allowance, different COP
  label); the combo starts unselected and Generate blocks until a fixing is
  chosen (`FixingType.None` added).
- **Form order**: Previous Job → Project Info → Options → Walls → Layers.
- **Auto zoom-to-fit** after generate, before the DXF Save-As, via
  `Application.Zoom(dsZoomRange_Fit, null, null)` (SDK-sample-verified), so
  closing the drawing no longer nags about unsaved zoom changes.
- **Customer drop-down**: Company Name is an editable ComboBox seeded from
  `Resources\customers.csv` (29 entries from Martin), copied on first use to
  `%AppData%\BesiaCAD\CanvasCovers\customers.csv` for Notepad editing;
  selecting a customer auto-fills the initials.
- Tests: 56 passing (`dotnet test`; re-confirm on the Windows build), Release build clean. New gotcha: a WPF
  (`UseWPF`) net48 project referencing `System.Web.Extensions` must also
  reference `System.Web` explicitly or the markup compiler fails with MC1000.
- Post-implementation simplify pass: `UserDataPaths` is the single source for
  the `%AppData%\BesiaCAD\CanvasCovers` folder (JobCache + CustomerDirectory
  both build from it); the previous-job cache is read once per dialog open;
  installer uses `lzma2/max` and documents that per-user AppData data
  deliberately survives uninstall.

**Previous shipped version: v1.5.0.**

This build contains the client's beta-review change list plus a final
release-readiness hardening pass before handoff:

- Option fields now validate strictly at Generate: bad/negative fixing
  allowance, quilt inset, and quilting spacing are blocked instead of silently
  falling back to defaults.
- Layer activation now fails fast if DraftSight refuses to activate a target
  layer, so geometry cannot quietly land on the wrong cutter layer.
- Missing dimension/title-note inserts are counted in `FailedInsertCount`, not
  just polylines and wall labels.
- Product picker, lift-blanket dialog, and DXF Save-As dialog are parented to
  DraftSight so they do not hide behind the host.
- Installer upgrades stop if the previous version's uninstaller exits with a
  non-zero result.
- DLL metadata is bumped to `1.5.0.0`.
- The operator quick-start HTML/PDF is updated for v1.5.0.

Automated verification after the hardening pass:

```powershell
dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj  # 42 passed
dotnet build CanvasCovers.sln -c Release                  # 0 warnings/errors
.\Installer\build.ps1                                     # installer built
```

The final hardening pass has not yet been live-clicked in DraftSight in this
session; the user is doing that local install/load/generate/export test.

## v1.5.0 Lift-Blanket Behaviour

The add-in loads in DraftSight 2026, registers a **CanvasCovers** ribbon tab,
opens a product picker, and launches a non-modal **Lift Blanket** dialog. The
dialog is parented to DraftSight, so the operator can pan/zoom while it stays
associated with the host window.

The lift-blanket form contains:

- Project fields: Company Name, Company Initials, AAC Order Number, Network
  Number, Project Name, Date.
- Three wall tabs: Left, Right, Rear. Left/Right use the fixed sheet schematic;
  Right is mirrored. Rear is a simple width/height tab and is disabled by
  Through Car.
- Options: Through Car, Plastic Cover on COP, Storage Bag, Glass Behind,
  Fixings, Fixing Allowance, Quilt Inset, Quilting Spacing, Draw Quilting
  Lines, Open DXF Export.
- Six cutter layers: `0`, `1 Rotary Blade`, `2 Drag Blade`, `3 Crease Tool`,
  `4 Drill Tool`, `5 Draw and Text`. `Defpoints` was removed.

## Calculation Model

All geometry math lives in `LiftBlanketCalculator` and is covered by headless
MSTest tests. The SDK generator should stay a translator only.

Width:

- Cut width is the entered total-width override when set.
- Otherwise cut width is `DR-L + S1 + S2 + S3 + DR-R`.
- There is no automatic +10mm width boost anymore. Martin adds shrinkage
  manually when entering sizes.

Height:

- `foldMidline = measuredHeight - fixingAllowance`
- `cutHeight = foldMidline * 2`
- Fixing allowance defaults by fixing type: Hooks 50, Press Studs 40, Eyelet
  30, Velcro 0, Self-adhesive Velcro 0. The operator can override it, but it
  must be numeric and non-negative.

COP:

- COP is optional per Left/Right wall.
- COP width is `S2`.
- COP left edge is `DR-L + S1` from the cut edge.
- Operator only types COP height and gap from bottom.
- Auto top gap is `(measuredHeight - fixingAllowance) - gapFromBottom -
  copHeight`; negative top gap blocks Generate.
- COP-off only hides the COP geometry/fields. It does not reshape the wall.

Quilting:

- Quilting is on by default and score/draws on the COP layer.
- Quilt Inset is a separate clearance from the outline, default 5mm. It does
  not affect cut width.
- Horizontal quilt lines span the full cut width minus Quilt Inset.
- Vertical quilt lines include non-zero door-return boundaries plus evenly
  divided interior lines.
- Lines fill the bottom measured half only, up to the fold midline.
- `EvenlySpaced` caps at 200 gaps, and UI validation requires quilting spacing
  at least 50mm.

Blanket text and filename:

- `BlanketText.Build(project)` returns:
  `<AAC order number> <company initials> <network number>`
- Empty sections are dropped.
- The same string is used for the wall text and DXF filename.
- Wall labels append `L`, `B`, or `R`; they are bottom-centre, inverted 180
  degrees, 25mm high, about 25mm down from the top edge.

COP reminders:

- Fixing type is always printed vertically inside enabled COP cutouts.
- `BAG` and `GLASS BEHIND` are printed there when their options are ticked.

## Validation And Feedback

Generate collects errors and shows them all at once. It blocks:

- Non-numeric, NaN, Infinity, negative, or missing numeric fields as
  appropriate.
- No enabled walls.
- Wall width <= 0.
- Measured height <= fixing allowance.
- COP enabled with `S2 <= 0`.
- COP extending past the right edge.
- COP crossing the fold.
- Quilting spacing < 50mm.
- Any unassigned layer role.

Non-blocking warnings and safety:

- Left/right width mismatch shows a live warning only. It never blocks.
- Generate and export are separate try/catch paths.
- A generate failure keeps the dialog open so inputs are not lost.
- The generator warns if DraftSight returns null from entity inserts counted by
  `FailedInsertCount`.
- Generation is wrapped in one undo record, so one Ctrl+Z removes it.

## Current End-To-End Surface

- COM add-in CLSID: `aa497758-3d06-46b7-9f75-7a8f2fffed7c`
- Product picker: Lift Blanket enabled; Caravan Annexe disabled.
- DXF export: native Save-As, R2018 ASCII DXF, owner-parented dialog, filename
  from blanket text or timestamp fallback.
- Installer: Inno Setup, per-machine, admin, RegAsm `/codebase`, XML deployed
  to ProgramData, upgrades auto-uninstall the previous version first.
- Rollback: `scripts\rollback-canvascovers.ps1 -StopDraftSight`
- Diagnostic command: `CANVASCOVERSLAYERTEST` — DEBUG builds only (registration
  is `#if DEBUG`-gated in `App.cs`, so it never ships in a Release/customer build).

## Not Built Yet

- Caravan Annexe product flow. Waiting on measurement sheet/sample DXF.
- Project save/load.
- Branded icons.
- Code signing. The add-in/installer still show Unknown Publisher.
- Licensing.
- Multi-version DraftSight support.
- Auto-update.

## Known Gotchas

Keep using the patterns in [CLAUDE.md](../CLAUDE.md), especially section 9:

- Do not call `EntityHelper.SetLayer` or `GetLayer` on freshly inserted
  entities.
- Use activate-based layer assignment through `LayerHelper`.
- `Layer.Activate()` returning false is now treated as a generate failure.
- Use `InsertAlignedDimension`, not `InsertLinearDimension`.
- Use `InsertSimpleNote`; its angle is radians.
- Parent non-modal WPF windows to the DraftSight HWND.
- Do not set `DialogResult` on non-modal windows.
- Guard WPF init-time events with `_initialized`.
- Reject NaN/Infinity before values reach WPF setters.
- Keep `WallBlanket` as a fixed schematic, not a value-scaled preview.

## Quick Verification

```powershell
# Close DraftSight first.
.\Installer\build.ps1
```

Run `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.7.0.exe` as admin, open
DraftSight, tick CanvasCovers in Add-Ins, open a new drawing, run the Lift
Blanket flow, Generate, export, reopen the DXF, and Ctrl+Z the generated
entities.
