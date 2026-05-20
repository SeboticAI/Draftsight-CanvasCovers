# Hello DraftSight — First-Time Setup

The goal of this document is to prove the DraftSight add-in environment
**without destabilizing DraftSight startup.** This isolates environment
problems from code problems and keeps a rollback path ready before every load
test.

If any step fails, **stop and resolve before moving on.** Do not try to debug
"is my code wrong, or is my setup wrong?" simultaneously — that's the slowest
possible way to learn this platform.

**Safety rule:** during development, add-in XML must use `startup="0"` until a
manual Add-Ins manager load has succeeded. A broken startup add-in can crash
DraftSight before you can disable it in the UI.

---

## Step 1 — Install DraftSight 2026 Professional trial

Get the trial from `draftsight.com`. **Professional tier** (or higher) — the
free version does not include the SDK.

Default install: `C:\Program Files\Dassault Systemes\DraftSight\`

The moment install finishes, **before launching DraftSight**, verify the SDK
exists. Open PowerShell:

```powershell
Test-Path "C:\Program Files\Dassault Systemes\DraftSight\APISDK\"
Test-Path "C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\"
Test-Path "C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAddin.dll"
Test-Path "C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll"
```

All four must return `True`. If any return `False`, the SDK didn't install —
stop. The trial may not include it, in which case contact a Dassault VAR
before going further.

---

## Step 2 — Launch DraftSight once

- Open DraftSight. Activate the trial. Close any splash / "what's new" dialog.
- Confirm it runs. Create an empty drawing. Close it. Close DraftSight.

This step exists because some DraftSight components only finish registering
after the first launch. Skipping it can produce inexplicable COM errors
later.

---

## Step 3 — Find the C# sample

Open `C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\` in File
Explorer.

In the observed DS 2026 install, C# samples were grouped under:
`C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\C#\`

Useful samples:
- `Simple\Ribbon` — command and ribbon registration pattern
- `Simple\DrawingToImage` — simple automation pattern
- `3D\*` — entity creation patterns, depending on the geometry needed

**If no obvious C# sample folder exists:** list the contents of `samples\`
and paste them into a chat — the SDK layout shifts between versions. The
right sample needs to be identified explicitly before continuing.

---

## Step 4 — Copy the sample to a working folder

Do not work in `Program Files` directly. It requires admin for every save
and breaks tooling.

```powershell
New-Item -ItemType Directory -Path "C:\Dev\DraftSightSamples" -Force
Copy-Item -Recurse "C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\C#\Simple\Ribbon" "C:\Dev\DraftSightSamples\"
```

This copy is for reading/build comparison only. Do not register or load the
raw sample into DraftSight unless it has a complete XML config, `startup="0"`,
and a rollback command ready.

---

## Step 5 — Open in Visual Studio

- Open the `.csproj` file from `C:\Dev\DraftSightSamples\Ribbon\` in Visual
  Studio 2022, or open the SDK's `dsSamples.sln` read-only.
- If VS prompts to "retarget" the project to a newer .NET version,
  **decline**. Keep it at whatever version the sample ships with (likely
  `net48`).
- Let it restore packages. Solution Explorer should show the project with
  `DSAddin.cs`, references including `DraftSight.Interop.dsAddin` and
  `DraftSight.Interop.dsAutomation`.

---

## Step 6 — Build it

- Build → Build Solution (`Ctrl+Shift+B`).
- Watch the output window for errors.
- Output DLL will land in `bin\Debug\` or `bin\Release\` depending on
  configuration.

**Common build failure:** "could not resolve reference
DraftSight.Interop.dsAutomation" or `DraftSight.Interop.dsAddin`. The
`HintPath` in the copied csproj points to a relative SDK location that may no
longer be valid. Fix the paths to:

```text
C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAddin.dll
C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll
```

If Visual Studio tries to register the add-in during build and fails with an
admin/registry error, disable automatic COM registration for the sample. Build
and registration must be separate steps during this project.

---

## Step 7 — Register and configure safely

For DraftSight 2026 C#/.NET Framework add-ins, use `RegAsm`, not `regsvr32`
and not `.comhost.dll`.

Before registering any add-in, write down the rollback command:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "<path-to-addin-dll>" /unregister
```

Manual development flow:

1. Close DraftSight.
2. Register the managed DLL in an Administrator PowerShell:
   ```powershell
   & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "<path-to-addin-dll>" /codebase
   ```
3. Create or copy the project XML into DraftSight's `addinConfigs` folder.
   Official docs use:
   `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\`

   Some installs/tools may use:
   `C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\`

   Check both and use the one DraftSight reads.
4. Keep XML `startup="0"` until a manual load succeeds.
5. Verify the XML's `<com clsid="..."></com>` matches the `[Guid("...")]`
   attribute on the main COM-visible add-in class.

---

## Step 8 — Launch DraftSight and verify

- Open DraftSight.
- Open the Add-Ins manager (`Tools → Add-Ins`, `Tools → Options → Add-ins`,
  or equivalent for the installed UI).
- If the add-in is listed, activate it manually. Do not tick Start Up yet.
- Look for the add-in's button/menu/command.
- Click the button. Confirm the expected behaviour.

---

## Step 9 — Outcome checkpoint

Three possible results:

1. **It works.** The button appears, clicks correctly, sample behaves as
   documented. Move on to scaffolding the guarded canvas cover project.
2. **Loaded but no button appears.** Registration worked, but the ribbon
   registration code failed silently. Look in the sample's source for the
   ribbon registration method, wrap it in a try/catch with `MessageBox.Show`,
   rebuild, re-test.
3. **Not loaded at all.** Work through the verification chain in `CLAUDE.md`
   §10 — every step in order. Stop at the first failure.
4. **DraftSight crashes at startup.** Stop reopening DraftSight. Run the
   rollback command from Step 7, remove/rename the XML from `addinConfigs`,
   and confirm DraftSight opens cleanly before debugging further.

Do not proceed to feature code until outcome #1 is achieved on the
`CanvasCovers` project-level hello-world.

---

## What to capture when you hit a problem

When asking for help (in chat or escalating), provide:

- The exact step number where it failed
- Full error message or screenshot
- Output of:
  ```powershell
  dir "C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\"
  dir "C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\"
  ```
- The `[Guid]` attribute from the sample's source vs the `clsid` in the XML

## Recovery command

If DraftSight crashes after add-in registration, run this before reopening
DraftSight:

```powershell
Stop-Process -Name DraftSight -Force -ErrorAction SilentlyContinue
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "<path-to-addin-dll>" /unregister
```

Then remove or rename the corresponding XML in `addinConfigs`.

This information shortcuts 90% of the back-and-forth.
