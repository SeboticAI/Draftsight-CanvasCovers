# DraftSight Add-in Playbook

The canonical reference for building, packaging, and distributing C# DraftSight
add-ins. Parallels the BesiaBIM Revit playbook — same shape, different host
application.

This document is the source of truth for *how* DraftSight add-ins are
structured. Update it whenever real-world quirks surface. Do not let lessons
get lost in chat history.

When working in Claude Code or Cursor: read this file in full before making
changes. Cite section numbers when explaining decisions.

---

## 1. The big picture

| Concern              | Choice                                              |
| -------------------- | --------------------------------------------------- |
| Language             | C# (latest), .NET SDK-style csproj                  |
| TFM                  | `net48` (DraftSight 2024–2026 all run on .NET Framework 4.x via COM) |
| DraftSight API refs  | Local DLLs from the SDK folder (no NuGet equivalent) |
| Interop model        | COM — add-in class is `[ComVisible(true)]` with stable `[Guid]`, deriving from `DsAddin` per the DS 2026 SDK sample |
| UI                   | WPF (preferred) or WinForms                         |
| Ribbon panel         | Shared `BesiaCAD Tools → <PanelName>` across add-ins (TBD) |
| Installer            | Inno Setup 6, per-machine (COM registration + XML config) |
| Distribution         | Single `*-Setup.exe`                                |
| Source layout        | One folder per add-in at repo root, plus `Installer\` |

**Why these choices**

- **COM interop is non-negotiable.** DraftSight's API uses the
  SolidWorks-derived automation model. The host process activates add-ins by
  CLSID, not by managed assembly loading. Trying to "avoid COM" is fighting
  the platform.
- **Local SDK references, not NuGet.** Unlike Revit (where Nice3point publishes
  excellent NuGet packages for every year), DraftSight has no community NuGet
  ecosystem. Reference DLLs directly from
  `C:\Program Files\Dassault Systemes\DraftSight\APISDK\`.
- **`net48` only.** DraftSight 2026 still loads add-ins through the COM bridge
  to .NET Framework. No .NET 6/8 equivalent yet. This is the opposite of the
  Revit story.
- **Per-machine install.** Unlike Revit's `%AppData%\Autodesk\Revit\Addins\`
  (per-user), DraftSight add-ins require COM registration and a project XML
  file under `ProgramData`. The installer needs admin / UAC.

---

## 2. Tooling requirements

### 2a. DraftSight install

- **DraftSight Professional, Premium, or Enterprise.** The free version does
  not include the SDK and cannot load add-ins. Confirm tier before any client
  work — this is the single biggest "the whole plan changes" question.
- Default install: `C:\Program Files\Dassault Systemes\DraftSight\`
- SDK lands at: `C:\Program Files\Dassault Systemes\DraftSight\APISDK\`
  - `samples\` — example projects in C#, VB.NET, and C++
  - `docs\` — API reference (HTML + `.chm`)
  - `tlb\` / `redist\` — local interop DLLs; exact folder varies by version
- If `APISDK\` is missing, the installed tier doesn't include it. Contact a
  Dassault VAR — sometimes the SDK ships separately even for paid tiers.

### 2b. Visual Studio

- **Visual Studio 2022 Community** (free) or higher.
- Workloads required: `.NET desktop development`.
- Official API help describes a `DSAddinCSharp` Visual Studio template, but
  the installed DS 2026 `APISDK\` tree may not include it. If the template is
  present, prefer it for a clean add-in baseline. If not, build a minimal
  project from the installed C# samples and the patterns in this playbook.

### 2c. Reference DLLs

The installed DraftSight 2026 SDK `C#\Simple\Ribbon` sample references:

- `DraftSight.Interop.dsAddin.dll` — the COM add-in base class (`DsAddin`)
- `DraftSight.Interop.dsAutomation.dll` — the main API (drawings, entities,
  commands, settings)

Other samples may also reference:

- `DraftSight.Interop.dsCommonAPIs.dll` — shared types, enums, if required by
  the specific APIs being used

