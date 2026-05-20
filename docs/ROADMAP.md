# Roadmap

Ordered list of what's queued, with rationale. Newly-completed items move
to [STATUS.md](STATUS.md); newly-identified items go in **Parking lot**
at the bottom of this file.

This is the file to consult when picking the project back up after a
break: "what was next?"

---

## Up next (in order)

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

### 2. DXF auto-export

**Why now:** biggest remaining UX gap. Operator currently has to know
DraftSight's File → Save As → DXF flow. We already know enough to
generate a sensible default filename and folder.

- After successful generation, optionally save the active document as
  DXF to a configured directory (e.g. `Documents/CanvasCovers/Output/`)
- Filename derived from project metadata:
  `{Company}-{NetworkNumber}-{ProductType}-{yyyy-MM-dd}.dxf`
  (fallback to `CanvasCovers-{timestamp}.dxf` if fields are blank)
- Add a "Save DXF" checkbox to the dialog, default on
- Possibly an "Output folder" field in a new settings panel

API call shape: `document.SaveAs(path, ...)` — verify signature via
PowerShell reflection of the interop DLL (we know the pattern works).

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

### 5. Inno Setup installer

**Why now:** for commercial release, "one EXE, click to install" is the
expected baseline. The PowerShell deploy script is fine for dev but not
acceptable to ship to a paying client.

- Inno Setup 6 script under `Installer/` (folder already exists in repo)
- `PrivilegesRequired=admin`, runs RegAsm in `[Run]`, drops XML in
  ProgramData
- Pin `AppId` GUID once and never change (per CLAUDE.md §1)
- Output: `BesiaCAD-CanvasCovers-Setup-{version}.exe`
- Verify rollback / uninstall removes everything cleanly

### 6. Code signing

**Why now:** "Unknown Publisher" warning on add-in load looks
unprofessional and degrades trust in a commercial release.

- Code-signing certificate purchase (annual)
- `signtool.exe` integration in the installer build script
- Sign both the DLL and the installer EXE
- Verify the warning is gone in a fresh install

---

## Mid-priority (after the above are done)

- **Field tooltips** — for the operator who doesn't already know the
  measurement form. Hover-help on each input.
- **Visual preview in the dialog** — small WPF canvas showing what
  will be drawn before Generate is clicked. Catches gross errors early.
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
- The full ACI palette (8-255) isn't previewed accurately in the
  layers panel swatches — unknown indices render as light grey. Not
  a problem in practice (most operators use 1-7).
- Through Car checkbox event fires during XAML init with
  `RearWallEnabled == null`; we guard against it but the guard is
  fragile. A `Loaded` event handler hookup would be more correct.
