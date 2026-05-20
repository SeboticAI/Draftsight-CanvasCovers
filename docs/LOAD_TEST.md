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

Close DraftSight, then in an **Administrator PowerShell** from the repo
root:

```powershell
.\scripts\deploy-canvascovers.ps1
```

Approx 5 seconds. Builds Release, copies DLL + icons + XML to
`C:\BesiaCAD\CanvasCovers\`, drops the XML into
`C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\`, runs
RegAsm. If DraftSight doesn't see the add-in afterwards, retry with
`-UseNoSpaceProgramDataPath` (some installs use the no-space variant).

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
  default).
- **Left Wall** — Main Width / Height, three Door Returns (zero if
  not needed), optional COP cutout.
- **Right Wall** — same shape, mirrored door return orientation.
- **Rear Wall** — Width / Height. Disabled if Through Car is ticked.
- **Options** — Through Car (omits rear wall), Plastic Cover on COP,
  Fixings dropdown.
- **Layers** — four editable rows (Outline / COP / Annotation /
  Titleblock) with layer name, ACI colour index, live colour swatch.
  Reset to Defaults button on the right.

Default values are sensible (1400 mm × 2150 mm typical), so generating
with defaults produces a meaningful drawing without touching anything.

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

- Change Outline's ACI from `1` (Red) to `4` (Cyan). The swatch on
  the right updates live.
- Click **Reset to Defaults**. All four rows return to baseline.

### 6. Generate

Click **Generate Drawing**. Dialog closes. Type `ZE` (Zoom Extents) at
the DraftSight command prompt or use the View → Zoom Extents button.

Expected:

- Three walls laid out left-to-right at world origin: LEFT WALL,
  RIGHT WALL, REAR WALL (or just two if Through Car was ticked).
- Each wall is a closed red polyline outline with vertical fold lines
  between any non-zero door return panels.
- If a wall had COP enabled: a blue rectangle inside the main wall
  area with "COP" green text in the centre.
- Below the walls, a yellow rectangle title block with six rows of
  project info, dividers between rows.

### 7. Try undo

Press **Ctrl+Z** once. Expect the entire generation (walls + COP +
labels + title block) to vanish in one undo step.

### 8. Try re-generation

Bring up the picker again, lift blanket, change some values, generate.
Open Layer Manager (`LAYER` command) — should still see the four CC-*
layers; no duplicates. Layer colours match what you configured.

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

If anything crashes or behaves weirdly:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

Unregisters, disables the XML, kills DraftSight. Reopen DraftSight to
verify it starts cleanly without the add-in. Then debug, redeploy, retest.
