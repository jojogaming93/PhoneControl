#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif
#ifndef ToolEntries
  #error ToolEntries is required
#endif
#ifndef AppVersion
  #define AppVersion "1.0.7"
#endif

[Setup]
AppId={{CF1762E2-784A-49DA-AF70-A6753131BE40}
AppName=PhoneControl
AppVersion={#AppVersion}
AppPublisher=jojogaming93
AppPublisherURL=https://github.com/jojogaming93
AppSupportURL=https://github.com/jojogaming93/PhoneControl/issues
DefaultDirName={localappdata}\Programs\PhoneControl
DefaultGroupName=PhoneControl
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.26100
OutputDir={#ReleaseDir}
OutputBaseFilename=PhoneControl-Payload-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=yes
DisableStartupPrompt=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
UninstallDisplayIcon={app}\PhoneControl.exe
SetupIconFile=..\assets\branding\phonecontrol.ico
CloseApplications=yes
CloseApplicationsFilter=PhoneControl.exe
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=PhoneControl Windows installer
VersionInfoCopyright=Copyright (c) 2026 jojogaming93

[InstallDelete]
; Remove obsolete app-owned documentation captures/profiles on upgrade.
; User mappings live separately under {localappdata}\PhoneControl.
Type: files; Name: "{app}\docs\images\*.png"
Type: files; Name: "{app}\docs\profiles\*.json"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,self-test-results.txt,release-manifest.json,\tools\scrcpy\*"
#include ToolEntries

[Icons]
Name: "{autoprograms}\PhoneControl"; Filename: "{app}\PhoneControl.exe"; WorkingDir: "{app}"; Check: ShouldCreateShortcuts
Name: "{autoprograms}\PhoneControl guide"; Filename: "{app}\docs\index.html"; Check: ShouldCreateShortcuts
Name: "{autodesktop}\PhoneControl"; Filename: "{app}\PhoneControl.exe"; WorkingDir: "{app}"; Check: ShouldCreateShortcuts

[Run]
Filename: "{app}\PhoneControl.exe"; Description: "Open PhoneControl"; Flags: nowait; Check: ShouldLaunch

[Code]
function ShouldUpdateTool(const Name, ExpectedHash: String): Boolean;
var InstalledFile: String;
begin
  InstalledFile := ExpandConstant('{app}\tools\scrcpy\') + Name;
  Result := True;
  if FileExists(InstalledFile) then begin
    try
      Result := CompareText(GetSHA256OfFile(InstalledFile), ExpectedHash) <> 0;
      if not Result then Log('Keeping identical bundled tool: ' + Name);
    except
      Log('Could not verify existing tool; attempting repair: ' + Name);
    end;
  end;
end;

function CmdLineParamExists(const Value: String): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then begin
      Result := True;
      Exit;
    end;
end;

function ShouldCreateShortcuts: Boolean;
begin
  Result := not CmdLineParamExists('/NOSHORTCUTS');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // Explicitly skip Ready too: DisableReadyPage alone intentionally shows it
  // when there were no preceding pages. Keep installation progress visible.
  Result := (PageID <> wpPreparing) and (PageID <> wpInstalling);
end;

function ShouldLaunch: Boolean;
begin
  // Automated deployments/tests should not open an interactive app.
  Result := CmdLineParamExists('/AUTOLAUNCH') and not CmdLineParamExists('/NOLAUNCH');
end;
