# Roadmap

Ordered list of what's queued, with rationale. Newly-completed items move
to [STATUS.md](STATUS.md); newly-identified items go in **Parking lot**
at the bottom of this file.

This is the file to consult when picking the project back up after a
break: "what was next?"

---

## Up next (in order)

### 0. CLIENT BETA FEEDBACK (current driver)

**v1.4.5 is the build going to Adelaide Annexe for beta testing.** The
lift-blanket flow is feature-complete: segment-driven COP, doubled-height
math, built quilting, the fixed sheet schematic with mirrored Left/Right
walls, the 7-layer panel, DXF export, and a full validation/feedback
layer. The next move is **the client's feedback on the real DXFs they
cut.**

When feedback arrives, the working principle (per the user) is:
**change only what's needed; don't touch stable old code.** Most-likely
feedback areas, with where they'd land:
- **Quilting spacing rule** — still the one unconfirmed geometry rule.
  Currently an operator-entered target, even-divided. If the client gives
  the real formula/lookup, it's a small change in
  `LiftBlanketCalculator.AddQuiltLines` + a test (CLIENT_QUESTIONS §3).
- **Diagram cosmetics** — the preview is a fixed schematic in
  `WallBlanket`; tweaks there don't touch the calculation.
- **Layer / colour fidelity** — the 7-layer panel + defaults in
  `LayerSettings`; adjust defaults, not the generator.
- **Text / annotation layout** — `LiftBlanketGenerator`'s
  `DrawProjectAnnotations` / `DrawTopLegend`.

Also pending: a short **help document** for the operator to send with the
installer (how the form maps to the sheet, what each field does). Flagged
for the next session.

### 1. Caravan annexe product flow

**Gated on:** measurement sheet / sample DXF from Adelaide Annexe & Canvas.

Once we have either, the work is roughly:

- `Models/Products/CaravanAnnexe/CaravanAnnexeJob.cs` — composite model
  (project + layers + caravan-specific panels + caravan-specific options)
- `Models/Products/CaravanAnnexe/...` — whatever panel / dimension types
  the form needs
- `Geometry/Products/CaravanAnnexe/CaravanAnnexeGenerator.cs` — layered
  output following the same `LayerHelper` activate pattern lift blanket
  uses
- `UI/Products/CaravanAnnexe/CaravanAnnexeWindow.xaml` (+ `.cs`) —
  reuses `BrandedHeader`, `ProjectMetadataPanel`, `LayersPanel`
- Enable the Caravan Annexe tile in `ProductPickerWindow.xaml`
- Wire it up in `OpenCanvasCoversCommand.Execute` switch

Until specs arrive this is "ready to build, not started". Don't invent
the structure — it'll be wrong.

### 2. ~~DXF auto-export~~ — DONE (v1.2.0)

Native Save-As after Generate, filename = network number (timestamp
fallback), R2018 ASCII DXF, gated on a dialog checkbox. v1.4.5 added an
export-specific error path (a save failure no longer reads as a generate
failure). A *configured output folder* (vs the operator picking each
time) is still possible if the client wants it — low priority.

### 3. Project save / load

**Why now:** the operator might fill 30 fields, generate, find one
wrong, want to tweak and re-generate. Right now they retype everything.

- Newtonsoft.Json or `System.Runtime.Serialization.Json` — the latter
  is in-box for net48 so likely simpler (no NuGet dependency)
- Save: dialog gets a "Save Project..." button → `SaveFileDialog` →
  serialise `LiftBlanketJob` (or `CaravanAnnexeJob`) to `.canvascover-job`
- Load: dialog gets a "Open Project..." button → `OpenFileDialog` →
  deserialise → call each UserControl's `Apply(model)` method (already
  exists for ProjectMetadataPanel and LayersPanel)
- File extension association: out of scope; operator opens via the
  dialog button

### 4. Branded icons

**Why now:** SDK placeholder icons (cogwheels) cheapen the demo. A real
logo / icon set elevates it.

