# Installer

Inno Setup 6 script + build pipeline for the BesiaCAD Canvas Covers add-in.
Primary install method — preferred over the PowerShell `scripts\deploy-*.ps1`
flow for both dev iteration and shipping.

## Prerequisites

- **Inno Setup 6** — download from https://jrsoftware.org/isdl.php. The
  default install path is `C:\Program Files (x86)\Inno Setup 6\`; the build
  script auto-detects it there.
- **.NET SDK** — to compile the add-in itself (any recent SDK with net48
  targeting pack works).
- **DraftSight closed** — the add-in DLL is locked while the host is running.
  `build.ps1` refuses to run if `DraftSight.exe` is alive.

## Build

```powershell
.\Installer\build.ps1
```

Produces `Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe`.

## Install / upgrade

Double-click the EXE. UAC prompts for admin (per CLAUDE.md §8 — the
add-in config XML lives under per-machine `ProgramData`).

The installer:

1. Drops the add-in payload at `C:\Program Files\BesiaCAD\CanvasCovers\`
   (Inno's `{commonpf64}\BesiaCAD\CanvasCovers`, locked via `DisableDirPage=yes`).
2. Rewrites the `@@INSTALLDIR@@` placeholder in the deployed `CanvasCovers.xml`
   to the real install path, so the Add-Ins manager icon resolves correctly
   regardless of install location.
3. Copies the rewritten `CanvasCovers.xml` to
   `C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\`.
4. Registers `CanvasCovers.dll` with 64-bit RegAsm (`/codebase`).
5. Closes DraftSight if open (set up in `[Setup] CloseApplications=force`).
6. **Upgrade path:** on re-install, `PrepareToInstall` silently runs the
   previous version's uninstaller first, so the install dir can change
   between versions without orphaning files at the old location.

Open DraftSight after install → Tools → Add-Ins → tick CanvasCovers. The XML
ships with `startup="0"`, so first activation is manual on purpose — verify it
loads cleanly before flipping startup mode.

## Uninstall

Settings → Apps → *BesiaCAD Canvas Covers* → Uninstall. The uninstall:

- Unregisters the DLL via `RegAsm /unregister`.
- Removes the XML from `addinConfigs`.
- Deletes the install folder (and the `Resources\` subfolder).

## Pinned identifiers (do not change)

| Field      | Value                                  | Notes                                                                                         |
| ---------- | -------------------------------------- | --------------------------------------------------------------------------------------------- |
| AppId      | `A27F4037-4A3F-4706-B839-B88836F132FD` | Permanent. Drives upgrade detection.                                                          |
| CLSID      | `aa497758-3d06-46b7-9f75-7a8f2fffed7c` | Must match `[Guid]` on `App` class + the `<com clsid="...">` element in `CanvasCovers.xml`. |
| Install dir | `{commonpf64}\BesiaCAD\CanvasCovers` (= `C:\Program Files\BesiaCAD\CanvasCovers`) | Locked via `DisableDirPage=yes`. XML's `@@INSTALLDIR@@` placeholder is rewritten to `{app}` after install. |

## Startup-crash recovery

If DraftSight refuses to launch after install, the normal uninstaller can't
run because DS can't start. Use:

```powershell
.\scripts\rollback-canvascovers.ps1
```

This runs `RegAsm /unregister` against the installed DLL and renames the
ProgramData XML to `.disabled` so DraftSight skips it on the next launch.
Open DraftSight, confirm it loads, then run the normal uninstaller from
Settings → Apps to clean up the rest.

## Versioning

Bump `MyAppVersion` in `CanvasCovers.iss` for each release. The output EXE
filename includes the version. Inno's AppId remains constant so existing
installs upgrade cleanly.
