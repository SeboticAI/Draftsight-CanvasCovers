# Architecture

Code tour for picking the project back up. Pair this with
[STATUS.md](STATUS.md) for current behaviour and [CLAUDE.md](../CLAUDE.md)
for DraftSight COM/add-in platform rules.

## Big Picture

One DraftSight COM add-in DLL exposes one ribbon button, **Canvas Covers**.
That button opens a product picker. Lift Blanket is implemented; Caravan Annexe
is a disabled future tile.

```text
App.ConnectToDraftSight
  -> OpenCanvasCoversCommand
    -> ProductPickerWindow
      -> LiftBlanketWindow
        -> LiftBlanketJob
          -> LiftBlanketCalculator
          -> LiftBlanketGenerator
          -> DxfExporter (optional)
```

The design split is intentional:

- WPF reads and validates operator input.
- `LiftBlanketCalculator` owns all testable geometry math and emits DTOs.
- `LiftBlanketGenerator` translates DTOs into DraftSight SDK calls.
- `DxfExporter` handles Save-As only after generation succeeds.

## Folder Layout

```text
CanvasCovers/
  App.cs
  CanvasCovers.csproj
  CanvasCovers.xml
  Commands/
    CommandBase.cs
    OpenCanvasCoversCommand.cs
    LayerTestCommand.cs
  Geometry/
    LayerHelper.cs
    Products/LiftBlanket/
      FixingAllowance.cs
      LiftBlanketCalculator.cs
      LiftBlanketGenerator.cs
      WallLayout.cs
  IO/
    DxfExporter.cs
    DxfExporter.Filename.cs
  Models/
    ProjectMetadata.cs
    LayerSetting.cs
    LayerSettings.cs
    ProductKind.cs
    Products/LiftBlanket/
      BlanketText.cs
      CopPlacement.cs
      LiftBlanketJob.cs
      LiftBlanketOptions.cs
      WallChecks.cs
      WallDimensions.cs
      WallSegments.cs
  UI/
    ProductPickerWindow.xaml(.cs)
    Controls/
      BrandedHeader.xaml(.cs)
      LayersPanel.xaml(.cs)
      ProjectMetadataPanel.xaml(.cs)
      WallBlanket.xaml(.cs)
    Products/LiftBlanket/
      LiftBlanketWindow.xaml(.cs)
      GenerateRequestedEventArgs.cs

CanvasCovers.Tests/
  Headless MSTest project. Links SDK-free production files; no DraftSight
  install required. Currently 42 tests.

Installer/
  CanvasCovers.iss
  build.ps1
  Output/ (ignored installer EXEs)

docs/help/
  Customer quick-start HTML and generated PDF.
```

## DraftSight Entry And Commands

`App.cs` is `[ComVisible(true)]` and derives from `DsAddin`. DraftSight calls
`ConnectToDraftSight`, where the add-in registers:

- `_CANVASCOVERSOPEN` / `CANVASCOVERSOPEN`
- `_CANVASCOVERSLAYERTEST` / `CANVASCOVERSLAYERTEST`
- a ribbon tab/panel/button

`OpenCanvasCoversCommand` owns the product picker, the lift-blanket dialog
lifetime, generation, and optional DXF export. It uses a static dialog guard so
the operator cannot stack multiple lift-blanket dialogs. Product picker,
lift-blanket dialog, and export Save-As are parented to DraftSight's main HWND.

Generation and export are separate try/catch blocks. If generation fails, the
dialog stays open. If export fails, the generated drawing remains valid and the
dialog closes.

## WPF UI

`ProductPickerWindow` is modal and returns `ProductKind`.

`LiftBlanketWindow` is non-modal. It hosts:

- `ProjectMetadataPanel`
- three `WallBlanket` tabs
- options fields
- `LayersPanel`

Generate flow:

1. Read project metadata.
2. Read and validate options.
3. Read and validate each enabled wall.
4. Read and validate layer role assignments.
5. Build `LiftBlanketJob`.
6. Raise `GenerateRequested`.
7. Close only if the subscriber does not set `Cancel`.

`WallBlanket` is a fixed schematic, not a live scaled preview. Typed values
never resize the drawing. The control keeps text boxes as persistent canvas
children and redraws only schematic shapes, preserving focus while typing.

Height seeding is one-way: Left wall height mirrors into Right/Rear until the
operator edits those heights manually.

## Models

`ProjectMetadata` fields:

- CompanyName
- CompanyInitials
- NetworkNumber
- OrderNumber
- ProjectName
- Date

`LiftBlanketOptions` fields:

- ThroughCar
- PlasticCoverOnCop
- Fixings
- FixingAllowanceMm
- QuiltInsetMm
- VerticalQuiltingSpacingMm
- QuiltingEnabled
- BagRequired
- GlassBehind

