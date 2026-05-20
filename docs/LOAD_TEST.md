# CanvasCovers Load Test

Use this only after `CanvasCovers` builds successfully.

## Safety first

Do not enable DraftSight startup loading during development. `CanvasCovers.xml`
uses `startup="0"` so a failed add-in is never loaded automatically when
DraftSight launches.

Keep this rollback command ready before opening DraftSight:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

## Deploy

Close DraftSight, then open PowerShell as Administrator from the repo root and
run:

```powershell
.\scripts\deploy-canvascovers.ps1
```

If DraftSight does not list the add-in, try the alternate ProgramData path
variant:

```powershell
.\scripts\deploy-canvascovers.ps1 -UseNoSpaceProgramDataPath
```

## Activate manually

1. Open DraftSight.
2. Open the Add-Ins manager (`Tools > Add-Ins`, `Tools > Options > Add-ins`,
   or the equivalent in the installed UI — the `ADDINS` command also works).
3. Tick the `CanvasCovers` checkbox to activate it. Do **not** enable Start Up.
4. Because the XML ships with `startup="0"`, you must re-tick the add-in every
   time you reopen DraftSight during development. This is intentional — it
   means a broken build cannot crash DraftSight at launch.

## Verify the ribbon button

After activation, scan the ribbon tabs across the top of the DraftSight
window. There should be a **CanvasCovers** tab at the end of the strip (after
`BIM`, or wherever the next free tab slot is on the current workspace).

1. Click the **CanvasCovers** tab.
2. The tab should contain a single panel named **Tools** with a **Hello**
   button. The button should show the placeholder icon copied from the SDK
   sample.
3. Click **Hello**.

Expected: the DraftSight command window prints `Hello CanvasCovers`.

The same command is also available from the command prompt — type
`CANVASCOVERSHELLO` and press Enter for an equivalent result. This is the
fallback path while the ribbon is being developed.

## Recover

If DraftSight crashes or behaves badly:

```powershell
.\scripts\rollback-canvascovers.ps1 -StopDraftSight
```

Then confirm DraftSight opens cleanly before trying another load.

## Why we built up incrementally

The earlier scaffold registered a toolbar button with empty icon paths. When
DraftSight transitioned into `dsUIState_Document` (i.e. the moment any
drawing was created or opened) it tried to render the toolbar and crashed.
The rebuild progressed through these gates:

1. ✅ Command registered (auto-complete picks it up at the command prompt).
2. ✅ `ExecuteNotify` fires (managed `Execute()` runs).
3. ✅ `GetCommandMessage().PrintLine(...)` surfaces in the command window.
4. ✅ Ribbon tab + panel + button registered via the SDK's verified path.
5. ✅ Real icon assets resolved relative to the deployed `Assembly.Location`.

Future increments (WPF form, geometry generation, code-signed installer)
build on this baseline. If any of the earlier gates regress, drop back to
the highest gate that still passes and bisect from there.
