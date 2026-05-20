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
│   ├── OpenCanvasCoversCommand.cs      Ribbon entry. Opens picker, routes to product.
│   ├── LayerTestCommand.cs             Diagnostic. Verifies activate-based layer pattern in isolation. Logs to %LocalAppData%\CanvasCovers\layertest.log.
│   └── Products/                       Future per-product CLI-only commands. Empty now.
├── Common/                             Empty placeholder for cross-cutting helpers.
├── Geometry/
│   ├── LayerHelper.cs                  Shared. EnsureLayer / Activate / RestoreOriginalActive. IDisposable.
│   └── Products/
│       └── LiftBlanket/
│           └── LiftBlanketGenerator.cs Lift-blanket-specific geometry. Three walls + COP cutouts + title block.
├── Models/
│   ├── ProjectMetadata.cs              Shared. Company, project, network number, date, etc.
│   ├── LayerSetting.cs                 Shared. Layer name + ACI colour index.
│   ├── LayerSettings.cs                Shared. Four LayerSettings (Outline, COP, Annotation, Titleblock).
│   ├── ProductKind.cs                  Enum (LiftBlanket, CaravanAnnexe). Used by picker → command dispatch.
│   └── Products/
│       └── LiftBlanket/
│           ├── LiftBlanketJob.cs       Composite: Project + Layers + 3 walls + Options.
│           ├── LiftBlanketOptions.cs   Through Car / Plastic Cover / Fixings (lift-blanket-specific).
│           └── WallDimensions.cs       Main width/height, door returns, COP fields.
├── Properties/
│   └── AssemblyInfo.cs                 Version, description, company. ComVisible, GUID for the assembly.
├── Resources/
│   ├── canvascovers_16.png             Placeholder ribbon button icon (small).
│   └── canvascovers_32.png             Placeholder ribbon button icon (large).
└── UI/
    ├── ProductPickerWindow.xaml/.cs    Top-level picker. Tile per available product. Returns ProductKind.
    ├── Controls/                       Shared WPF UserControls reusable across product dialogs.
    │   ├── BrandedHeader.xaml/.cs      Navy banner with company name + contact info. Subtitle is settable.
    │   ├── ProjectMetadataPanel.xaml/.cs   8-field grid; Read() returns ProjectMetadata; Apply(model) populates.
    │   └── LayersPanel.xaml/.cs        4-row table of layer name/ACI/swatch; Read(errors) returns LayerSettings; Reset to Defaults.
    └── Products/
        └── LiftBlanket/
            └── LiftBlanketWindow.xaml/.cs  Lift blanket dialog. Hosts the three UserControls plus wall sections and options.

docs/                                   This folder. See README.md for index.
scripts/                                deploy-canvascovers.ps1 / rollback-canvascovers.ps1
Installer/                              Empty for now. Inno Setup will live here.
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

### `LiftBlanketGenerator`

Takes a `LiftBlanketJob`. Reads layer config from `job.Layers`,
ensures the four layers exist, then draws walls left-to-right at the
world origin with the title block below. Wrapped in
`SketchManager.StartUndoRecord` / `StopUndoRecord` so one Ctrl+Z
reverts the whole generation.

Each entity insert is preceded by `layers.Activate(<layer>.Name)` so
the entity lands on the correct layer.

### WPF UserControls

Three of them, all in `UI/Controls/`. Designed to be reused by every
product dialog:

- `BrandedHeader` — navy banner. Hardcoded Adelaide Annexes branding.
  Single public string property `Subtitle` set by the host window.
- `ProjectMetadataPanel` — 8-field grid + DatePicker. Exposes
  `Read()` returning `ProjectMetadata` and `Apply(metadata)` for
  populating from saved state (used by future Save/Load).
- `LayersPanel` — 4-row editable table with live colour swatches and
  a "Reset to Defaults" button. Exposes `Read(List<string> errors)`
  returning `LayerSettings` (appends errors instead of throwing) and
  `Apply(settings)`.

### `ProductPickerWindow`

Same `BrandedHeader` as the product dialogs. Two `Button` tiles styled
with a custom `ControlTemplate` so they look like card tiles with hover
highlight. Disabled tiles have `ToolTipService.ShowOnDisabled=True`
so the operator gets an explanation of *why* it's disabled.

Returns the chosen `ProductKind` to the caller (`OpenCanvasCoversCommand`).

### `LiftBlanketWindow`

The actual product dialog. Hosts the three UserControls plus
lift-blanket-specific sections (three walls + options). On Generate,
runs multi-error validation: every field is read, errors accumulated
in a `List<string>`, all displayed at once if any are present.

The Through Car checkbox is wired to `ThroughCarOption_Changed`: ticking
it disables the Rear Wall enable checkbox (since Through Car means no
rear wall). Unticking only re-enables; doesn't force-check, preserving
user intent.

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