- 16×16 and 32×32 PNGs for the ribbon button
- Possibly bigger versions for the product picker tiles
- Should match Adelaide Annexes & Canvas brand if going to them, or
  Sebotic / BesiaCAD brand if we're productising

Needs design asset from outside — not a coding task.

### 5. Code signing

**Why now:** "Unknown Publisher" warning on add-in load looks
unprofessional and degrades trust in a commercial release.

- Code-signing certificate purchase (annual)
- `signtool.exe` integration in the installer build script
- Sign both the DLL and the installer EXE
- Verify the warning is gone in a fresh install

---

## Mid-priority (after the above are done)

- **Field tooltips** — for the operator who doesn't already know the
  measurement form. Hover-help on each input. (A standalone help doc is
  the near-term substitute — see Up-next §0.)
- ~~**Visual preview in the dialog**~~ — DONE. The `WallBlanket` fixed
  schematic shows the layout + dimension lines before Generate.
- **Multi-job batch** — operator has 5 jobs queued up; generate them
  one after another into separate drawings or a single drawing.
- **Cutting-list / measurement summary print** — alongside the DXF,
  produce a PDF for production paperwork.
- **Licensing infrastructure (BesiaBIM Ed25519 token integration)** —
  necessary if going subscription. Already noted in `CLAUDE.md` §13.
- **Multi-version DraftSight support** — DS 2024 / 2025 / 2026
  compatibility.

## Long-term / strategic

- **More products** — Bag Awnings, Mesh Walls, Bed End Bag Flys, Storm
  Flaps, Caravan Covers, Ute Canopies. The architecture absorbs each
  one in roughly the same shape as caravan annexe.
- **Productisation through SeboticAI website** — see
  [BUSINESS_CONTEXT.md](BUSINESS_CONTEXT.md) for the rationale.
  Requires generalising the branding (currently Adelaide Annexes
  hardcoded), building a licence-key flow, and a marketing surface.
- **Generalised "canvas product" framework** — at some point the
  per-product code paths converge enough that a small DSL or template
  system might be cheaper than each new file tree. Not worth doing
  before 3-4 products exist.

---

## Parking lot

Things noticed during development that aren't critical but shouldn't be
lost. Each entry is one line; expand if/when picked up.

- Orphan-tab cleanup only checks the active workspace; if the user
  switches workspaces between sessions, stale tabs on the other
  workspace remain. Fix by iterating all workspaces.
- `LayerHelper` doesn't release COM RCWs explicitly. Fine at our
  scale; revisit if memory pressure shows in long sessions.
- AssemblyInfo isn't auto-bumped on build. Manual edits before tagging
  a release are required.
- Hardcoded SDK path in `CanvasCovers.csproj` HintPath. Multi-machine
  dev would need an environment variable or csproj property.
- Title block layout is row-of-text; not pretty. A grid-cell layout
  with proper field/value alignment would be more professional but
  needs more InsertNote calls and field-precise positioning.
- The layers panel colour dropdown offers only ACI 1-7 (the cutter's
  palette). The full 8-255 range isn't selectable. Fine in practice.
- WPF init-order: handlers fire during `InitializeComponent`. Resolved
  with the order-independent `_initialized` flag (window) and the
  `_drLeft == null` guard (WallBlanket) — see STATUS gotcha #8.
- **Deferred review nice-to-haves (low value, not worth touching stable
  code for):** the Fixings dropdown change triggers a double redraw of
  all 3 blankets; `LayerSettings.Cop/.Outline/...` accessors re-run a
  7-item LINQ scan on each access in the generator (resolve once if ever
  a hot path); `AddDashedV` allocates a `DoubleCollection` per frame.
  None affect correctness or perceptible performance.
- Blank network number still produces a timestamp DXF filename with no
  warning; the wall labels are then unlabelled. Could add a soft confirm
  at generate time if the client finds it confusing.
