# Load Test

Runtime walkthrough for verifying a fresh CanvasCovers installer in DraftSight.
Use this before sending a build to the customer.

For first-time SDK/environment setup, see [HELLO_DRAFTSIGHT.md](HELLO_DRAFTSIGHT.md).
For current behaviour, see [STATUS.md](STATUS.md).

## Safety First

`CanvasCovers.xml` ships with `startup="0"`, so DraftSight does not auto-load
the add-in at launch. Manual activation is required each session.

Keep this rollback command ready before any load test:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

It unregisters the COM DLL and disables the XML if a build breaks startup.

## Build Installer

Close DraftSight, then run from the repo root:

```powershell
.\Installer\build.ps1
```

Expected output:

```text
Installer\Output\BesiaCAD-CanvasCovers-Setup-1.5.0.exe
```

The script builds Release and compiles the Inno Setup installer. The installer
will close/upgrade/uninstall previous versions as needed.

## Install

1. Double-click `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.5.0.exe`.
2. Approve UAC.
3. Confirm installation completes.

Installer responsibilities:

- payload to `C:\Program Files\BesiaCAD\CanvasCovers`
- XML to `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs`
- `@@INSTALLDIR@@` bitmap path rewrite
- 64-bit RegAsm `/codebase`
- previous-version uninstall before upgrade; non-zero uninstall exit blocks the
  new install

## Activate In DraftSight

1. Open DraftSight.
2. Open Add-Ins: `Tools -> Add-Ins` or command `ADDINS`.
3. Tick **CanvasCovers**. Leave Start Up unticked.
4. Open a new drawing if no drawing is active.

Expected: a **CanvasCovers** ribbon tab appears with a **Canvas Covers** button.

If the tab does not appear, check for a MessageBox from `ConnectToDraftSight`.

## Product Picker

Click **CanvasCovers -> Canvas Covers**.

Expected:

- Product picker appears in front of DraftSight.
- Lift Blanket tile is enabled.
- Caravan Annexe tile is disabled and has a tooltip.
- Cancel closes with no side effects.

## Lift Blanket Dialog

Click Lift Blanket.

Expected:

- Non-modal dialog appears in front of DraftSight.
- You can pan/zoom/select in DraftSight while it remains open.
- Project Information fields are: Company Name, Company Initials, AAC Order
  Number, Network Number, Project Name, Date.
- Left/Right/Rear tabs appear.
- Left/Right tabs show the fixed schematic; Right is mirrored.
- Rear tab has width/height only.
- Options include Through Car, Plastic Cover, Storage Bag, Glass Behind,
  Fixings, Fixing Allowance, Quilt Inset, Quilting Spacing, Draw Quilting
  Lines, Open DXF Export.
- Layers panel shows six rows: `0`, `1 Rotary Blade`, `2 Drag Blade`,
  `3 Crease Tool`, `4 Drill Tool`, `5 Draw and Text`.

## Validation Checks

Try these before a real generation:

- Put `abc` in a wall numeric field. Generate should show a red validation
  error.
- Put `abc` in Fixing Allowance, Quilt Inset, or Quilting Spacing. Generate
  should show a red validation error; it should not silently use defaults.
- Put `-50` in Fixing Allowance. Generate should reject it. The field expects
  positive magnitude, not a signed subtraction.
- Put `-5` in Quilt Inset. Generate should reject it.
- Set Quilting Spacing below 50 while quilting is enabled. Generate should
  reject it.
- Set measured height <= fixing allowance. Generate should reject it.
- With COP enabled, set COP height + bottom gap so the COP crosses the fold.
  The top-gap readout should turn red, and Generate should reject it.
- Untick every wall. Generate should reject it.
- Untick a layer role's only checkbox. Generate should reject it.

## Schematic Checks

- Type multi-digit numbers into the embedded fields; focus should not drop per
  keystroke.
- Clear a field or type a temporary `-`; the preview should not crash.
- Left height should seed into Right/Rear until those heights are manually
  edited.
- Left/right width mismatch should show a warning only; it must not block.
- Through Car disables the Rear tab and excludes Rear from generation. Unticking
  Through Car re-enables the tab but does not force Rear included.

## Generate A Reference Job

Use Left Wall:

- DR-L `250`
- S1 `350`
- S2 `240`
- S3 `1400`
- DR-R `0`
- Measured Height `2200`
- Fixings `Hooks Facing Out` (allowance 50)
- Quilt Inset `5`
- COP enabled
- COP Height `1300`
- From bottom `600`

Expected live top gap:

```text
(2200 - 50) - 600 - 1300 = 250
```

Generate, then Zoom Extents.

Expected drawing:

- Walls in `L -> B -> R` order unless Through Car is on.
- Cut outline is a plain rectangle on `1 Rotary Blade`.
- Reference left cut width is `2240`, no automatic +10mm.
- Reference left cut height is `4300`.
- COP is on `5 Draw and Text`, X `600..840`, Y `600..1900`.
- Quilt lines are on `5 Draw and Text`, bottom half only, inset 5mm from the
  cut edge where applicable.
- Wall label uses blanket text plus suffix, e.g. `<order> <initials> <network> L`,
  inverted near the top edge.
- COP reminder text is vertical inside the COP cutout.
- Width dimensions appear below walls; height dimension appears on the leftmost
  wall only.
- Project notes/info are free-floating text, not a boxed title block.

## DXF Export

With **Open DXF export dialog after generating** ticked:

1. Save-As dialog appears in front of DraftSight.
2. Filename is based on blanket text:
   `<AAC order number> <company initials> <network number>.dxf`
3. Save as R2018 ASCII DXF.
4. Reopen the DXF or inspect it with `reference\parse-dxf.ps1`.

Canceling export should leave the generated drawing intact.

## Undo And Regenerate

- Press Ctrl+Z once. The full generated set should disappear in one undo step.
- Reopen the picker, change values, and generate again.
- Layer Manager should show the six cutter layers without duplicates.

## Failure Path

Close all drawings, then run the Lift Blanket flow and click Generate.

Expected:

- MessageBox says no active drawing is open.
- Dialog stays open.
- Entered values remain.
- Open a drawing and Generate again without retyping.

## Diagnostic Layer Test

If layer assignment is suspect, run this DraftSight command:

```text
CANVASCOVERSLAYERTEST
```

It writes `%LocalAppData%\CanvasCovers\layertest.log`.

## Recover

If DraftSight still launches, uninstall via Windows Settings -> Apps ->
**BesiaCAD Canvas Covers**.

If DraftSight will not launch:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

Then open DraftSight, confirm it is healthy, and use Settings -> Apps to clean
up the remaining install.
