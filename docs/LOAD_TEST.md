# Load Test

Runtime walkthrough — deploy the add-in, activate it in DraftSight, and
exercise the full path through the product picker into the lift blanket
flow. Use this whenever a fresh build needs verifying.

For first-time SDK / environment setup, see
[HELLO_DRAFTSIGHT.md](HELLO_DRAFTSIGHT.md).
For the current behaviour the add-in offers, see
[STATUS.md](STATUS.md).

---

## Safety first

`CanvasCovers.xml` ships with `startup="0"` so the add-in is never
auto-loaded at DraftSight launch. Manual activation is required every
session. This is intentional — a broken build cannot kill DraftSight at
startup.

Keep this rollback command pinned to a terminal before any load test:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

If anything goes sideways, run that first, then debug. It unregisters
the COM DLL and disables the XML.

---

## Deploy

Close DraftSight, then in a normal PowerShell from the repo root:

```powershell
.\Installer\build.ps1
```

Approx 10 seconds. Builds Release and compiles an Inno Setup installer
into `Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe`.

Double-click that EXE (UAC prompts for admin). It:

- Installs payload to `C:\Program Files\BesiaCAD\CanvasCovers\`.
- Rewrites the `@@INSTALLDIR@@` placeholder in the deployed XML to the
  real install path.
- Drops the XML into
  `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\`.
- Runs 64-bit RegAsm `/codebase`.
- If a previous version is installed, silently uninstalls it first
  (`PrepareToInstall`).

---

## Activate

1. Open DraftSight.
2. Open the Add-Ins manager: `Tools → Add-Ins` (or the `ADDINS` command
   at the prompt).
3. Tick **CanvasCovers**. Do not enable Start Up.

DraftSight loads our DLL and calls `App.ConnectToDraftSight`. The
ribbon should now have a **CanvasCovers** tab containing a **Tools**
panel with a **Canvas Covers** button.

If the tab doesn't appear: open a drawing first (the tab may be tied
to `dsUIState_Document`), or check whether `ConnectToDraftSight` showed
a MessageBox error — those surface load failures explicitly.

---

## Click-through path

### 1. Open the picker

Click **CanvasCovers** tab → **Canvas Covers** button.

The Product Picker window opens. Branded navy header at top, two tiles
underneath:

- **Lift Blanket** — clickable, AVAILABLE badge
- **Caravan Annexe** — disabled, COMING SOON badge. Hovering shows
  a tooltip explaining the gating.

Cancel returns you to DraftSight with no side effects.

### 2. Pick Lift Blanket

Click the Lift Blanket tile. Picker closes, the Lift Blanket dialog
opens. Same navy header (with "Lift Blanket Generator" subtitle), then
sections top-to-bottom:

- **Project Information** — Company, Network Number, Project,
  O/Number, Sales Contact, Mobile, Measured By, Date (today by
  default), plus a multi-line **Notes** field for delivery
  instructions / special requests.
- **Left Wall** — Main Width / Height, three Door Returns (zero if
  not needed), optional COP cutout.
- **Right Wall** — same shape, mirrored door return orientation.
- **Rear Wall** — Width / Height. Disabled if Through Car is ticked.
- **Options** — Through Car (omits rear wall), Plastic Cover on COP,
  Fixings dropdown.
- **Layers** — four editable rows with layer name, ACI colour index,
  live colour swatch. Defaults match Adelaide Annexe's cutter
  convention: `1 Rotary Blade` (ACI 5 blue) for Outline + COP,
  `5 Draw and Text` (ACI 6 magenta) for Annotation, `0` (ACI 7 white)
  for Title block / project info / dimensions. Reset to Defaults
  button on the right.

On the right side of the dialog: a **wall diagram** side panel that
retargets (Left / Right / Rear) when the mouse enters a wall section,
and highlights the matching dim when a field gains focus.

Default values are sensible (1400 mm × 2150 mm typical), so generating
with defaults produces a meaningful drawing without touching anything.

The dialog is **non-modal** — you can pan / zoom / select inside
DraftSight while the form is open; the form stays above DraftSight
without blocking input.

### 3. Try the validation

- Put garbage (`abc`) in one numeric field.
- Click Generate. Expect a red error message at the bottom listing
  *all* invalid fields at once (not just the first).
- Fix them. Click Generate again.

### 4. Try Through Car

- Tick **Through Car (omits rear wall)** in Options.
- Scroll up — the Rear Wall section's "Include this wall" checkbox
  should immediately uncheck *and* become disabled.
- Untick Through Car — checkbox re-enables but stays unchecked
  (preserving user intent). Tick it manually to include rear.

### 5. Try the layers panel

- Change Outline's ACI from `5` (Blue) to `1` (Red). The swatch on
  the right updates live.
- Click **Reset to Defaults**. All four rows return to the
  Adelaide-Annexe cutter convention.

### 6. Generate

Click **Generate Drawing**. Dialog closes. Type `ZE` (Zoom Extents) at
the DraftSight command prompt or use the View → Zoom Extents button.

Expected:

- Three walls laid out left-to-right at world origin (or just two if
  Through Car was ticked).
- Each wall is a closed **blue** polyline outline (on `1 Rotary
  Blade`) with vertical fold lines between any non-zero door return
  panels.
- If a wall had COP enabled: a blue COP cutout polyline inside the
  main wall area, with a small magenta "COP" label centred in it.
- **No boxed title block.** Instead:
  - A **white legend** across the top: `HEIGHT / WIDTH / RETURNS /
    V QUILT / H QUILT / COP / TEXT / INFO / STENCIL / SCALE / OTHER`
  - A **white project info column** to the right of the walls:
    project metadata, then static reference text (FIXINGS allowance
    table, WIDTH/HEIGHT formula reminder, vertical quilting spacing
    lookup), then NOTES if populated.
- **White DIMENSION entities** (selectable, editable):
  - Main width below each wall
  - Main height on the outer-left side of the leftmost wall only
    (other walls share the height; duplicates would stack)
  - Each non-zero door return labelled across the top of its wall
  - COP top-offset / height / width around the COP when enabled

### 7. Try undo

Press **Ctrl+Z** once. Expect the entire generation (walls + COP +
text + dims) to vanish in one undo step.

### 8. Try re-generation

Bring up the picker again, lift blanket, change some values, generate.
Open Layer Manager (`LAYER` command) — should see `0`, `1 Rotary
Blade`, `5 Draw and Text` (plus DraftSight's built-ins); no
duplicates. Layer colours match what you configured.

### 9. Try the failure path

Close all open DraftSight drawings, then click the ribbon button,
pick Lift Blanket, click Generate. Expected: a "No active drawing is
open" MessageBox, **dialog stays open** with your values intact.
Open a drawing, click Generate again — should succeed without
retyping anything.

---

## Diagnostic command

If anything is suspect with the layer system, run the isolated test:

```
CANVASCOVERSLAYERTEST
```

at the DraftSight command prompt. It walks you through 8 dialogs, one
per API call, and writes a timestamped breadcrumb to
`%LocalAppData%\CanvasCovers\layertest.log`. Final dialog says SUCCESS
if the activate-based layer pattern is working.

---

## Recover

If anything crashes or behaves weirdly **but DraftSight still launches**:
uninstall via Settings → Apps → *BesiaCAD Canvas Covers*. Reopen
DraftSight to confirm clean state, then rebuild and reinstall.

If **DraftSight refuses to launch** (the normal uninstaller can't run):

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

Unregisters the COM DLL and disables the XML so DraftSight can open.
Then use Settings → Apps to clean up the rest, debug, rebuild, retest.