The sample points to SDK-local interop DLLs and uses `SpecificVersion=False`.
Exact local folder names vary by install (`tlb\` vs `redist\`), so verify the
paths on the development machine before scaffolding.

Reference the add-in and automation DLLs in the project. Example shape:

```xml
<Reference Include="DraftSight.Interop.dsAddin">
  <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAddin.dll</HintPath>
  <SpecificVersion>False</SpecificVersion>
  <Private>True</Private>
  <EmbedInteropTypes>False</EmbedInteropTypes>
</Reference>
<Reference Include="DraftSight.Interop.dsAutomation">
  <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll</HintPath>
  <SpecificVersion>False</SpecificVersion>
  <Private>True</Private>
  <EmbedInteropTypes>True</EmbedInteropTypes>
</Reference>
```

**Interop embedding is per reference.** Keep `EmbedInteropTypes=False` for
`dsAddin` because the add-in class derives from `DsAddin`. Use
`EmbedInteropTypes=True` only for automation/common interop references after a
test build proves the SDK accepts it. If embedding causes compile errors, set
it to `False` and rely on the DraftSight install's interop assemblies.

### 2d. Inno Setup (installer time, not day-one)

Inno Setup 6.3+ for MSI-free single-EXE distribution. Skipped during demo
phase — added when productising.

### 2e. Code signing (before first client release)

`signtool.exe` + a code-signing cert. DraftSight shows "Unknown Publisher" on
add-in load until signed. Deferred until post-demo.

---

## 3. Project layout

```
<RepoRoot>\
├─ CLAUDE.md                   ← this file
├─ README.md
├─ docs\
│  ├─ PROJECT_BRIEF.md
│  ├─ DEMO_SCOPE.md
│  ├─ HELLO_DRAFTSIGHT.md
│  └─ CURSOR_PROMPT.md
├─ Installer\
│  ├─ <Suite>.iss              ← Inno Setup script (later)
│  ├─ build.ps1                ← one-shot build + package (later)
│  └─ Output\                  ← installer .exe lands here
├─ <AddinName>\
│  ├─ <AddinName>.csproj
│  ├─ <AddinName>.xml          ← DraftSight add-in config (deployed to ProgramData)
│  ├─ App.cs                   ← COM-visible entry class, ribbon registration
│  ├─ Common\                  ← shared helpers
│  ├─ Commands\                ← command classes invoked from ribbon buttons
│  ├─ Models\                  ← parameter POCOs
│  ├─ Geometry\                ← drawing logic (DraftSight equivalent of Revit's Services\)
│  ├─ UI\                      ← WPF Window + ViewModel
│  ├─ Resources\
│  │  ├─ <addin>_32.png        ← ribbon icon (large)
│  │  └─ <addin>_16.png        ← ribbon icon (small)
│  └─ Properties\AssemblyInfo.cs
```

**Naming convention.** Pick a suite name (e.g. `BesiaCAD`) and use it in the
ribbon tab name and the installer's `AppId`. Individual add-in project names
stay short (e.g. `CanvasCovers`, not `BesiaCADCanvasCovers`).

---

## 4. csproj template

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>CanvasCovers</RootNamespace>          <!-- CHANGE -->
    <AssemblyName>CanvasCovers</AssemblyName>            <!-- CHANGE -->
    <UseWPF>true</UseWPF>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>

    <!-- .NET Framework COM registration is done with RegAsm; do not expect a .comhost.dll. -->
    <RegisterForComInterop>false</RegisterForComInterop>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="DraftSight.Interop.dsAddin">
      <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAddin.dll</HintPath>
      <SpecificVersion>False</SpecificVersion>
      <Private>True</Private>
      <EmbedInteropTypes>False</EmbedInteropTypes>
    </Reference>
    <Reference Include="DraftSight.Interop.dsAutomation">
      <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll</HintPath>
      <SpecificVersion>False</SpecificVersion>
      <Private>True</Private>
      <EmbedInteropTypes>True</EmbedInteropTypes>
    </Reference>
    <!-- Add DraftSight.Interop.dsCommonAPIs only if a verified SDK sample/API requires it. -->
  </ItemGroup>

  <ItemGroup>
    <None Include="Resources\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

**Differences from a Revit csproj:**
- Single TFM (`net48`), no multi-target.
- No NuGet packages — references the SDK DLLs directly.
- DraftSight is COM-registered with `RegAsm`; SDK sample does not use
  `.comhost.dll` / `regsvr32`.
- `<UseWPF>` works the same.

---

## 5. The XML config file (DraftSight's equivalent of `.addin`)

Official DraftSight 2026 API help says C#, VB.NET, and COM add-ins need a
project-specific XML file in an `addinConfigs` folder. The documented Windows
location is:
`C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\<AddinName>.xml`

Some installs/tools may use `C:\ProgramData\DassaultSystemes\DraftSight\...`
without the space. Check both paths on the target machine and use the one
DraftSight's Add-Ins manager reads.

Use `startup="0"` during development. Only change to `startup="1"` after the
add-in has loaded manually and the rollback command has been tested. A broken
startup add-in can crash DraftSight on launch.

```xml
<?xml version="1.0" encoding="utf-8"?>
<addinmanager>
  <draftsight version="">
    <addin help="Generate a demo canvas cover" startup="0" name="CanvasCovers" premiumOnly="2">
      <com clsid="{REPLACE-WITH-YOUR-GUID}"></com>
      <button bitmap="C:\BesiaCAD\CanvasCovers\Resources\canvascovers_16.png"></button>
    </addin>
  </draftsight>
</addinmanager>
```

**Field meanings:**
- `draftsight version` — leave empty for the current DraftSight version unless
  a local API sample proves a stricter value is needed.
- `startup="0"` — show in Add-Ins manager without automatic startup loading.
  Use `"1"` only after manual load is proven safe.
- `name` — display name in DraftSight's add-in manager.
- `premiumOnly` — official help documents `1` for Pro/Premium and `2` for
  Premium-only. We are targeting the installed Premium trial for the demo.
- `clsid` — must match the `[Guid("...")]` attribute on the `App` class.
  **Generate a fresh GUID per add-in.** Two add-ins with the same GUID will
  collide.
- `button bitmap` — 16x16 `.png` path for the Add-Ins manager icon. If unsure,
  use a placeholder path and verify whether DraftSight requires the file.

---

## 6. `App.cs` — the COM-visible entry class

```csharp
using System;
using System.Runtime.InteropServices;
using DraftSight.Interop.dsAddin;
using DraftSight.Interop.dsAutomation;

namespace CanvasCovers
{
    [ComVisible(true)]
    [Guid("REPLACE-WITH-YOUR-GUID")]
    [ProgId("CanvasCovers.App")]
    [ClassInterface(ClassInterfaceType.None)]
    public class App : DsAddin
    {
        private Application _app;
        private int _cookie;
        private string _addinGuid;

        public App()
        {
            _addinGuid = GetType().GUID.ToString();
        }

        public bool ConnectToDraftSight(object application, int cookie)
        {
            try
            {
                _app = application as Application;
                _cookie = cookie;

                if (_app == null)
                {
                    System.Windows.MessageBox.Show(
                        "CanvasCovers failed to load: DraftSight application object was not available.",
                        "Add-in error");
                    return false;
                }

                RegisterRibbonButton();
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"CanvasCovers failed to load: {ex.Message}\n\n{ex.StackTrace}",
                    "Add-in error");
                return false;
            }
        }

        public bool DisconnectFromDraftSight()
        {
            try
            {
                _app = null;
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"CanvasCovers failed to unload: {ex.Message}\n\n{ex.StackTrace}",
                    "Add-in error");
                return false;
            }
        }

        private void RegisterRibbonButton()
        {
            // TODO: copy the exact call pattern from the installed SDK sample
            // `C#\Simple\Ribbon` before implementing. Verified sample pattern:
            // CreateCommand2/CreateUserCommand -> AddWorkspace/GetWorkspace ->
            // AddRibbonTab -> InsertRibbonPanel -> InsertRibbonRow ->
            // InsertRibbonCommandButton.
        }
    }
}
```

**Key differences from Revit's `IExternalApplication`:**
- COM attributes (`[ComVisible]`, `[Guid]`, `[ProgId]`, `[ClassInterface]`)
  are mandatory, not decorative. Forget any of these and the load silently
  fails.
- The DS 2026 SDK sample derives from `DraftSight.Interop.dsAddin.DsAddin`.
  Do not invent an `IDraftSightAddIn` interface unless a local SDK sample
  proves that interface exists.
- `ConnectToDraftSight` returns `bool` — not Revit's `Result` enum.
- The `cookie` parameter is a session token DraftSight gives; store it in
  case a verified API call needs it.
- The `MessageBox` fallback is critical during development — DraftSight will
  *silently* refuse to load a broken add-in unless errors are surfaced
  explicitly.

---

## 7. The dev loop (single most important thing to internalize)

The Inno Setup installer is the primary install method for both dev iteration
and shipping. The PowerShell deploy script has been removed; only the
startup-crash rollback script remains in `scripts\`.

1. Edit C# in Visual Studio (or Cursor / Claude Code).
2. **Close DraftSight** if running. The DLL is locked while the host process
   is alive — same problem as Revit. `Installer\build.ps1` refuses to run if
   `DraftSight.exe` is alive, as a safety net.
3. From the repo root: `.\Installer\build.ps1`. This rebuilds the project in
   Release configuration and compiles the installer EXE into
   `Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe`.
4. Run the installer EXE as administrator (UAC prompts automatically). It:
   - Lays down the payload at `C:\BesiaCAD\CanvasCovers\` (fixed — the XML
     hardcodes the ribbon button bitmap path there).
   - Copies `CanvasCovers.xml` to
     `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\`.
   - Runs 64-bit `RegAsm.exe /codebase` on the DLL.
   - Re-running over an existing install upgrades cleanly (same `AppId`).
5. Open DraftSight → Tools → Add-Ins → tick CanvasCovers. The XML ships with
   `startup="0"` so first activation is manual on purpose — verify the load
   is clean before flipping startup mode.
6. Verify the ribbon button appears.
7. Click → test.

**Uninstall:** Settings → Apps → *BesiaCAD Canvas Covers* → Uninstall. The
uninstaller runs `RegAsm /unregister`, removes the XML from `addinConfigs`,
and deletes the payload folder.

**Startup-crash recovery:** if DraftSight refuses to launch after install
(meaning the normal uninstaller can't run because DS can't start), use
`.\scripts\rollback-canvascovers.ps1`. See §10 below for details.

---

## 8. Inno Setup script — live, not deferred

Lives at [Installer\CanvasCovers.iss](Installer/CanvasCovers.iss) with a build
pipeline at [Installer\build.ps1](Installer/build.ps1). See
[Installer\README.md](Installer/README.md) for end-to-end usage. Pinned
identifiers:

| Field        | Value                                  |
| ------------ | -------------------------------------- |
| `AppId`      | `A27F4037-4A3F-4706-B839-B88836F132FD` |
| Install dir  | `C:\BesiaCAD\CanvasCovers` (locked via `DisableDirPage=yes`) |
| Architecture | `x64compatible` (not `x64` — deprecated since Inno 6.3) |

Critical settings:

1. **`PrivilegesRequired=admin`** — the XML config folder under
   `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\` is per-machine.
2. **`[Run]` invokes 64-bit RegAsm:**
   `Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: "/codebase ""{app}\CanvasCovers.dll"""`
3. **XML ships with `startup="0"`.** First activation is manual via the
   Add-Ins manager — only flip to `startup="1"` after a clean test load.
4. **`CloseApplications=force` / `CloseApplicationsFilter=DraftSight.exe`** —
   the installer closes DraftSight before writing the locked DLL.
5. **`[UninstallRun]` reverses `RegAsm`** with `/unregister`, and
   `[UninstallDelete]` removes the ProgramData XML and the install folder.

To bump versions, edit `MyAppVersion` in `CanvasCovers.iss`. Never change
`AppId` — upgrade detection depends on it.

---

## 9. Gotchas (the ones that will bite)

- **The free DraftSight tier has no SDK.** Always confirm tier before scoping.
- **DraftSight must be closed during build/deploy.** Same locked-file problem
  as Revit. Plan dev loop accordingly.
- **CLSID conflicts.** Reusing a GUID across two add-ins makes the second
  silently fail to load. Generate fresh per add-in, write it into both the
  `[Guid]` attribute and the XML.
- **COM registration fails silently.** If the managed DLL is not registered
  with `RegAsm`, or the optional XML config points to the wrong CLSID/path,
  DraftSight just doesn't load the add-in — no error, no log. Use the SDK's
  add-in manager UI to verify load state.
- **Interop embedding is reference-specific.** `dsAddin` should not be
  embedded when deriving from `DsAddin`; automation/common references can use
  embedding if the project compiles with it.
- **`net48` only.** Don't attempt .NET 6/8 — the COM bridge doesn't support
  it for DraftSight 2026.
- **AppId is forever.** Same warning as Revit — pick the Inno `AppId` GUID
  once, never change it.
- **Surface load errors with MessageBox during dev.** A `try/catch` around
  `ConnectToDraftSight`'s body with `MessageBox.Show(ex.Message)` is the
  single most useful debugging aid. Remove only when shipping.
- **Don't develop in `Program Files`.** Requires admin to save and breaks
  Visual Studio's hot reload. Always copy samples to `C:\Dev\...` first.
- **Keep a rollback command ready before every load test.** If DraftSight
  crashes at startup after registering an add-in, unregister it before trying
  to launch DraftSight again.
- **Do not call `Application.RemoveUserInterface(guid)` preemptively on first
  connect.** The SDK `Simple\Ribbon` sample only calls it from
  `DisconnectFromDraftSight`, after UI has actually been added. Calling it
  against a GUID with nothing registered can leave DraftSight's internal UI
  registry in an inconsistent state and crash the host on the next document
  state transition.
- **Toolbar items must have non-empty icon paths.** `CreateUserCommand` with
  `SmallIcon=""` / `LargeIcon=""`, then `InsertToolbarItem` referencing that
  user command, crashes DraftSight the moment the toolbar tries to render
  (typically when `dsUIState_Document` activates — i.e. when the first
  drawing is created or opened). Menu items are text-only and tolerate empty
  icons; toolbar items do not. If you don't have icons yet, register the
  command via `CreateCommand2` only and invoke it from the command prompt.
- **`AddMenu` / `AddToolbar` is the SDK's *non-preferred* path.** The
  `Simple\Ribbon` sample ships those calls commented out and uses the Ribbon
  API instead. Mirror the ribbon path once UI is needed; treat the menu/
  toolbar code as a stop-gap.
- **`MessageBox.Show` from a `Command.ExecuteNotify` handler is risky.** It's
  a WPF/WinForms modal called from a COM event callback into the host's
  message loop. Use `application.GetCommandMessage().PrintLine(...)` for
  command feedback, as the SDK sample does. Reserve `MessageBox.Show` for
  the `ConnectToDraftSight` / `DisconnectFromDraftSight` catch blocks where
  surfacing the error is the whole point.
- **Do not call `EntityHelper.SetLayer` or `EntityHelper.GetLayer` on a
  freshly-inserted entity.** Both crash DraftSight natively (no managed
  exception — the host process dies). Verified by isolated step-by-step
  test: `Application.GetEntityHelper()` returns a non-null EntityHelper,
  then calling either `SetLayer(polyline, name)` or `GetLayer(polyline)`
  on a polyline returned by `SketchManager.InsertPolyline2D(...)` faults
  the process. The SDK `DockControl2/EntitiesProperties.cs` sample calls
  SetLayer successfully but on entities that have been sitting in the
  document (obtained via `GetFilteredEntities`), not on fresh RCWs from
  an `Insert*` call. **Use the activate pattern instead** (mirrored from
  the C++ `BlockCustomData` sample): save current active layer via
  `LayerManager.GetActiveLayer()`, activate the target layer with
  `targetLayer.Activate()`, insert the entity (lands on active layer at
  creation time), then restore the original active layer with
  `originalActive.Activate()`. No `EntityHelper` call is ever needed.

---

## 10. Verification chain (for diagnosing "my add-in won't load")

Run in order. Stop at the first failure — that's the bug.

1. **Does the SDK folder exist?**
   `dir "C:\Program Files\Dassault Systemes\DraftSight\APISDK"`
   If no, tier mismatch.
2. **Does the build produce the managed DLL?**
   After `dotnet build`, check `bin\Release\net48\` for
   `<AddinName>.dll`.
3. **Is COM registered?**
   Run 64-bit `RegAsm.exe /codebase <path>\<AddinName>.dll` manually — should
   succeed. Failure = COM visibility, platform target, or reference issue.
4. **Does the XML config exist in the right folder?**
   Check both documented ProgramData variants if needed. The XML should be
   listed and should use `startup="0"` during development.
5. **Does the XML's CLSID match the assembly's `[Guid]`?**
   Diff them by eye. One character wrong = silent failure.
6. **Does DraftSight see it?**
   Open DraftSight → Tools → Add-Ins (menu name varies by version). The
   add-in should appear in the list, even if disabled.
7. **Does the ribbon button appear?**
   If add-in is loaded but no button, the error is inside
   `ConnectToDraftSight` — that's where the MessageBox catches it.

### Startup crash recovery

If DraftSight crashes on launch after install, the normal Settings → Apps
uninstaller can't run (DS won't start). Use the rollback script:

1. Do **not** repeatedly reopen DraftSight.
2. Run `.\scripts\rollback-canvascovers.ps1` (admin shell). It runs
   `RegAsm /unregister` against the installed DLL and renames the XML in
   `addinConfigs` to `.disabled`.
3. Confirm DraftSight opens cleanly.
4. Once DS is healthy, run the proper uninstaller from Settings → Apps to
   clean up the rest of the install (the rollback script intentionally does
   not touch `C:\BesiaCAD\CanvasCovers\` so the uninstaller can still find
   the install).
5. Only then debug the add-in, starting with a minimal no-ribbon load test.

If the rollback script itself fails (path mismatch, etc.), fall back to the
manual sequence:
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe C:\BesiaCAD\CanvasCovers\CanvasCovers.dll /unregister`
- Delete or rename `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\CanvasCovers.xml`.

