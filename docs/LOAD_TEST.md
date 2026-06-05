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
- **Wall tabs** — a **TabControl** with **Left Wall / Right Wall /
  Rear Wall** tabs (replacing the old stacked text-row sections). Each
  tab is a drawn blanket with the input fields embedded on the drawing
  where they sit on the paper sheet, and it **redraws live** as you
  type:
    - five **Segment** boxes (DR-left / Seg1 / Seg2 / Seg3 / DR-right —
      sum = wall width, place 0 if not needed)
    - a **Measured Height** (the raw "top of hook to bottom of blanket"
      number, BEFORE the tool subtracts the allowance and doubles it)
    - when **COP** is enabled (Left/Right only — the Rear tab has no
      COP): a **COP height** and a **bottom gap** (gap from the wall
      bottom up to the COP). The COP **width and left position are no
      longer typed** — the COP width IS the middle segment (**Seg2**)
      and its left edge is derived from the segments (door-return +
      Seg1). The **top gap** (from the COP top up to the fold line) is
      shown **auto / read-only**; it turns red ("crosses fold") if the
      COP would cross the fold midline.
    - the **Rear Wall** tab is disabled when Through Car is ticked.
- **Options** — Through Car (omits rear wall), Plastic Cover on COP,
  Fixings dropdown, **Fixing Allowance (mm)** (auto-fills from the
  fixing type; editable), **Edge Allowance (mm)** (default 10; split
  half each horizontal side of the cut, and the same half insets the
  quilting), **Quilting Spacing (mm)** (target gap; the line count is
  even-divided so there is no remainder gap), a **"Draw quilting lines"**
  checkbox (on by default), and **"Open DXF export dialog after
  generating"** (on by default).
- **Layers** — four editable rows with layer name, ACI colour index,
  live colour swatch. Defaults match the client's cutter convention:
  `1 Rotary Blade` (ACI 5 blue) for **Outline (cuts)**,
  `5 Draw and Text` (ACI 6 magenta) for **COP + quilt lines
  (draw/score)** and **Annotation**, `0` (ACI 7 white) for Title block /
  project info / dimensions. Reset to Defaults button on the right.

Default values are sensible (Seg2 = 240, Seg3 = 1400, Measured Height
2200), so generating with defaults produces a meaningful drawing without
touching anything.

The dialog is **non-modal** — you can pan / zoom / select inside
DraftSight while the form is open; the form stays above DraftSight
without blocking input.

### 3. Try the validation

- Put garbage (`abc`) in one numeric field.
- Click Generate. Expect a red error message at the bottom listing
  *all* invalid fields at once (not just the first).
- With COP enabled, set the COP height + bottom gap so they overflow
  the measured half (e.g. bottom gap 600 + COP height 2000 on a 2200
  measured / 50 allowance wall). The auto **top gap** label turns red
  ("crosses fold") and Generate is **blocked** with a "crosses the fold
  line" error.
- Fix them. Click Generate again.

### 3b. Try the live blanket

- Type into a Segment or Measured Height field on the active tab and
  watch the drawn blanket **reshape to true proportion** as you type —
  a taller measured height makes a visibly taller rectangle.
- Confirm that typing **does not drop keyboard focus per keystroke**:
  you can type a multi-digit number straight through without the cursor
  jumping out of the field after each character. (The fields are
  persistent children of the canvas, repositioned-not-rebuilt on each
  redraw, precisely so focus survives.)
- Switch tabs (Left / Right / Rear) — each keeps its own values.

### 4. Try Through Car

- Tick **Through Car (omits rear wall)** in Options.
- The **Rear Wall tab** should immediately become **disabled** (you
  can no longer select it).
- Untick Through Car — the Rear tab re-enables but the rear wall stays
  not-included (preserving user intent). Re-include it via its tab if
  you want the rear wall.

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
  Through Car was ticked), each labelled `L` / `R` / `B`.
- Each wall is a **plain closed blue rectangle** (on `1 Rotary
  Blade`). Its height is the **doubled** cut height — e.g. a Measured
  Height of 2200 with a 50 fixing allowance produces a 4300-tall
  rectangle (`(2200 − 50) × 2`). Its width is the segment sum + the
  edge allowance (default 10mm). There are **no** door-return steps.
- If a wall had COP enabled: a **magenta** COP rectangle (on
  `5 Draw and Text` — the DRAW/score layer, NOT the blue cut layer)
  in the lower (measured) half of the wall, with a "COP" label centred
  in it. Its width is the wall's middle segment (Seg2).
- **Magenta quilt lines** (also on `5 Draw and Text`, the draw/score
  layer) — vertical + horizontal score lines filling **only the bottom
  half** up to the fold midline, bounded left/right by the door-return
  boxes and inset by half the edge allowance, spaced by the Quilting
  Spacing target (even-divided). Absent if "Draw quilting lines" was
  unticked.
- **No boxed title block.** Instead:
  - A **white legend** across the top: `HEIGHT / WIDTH / RETURNS /
    V QUILT / H QUILT / COP / TEXT / INFO / STENCIL / SCALE / OTHER`
  - A **white project info column** to the right of the walls:
    project metadata, FIXINGS allowance table, a `FIXING ALLOWANCE`
    line, the WIDTH/HEIGHT formula reminder, then NOTES if populated.
- **White DIMENSION entities** (selectable, editable):
  - Cut width below each wall
  - Cut height on the outer-left side of the leftmost wall only
    (other walls share the height; duplicates would stack)

### 6a. Reproduce the reference job (geometry check)

To confirm the geometry matches the client's real DXF, reproduce job
**12346** left wall on the **Left Wall tab**: segments `250 / 350 /
240 / 1400 / 0` (DR-left / Seg1 / Seg2 / Seg3 / DR-right), Measured
Height `2200`, Fixing = Hooks Facing Out (allowance auto-fills 50),
Edge Allowance `10`. Enable COP and type **only** its vertical numbers:
COP height `1300`, bottom gap `600`. (There are **no** COP-width or
COP-from-left fields any more — the width comes from Seg2 = `240` and
the left edge is derived from the segments.) Confirm the auto **top gap**
label reads **250** (= `(2200 − 50) − 600 − 1300`).

Generate, then select the left cut rectangle and read its size: it
should be **2250 wide × 4300 tall** (2240 segments + 10 edge allowance;
(2200−50)×2). The COP rectangle should be **240 wide × 1300 tall**,
its left edge at **X 605** and right edge at **X 845** (`edgeAllowance/2
(5) + DoorReturnLeft (250) + Seg1 (350)` = 605; + Seg2 240 = 845), and
its **Y from 600 to 1900** — entirely in the lower (measured) half.
Compare against the numbers in
[`reference/DXF_FINDINGS.md`](../reference/DXF_FINDINGS.md).

### 6b. DXF export

If "Open DXF export dialog after generating" was ticked (default), a
Save-As dialog appears after Generate, pre-filled with the network
number as the filename and a `.dxf` filter. Pick a folder, save, and
re-open the file (or run `reference\parse-dxf.ps1 -Path <saved.dxf>`)
to confirm the cut rectangle + COP coordinates round-trip correctly.

### 7. Try undo

Press **Ctrl+Z** once. Expect the entire generation (walls + COP +
quilt lines + text + dims) to vanish in one undo step.

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
