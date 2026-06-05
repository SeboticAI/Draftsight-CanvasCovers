# Architecture

Code tour for picking the project back up. Pairs with
[STATUS.md](STATUS.md) (what works now) and [ROADMAP.md](ROADMAP.md)
(what's next).

For the *platform* layer — how DraftSight loads add-ins, the COM
contract, the build/deploy cycle — see [`/CLAUDE.md`](../CLAUDE.md).
This file covers only *our* code on top of that.

---

## Big picture

One add-in DLL, multiple product flows. The add-in exposes a single
ribbon button "Canvas Covers" which opens a **Product Picker**. Picking
a product launches that product's WPF dialog. Filling and submitting
the dialog runs that product's geometry generator, which produces
DraftSight entities on named layers.

```
ribbon button ──► OpenCanvasCoversCommand
                       │
                       ▼
                 ProductPickerWindow
                       │
                       ├──► LiftBlanketWindow ──► LiftBlanketGenerator ──► entities
                       │
                       └──► CaravanAnnexeWindow ──► CaravanAnnexeGenerator   (future)
```

Everything below the picker is product-specific. Everything above the
dialog (branding, project metadata fields, layers config) is shared via
WPF UserControls.

---

## Folder layout

```
CanvasCovers/
├── App.cs                              COM entry point. Registers commands and ribbon. Performs orphan-tab cleanup.
├── CanvasCovers.csproj
├── CanvasCovers.xml                    DraftSight add-in config (CLSID, help text, button bitmap path)
├── Commands/
│   ├── CommandBase.cs                  Wraps CreateCommand2 + ExecuteNotify + CreateUserCommand.
│   ├── OpenCanvasCoversCommand.cs      Ribbon entry. Opens picker, shows lift-blanket dialog non-modally, runs generator on GenerateRequested. Generate + export are SEPARATE try/catch blocks (an export failure isn't reported as a generate failure); warns if the generator's FailedInsertCount > 0.
│   └── LayerTestCommand.cs             Diagnostic. Verifies activate-based layer pattern in isolation. Logs to %LocalAppData%\CanvasCovers\layertest.log.
├── Common/                             Empty placeholder for cross-cutting helpers.
├── Geometry/
│   ├── LayerHelper.cs                  Shared. EnsureLayer / Activate / RestoreOriginalActive. IDisposable.
│   └── Products/
│       └── LiftBlanket/
│           ├── LiftBlanketCalculator.cs Headless, SDK-free geometry math: cut width (+ edge allowance), cut height ((measured−allowance)×2), fold midline, segment-derived COP placement in the bottom half, auto top-gap, even-divided quilt lines, identifier label. Unit-tested. Emits WallLayout DTOs.
│           ├── WallLayout.cs           DTOs the calculator emits / the generator consumes: RectSpec, DimSpec, LabelSpec, LineSpec (quilt lines), WallLayout.
│           ├── FixingAllowance.cs       Static lookup: FixingType → default mm allowance (Hooks 50, Press Studs 40, Velcro 0). Headless, unit-tested.
│           └── LiftBlanketGenerator.cs Thin SDK translator: creates all 7 layers, walks WallLayout (L → Rear → R), emits cut rects (Outline) + COP + quilt lines (draw) + labels (Annotation) + DIMENSION entities + free-floating layer-0 text. DIMSCALE bumped via RunCommand. Counts null SDK inserts (FailedInsertCount). NO geometry math (all in the calculator).
├── IO/
│   ├── DxfExporter.Filename.cs         Headless half: DefaultFileName(ProjectMetadata) → "<networkNumber>.dxf" (timestamp fallback). Unit-tested.
│   └── DxfExporter.cs                  SDK half: native Save-As dialog → Document.SaveAs(path, R2018_ASCII_DXF, out errors).
├── Models/
│   ├── ProjectMetadata.cs              Shared. Company, project, network number, date, notes, etc.
│   ├── LayerSetting.cs                 Shared. Layer name + ACI colour index.
│   ├── LayerSettings.cs                Shared. The cutter's full SEVEN-layer set (0, the four tool-blade layers, 5 Draw and Text, Defpoints) + a role→layer assignment (OutlineLayer/CopLayer/AnnotationLayer/TitleblockLayer). The Outline/Cop/Annotation/Titleblock accessors are COMPUTED (resolve the assigned name to a LayerSetting). All seven created in the DXF.
│   ├── ProductKind.cs                  Enum (LiftBlanket, CaravanAnnexe). Used by picker → command dispatch.
│   └── Products/
│       └── LiftBlanket/
│           ├── LiftBlanketJob.cs       Composite: Project + Layers + 3 walls + Options.
│           ├── LiftBlanketOptions.cs   Through Car / Plastic Cover / Fixings + FixingAllowanceMm (editable) + EdgeAllowanceMm (editable, default 10) + VerticalQuiltingSpacingMm + QuiltingEnabled (on by default).
│           ├── WallSegments.cs         Five bottom-row measurement boxes (DrLeft/Seg1/Seg2/Seg3/DrRight); TotalWidth = their sum. Also drive COP horizontal geometry (Seg2 = COP width, DrLeft+Seg1 = COP left offset).
│           ├── CopPlacement.cs         Per-wall COP VERTICAL inputs only: Enabled + Height + GapFromBottom. The horizontal geometry (width = Seg2, left offset = edgeAllowance/2 + DrLeft + Seg1) is derived by the calculator from the segments.
│           └── WallDimensions.cs       Enabled + Segments + MeasuredHeight + Cop; Width = Segments.TotalWidth.
├── Properties/
│   └── AssemblyInfo.cs                 Version, description, company. ComVisible, GUID for the assembly.
├── Resources/
│   ├── canvascovers_16.png             Placeholder ribbon button icon (small).
│   └── canvascovers_32.png             Placeholder ribbon button icon (large).
└── UI/
    ├── ProductPickerWindow.xaml/.cs    Top-level picker (modal). Tile per available product. Returns ProductKind.
    ├── Controls/                       Shared WPF UserControls reusable across product dialogs.
    │   ├── BrandedHeader.xaml/.cs      Navy banner with company name + contact info. Subtitle is settable.
    │   ├── ProjectMetadataPanel.xaml/.cs   8-field grid + multi-line NOTES. Read() returns ProjectMetadata; Apply(model) populates.
    │   ├── LayersPanel.xaml/.cs        SEVEN-layer table: each cutter layer row has a name, an ACI colour-swatch dropdown, and four role CHECKBOXES (Cut/COP/Annot./Title). A role lives on exactly one layer. Read(errors) returns LayerSettings (errors if a role is unassigned); Reset to Defaults.
    │   └── WallBlanket.xaml/.cs        Per-wall input surface drawn as a FIXED schematic copy of the paper sheet (NOT to scale — positions are canvas-derived, never value-driven). Hosts the embedded measurement fields + real dimension-symbol lines. Left/Right walls MIRROR each other (Right = COP + fields on the opposite side). Configure(isRear, mirrored) selects the mode. Live-redraws on edit without dropping focus (fields are persistent canvas children; only drawn shapes are rebuilt). (Replaced the old true-proportion preview that collapsed on a keystroke, and the even-older passive WallDiagram.)
    └── Products/
        └── LiftBlanket/
            ├── LiftBlanketWindow.xaml/.cs       Non-modal dialog. Hosts UserControls + a TabControl of three WallBlanket tabs (Left/Right/Rear) + options. Fires GenerateRequested.
            └── GenerateRequestedEventArgs.cs   Cancel-able EventArgs. Generator sets Cancel=true on failure so the dialog stays open for retry.

CanvasCovers.Tests/                     Headless MSTest project (net48). Links the SDK-free source (calculator, fixing allowance, models, DXF filename half) via <Compile ... Link=...> and references NO interop, so it runs on any machine without DraftSight. 29 tests (calculator geometry incl. segment-driven COP, auto top-gap, quilting + line-count cap; fixing allowance; wall model; DXF filename).
CanvasCovers.sln                        Ties the add-in + test project together.
docs/                                   Documentation (this folder). See README.md for index.
scripts/                                rollback-canvascovers.ps1 — startup-crash recovery only. Install path is the Inno installer.
Installer/                              CanvasCovers.iss + build.ps1 + Output/. See Installer/README.md.
reference/                              (gitignored) Client-supplied DXFs + measurement sheets + DXF_FINDINGS.md (the reverse-engineering analysis) + parse-dxf.ps1 (the inspector).
```

---

## Key types and their responsibilities

### `App.cs`

The COM-visible entry point — `[ComVisible(true)] [Guid(...)] class App : DsAddin`.
DraftSight instantiates this when the add-in is activated, calls
`ConnectToDraftSight`, hands us the application object, and we register
our commands + ribbon. On unload, `DisconnectFromDraftSight` is called
and we tear down our UI registration.

The orphan-cleanup logic in `RemoveOrphanRibbonItems` iterates the
active workspace's tabs and removes any with our add-in's GUID before
adding a fresh one. This recovers from crashes where the previous
session didn't call `DisconnectFromDraftSight` cleanly.

### `CommandBase`

Each command (ribbon button, command-line entry, diagnostic) extends
this. It abstracts the SDK's three-step registration:

1. `RegisterCommand()` — `CreateCommand2` + wire `ExecuteNotify` to
   the derived class's `Execute()` method.
2. `CreateUserCommand()` — package the command so it can be referenced
   by ribbon / menu / toolbar items. Returns an ID we use to bind the
   ribbon button.

`OpenCanvasCoversCommand` is the only one currently bound to the ribbon.
`LayerTestCommand` is registered but not surfaced in the UI — typed at
the DraftSight command prompt.

### `LayerHelper`

Wraps the SDK's `LayerManager`. Three operations:

- `EnsureLayer(name, colorIndex?)` — create the layer if missing,
  apply colour (best-effort). Returns the Layer COM RCW.
- `Activate(name)` — make the named layer active so the next entity
  insert lands on it. Tracks the current active to no-op on repeat
  calls.
- `RestoreOriginalActive()` — switches active back to whatever it
  was when the LayerHelper was constructed. Also called from
  `Dispose()` so a `using` block guarantees restoration.

**Why activate-based and not `EntityHelper.SetLayer`:** see
[`/CLAUDE.md`](../CLAUDE.md) §9. `SetLayer` and `GetLayer` crash the
host when called on freshly-inserted entities. The activate-based path
is the SDK's `BlockCustomData` (C++) sample pattern and is the one we
verified works.

### `LiftBlanketCalculator` (the math core)

Headless and SDK-free, so it is **unit-tested** (see
`CanvasCovers.Tests`). Takes the per-job fixing allowance **and edge
allowance** in its ctor;
`LayoutWall(wall, originX, projectTag, suffix, quiltingEnabled, verticalQuiltingSpacingMm)`
returns a `WallLayout` DTO carrying the cut `RectSpec`, the fold midline
Y, an optional COP `RectSpec`, the quilt `LineSpec` list, the identifier
`LabelSpec`, and the width `DimSpec`. It encodes the rules recovered from
the client's reference DXFs (`reference/DXF_FINDINGS.md`):

1. `cutHeight = (measuredHeight − fixingAllowance) × 2` (the blanket
   folds in half; the measured panel is doubled).
2. `cutWidth = Σ segments + edgeAllowance`. The edge allowance is an
   operator field (default 10mm) split evenly — half on each horizontal
   side of the cut rect — replacing the old hardcoded +10mm rule.
3. COP horizontal geometry is **derived from the bottom-row segments**:
   width = the middle segment (Seg2), left edge = `edgeAllowance/2 +
   DoorReturnLeft + Seg1` (measured from the door-return line). Only the
   COP *vertical* numbers (height, gap-from-bottom) are operator input.
   The COP sits in the **bottom (measured) half**; the top half is a
   mirror, so no COP geometry is needed above the fold midline.
   `AutoTopGap(...)` computes the gap from the COP top up to the fold
   line (`(measuredHeight − fixingAllowance) − gapFromBottom − copHeight`);
   a negative value means the COP crosses the fold, which the dialog
   treats as a blocking validation error before generating.
4. Quilting: `EvenlySpaced(...)` divides a span into equal gaps as close
   as possible to the operator's target spacing (rounding the line count
   so there is no remainder gap). `AddQuiltLines` fills only the bottom
   half (up to the fold midline) with vertical + horizontal lines,
   bounded left/right by the door-return boxes and inset by half the edge
   allowance on every edge.