`LayerSettings` defines the six cutter layers and four drawing roles:

- OutlineLayer
- CopLayer
- AnnotationLayer
- TitleblockLayer

Default layers:

- `0`
- `1 Rotary Blade`
- `2 Drag Blade`
- `3 Crease Tool`
- `4 Drill Tool`
- `5 Draw and Text`

Default roles:

- Cut outline -> `1 Rotary Blade`
- COP and quilting -> `5 Draw and Text`
- Annotation -> `5 Draw and Text`
- Title/project/dimensions -> `0`

`WallDimensions.Width` uses `TotalWidthOverride` when it is greater than zero;
otherwise it uses the sum of `WallSegments`.

## Geometry Core

`LiftBlanketCalculator` is SDK-free and unit-tested. Keep all geometry rules
there when possible.

Current rules:

- `cutWidth = totalWidthOverride > 0 ? totalWidthOverride : sum(segments)`
- No automatic +10mm edge boost.
- `foldMidline = measuredHeight - fixingAllowance`
- `cutHeight = foldMidline * 2`
- COP width = `Seg2`
- COP left = `DoorReturnLeft + Seg1`
- COP vertical inputs = height and gap from bottom
- Auto top gap = `foldMidline - gapFromBottom - copHeight`
- Quilt lines use `QuiltInsetMm` as clearance, bottom half only
- Quilt line spacing is an even-divided target spacing with a 200-gap cap
- Blanket label text is bottom-centre, inverted 180 degrees, 25mm high
- COP reminders are vertical inside the cutout

`BlanketText.Build(project)` creates the shared wall-label/export-name base:

```text
<AAC order number> <company initials> <network number>
```

Empty sections are dropped.

## DraftSight SDK Translator

`LiftBlanketGenerator`:

- gets active document/model/sketch
- ensures all six configured layers exist
- ensures each role's resolved layer exists
- sets `DIMSCALE` to 30 best-effort
- starts one undo record
- enumerates walls in `L -> B -> R` order, omitting Rear when Through Car
- draws cut outline, COP, reminders, quilting, labels, dimensions, and
  project notes
- stops the undo record in `finally`

Layer assignment is activate-based via `LayerHelper`. Do not use
`EntityHelper.SetLayer/GetLayer` on fresh entities; they crash DraftSight.

`LayerHelper.Activate(name)` now throws if `Layer.Activate()` returns false.
That is intentional: wrong cutter layer is worse than a visible generate
failure.

`FailedInsertCount` increments when DraftSight returns null for polylines,
labels, dimensions, or title/project notes. The command warns the operator
after generation if any inserts failed.

Text angles in DTOs are degrees; the SDK boundary converts to radians before
calling `InsertSimpleNote`.

## DXF Export

`DxfExporter.Filename.cs` is headless and unit-tested. It uses
`BlanketText.Build(project)` plus `.dxf`, stripping filesystem-invalid
characters and falling back to a timestamp when blank.

`DxfExporter.cs` is SDK/WPF-specific. It opens an owner-parented
`SaveFileDialog`, then calls:

```csharp
document.SaveAs(path, dsDocumentSaveAs_R2018_ASCII_DXF, out errors)
```

Cancel is a no-op. Save errors are surfaced with a message.

## Installer

`Installer\build.ps1`:

- refuses to run while DraftSight is open
- builds `CanvasCovers.csproj` Release
- compiles `Installer\CanvasCovers.iss`
- writes `Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe`

The installer:

- installs to `C:\Program Files\BesiaCAD\CanvasCovers`
- copies interop DLLs and resources
- deploys `CanvasCovers.xml` to ProgramData addinConfigs
- rewrites `@@INSTALLDIR@@` in XML
- runs 64-bit RegAsm `/codebase`
- on upgrade, silently runs the previous uninstaller first and now stops if
  that uninstaller returns non-zero

## Adding A New Product

Add product-specific models, calculator/generator, and WPF window under a new
product folder. Reuse:

- `ProjectMetadata`
- `LayerSettings`
- `BrandedHeader`
- `ProjectMetadataPanel`
- `LayersPanel`
- `LayerHelper`
- the command/event pattern used by Lift Blanket

Do not fork shared controls unless the new product genuinely needs different
behaviour.

## Test Strategy

`CanvasCovers.Tests` links SDK-free production source files. Add tests for:

- geometry rules
- model defaults
- validation helpers that can be made headless
- filename/blanket text rules
- layer defaults

The WPF UI, DraftSight SDK calls, COM registration, and installer are verified
by build/load testing, not unit tests.

## Useful Verification Commands

```powershell
dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj
dotnet build CanvasCovers.sln -c Release
.\Installer\build.ps1
```

For DraftSight API signature checks, use reflection against:

```text
C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll
```

See [CLAUDE.md](../CLAUDE.md) section 9 before changing SDK call patterns.
