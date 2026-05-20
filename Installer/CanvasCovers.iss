; ---------------------------------------------------------------------------
; BesiaCAD Canvas Covers — Inno Setup installer script
;
; AppId is permanent — never change it (CLAUDE.md §1).
; Install dir is fixed at C:\BesiaCAD\CanvasCovers because CanvasCovers.xml
; hardcodes the ribbon button bitmap path relative to that folder.
;
; Requires Inno Setup 6.x. Build via Installer\build.ps1.
; ---------------------------------------------------------------------------

#define MyAppName          "BesiaCAD Canvas Covers"
#define MyAppVersion       "1.0.0"
#define MyAppPublisher     "BesiaCAD"
#define MyAppURL           "https://seboticai.com"
#define MyAddinName        "CanvasCovers"
#define MyAddinDll         "CanvasCovers.dll"
#define MyAddinXml         "CanvasCovers.xml"
#define MyRegAsm           "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
#define MyAddinConfigsDir  "{commonappdata}\Dassault Systemes\DraftSight\addinConfigs"

; PayloadDir is overridden on the command line by build.ps1 to point at the
; freshly-built bin\Release\net48 folder. Default makes the script runnable
; from the IDE for quick sanity checks.
#ifndef PayloadDir
  #define PayloadDir "..\CanvasCovers\bin\Release\net48"
#endif

[Setup]
AppId={{A27F4037-4A3F-4706-B839-B88836F132FD}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName=C:\BesiaCAD\{#MyAddinName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputDir=Output
OutputBaseFilename=BesiaCAD-{#MyAddinName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
CloseApplications=force
CloseApplicationsFilter=DraftSight.exe

[Files]
Source: "{#PayloadDir}\{#MyAddinDll}";                       DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\DraftSight.Interop.dsAddin.dll";      DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\DraftSight.Interop.dsAutomation.dll"; DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\Resources\*";                         DestDir: "{app}\Resources";    Flags: ignoreversion recursesubdirs createallsubdirs

; The XML config gets dropped into the per-machine addinConfigs folder.
; Same source XML is also kept alongside the DLL for transparency.
Source: "{#PayloadDir}\{#MyAddinXml}";                       DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\{#MyAddinXml}";                       DestDir: "{#MyAddinConfigsDir}"; Flags: ignoreversion

[Run]
Filename: "{#MyRegAsm}"; \
  Parameters: "/codebase ""{app}\{#MyAddinDll}"""; \
  StatusMsg: "Registering CanvasCovers with .NET COM (RegAsm)..."; \
  Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{#MyRegAsm}"; \
  Parameters: "/unregister ""{app}\{#MyAddinDll}"""; \
  RunOnceId: "UnregisterCanvasCoversDll"; \
  Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files;          Name: "{#MyAddinConfigsDir}\{#MyAddinXml}"
Type: filesandordirs; Name: "{app}\Resources"
Type: dirifempty;     Name: "{app}"
