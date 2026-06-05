# Lift Blanket redesign: sheet-mirroring input, segment-driven COP, quilting

**Date:** 2026-06-05
**Status:** Design — approved, pending spec review
**Supersedes:** the v1.2.0 COP-placement model (Width / Height / GapFromBottom /
OffsetFromLeft) and the separate side-panel `WallDiagram` preview.

---

## 1. Why

The v1.2.0 live test surfaced that the COP input model does not match how the
client's measurement sheet actually works. The operator was asked to type COP
width / height / offsets as free numbers; in reality the **bottom-row segments
ARE the COP's horizontal geometry**, and the COP is positioned relative to the
door-return lines, not the blanket edge. The operator should type exactly what
is on the paper sheet and nothing more.

Three changes follow from that:

1. **Segment-driven COP.** The five bottom-row boxes fully determine the COP
   horizontally; a three-box vertical stack determines it vertically. The old
   four COP fields are deleted.
2. **The drawn blanket becomes the input form.** Instead of a passive
   side-panel preview plus separate text rows, each wall is a drawn blanket
   with the input fields embedded on it where the measurements live on the
   paper sheet. Walls are arranged in **tabs** (Left / Right / Rear).
3. **Quilting is built** (previously deferred): vertical + horizontal quilt
   lines in the bottom half, driven by an operator-entered spacing with even
   division, inset by a unified edge allowance.

---

## 2. Geometry rules (the source of truth)

All measurements in millimetres. Reference job is 12346, Left wall:
bottom row `250 / 350 / 240 / 1400 / 0`, measured height `2200`, fixing
allowance `50`, COP vertical `top 300 / 1300 / bottom 600`.

### 2.1 Width (horizontal)

- The five bottom-row boxes are: `DR-L | S1 | S2 | S3 | DR-R`.
  - `DR-L` / `DR-R` = door-return corner tabs (0 if none).
  - `S1` = gap from the left door-return line to the COP's left edge.
  - `S2` = **the COP width**.
  - `S3` = gap from the COP's right edge to the right door-return line.
- **Segment sum** = `DR-L + S1 + S2 + S3 + DR-R` (= 2240 for the reference).
- **Edge allowance** is a single operator-editable value (default **10**),
  split evenly: half on the left edge, half on the right edge, half on the
  bottom edge. For allowance 10 → 5mm each side; for 12 → 6mm each side.
- **Cut width** = segment sum + edge allowance (= 2250 for allowance 10).
- The cut rectangle is the segment grid **grown by half the allowance on each
  horizontal side**. Equivalently: the segment grid sits inset by
  `allowance / 2` from each cut edge.
- **COP left edge** (in segment-grid coordinates) = `DR-L + S1` (= 590).
  In cut-rect coordinates that is `allowance/2 + DR-L + S1`.
- The segments are **always** typed and **always** summed into the width,
  whether or not COP is enabled. Disabling COP does not change the width math;
  it only stops the COP rectangle from being drawn (S2 becomes just part of
  the plain rectangle).

### 2.2 Height (vertical) — UNCHANGED math

- **Measured height** is always typed (e.g. 2200).
- **Fixing allowance** (existing field, per fixing type, editable) is
  subtracted, then the result is doubled:
  `cutHeight = (measuredHeight − fixingAllowance) × 2` (= 4300).
- **Fold midline** = `measuredHeight − fixingAllowance` (= 2150). The blanket
  folds here; the top half mirrors the bottom.
- This is the v1.2.0 math, unchanged.

### 2.3 COP vertical placement

- With COP enabled, the operator types **bottom gap** (wall bottom → COP
  bottom, = 600) and **COP height** (= 1300).
- **Top gap is auto-derived and read-only**, referenced to the **fold line**:
  `topGap = (measuredHeight − fixingAllowance) − bottomGap − copHeight`
  = `foldMidline − bottomGap − copHeight` = 2150 − 600 − 1300 = **250**.
  Confirmed against the reference DXF (COP-top to fold ≈ 244.7 ≈ 250). The
  fixing allowance comes **off** the top gap — the sheet's hand-written "300"
  is the pre-fixing figure; the true drawn gap to the fold is 250.
- **Blocking validation:** if `topGap < 0` (i.e. `bottomGap + copHeight >
  foldMidline`), Generate is blocked with an error. This replaces the existing
  `CopFitsInBottomHalf` check, now expressed against the fold midline.
