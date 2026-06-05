# Reverse-engineering the client reference DXFs

Derived from `reference/dxf/12345 TEST 12345.dxf` (2-wall) and
`12346 TEST 12346.dxf` (3-wall). This folder is gitignored; this file
is a working analysis, not a shipped doc. Promote confirmed rules into
the generator + docs once validated.

Inspector script: `reference/parse-dxf.ps1 -Path <dxf>`.

---

## Headline: the current wall model is wrong-shaped

The cut piece is **a plain closed rectangle per wall** on layer
`1 Rotary Blade`. There are **no door-return steps** in the cut path.
The "3 door returns on one side" model in `WallDimensions.cs` does not
appear in either real drawing. Everything else (COP, quilting, wall
labels) lives on `5 Draw and Text` (draw/score, not cut).

## Layer usage (ground truth)

| Layer            | Carries                                                        |
| ---------------- | ------------------------------------------------------------- |
| `1 Rotary Blade` | Cut rectangles only (one closed LWPOLYLINE per wall)          |
| `5 Draw and Text`| COP rectangle, vertical + horizontal quilt LINES, wall label TEXT (h=20), COP detail callout |
| `0`              | DIMENSION entities, FIXINGS/formula/quilting reference TEXT (h=100), project metadata TEXT (h=200), top legend |

So COP and quilting are **NOT** cut — they're draw/score. Current code
puts COP on its own `Cop` layer and quilting isn't emitted at all.

## Job 12345 (2 walls: L, R)

- L: X 1910.5..4550.5 → **width 2640**, Y 1043.4..5743.4 → **height 4700**
- R: X 6050.5..8290.5 → **width 2240**, height 4700
- Gap between walls ≈ 1500 (wider than 12346's 715 — gap is not fixed)
- COP per wall: 240 wide × 1400 tall, centred horizontally? (L COP X
  3641.3..3881.3 → centre 3761, wall centre 3230 — **NOT centred**,
  offset toward right). Revisit.
- Wall label TEXT: `12345 TEST 12345 L` / `... R` at h=20

## Job 12346 (3 walls: L, R, B — "Through Car" OFF)

- All three: **height 4300** (Y 1243.4..5543.4)
- L width 2240, R width 1400, B width 2240
- Gaps L→R and R→B both ≈ **715**
- B (back wall) drawn right-to-left (vertices reversed) but same rect
- COP on L: 240w × 1300h, top offset 2400 from wall top
- Wall labels: `... L`, `... R`, `... B` at h=20

## The math is pre-applied in the cut geometry

Cut **height** is the already-doubled value. Measurement sheet showed
`2400 less fixing -50 = 2350 ×2 = 4700` and `... = 4300`. The DXF
height equals that ×2 result. So:

    cutHeight = (measuredHeight - fixingAllowance) * 2

`WIDTH - ADD 10mm` reference text suggests cut width = measuredWidth + 10,
but not yet confirmed against a measured-width source number.

Fixing allowances (from on-drawing FIXINGS table, all jobs identical):
`HOOKS = -50`, `PRESS STUDS = -40`, `EYELET TG9 = -30`,
`EYELET TG7 = -30`, `VELCRO = 0`.

## Quilting — BUILT (v1.3.0+) with an agreed default; exact spacing rule still unconfirmed

**Status update:** quilting is now implemented. The agreed rules (from the
client over the redesign sessions, see the design spec):
- HORIZONTAL lines run the **full cut width** minus the edge clearance
  (edge-to-edge, NOT bounded by the door returns).
- VERTICAL lines sit on the **DR-L and DR-R boundaries** (skipped when
  that door-return segment is 0), plus even-fill verticals between them.
- Both sets fill **only the bottom (measured) half**, up to the fold
  midline, inset by half the edge allowance.
- Spacing is an **operator-entered target**, even-divided so there's no
  remainder gap (`LiftBlanketCalculator.AddQuiltLines` / `EvenlySpaced`,
  capped at 200 gaps). The on-DXF "VERTICAL QUILTING SPACING" lookup
  (below) never reconciled cleanly with measured spacing, so the operator
  types the value for now — the client may give the real formula after
  beta. The original analysis follows.

### Original analysis (unreconciled)

The on-drawing lookup is labelled **"VERTICAL QUILTING SPACING"**:
`4700=783, 4500=750, 4300=716, 4100=683`. Roughly height/6.

But measured line spacings don't cleanly match a single rule:

- 12346 (height 4300, table→716): **horizontal** lines spaced **712**
  (≈716 ✓), but the lines span the full wall width.
- 12346 **vertical** lines spaced **660** (≠716), spanning only
  Y 1248.9..3388.2 (~2139 tall, NOT full 4300 height).

So either:
- (a) the table drives spacing and my H/V orientation labels are flipped
  vs the client's terminology (their "vertical quilting" = lines running
  vertically but *spaced* by the table value horizontally?), or
- (b) full-width lines = main quilting (table-driven), short vertical
  lines = COP-region detail quilting (different rule).

**Do not bake a quilting rule until this is resolved** — likely needs a
one-line confirmation from the client or a third sample.

## COP detail callout (12346 only)

A small rect (500×200) at far right X 10107..10607 Y 3627..3827 on
`5 Draw and Text` with a leader: V line 10357 (Y 3627→2127) + H arrow
10257..10457 at Y 2127. Looks like a COP zoom/detail box. Low priority
for "full drawings" but noted.

## Implications for the redesign

1. `WallDimensions` should become: per-wall **measured** width + height
   + fixing type, with cut width/height **derived** (×2 + allowance),
   plus COP (offset/size) and a quilt flag. Drop DoorReturn1/2/3.
2. Walls are L / R / B(ack). "Through Car" OFF ⇒ B present (3 walls);
   ON ⇒ no B (2 walls). Matches existing ThroughCar option semantics.
3. Cut layer gets rectangles only. COP + quilt + labels go on
   `5 Draw and Text`. Reference text + dims + project info on `0`.
4. Wall identifier labels (`<net> <name> L/R/B`, h=20) should come back
   (CLIENT_QUESTIONS §6) — the real drawings carry them.
