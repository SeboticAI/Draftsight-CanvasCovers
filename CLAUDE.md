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
| Interop model        | COM — add-in class is `[ComVisible(true)]` with stable `[Guid]` |
| UI                   | WPF (preferred) or WinForms                         |
| Ribbon panel         | Shared `BesiaCAD Tools → <PanelName>` across add-ins (TBD) |
| Installer            | Inno Setup 6, per-machine (XML config lives in `ProgramData`) |
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
  `C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\`.
- **`net48` only.** DraftSight 2026 still loads add-ins through the COM bridge
  to .NET Framework. No .NET 6/8 equivalent yet. This is the opposite of the
  Revit story.
- **Per-machine install.** Unlike Revit's `%AppData%\Autodesk\Revit\Addins\`
  (per-user), DraftSight's add-in config folder is
  `C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\` — a per-machine
  location. The installer needs admin / UAC.

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
  - `redist\` — the DLLs the csproj references
- If `APISDK\` is missing, the installed tier doesn't include it. Contact a
  Dassault VAR — sometimes the SDK ships separately even for paid tiers.

### 2b. Visual Studio

- **Visual Studio 2022 Community** (free) or higher.
- Workloads required: `.NET desktop development`.
- After SDK install, verify: `File → New → Project` shows a "DraftSight
  Add-In" template. If not, the SDK template wasn't registered — re-run the
  DraftSight installer with "Repair" or copy the template from
  `APISDK\templates\` manually.

### 2c. Reference DLLs

From `C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\`:

- `DraftSight.Interop.dsAutomation.dll` — the main API (drawings, entities,
  commands, settings)
- `DraftSight.Interop.dsCommonAPIs.dll` — shared types, enums

Reference both as:

```xml
<Reference Include="DraftSight.Interop.dsAutomation">
  <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\DraftSight.Interop.dsAutomation.dll</HintPath>
  <Private>False</Private>
  <EmbedInteropTypes>True</EmbedInteropTypes>
</Reference>
```

**`EmbedInteropTypes=True` matters.** It embeds the interop types into the
assembly so the interop DLLs don't need to ship separately. Without it, the
add-in breaks on machines where the SDK lives at a non-default path.

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
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>CanvasCovers</RootNamespace>          <!-- CHANGE -->
    <AssemblyName>CanvasCovers</AssemblyName>            <!-- CHANGE -->
    <UseWPF>true</UseWPF>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>

    <!-- COM hosting: required for DraftSight to activate the add-in -->
    <EnableComHosting>true</EnableComHosting>
    <RegisterForComInterop>true</RegisterForComInterop>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="DraftSight.Interop.dsAutomation">
      <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\DraftSight.Interop.dsAutomation.dll</HintPath>
      <Private>False</Private>
      <EmbedInteropTypes>True</EmbedInteropTypes>
    </Reference>
    <Reference Include="DraftSight.Interop.dsCommonAPIs">
      <HintPath>C:\Program Files\Dassault Systemes\DraftSight\APISDK\redist\DraftSight.Interop.dsCommonAPIs.dll</HintPath>
      <Private>False</Private>
      <EmbedInteropTypes>True</EmbedInteropTypes>
    </Reference>
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
- `EnableComHosting` + `RegisterForComInterop` — Revit doesn't need these.
- `<UseWPF>` works the same.

---

## 5. The XML config file (DraftSight's equivalent of `.addin`)

Lives at:
`C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\<AddinName>.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<addin version="2026" startup="1" name="CanvasCovers" premium-only="0">
  <com clsid="{REPLACE-WITH-YOUR-GUID}" />
</addin>
```

**Field meanings:**
- `version` — DraftSight major version. For DS 2026, use `2026`. For
  multi-version support, ship one XML per target version.
- `startup="1"` — load on every DraftSight launch. `"0"` = manual load only.
- `name` — display name in DraftSight's add-in manager.
- `premium-only="0"` — set to `1` only if the add-in is intended for Premium
  licenses only.
- `clsid` — must match the `[Guid("...")]` attribute on the `App` class.
  **Generate a fresh GUID per add-in.** Two add-ins with the same GUID will
  collide.

---

## 6. `App.cs` — the COM-visible entry class

```csharp
using System;
using System.Runtime.InteropServices;
using DraftSight.Interop.dsAutomation;

namespace CanvasCovers
{
    [ComVisible(true)]
    [Guid("REPLACE-WITH-YOUR-GUID")]
    [ProgId("CanvasCovers.App")]
    [ClassInterface(ClassInterfaceType.None)]
    public class App : IDraftSightAddIn
    {
        private DraftSightApplication _app;
        private int _cookie;