Keeping the math here (not in the generator) means the rules are
testable without DraftSight, and the generator stays a thin translator.

### `LiftBlanketGenerator` (the SDK translator)

Takes a `LiftBlanketJob`. Reads layer config from `job.Layers`,
**creates all seven cutter layers** (so the DXF always carries the full
set), bumps `DIMSCALE` via `Application.RunCommand` so dim text reads at
lift-blanket scale, then walks each wall via the calculator (**L → Rear
→ R**, so the back wall sits in the middle; Rear omitted when Through
Car) and emits the resulting `WallLayout` as DraftSight entities.
Wrapped in `SketchManager.StartUndoRecord` / `StopUndoRecord` so one
Ctrl+Z reverts the whole generation. **No geometry math lives here** —
it only translates DTOs into SDK calls. SDK inserts route through an
`InsertPolyline` helper that increments `FailedInsertCount` when the SDK
returns null, so the command can warn the operator that some geometry
silently failed to draw.

Layer assignment (matches the reference DXFs):

- Cut rectangle → **Outline** layer (`1 Rotary Blade`, cut).
- COP rectangle → **Cop** layer (`5 Draw and Text`, draw/score — NOT
  cut).
- Quilt lines (vertical + horizontal, bottom half only) → **Cop** layer
  (`5 Draw and Text`, draw/score — NOT cut).
