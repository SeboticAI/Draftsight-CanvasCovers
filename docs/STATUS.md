# Project Status

Snapshot of where the codebase is right now. Update this file whenever the
working state changes meaningfully (new feature lands, refactor commits,
gating issue identified). Skim [ROADMAP.md](ROADMAP.md) for what's queued
next.

**Current version: v1.5.0** on `main`. Installer:
`Installer\Output\BesiaCAD-CanvasCovers-Setup-1.5.0.exe`. This build implements
the client's **beta-review change list** (see
[CHANGE_REQUESTS_BETA_REVIEW.md](CHANGE_REQUESTS_BETA_REVIEW.md) for the
authoritative per-item record). 42 headless tests pass; the add-in + installer
build clean via `dotnet`. Live-tested in DraftSight through several fix rounds.

### What changed in v1.5.0 (from the beta review)

- **Outline = entered width** (the old +10mm edge boost is gone); a new
  **Quilt Inset** field (default 5mm) is the only quilt-line clearance. Existing
  outlines are ~10mm narrower than v1.4.5 — the operator adds shrinkage manually.
- **Blanket text** = `AAC order + company initials + network` — one string used
  for the printed label (bottom-centre, **inverted 180°**, ~25mm from the top,
  25mm high) AND the export filename.
- **Project fields** trimmed to Company name, Company Initials, Network, AAC
  Order No, Project name, Date (Sales contact / Measured by / Mobile / Notes
  removed).
- **Total-width override** field (optional) lets the operator skip the five
  segments; COP-off now only hides the COP, it does not reshape the wall.
- **Fixings:** added Eyelet (−30) and self-adhesive Velcro (0); dropdown shows
  the allowance; height seeds from the left wall; **live** L/R width-mismatch
  warning.
- **COP-cutout reminders** (BAG / fixing / glass-behind) printed **vertical**
  inside the cutout. **Defpoints layer removed** (now six cutter layers).
- SDK note: `InsertSimpleNote` rotation is **radians** — see CLAUDE.md §9.

Deferred / not-in-this-build: items 9 (velcro corners, optional), 17 (needs
clarification), 21 (parked); 22–25 are DraftSight profile/settings, not add-in;
27–28 are future. Next sessions: thorough review + optimisation.

> Note: the sections below predate v1.5.0 in places (they still describe the
> edge-allowance and seven-layer behaviour). Treat the v1.5.0 summary above and
> CHANGE_REQUESTS_BETA_REVIEW.md as current where they differ; these sections
> will be refreshed during the review pass.

---

## Headline

The add-in loads cleanly in DraftSight 2026, surfaces a branded ribbon
button, presents a product picker, and drives a **non-modal** Lift Blanket
dialog (DraftSight stays interactive — pan/zoom while the form is open). The
dialog has: project metadata + a **Left / Right / Rear tabbed input** where
each wall is drawn as a **fixed schematic copy of the paper measurement
sheet** with the input fields embedded on the drawing, an Options section,
a **7-layer cutter-layer panel**, and a free-text NOTES field. Generate emits
a layered drawing — blue cut rectangles, segment-driven COP cutouts on the
draw layer, bottom-half quilting, DIMENSION entities, and free-floating
worksheet text — then optionally exports a native R2018 ASCII DXF. Ctrl+Z
reverts the whole generation in one step.

The **calculation model** (the cut geometry) and the **preview diagram** are
two separate things, and this distinction is load-bearing:

- The **calculation** (in the headless `LiftBlanketCalculator`) produces the
  real cut geometry: doubled height, segment-driven COP, quilting. It is
  unit-tested against the client's reference DXF coordinates. **This is what
  ends up in the DXF.**
- The **preview diagram** (in `WallBlanket`) is a *fixed schematic* — it
  never resizes or rescales based on typed values. It exists only to show the
  operator the layout and host the input fields where they sit on the paper
  sheet. (An earlier true-proportion preview collapsed to an unusable sliver
  when a single digit was typed mid-edit — the fixed schematic deliberately
  avoids that whole class of bug.)

---

## The calculation model (how it computes the cut geometry)

All in `LiftBlanketCalculator` (SDK-free, headless, unit-tested). For each
enabled wall:

**Width (horizontal):**
- The operator types five bottom-row segment boxes: `DR-L | S1 | S2 | S3 | DR-R`
  (door-return-left, three interior segments, door-return-right). Place 0
  where not needed.
- `cutWidth = (DR-L + S1 + S2 + S3 + DR-R) + edgeAllowance`. The edge
  allowance is an operator field, default **10 mm**, split evenly: half on
  each horizontal side, and that same half is the inset for quilting.

**Height (vertical):**
- The operator types the **measured** height (top-of-hook / centre-of-press-
  stud to bottom of blanket).
- `foldMidline = measuredHeight − fixingAllowance` (the blanket folds here).
- `cutHeight = foldMidline × 2` (the bottom panel is the measured half; the
  top is its mirror). So measured 2200, fixing 50 → fold 2150, cut 4300.