        public bool ConnectToDraftSight(DraftSightApplication application, int cookie)
        {
            _app = application;
            _cookie = cookie;

            try
            {
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
            _app = null;
            return true;
        }

        private void RegisterRibbonButton()
        {
            // Ribbon registration — exact API surface verified against the
            // SDK sample `Sample_CSharp_AddIn`. Pattern:
            //   GetCommandManager() → AddTab → AddPanel → AddCommandItem
            // Routes clicks to a registered command name.
        }
    }
}
```

**Key differences from Revit's `IExternalApplication`:**
- COM attributes (`[ComVisible]`, `[Guid]`, `[ProgId]`, `[ClassInterface]`)
  are mandatory, not decorative. Forget any of these and the load silently
  fails.
- `ConnectToDraftSight` returns `bool` — not Revit's `Result` enum.
- The `cookie` parameter is a session token DraftSight gives; pass it back
  when invoking certain APIs.
- The `MessageBox` fallback is critical during development — DraftSight will
  *silently* refuse to load a broken add-in unless errors are surfaced
  explicitly.

---

## 7. The dev loop (single most important thing to internalize)

1. Edit C# in Visual Studio (or Cursor / Claude Code).
2. Build (`Ctrl+Shift+B` or `dotnet build`) → produces
   `bin\Release\<AddinName>.dll` + `<AddinName>.comhost.dll`.
3. **Close DraftSight** if running. The DLL is locked while the host process
   is alive — same problem as Revit.
4. Copy outputs to the install location (a post-build event automates this).
5. Open DraftSight. Verify the ribbon button appears.
6. Click → test.

**Post-build copy script (csproj snippet):**

```xml
<Target Name="DeployToDraftSight" AfterTargets="Build">
  <PropertyGroup>
    <DeployDir>C:\BesiaCAD\$(AssemblyName)\</DeployDir>
  </PropertyGroup>
  <MakeDir Directories="$(DeployDir)" />
  <Copy SourceFiles="$(TargetDir)$(AssemblyName).dll;$(TargetDir)$(AssemblyName).comhost.dll"
        DestinationFolder="$(DeployDir)"
        SkipUnchangedFiles="true" />
</Target>
```

The XML config file pointing at `$(DeployDir)` is placed manually once, then
left alone.

---

## 8. Inno Setup script (when ready to ship)

Deferred. When productising:

1. **`PrivilegesRequired=admin`** (not `lowest`). The XML config folder
   `C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\` is per-machine.
2. **Register COM during install.** Either `regsvr32` on the `.comhost.dll`
   or `RegAsm.exe` in a `[Run]` block. Inno can do this via:
   `Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\<Addin>.comhost.dll"""`

Skeleton to expand later:

```inno
[Setup]
AppId={{NEW-GUID-HERE}
AppName=BesiaCAD Canvas Covers
AppVersion=1.0.0
AppPublisher=BesiaBIM
DefaultDirName={pf}\BesiaCAD\CanvasCovers
PrivilegesRequired=admin
OutputBaseFilename=BesiaCAD-CanvasCovers-Setup-1.0.0
```

---

## 9. Gotchas (the ones that will bite)

- **The free DraftSight tier has no SDK.** Always confirm tier before scoping.
- **DraftSight must be closed during build/deploy.** Same locked-file problem
  as Revit. Plan dev loop accordingly.
- **CLSID conflicts.** Reusing a GUID across two add-ins makes the second
  silently fail to load. Generate fresh per add-in, write it into both the
  `[Guid]` attribute and the XML.
- **COM registration fails silently.** If `comhost.dll` isn't registered or
  the path in the XML is wrong, DraftSight just doesn't load the add-in — no
  error, no log. Use the SDK's add-in manager UI to verify load state.
- **`EmbedInteropTypes=True` matters.** Without it, deployed add-ins break on
  machines where the DraftSight SDK lives at a non-default path.
- **`net48` only.** Don't attempt .NET 6/8 — the COM bridge doesn't support
  it for DraftSight 2026.
- **AppId is forever.** Same warning as Revit — pick the Inno `AppId` GUID
  once, never change it.
- **Surface load errors with MessageBox during dev.** A `try/catch` around
  `ConnectToDraftSight`'s body with `MessageBox.Show(ex.Message)` is the
  single most useful debugging aid. Remove only when shipping.
- **Don't develop in `Program Files`.** Requires admin to save and breaks
  Visual Studio's hot reload. Always copy samples to `C:\Dev\...` first.

---

## 10. Verification chain (for diagnosing "my add-in won't load")

Run in order. Stop at the first failure — that's the bug.

1. **Does the SDK folder exist?**
   `dir "C:\Program Files\Dassault Systemes\DraftSight\APISDK"`
   If no, tier mismatch.
2. **Does the build produce both DLLs?**
   After `dotnet build`, check `bin\Release\net48\` for
   `<AddinName>.dll` AND `<AddinName>.comhost.dll`.
3. **Is COM registered?**
   Run `regsvr32 <path>\<AddinName>.comhost.dll` manually — should succeed
   silently. Failure = COM hosting misconfigured in csproj.
4. **Does the XML exist in the right folder?**
   `dir "C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\"` — the
   XML should be listed.
5. **Does the XML's CLSID match the assembly's `[Guid]`?**
   Diff them by eye. One character wrong = silent failure.
6. **Does DraftSight see it?**
   Open DraftSight → Tools → Add-Ins (menu name varies by version). The
   add-in should appear in the list, even if disabled.
7. **Does the ribbon button appear?**
   If add-in is loaded but no button, the error is inside
   `ConnectToDraftSight` — that's where the MessageBox catches it.

---

## 11. Reference samples to study

From `C:\Program Files\Dassault Systemes\DraftSight\APISDK\samples\`:

- **`Sample_CSharp_AddIn`** — the canonical hello-world. Start here.
- **`Sample_CSharp_RibbonCustomization`** — ribbon button + panel registration.
- **`Sample_CSharp_DrawingEntities`** — creating lines, circles, polylines.
- **`Sample_CSharp_LayerManagement`** — layer creation and assignment.

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