- With COP disabled, the three vertical boxes (bottom gap, COP height, top
  gap) are blank and disabled.

### 2.4 Quilting

Drawn on the **draw/score layer** (`5 Draw and Text`), never the cut layer.

- **Two line sets:** vertical lines (running top→bottom, spaced across the
  width) AND horizontal lines (running left→right, spaced up the height).
- **Extent:** bottom (measured) half only — lines fill from the bottom inset
  up to the **fold midline** (2150), never the full doubled height.
- **Side bounds:** the quilt region is bounded on the left by the **first
  bottom-row box** (`DR-L`) in from the left edge, and on the right by the
  **fifth bottom-row box** (`DR-R`) in from the right edge. So the quilted
  zone spans from `DR-L` to `(cutWidth − DR-R)` in cut-rect X. Plus the edge
  inset (`allowance / 2`) is added inboard so the outermost lines sit just
  inside the outline. (For the reference Left wall, `DR-L = 250`, `DR-R = 0`,
  so the zone is 250 → cutWidth on the segment grid.)
- **Bottom bound:** lines start `allowance / 2` up from the bottom outline
  (same tolerance as the sides — one unified number).
- **Spacing:** a single operator-entered **vertical quilting spacing** value
  (no lookup table) is the *target* gap between the **horizontal** lines up the
  height. The actual count is **rounded up or down so the lines divide the
  region evenly** — no remainder gaps. The **vertical** lines even-divide the
  bounded width the same way.
- The lookup table from the reference DXF is NOT baked in; the operator types
  the spacing. (The exact client rule for the table is still unconfirmed; the
  even-division approach is the agreed default.)

### 2.5 Open question deferred to the client (does not block this work)

- Whether quilting should ever extend above the fold or use a fixed lookup
  table rather than a typed spacing. For now: bottom half, typed spacing,
  even division.

---

## 3. UI design

### 3.1 Tabbed wall layout

- The Lift Blanket dialog's three stacked wall sections are replaced by a
  **tab control**: `Left | Right | Rear`. Rear tab is disabled when Through
  Car is ticked (mirrors existing behaviour). 3 tabs (Through Car off) or
  2 tabs (on).
- Shared, non-tabbed regions remain: branded header, Project Information
  (incl. Notes), Options (Through Car, Plastic Cover, Fixings, Fixing
  Allowance, **Edge Allowance**, **Vertical Quilting Spacing**, Export DXF
  toggle), and the Layers panel.

### 3.2 The drawn blanket as the form

Each wall tab shows a **single drawn blanket** with input fields embedded on
it, positioned where the measurement lives on the paper sheet:

- Five **segment fields** along the bottom edge (`DR-L / S1 / S2 / S3 / DR-R`).
  S2 is visually marked as the COP-width driver.
- **Measured height** field on the outer-left side.
- COP **bottom gap** and **COP height** fields beside the COP rectangle;
  **top gap** shown as a read-only/greyed auto value.
- An **"Include this wall"** checkbox and an **"Include COP cutout"** checkbox
  in the tab header. (These remain two distinct toggles — wall inclusion vs
  COP presence. The live test confusion was a mis-tick, not a code bug.)
- **Dimension lines / leaders** on the schematic: a leader from the bottom
  edge up to the COP base (the "from bottom" value), a leader for COP height,
  and **dashed detail lines** showing the zone each segment controls.

### 3.3 Live, true-proportion preview

- The drawn blanket **reshapes to the true height:width ratio** of the entered
  values (scaled to fit the tab). Real blankets stay in reasonable aspect
  ratios, so visual honesty is worth more than a fixed box.
- **Defensive clamp:** the aspect ratio is clamped to a sane range (e.g.
  between 1:3 and 3:1) so a freak/typo input cannot collapse the layout or
  push fields off-canvas.
- The preview updates **live** as fields change: segment dividers, COP
  position/size, quilt lines, and the auto top-gap all recompute on each
  edit. Redraw reuses the existing lightweight canvas-rebuild path.
- The displayed dimension **values are always exact** (the typed numbers and
  derived top gap), independent of the drawn scale.

---

## 4. Code architecture (mirrors the existing calculator/generator split)

Per [[project_liftblanket_calculator_split]], math stays SDK-free and tested;
SDK emission stays a thin translator.

### 4.1 Models (`Models/Products/LiftBlanket/`)

- **`WallSegments`** — unchanged shape (`DoorReturnLeft / Seg1 / Seg2 / Seg3 /
  DoorReturnRight`, `TotalWidth`). Comment updated to reflect that Seg2 is the
  COP width and segments bound the COP.