- The fixing allowance defaults per fixing type (Hooks −50, Press Studs −40,
  Eyelet TG9/TG7 −30, Velcro 0) and is operator-editable per job.

**COP (the cut-out panel) — segment-driven:**
- The COP's **width is the middle segment, Seg2**. Its left edge =
  `edgeAllowance/2 + DR-L + S1` (measured from the door-return line, not the
  blanket edge). So you don't type a COP width/offset — the segments drive it.
- Vertically the operator types **bottom-gap** (wall bottom → COP bottom) and
  **COP height**. The **top gap** (COP top → fold line) is auto-derived:
  `autoTopGap = foldMidline − bottomGap − copHeight`. A negative top gap means
  the COP crosses the fold — a **blocking validation error**. The COP sits in
  the bottom (measured) half only; the top half mirrors it at cut time.

**Quilting (score lines on the draw layer, bottom half only):**
- **Horizontal lines** run the **full cut width minus the edge clearance**
  (`half … cutWidth−half`), spaced up the height by the operator's
  "vertical quilting spacing" target, even-divided (the line count is rounded
  so there is no remainder gap). They fill from the bottom clearance up to the
  fold midline.
- **Vertical lines**: one on the **DR-L boundary** and one on the **DR-R
  boundary** (each skipped if that door-return segment is 0), **plus**
  even-fill interior verticals between those two boundaries. Same bottom-to-
  fold extent.
- `EvenlySpaced` rounds the gap count to land on even spacing and **caps it at
  200 gaps** so a tiny spacing value can't emit thousands of lines and freeze
  the host.

**Walls + DXF layout:**
- Walls are drawn left-to-right as **L → Rear → R** (the back wall sits in the
  middle, between the side walls). Through Car omits the rear wall.
- Wall identifier label per wall: `<network> <project> L/R/B`, text height 20.

---

## The UI (what the operator sees)

- **Left / Right / Rear tabs.** Each L/R tab is a **fixed sheet schematic**:
  a landscape blanket rectangle, the COP drawn over the S2 column, the five
  segment fields in a row below, a measured-height field on the side, and the
  COP height / from-bottom fields beside their dimension lines. Real
  dimension-symbol lines (with arrowheads) annotate each section. **The Right
  wall is a mirror of the Left** (COP and its fields on the opposite side),
  since the two walls face each other on the sheet. The Rear tab is just a
  Width + Height (no segments, no COP) and is disabled when Through Car is
  ticked.
- All input fields **start empty** with greyed external labels. The schematic
  draws a sensible default layout regardless. Typing fills the fields; the
  drawing does **not** rescale.
- The auto top-gap shows live beside the COP and turns red with a ⚠ when the
  COP crosses the fold.
- **Options:** Through Car, Plastic Cover on COP, Fixings dropdown (auto-fills
  the fixing allowance), Fixing Allowance (mm), Edge Allowance (mm), Quilting
  Spacing (mm), "Draw quilting lines", and "Open DXF export dialog after
  generating".
- **Layers panel:** all **seven cutter layers** (`0`, `1 Rotary Blade`,
  `2 Drag Blade`, `3 Crease Tool`, `4 Drill Tool`, `5 Draw and Text`,
  `Defpoints`) as rows, each with an **ACI colour swatch dropdown** and four
  **role checkboxes** (Cut / COP / Annot. / Title). A role belongs to exactly
  one layer (ticking it elsewhere unticks the prior). All seven layers are
  created in the DXF; only assigned ones carry geometry. Defaults: Cut →
  `1 Rotary Blade` (blue 5), COP + Annotation → `5 Draw and Text` (magenta 6),
  Title → `0` (white 7). "Reset to Defaults" button.

---

## Validation + user feedback (added v1.4.5)

The app surfaces problems instead of failing silently:

- **Blocking validation at Generate** (all errors shown at once): non-numeric
  / negative / NaN / Infinity fields; a wall with no width; **measured height
  must exceed the fixing allowance** (else the cut inverts); **COP must fit
  horizontally** within the cut piece; **COP must not cross the fold**;
  **quilting spacing ≥ 50 mm**; **at least one wall enabled**; every layer
  role assigned.
- **Generate vs export are separate**: an export failure reads "the drawing
  generated, but DXF export failed", not a misleading "failed to generate".
- **Missing-entity warning**: the generator counts SDK inserts that return
  null and warns "N entities could not be drawn" so a silently-incomplete
  drawing is surfaced.
- A generate failure (e.g. no drawing open) keeps the dialog open and tells
  the operator to Ctrl+Z any partial geometry before retrying.

---

## What works end-to-end

- COM add-in load (CLSID `aa497758-3d06-46b7-9f75-7a8f2fffed7c`), ribbon
  registration, orphan-tab cleanup at startup.
- Product picker (Lift Blanket available; Caravan Annexe disabled with a
  tooltip).
- The full Lift Blanket dialog described above, non-modal, parented to the
  DraftSight HWND, with focus-preserving live redraw.