- Wall identifier label (`<net> <name> L/R/B`, height 20) →
  **Annotation** layer (`5 Draw and Text`).
- Dimensions + free-floating worksheet text → **Titleblock** layer
  (`0`, white).

The text layout is **free-floating, not a boxed title block** —
mirrors the reference DXFs: a horizontal worksheet legend above the
walls; right of the walls a project-metadata column + a FIXINGS table +
the WIDTH/HEIGHT formula reminder + a FIXING ALLOWANCE line. Width dim
below each wall; height dim only on the **leftmost** wall's outer side
(other walls share the same height, so duplicate dims would stack in
the gap). The calculator emits the width dim's extension points on the
wall bottom edge (`LineY = 0`); the generator applies its own `DimGap`
offset — keeping layout decisions out of the math core.

Dimensions use `InsertAlignedDimension` (not `InsertLinearDimension`
— that's horizontal-only; see CLAUDE.md §9). Text uses
`InsertSimpleNote` (not `InsertNoteWithParameters` — that silently
produces no visible output).

### `DxfExporter`

Split into a headless filename half (`DxfExporter.Filename.cs`,
unit-tested — `DefaultFileName` derives `<networkNumber>.dxf`) and an
SDK half (`DxfExporter.cs` — pops a WPF `SaveFileDialog`, then
`Document.SaveAs(path, dsDocumentSaveAs_R2018_ASCII_DXF, out errors)`).
Invoked from `OpenCanvasCoversCommand` after a successful Generate when
the dialog's "export" checkbox is ticked. Best-effort: cancel is a
no-op, a save error surfaces a message but does not throw.

Each entity insert is preceded by `layers.Activate(<layer>.Name)` so
the entity lands on the correct layer.

### WPF UserControls

Four of them, all in `UI/Controls/`. Reusable across product dialogs:

- `BrandedHeader` — navy banner. Hardcoded Adelaide Annexes branding.
  Single public string property `Subtitle` set by the host window.
- `ProjectMetadataPanel` — 8-field grid + DatePicker + multi-line
  NOTES textbox. Exposes `Read()` returning `ProjectMetadata` and
  `Apply(metadata)` for populating from saved state.
- `LayersPanel` — the cutter's **seven-layer** table built in code-
  behind. Each row: a layer-name label, an ACI colour-swatch dropdown,
  and four role checkboxes (Cut / COP / Annot. / Title). A role belongs
  to exactly one layer (ticking it on one row unticks it on any other,
  guarded by `_suppressRoleSync`). The two-roles-on-one-layer case
  (COP + Annotation both default to `5 Draw and Text`) round-trips
  because `RoleChecks` is keyed by role. Exposes `Read(List<string>
  errors)` returning `LayerSettings` (error per unassigned role) and
  `Apply(settings)`. "Reset to Defaults" button.
- `WallBlanket` — per-wall input surface drawn as a **FIXED schematic**
  on a `Canvas`. **It is deliberately NOT to scale** — the wall
  rectangle and every internal feature sit at fixed canvas-derived
  positions; typed values never move or rescale anything (an earlier
  true-proportion version collapsed to a sliver when a digit was typed
  mid-edit). The schematic illustrates the layout and hosts the embedded
  fields (five segments, measured height, and — when COP is on — COP
  height + gap-from-bottom) with real dimension-symbol lines. **Left and
  Right walls mirror each other** (Right = COP and its fields on the
  opposite side), since the walls face each other on the sheet;
  `Configure(isRear, mirrored)` selects the mode. The embedded `TextBox`es
  and their labels are **persistent children** added once and only
  repositioned each redraw — only the drawn shapes are torn down and
  rebuilt — so editing never drops focus; fields get a high `ZIndex` so a
  drawn shape can't cover them and swallow clicks. The auto top-gap turns
  red ("crosses fold") live. `SetSharedParams(fixing, edge, quiltSpacing,
  quiltOn)` pushes the options-panel values in (the control only uses the
  fixing allowance, for the top-gap readout). Every typed value is parsed
  through a NaN/Infinity-rejecting `ParseOr`, and the whole redraw is in a
  swallow-all `try/catch` (no dispatcher exception handler exists in-host,
  so an unhandled throw would crash DraftSight).

### `ProductPickerWindow`

Same `BrandedHeader` as the product dialogs. Two `Button` tiles styled
with a custom `ControlTemplate` so they look like card tiles with hover
highlight. Disabled tiles have `ToolTipService.ShowOnDisabled=True`
so the operator gets an explanation of *why* it's disabled.

Returns the chosen `ProductKind` to the caller (`OpenCanvasCoversCommand`).

### `LiftBlanketWindow`

The actual product dialog. **Non-modal** — opened via `Show()` with
the DraftSight main HWND as `WindowInteropHelper.Owner`, so the
operator can pan/zoom DraftSight while the form is open.

Hosts the shared UserControls (header, metadata panel, layers panel)
plus a `TabControl` of three `WallBlanket` tabs (Left / Right / Rear,
Right mirrored) and an options panel. The Rear tab is disabled under
Through Car. The options panel's edge-allowance, quilting-spacing and
quilting-on values are pushed into every blanket via `SetSharedParams`.

On Generate, runs **multi-error validation** (all errors shown at once),
the operator-feedback layer added in v1.4.5:
- Every field through `ReadPositive`/`ReadNonNegative`, which now reject
  NaN/Infinity as well as negative/non-numeric.
- A wall needs a non-zero width; **measured height must exceed the fixing
  allowance** (else the cut inverts); the **COP must fit horizontally**
  (`half + DR-L + S1 + S2 ≤ cutWidth − half`) and **not cross the fold**
  (`AutoTopGap < 0`); **Seg2 > 0** when COP is on.
- **Quilting spacing ≥ 50 mm** (a tiny value would emit thousands of
  lines); **at least one wall enabled**; every layer role assigned.

The `_initialized` flag guards the Options `TextChanged` handlers, which
WPF fires *during* `InitializeComponent` before later-declared elements
exist (see CLAUDE.md §9 / STATUS gotcha #8).

Hand-off to the generator uses a `Cancel`-able event pattern (see
[`GenerateRequestedEventArgs.cs`](../CanvasCovers/UI/Products/LiftBlanket/GenerateRequestedEventArgs.cs)):

```csharp
public event EventHandler<GenerateRequestedEventArgs> GenerateRequested;

// In GenerateButton_Click:
var args = new GenerateRequestedEventArgs(Job);
GenerateRequested?.Invoke(this, args);
if (!args.Cancel) Close();
```

The consumer (`OpenCanvasCoversCommand`) runs the generator inside
its handler; if it throws, the handler shows a `MessageBox` and sets
`args.Cancel = true` so the dialog stays open and the operator can
fix inputs and retry.

Esc closes the dialog via a `KeyBinding` to
`ApplicationCommands.Close` — **not** via `IsCancel="True"` on the
Cancel button, because setting `DialogResult` on a non-modal window
throws.

The Through Car checkbox is wired to `ThroughCarOption_Changed`:
ticking it disables the **Rear tab** (and marks the rear wall not
included). Unticking only re-enables the tab; it doesn't force the
rear wall back on, preserving user intent.

---

## Adding a new product

The architecture's main payoff. Steps to add (say) a caravan annexe:

1. **Model** — `Models/Products/CaravanAnnexe/CaravanAnnexeJob.cs`
   plus whatever supporting types (panels, options) belong to it.
   `ProjectMetadata` and `LayerSettings` are reused; do not duplicate.
2. **Geometry** — `Geometry/Products/CaravanAnnexe/CaravanAnnexeGenerator.cs`.
   Take a `using (LayerHelper layers = new LayerHelper(document))`,
   `EnsureLayer` your layers, wrap inserts in
   `sketch.StartUndoRecord()` / `StopUndoRecord()`. Use
   `LayerHelper.Activate(...)` before each insert.
3. **UI** — `UI/Products/CaravanAnnexe/CaravanAnnexeWindow.xaml` (+ `.cs`).
   Include the three UserControls (`BrandedHeader`,
   `ProjectMetadataPanel`, `LayersPanel`). Set the subtitle on the
   header. Add product-specific input sections. Expose a public
   `CaravanAnnexeJob Job` property set when Generate succeeds.
4. **Picker tile** — In `ProductPickerWindow.xaml`, switch the Caravan
   tile from `IsEnabled="False"` to enabled, set its Click handler,
   and in `ProductPickerWindow.xaml.cs` set `SelectedProduct =
   ProductKind.CaravanAnnexe` and close with `DialogResult = true`.
5. **Dispatch** — In `OpenCanvasCoversCommand.Execute`, add a case
   to the switch:

   ```csharp
   case ProductKind.CaravanAnnexe:
       RunCaravanAnnexe();
       break;
   ```

   Add the `RunCaravanAnnexe()` helper mirroring `RunLiftBlanket()`.

No existing files are restructured. No shared code is forked. This is
the test of whether the multi-product architecture actually delivers.

---

## Useful PowerShell snippets

The interop DLL's actual method signatures aren't well documented.
When you need to confirm a signature, use reflection:

```powershell
$asm = [Reflection.Assembly]::LoadFrom(
    "C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll")
$asm.GetTypes() |
    Where-Object { $_.Name -eq "ILayerManager" } |
    ForEach-Object {
        $_.GetMethods() | ForEach-Object {
            $params = ($_.GetParameters() | ForEach-Object {
                $mod = ""; if ($_.IsOut) { $mod = "out " }
                "$mod$($_.ParameterType.Name) $($_.Name)"
            }) -join ', '
            "$($_.ReturnType.Name) $($_.Name)($params)"
        }
    }
```

We've already used this to verify `LayerManager.CreateLayer`,
`EntityHelper.SetLayer`/`GetLayer`, `Layer.Activate`, `Layer.Color`,
`Application.GetActiveWorkspace`, `SketchManager.StartUndoRecord`. The
results are baked into the code; this snippet is for future API queries.