---

## 11. Reference samples to study

From `C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\`:

- **`C#\Simple\Ribbon`** — ribbon button + command registration. Start here.
- **`C#\Simple\DrawingToImage`** — simple automation pattern.
- **`C#\3D\*` and other simple samples** — entity creation patterns as needed.
- **Layer / linetype samples** — names vary by SDK version; list `samples\C#`
  and pick the closest match before writing layer code.

Names may differ slightly across DraftSight versions. List the folder contents
and find the closest match.

Use samples as *reference*, not as a starting template — copy the patterns
into a clean project structured per §3.

---

## 12. Working with AI assistants on this codebase

When using Claude Code, Cursor, or any AI assistant:

- **Read this file first.** It is the source of truth. Cite section numbers
  when justifying decisions.
- **Verify any DraftSight API signature before using it.** AI knowledge of
  the DraftSight API is thin and outdated. When in doubt, check
  `APISDK\samples\` for a known-working example, or open the API `.chm`
  reference. Do not trust hallucinated method names.
- **Surface uncertainty explicitly.** If the AI is unsure whether a method
  exists, it should say so and ask to verify, not guess.
- **Generate fresh GUIDs.** Never copy a GUID from documentation, even this
  one. Use `[guid]::NewGuid()` in PowerShell or the VS Tools → Create GUID
  feature.
- **One concern per change.** No mass refactors during the demo phase. Each
  edit should be reversible.
- **Test compiles after every change.** Don't pile up unverified edits.

---

## 13. What's deliberately not in this playbook yet

Will be added as encountered on real projects:

- Multi-version support (running one codebase across DS 2024/2025/2026).
- License-gating against the BesiaBIM licensing API (Ed25519 token
  integration).
- Auto-update mechanism.
- Telemetry / error reporting.
- The DraftSight equivalent of Revit's `RevitLookup` (research needed).
- Code signing specifics for the COM-registered DLL.

Move items from this list to documented patterns as they're solved.