- Generator: creates all 7 layers; draws cut rectangles on the cut layer, COP
  + quilt lines + labels on the draw layer, dims + worksheet text on layer 0;
  undo-grouped; `DIMSCALE` bumped to 30; `InsertAlignedDimension` for dims.
- Native DXF export (Save-As, filename = network number, R2018 ASCII DXF).
- Diagnostic `_CANVASCOVERSLAYERTEST` command (logs to
  `%LocalAppData%\CanvasCovers\layertest.log`).
- Inno Setup installer (`Installer\CanvasCovers.iss` + `build.ps1`).

## What's deliberately not yet built

- **Caravan annexe flow.** Picker tile exists; dialog + generator do not.
  Gated on a measurement form / sample DXF from the client.
- **Quilting spacing rule confirmation.** Quilting is built, but the exact
  spacing *rule* is operator-entered and unconfirmed. The agreed default is to
  even-divide a typed target. The on-DXF "VERTICAL QUILTING SPACING" lookup
  never reconciled cleanly (see `reference/DXF_FINDINGS.md`); the client may
  refine it after beta (CLIENT_QUESTIONS §3).
- **Project save/load** (no JSON serialisation of `LiftBlanketJob`).
- **Branded icons** (using the SDK sample placeholder PNGs).
- **Code signing** (add-in shows "Unknown Publisher").

## Known gotchas (don't re-learn these)

Detailed in [`/CLAUDE.md`](../CLAUDE.md) §9. The short version, with the
v1.4.x additions:

1. `Application.RemoveUserInterface(guid)` preemptively on first connect
   corrupts internal state — iterate tabs and remove by GUID instead.
2. Toolbar items with empty icon paths crash the host — use the ribbon.
3. `EntityHelper.SetLayer/GetLayer` crash on freshly-inserted entities — use
   the activate-based pattern (`LayerHelper`).
4. `InsertLinearDimension` is horizontal-only — use `InsertAlignedDimension`.
5. `InsertNoteWithParameters` produces no visible output — use
   `InsertSimpleNote`.
6. `MessageBox.Show` from a `Command.ExecuteNotify` is risky — reserve for
   catch blocks.
7. Non-modal dialogs need their owner HWND set; setting `DialogResult` throws,
   so use `KeyBinding` for Esc.
8. **(v1.4.x) WPF init-order NRE.** Event handlers wired in XAML
   (`TextChanged`, `Checked`) fire *during* `InitializeComponent`, before
   later-declared named elements exist. Guard with an order-independent
   `_initialized` flag set after `InitializeComponent`, not by null-checking a
   specific element (which may be declared before the trigger and so never be
   null when it fires).
9. **(v1.4.x) WPF setters throw on non-finite/negative.** `Rectangle.Width/
   Height` and `Line` coords throw on NaN/Infinity/negative, and
   `double.TryParse("NaN")` returns true. Any typed value that feeds a WPF
   size must go through a NaN/Infinity-rejecting parse, and the live redraw
   must be wrapped in a swallow-all try/catch (no dispatcher exception handler
   exists in-host — an unhandled throw crashes DraftSight).
10. **(v1.4.x) Z-order: drawn shapes can cover input fields and swallow
    clicks.** Give embedded input fields a high `Panel.ZIndex` so a line/rect
    drawn later in the redraw can't intercept their clicks.
11. **(v1.4.x) The preview is a FIXED schematic on purpose.** Do not make it
    rescale to typed values — that reintroduces the collapse-on-keystroke bug.
    The real geometry lives in the calculator; the preview only illustrates.

## Current commit baseline

- Branch: `feat/sheet-mirroring-cop-quilting`
- Version: **v1.4.5** (`MyAppVersion` in `CanvasCovers.iss`;
  `AssemblyVersion`/`AssemblyFileVersion` 1.4.5.0). Installer built:
  `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.4.5.exe`.
- Build: clean (`dotnet build -c Release` → 0 warnings, 0 errors).
- Tests: **29 passing** (`dotnet test CanvasCovers.Tests`) — headless unit
  tests over `LiftBlanketCalculator` (segment-driven COP, auto top-gap,
  quilting + the line-count cap), `FixingAllowance`, the wall model, and the
  DXF filename rule. The SDK-emission layer (generator + exporter) and the WPF
  UI are verified live in DraftSight, not by tests.
- Layer defaults: `1 Rotary Blade` (ACI 5 blue) = Cut; `5 Draw and Text`
  (ACI 6 magenta) = COP + quilt + annotation (draw/score, NOT cut); `0`
  (ACI 7 white) = dims + worksheet text. All 7 cutter layers created in the
  DXF.

## Quick verification

```powershell
# Close DraftSight first (the build script refuses while it's running).
.\Installer\build.ps1
# Then run the EXE in Installer\Output\ as admin (UAC prompts).
```

In DraftSight after install: new drawing → tick CanvasCovers in Add-Ins →
ribbon "CanvasCovers" tab → "Canvas Covers" button → "Lift Blanket" tile →
fill a wall tab → Generate. To remove: Settings → Apps → *BesiaCAD Canvas
Covers* → Uninstall. Upgrades auto-uninstall the previous version.
