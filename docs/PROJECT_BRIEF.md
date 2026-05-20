# Project Brief — DraftSight Canvas Cover Generator

## The client

A canvas cover manufacturer producing covers for caravans, lifts, and various
other applications. Found us through tutoring work. Currently drafting canvas
cutouts manually in DraftSight — entering dimensions, stitching lines, and
fixing positions by hand for every job.

DraftSight tier confirmed: **Professional, Premium, or Enterprise** (SDK
access available — verify exactly which tier on first site visit).

## The problem they have

Every canvas cover follows the same conceptual pattern but with different
numbers:

- Outer dimensions (the actual cover shape)
- Hem allowance (offset for folded / sewn edges)
- Stitching lines (offset from hem or seam)
- Fixings (eyelets, snaps, webbing loops) placed around the perimeter
- Output goes to a cutting workflow

Today, an operator draws each cover by hand in DraftSight. Skilled labour,
repetitive, error-prone, and a productivity ceiling on the business.

## The solution we're proposing

A DraftSight add-in that:

1. Adds a ribbon button to launch a form
2. Operator enters the parameters in a clean WPF UI
3. Click "Generate"
4. Add-in draws the cover into the active DraftSight drawing, with
   geometry on the correct layers (cutting line, stitch line, fixings,
   reference)
5. The DXF is then ready for whatever downstream production workflow
   they already use

## Why this approach (vs LISP, macros, standalone tools)

- **LISP** can draw geometry but UI is crude (command-line prompts). Bad fit
  for a production operator.
- **Macros (Basic)** — limited maintainability and packaging.
- **C# add-in** — proper WPF form, persistent settings, real error handling,
  path to license-gate and resell. Reuses the same architectural pattern as
  the BesiaBIM Revit work.
- **Standalone tool that emits DXF** — would work, but loses the in-CAD
  experience. The wow factor of the demo depends on entities appearing
  *inside DraftSight* where the operator already lives.

## Why this is a strategic project (not just a one-off)

- Same pattern as BesiaBIM Revit add-ins. Code patterns, packaging, and
  (eventually) licensing infrastructure all transfer.
- Canvas / awning / marine cover firms across the country face the same
  problem. The custom-for-one-firm → generalize-to-catalog playbook applies.
- Positions a "BesiaCAD" track inside the broader BesiaBIM platform — same
  thesis (deterministic workflow tooling, not AI chat), different host CAD.

## The current phase: free demo

The client has not committed money yet. The plan is:

1. Build a **free, focused demo** showing one cover type working end-to-end.
2. Show it in person — the moment they see their own job materialize from a
   form, the commercial conversation gets much easier.
3. Then propose a paid build (Phase 0 spec → Phase 1 MVP → Phase 2
   parametric engine).

The demo's job is not to be useful. Its job is to be *convincing*.

## Demo scope (full detail in `DEMO_SCOPE.md`)

- One cover type: rectangular with rounded corners.
- WPF form with ~6 inputs (dimensions, corner radius, hem, stitch offset,
  eyelet spacing, eyelet inset).
- Output: layered DXF entities drawn into the active drawing.
- Layers: `CUT`, `STITCH`, `FIX`, `REF`.

## Out of scope for the demo

- Multiple cover types
- Persistence / saved presets across sessions
- Licensing or DRM
- Multi-version DraftSight support
- Code signing
- Installer (just a manual deploy from `bin\Release\` for the demo)

## Commercial structure (post-demo, not part of this build)

Two-track model, same as BesiaBIM:

1. **Custom build fee** — paid Phase 0 discovery + Phase 1 MVP build,
   delivered as a tool that fits their shop exactly.
2. **IP retained for resale** — BesiaBIM owns the engine and can generalize
   it to other firms. Client gets a 20% lifetime discount on the future
   commercial product and confidentiality on their specific configurations.

Numbers and contract terms are in a separate proposal document (not in
this repo — kept separate for confidentiality).

## What we need from the client before paid work starts

- One or more sample DXFs of recent jobs (to copy their layer/linetype
  conventions exactly)
- 2–3 hours of shop-floor observation to extract the real rules behind
  stitch offsets and fixing placement
- Confirmation of DraftSight tier (already assumed Professional+, must verify)

## Risks worth tracking

- **SDK availability.** If the trial doesn't include the SDK, scope changes.
- **Geometry complexity.** Caravan covers in particular can have asymmetric
  panels, darts, and curved sections. The demo deliberately picks a simple
  shape; real Phase 1 will need to handle more.
- **DraftSight API stability.** Less battle-tested than Revit's. May hit
  undocumented quirks.
- **Stitching/fixing rules.** Demo uses simple "even spacing" — real rules
  might depend on cover type, edge length, fabric weight, etc. Phase 0
  discovery has to surface these.
