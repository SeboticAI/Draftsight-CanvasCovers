# Roadmap

Ordered list of what is queued next. Move completed work into
[STATUS.md](STATUS.md), and add newly noticed but non-urgent items to the
parking lot.

## Up Next

### 0. Customer Handoff Test

**Current build:** v1.7.0,
`Installer\Output\BesiaCAD-CanvasCovers-Setup-1.7.0.exe`.

The lift-blanket add-in is feature-complete for the production customer handoff:
segment-driven COP, doubled-height math, total-width override, Quilt Inset,
vertical COP reminders, six-layer cutter panel, DXF export, strict validation,
previous-job memory (walls + options + project notes), and release-readiness
hardening.

Immediate next action is live install/load/generate/export testing in
DraftSight (including the round-3 notes carry-over), then hand the installer and
quick-start PDF to Adelaide Annexes & Canvas.

When customer feedback arrives, the working principle remains:
**change only what is needed; do not touch stable old code.**

Likely feedback areas and where they land:

- **Quilting spacing rule**: currently operator-entered target spacing with
  even division. If Martin provides a real lookup/formula, change
  `LiftBlanketCalculator.AddQuiltLines` and add tests.
- **Diagram cosmetics**: fixed schematic only; changes go in
  `WallBlanket.xaml.cs` and should not touch calculator math.
- **Layer / colour fidelity**: defaults and role assignment live in
  `LayerSettings` / `LayersPanel`.
- **Text / annotation layout**: free-floating notes live in
  `LiftBlanketGenerator.DrawProjectAnnotations` / `DrawTopLegend`.
- **Operator wording/help**: update `docs/help/CanvasCovers-Quick-Start.html`
  and regenerate the PDF.

### 1. Caravan Annexe Product Flow

**Gated on:** measurement sheet or sample DXF from Adelaide Annexes & Canvas.

Once specs arrive, add the product in the same shape as Lift Blanket:

- `Models/Products/CaravanAnnexe/CaravanAnnexeJob.cs`
- supporting caravan-specific model types
- `Geometry/Products/CaravanAnnexe/CaravanAnnexeGenerator.cs`
- `UI/Products/CaravanAnnexe/CaravanAnnexeWindow.xaml` and `.cs`
- enable the Caravan Annexe picker tile
- dispatch it from `OpenCanvasCoversCommand`

Do not invent the geometry before receiving the real sheet/DXF.

### 2. Project Save / Load

Why: the operator can enter many fields, generate, spot one issue, and want to
tweak without retyping.

Likely shape:

- Save/load `LiftBlanketJob` as a `.canvascover-job` JSON file.
- Prefer in-box `System.Runtime.Serialization.Json` unless a strong reason for
  Newtonsoft.Json appears.
- Add Save Project / Open Project buttons to the lift-blanket dialog.
- Reuse existing `Apply(...)` methods on shared controls and add missing ones
  on `WallBlanket` / `LiftBlanketWindow`.

### 3. Branded Icons

Current ribbon icons are SDK placeholders. Replace with real 16x16 and 32x32
PNGs before a polished commercial release.

### 4. Code Signing

The installer and add-in are still unsigned. Before broader distribution:

- buy/use a code-signing certificate
- sign the DLL and installer EXE
- integrate signing into `Installer\build.ps1`
- verify DraftSight no longer shows Unknown Publisher

## Mid Priority

- Field-level tooltips in the form.
- Configured default export folder if the customer asks for it.
- Multi-job batch generation.
- Cutting-list / measurement-summary PDF.
- Licensing infrastructure.
- Multi-version DraftSight support.

## Long Term

- More canvas products: bag awnings, mesh walls, bed end bag flys, storm
  flaps, caravan covers, ute canopies.
- Productisation through SeboticAI/BesiaCAD with generalised branding,
  licensing, and website distribution.
- Consider a reusable canvas-product framework only after several products
  prove enough shared structure.

## Parking Lot

- Orphan-tab cleanup only checks the active workspace; stale tabs can remain in
  other workspaces.
- `LayerHelper` does not explicitly release COM RCWs. Fine at current scale.
- Version bumping is manual across `CanvasCovers.iss` and `AssemblyInfo.cs`.
- `CanvasCovers.csproj` has hardcoded DraftSight SDK paths.
- Title/project annotation layout is row-of-text; a grid layout would look more
  polished.
- Layer colour dropdown exposes only ACI 1-7.
- Fixings dropdown can trigger a double redraw of the three blanket controls.
- `LayerSettings` role accessors do small repeated LINQ scans; not a hot path.
- Blank blanket text falls back to a timestamp filename and blank wall labels
  except suffix. Add a soft warning only if the customer finds this confusing.
