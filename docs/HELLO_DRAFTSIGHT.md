# Hello DraftSight — First-Time Setup

The goal of this document is to get the SDK's hello-world sample loading
**before writing any new code.** This isolates environment problems from code
problems. Skipping this step costs hours every time.

If any step fails, **stop and resolve before moving on.** Do not try to
debug "is my code wrong, or is my setup wrong?" simultaneously — that's the
slowest possible way to learn this platform.

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
Test-Path "C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\DraftSight.Interop.dsAutomation.dll"
```

All three must return `True`. If any return `False`, the SDK didn't install —
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

Look for a folder like `Sample_CSharp_AddIn` or `CSharpAddin` — the exact
name varies by version.

**If no obvious C# sample folder exists:** list the contents of `samples\`
and paste them into a chat — the SDK layout shifts between versions. The
right sample needs to be identified explicitly before continuing.

---

## Step 4 — Copy the sample to a working folder

Do not work in `Program Files` directly. It requires admin for every save
and breaks tooling.

```powershell
New-Item -ItemType Directory -Path "C:\Dev\DraftSightSamples" -Force
Copy-Item -Recurse "C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\Sample_CSharp_AddIn" "C:\Dev\DraftSightSamples\"
```

(Adjust the sample folder name if it's different.)

---

## Step 5 — Open in Visual Studio

- Open the `.sln` file from `C:\Dev\DraftSightSamples\Sample_CSharp_AddIn\`
  in Visual Studio 2022.
- If VS prompts to "retarget" the project to a newer .NET version,
  **decline**. Keep it at whatever version the sample ships with (likely
  `net48`).
- Let it restore packages. Solution Explorer should show the project with
  `App.cs` (or similar), references including
  `DraftSight.Interop.dsAutomation`, and possibly an XML config file.

---

## Step 6 — Build it

- Build → Build Solution (`Ctrl+Shift+B`).
- Watch the output window for errors.
- Output DLL will land in `bin\Debug\` or `bin\Release\` depending on
  configuration.

**Common build failure:** "could not resolve reference
DraftSight.Interop.dsAutomation". The `HintPath` in the csproj points to a
wrong location. Edit the csproj, fix the path to match the actual install
location.

---

## Step 7 — Register and configure

The SDK usually includes a `register.bat` or `install.bat` that does the
COM registration and copies the XML config. **Look for it before doing
anything manually.**

If a register script exists, run it as Administrator. Verify it succeeded:

```powershell
dir "C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\"
```

The sample's XML should be in there.

If no register script exists, the manual process is:

1. Register the COM host:
   ```powershell
   regsvr32 "<path-to-sample>\bin\Debug\Sample_CSharp_AddIn.comhost.dll"
   ```
2. Copy the sample's XML config to
   `C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\`.
3. Verify the XML's `<com clsid="..."/>` matches the `[Guid("...")]` attribute
   on the sample's main class.

---

## Step 8 — Launch DraftSight and verify

- Open DraftSight.
- Look for the sample's button on the ribbon (usually a tab called "Sample"
  or similar).
- If the ribbon button doesn't appear, check `Tools → Add-Ins` (menu name
  varies by version) — the sample should be listed.
- Click the button. Confirm the expected behaviour (usually a message box
  or some visible drawing action).

---

## Step 9 — Outcome checkpoint

Three possible results:

1. **It works.** The button appears, clicks correctly, sample behaves as
   documented. **Move on** to scaffolding the canvas cover project (see
   `CURSOR_PROMPT.md`).
2. **Loaded but no button appears.** Registration worked, but the ribbon
   registration code failed silently. Look in the sample's source for the
   ribbon registration method, wrap it in a try/catch with `MessageBox.Show`,
   rebuild, re-test.
3. **Not loaded at all.** Work through the verification chain in `CLAUDE.md`
   §10 — every step in order. Stop at the first failure.

Do not proceed to writing the canvas cover code until outcome #1 is achieved.

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

This information shortcuts 90% of the back-and-forth.
