; ---------------------------------------------------------------------------
; BesiaCAD Canvas Covers -- Inno Setup installer script
;
; AppId is permanent -- never change it (CLAUDE.md section 1).
;
; The CanvasCovers.xml bitmap attribute hardcodes a path; the [Code] section
; rewrites it to {app} after install so the file can move freely.
;
; Requires Inno Setup 6.x. Build via Installer\build.ps1.
; ---------------------------------------------------------------------------

#define MyAppName          "BesiaCAD Canvas Covers"
#define MyAppVersion       "1.0.1"
#define MyAppPublisher     "BesiaCAD"
#define MyAppURL           "https://seboticai.com"
#define MyAddinName        "CanvasCovers"
#define MyAddinDll         "CanvasCovers.dll"
#define MyAddinXml         "CanvasCovers.xml"
#define MyRegAsm           "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
#define MyAddinConfigsDir  "{commonappdata}\Dassault Systemes\DraftSight\addinConfigs"

; Placeholder in the source XML's bitmap path. The [Code] procedure below
; replaces it with {app} after install. Must match the literal placeholder
; in CanvasCovers\CanvasCovers.xml.
#define MyXmlPathToken     "@@INSTALLDIR@@"

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
DefaultDirName={commonpf64}\BesiaCAD\{#MyAddinName}
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
; Always use the current DefaultDirName, never the previous install's location.
; Required so the 1.0.0 -> 1.0.1 move (C:\BesiaCAD -> Program Files\BesiaCAD)
; actually relocates instead of reusing the old path.
UsePreviousAppDir=no

[Files]
Source: "{#PayloadDir}\{#MyAddinDll}";                       DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\DraftSight.Interop.dsAddin.dll";      DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\DraftSight.Interop.dsAutomation.dll"; DestDir: "{app}";              Flags: ignoreversion
Source: "{#PayloadDir}\Resources\*";                         DestDir: "{app}\Resources";    Flags: ignoreversion recursesubdirs createallsubdirs

; XML drops into both {app} (transparency / reference) and the per-machine
; addinConfigs folder DraftSight actually reads. Both copies get their bitmap
; path rewritten after install by RewriteBitmapPath.
Source: "{#PayloadDir}\{#MyAddinXml}";                       DestDir: "{app}";                DestName: "{#MyAddinXml}"; Flags: ignoreversion; AfterInstall: RewriteBitmapPath
Source: "{#PayloadDir}\{#MyAddinXml}";                       DestDir: "{#MyAddinConfigsDir}"; DestName: "{#MyAddinXml}"; Flags: ignoreversion; AfterInstall: RewriteBitmapPath

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

[Code]
// Replaces the dev-time bitmap path inside the just-installed CanvasCovers.xml
// with the real install dir, so the Add-Ins manager icon resolves regardless
// of where the user installed (or future moves of the install dir).
procedure RewriteBitmapPath();
var
  ContentBytes: AnsiString;
  ContentText: String;
begin
  // LoadStringFromFile returns AnsiString (raw bytes); StringChangeEx needs
  // Unicode String. The XML is pure ASCII so the round-trip is lossless.
  if not LoadStringFromFile(CurrentFileName, ContentBytes) then
    Exit;
  ContentText := String(ContentBytes);
  if StringChangeEx(ContentText, '{#MyXmlPathToken}', ExpandConstant('{app}'), True) > 0 then
  begin
    ContentBytes := AnsiString(ContentText);
    SaveStringToFile(CurrentFileName, ContentBytes, False);
  end;
end;

// Reads UninstallString from the Inno-registered uninstall key for this AppId.
// HKLM64 first (we're a 64-bit installer); falls back to HKLM for safety.
function GetPreviousUninstallString(): String;
var
  Key: String;
begin
  Result := '';
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#SetupSetting("AppId")}_is1';
  if RegQueryStringValue(HKLM64, Key, 'UninstallString', Result) then Exit;
  RegQueryStringValue(HKLM, Key, 'UninstallString', Result);
end;

// Runs the previous version's uninstaller silently before this one installs.
// Necessary so the 1.0.0 -> 1.0.1 install-dir move does not orphan files at
// C:\BesiaCAD\CanvasCovers. Same-path upgrades (1.0.1 -> 1.0.2) also benefit
// by leaving no stale files behind.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Cmd: String;
  ResultCode: Integer;
begin
  Result := '';
  Cmd := GetPreviousUninstallString();
  if Cmd = '' then
    Exit;
  Cmd := RemoveQuotes(Cmd);
  if not Exec(Cmd, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE,
              ewWaitUntilTerminated, ResultCode) then
    Result := 'Failed to uninstall the previous version of {#MyAppName} (exit code '
              + IntToStr(ResultCode) + '). Uninstall it manually via Settings -> Apps and re-run this installer.';
end;
