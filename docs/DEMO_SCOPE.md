# Demo Scope

This document defines exactly what the demo build will and will not do.
**Treat it as a contract.** Anything outside this scope is deferred to paid
work, no matter how easy it seems to add.

The demo's job is to be *convincing*, not useful. It needs to make the client
say "oh shit, you can do that?" so the commercial conversation becomes easy.
Scope creep is the enemy.

---

## In scope

### Cover type

**One only: rectangular cover with rounded corners.**

This shape was chosen because:
- Universally recognizable across industries (caravan, lift, marine, awning)
- Demonstrates all the primitives the real engine will need: outer profile,
  offset paths, point arrays for fixings, layered output
- Simple enough to build in 1–2 focused days

### User interface

A single WPF window launched from a ribbon button labelled "Generate Cover".

Form fields (all numeric, all in millimetres):

| Field                  | Default | Notes                                  |
| ---------------------- | ------- | -------------------------------------- |
| Width                  | 2000    | Outer width of the cover               |
| Length                 | 3000    | Outer length of the cover              |
| Corner radius          | 50      | Radius of the rounded corners          |
| Hem allowance          | 25      | Inset from outer edge for the hem line |
| Stitch offset from hem | 10      | Inset from hem line for stitch line    |
| Eyelet spacing         | 250     | Centre-to-centre spacing along edges   |
| Eyelet inset from edge | 15      | Inset from outer edge for eyelet row   |

Plus:
- A "Generate" button
- A "Cancel" button (closes the form without doing anything)

### Output (drawn into the active DraftSight drawing)

Four layers, created if they don't exist:

| Layer  | Colour | Linetype   | Contains                                       |
| ------ | ------ | ---------- | ---------------------------------------------- |
| `CUT`  | Red    | Continuous | The outer cutting profile (rounded rectangle)  |
| `STITCH` | Cyan | Dashed     | The stitch line (offset inward from hem)       |
| `FIX`  | Yellow | Continuous | Eyelet positions as small circles (radius 3mm) |
| `REF`  | Grey   | Continuous | Centerlines along width and length axes        |

Geometry rules:
- Cutting line: rounded rectangle, dimensions = width × length, with the
  specified corner radius.
- Hem line: offset of the cutting line inward by `hem allowance`. Used
  internally as the reference for the stitch line; not drawn as a separate
  entity for the demo.
- Stitch line: offset of the hem line inward by `stitch offset from hem`.
  Drawn on `STITCH` layer.
- Eyelets: distributed along all four edges at `eyelet spacing` intervals,
  inset from the outer edge by `eyelet inset from edge`. Corner eyelets are
  placed at the first valid position along each edge.
- Centerlines: two lines, one across the width axis, one across the length
  axis, extending the full dimension on `REF`.

### Validation

- Numeric-only inputs.
- Reject negative or zero values with a friendly error message.
- Reject `corner radius > min(width, length) / 2` with a clear explanation.
- No other validation in the demo.

### Behaviour

- Opens against the currently active drawing in DraftSight.
- If no drawing is open, prompts the operator to create one first.
- Geometry is drawn at world origin (0,0); no UCS handling in the demo.
- After generating, the form closes. To generate another, click the ribbon
  button again.

---

## Out of scope — explicit non-goals

The following are **deliberately not in the demo.** If asked to add them,
push back and ask whether it changes the commercial conversation.

### Cover variations
- Multiple cover types (caravan, boat, generic)
- Asymmetric panels or darts
- Variable corner radii (different at each corner)
- Curved (non-circular) edges
- Cutouts for handles, vents, windows
- Reinforcement patches

### Fixings
- Multiple fixing types (only one type: eyelets as small circles)
- Custom fixing block definitions
- Variable spacing along different edges
- Manual fixing positions (operator-placed)

### Output
- DXF file export (geometry is drawn into the live drawing only; export
  later)
- Cutting-list / bill-of-materials output
- Drawing title block / annotation

### Persistence
- Saved presets / templates
- Last-used values
- Project-level configuration
- Multi-job batch generation

### UI polish
- Dark mode
- Multi-language
- Help / tooltips beyond the most basic
- Drag-resize the window
- Live preview as the operator types

### Platform
- Multi-version DraftSight support (target DS 2026 only)
- Localized units (mm only; no inches)
- License gating (demo runs unconditionally)
- Code signing
- Auto-update
- Telemetry

### Productisation
- Installer (manual deploy from `bin\Release\` for the demo)
- Documentation beyond a one-page quickstart
- Support process

---

## Done criteria

The demo is "done" when:

1. DraftSight 2026 opens, ribbon button is visible.
2. Click the button → form appears.
3. Enter the default values → click Generate → all four layers exist with
   correct geometry on each.
4. Change the dimensions → click Generate → geometry redraws with new
   numbers.
5. Invalid input (negative, zero, oversized corner radius) → friendly error,
   no geometry drawn.
6. Demo can be loaded and run from `bin\Release\` by following a one-page
   quickstart, without any external help.

Any feature not listed here is not part of "done" — even if obviously useful.

---

## What to do if scope creep tempts you

If during the build you find yourself thinking "this would be easy, I'll just
add..." — stop. Write the idea into `docs/POST_DEMO_BACKLOG.md` (create the
file if it doesn't exist). Move on.

Scope creep on a demo is how a two-day build becomes a two-week build, and
how the commercial moment slips out of reach.