- **`CopPlacement`** — **reshaped**. Remove `Width` and `OffsetFromLeft`
  (now derived: width = `Seg2`, offset = `DoorReturnLeft + Seg1`). Keep
  `Enabled`, `Height` (COP height), `GapFromBottom` (bottom gap). Top gap is
  computed, not stored.
- **`LiftBlanketOptions`** — add `EdgeAllowanceMm` (default 10, replacing the
  hardcoded `WidthAllowanceMm = 10` constant) and `VerticalQuiltingSpacingMm`
  (operator-entered target spacing). Keep `QuiltingEnabled` but it now
  defaults **on** (quilting is built). Existing fields unchanged.

### 4.2 Calculator (`Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`)

- `CutWidth(summedSegments, edgeAllowance)` — parameterise the allowance
  instead of the `+10` constant.
- COP rect computed from segments: `copX0 = originX + edgeAllowance/2 +
  DoorReturnLeft + Seg1`, `copWidth = Seg2`. Cut rect grows by
  `edgeAllowance/2` on each horizontal side relative to the segment grid.
- `AutoTopGap(measuredHeight, fixingAllowance, bottomGap, copHeight)` →
  `foldMidline − bottomGap − copHeight`.
- Replace `CopFitsInBottomHalf` semantics with a `TopGap >= 0` check
  (equivalent, but expressed via the new derivation).
- **New: quilt-line layout.** A method that, given the cut rect, segment
  bounds, fold midline, edge allowance, and target vertical spacing, returns
  the set of vertical and horizontal quilt `LineSpec`s (even-divided,
  inset). Emits a new `QuiltSpec`/`LineSpec` DTO in `WallLayout`.
- All of the above is unit-tested headlessly against the reference numbers
  (cut 2250×4300, COP left 590+5, top gap 250, quilt extents to 2150, even
  division).

### 4.3 Generator (`LiftBlanketGenerator.cs`)

- Replace the hardcoded `WidthAllowanceMm`-derived behaviour with the
  options-driven allowance.
- Draw the new quilt `LineSpec`s on the COP/draw layer via `InsertLine` (or
  `InsertPolyline2D` per line) — thin translation, no math.
- COP emission unchanged in shape; only its coordinates now come from the
  segment-derived calc.

### 4.4 UI

- **`WallDiagram`** is substantially rewritten: from a passive highlight
  preview into the **interactive, embedded-field, true-proportion, live**
  blanket. It hosts the segment/COP/height input `TextBox`es positioned on
  the canvas, recomputes layout on `TextChanged`, and renders dimension
  leaders + dashed zone-detail lines + quilt lines.
- **`LiftBlanketWindow`** — wall sections become a `TabControl`; `ReadWall`
  pulls from the per-tab embedded fields; remove the COP width/offset reads;
  add edge-allowance and quilting-spacing reads from Options; wire the
  negative-top-gap blocking validation.

### 4.5 Tests (`CanvasCovers.Tests`)

- Update COP tests for the segment-derived model (width = Seg2, offset =
  DR-L + S1 + allowance/2).
- New tests: auto top gap = 250 for the reference; edge-allowance split;
  quilt even-division (count + positions + 2150 extent + inset).
- Existing width/height/fold tests adjusted for the parameterised allowance.

---

## 5. What is explicitly NOT changing

- Height math `(measured − fixing) × 2`.
- Layer convention (`1 Rotary Blade` cut, `5 Draw and Text` COP+quilt+labels,
  `0` dims+text).
- DXF export (native Save-As, filename = network number).
- The calculator/generator/test split architecture.
- Project metadata, Notes, Through Car semantics, the failure-path /
  keep-dialog-open behaviour.

---

## 6. Risks / things to verify live

1. Embedding live `TextBox`es on a WPF `Canvas` that also reshapes — field
   re-layout on aspect-ratio change must stay stable (hence the clamp).
2. Quilt line counts: even-division rounding must never produce zero lines or
   absurd counts for small regions — guard with min/max.
3. The whole-implementation end-to-end review is mandatory before shipping
   (per [[project_liftblanket_calculator_split]] — the LayersPanel/model drift
   class of bug): trace one job dialog → job → calc → generator → SDK, and
   confirm the `LayersPanel` defaults still agree with the model.
4. Live verification in DraftSight against reference job 12346 + DXF parse.
