; Inno Setup script for NightLock Guard.
; Produces a single, friendly NightLockGuard-Setup.exe: the user double-clicks it, types a parent
; password and a lock schedule, and the installer copies the (prebuilt, self-contained) binaries,
; configures them, and registers the Windows service + logon task. No PowerShell or .NET SDK needed
; on the target machine.
;
; Build it with scripts\build-installer.ps1 (which publishes the apps into installer\payload first).

#define MyAppName "NightLock Guard"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "k1lumiyoung"

[Setup]
AppId={{8B6C2F2E-7E2D-4C0A-9A2E-9B5E6E2D3C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\NightLockGuard
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=NightLockGuard-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupIconFile=..\assets\moon.ico
UninstallDisplayIcon={app}\NightLock.Helper.exe

[Files]
Source: "payload\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "post-install.ps1"; DestDir: "{app}"; Flags: ignoreversion

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -Command ""sc.exe stop NightLockGuard; sc.exe delete NightLockGuard; schtasks /delete /tn NightLockGuardHelper /f; Get-Process NightLock.Helper,NightLock.Service,NightLock.Admin -ErrorAction SilentlyContinue | Stop-Process -Force"""; Flags: runhidden; RunOnceId: "NightLockRemove"

[Code]
var
  PwPage: TInputQueryWizardPage;
  SchedPage: TInputQueryWizardPage;

function ValidTime(S: String): Boolean;
var
  H, M: Integer;
begin
  Result := False;
  if Length(S) <> 5 then exit;
  if S[3] <> ':' then exit;
  H := StrToIntDef(Copy(S, 1, 2), -1);
  M := StrToIntDef(Copy(S, 4, 2), -1);
  Result := (H >= 0) and (H <= 23) and (M >= 0) and (M <= 59);
end;

procedure InitializeWizard;
begin
  PwPage := CreateInputQueryPage(wpSelectDir,
    'Parent password',
    'Set the NightLock parent password',
    'This password unlocks the computer during locked hours. It is NOT your Windows password.');
  PwPage.Add('Password:', True);
  PwPage.Add('Confirm password:', True);

  SchedPage := CreateInputQueryPage(PwPage.ID,
    'Lock schedule',
    'When should the computer be locked?',
    'Use 24-hour HH:mm. You can change all of this later in the settings panel.');
  SchedPage.Add('Lock starts at (HH:mm):', False);
  SchedPage.Add('Lock ends at (HH:mm):', False);
  SchedPage.Add('Password-unlock minutes (1-240):', False);
  SchedPage.Values[0] := '23:00';
  SchedPage.Values[1] := '08:00';
  SchedPage.Values[2] := '30';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Minutes: Integer;
begin
  Result := True;
  if CurPageID = PwPage.ID then
  begin
    if Length(PwPage.Values[0]) < 4 then
    begin
      MsgBox('The parent password must be at least 4 characters.', mbError, MB_OK);
      Result := False;
    end
    else if PwPage.Values[0] <> PwPage.Values[1] then
    begin
      MsgBox('The passwords do not match.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = SchedPage.ID then
  begin
    if (not ValidTime(SchedPage.Values[0])) or (not ValidTime(SchedPage.Values[1])) then
    begin
      MsgBox('Times must be in 24-hour HH:mm format, e.g. 23:30.', mbError, MB_OK);
      Result := False;
    end
    else if SchedPage.Values[0] = SchedPage.Values[1] then
    begin
      MsgBox('Lock start and end cannot be the same time.', mbError, MB_OK);
      Result := False;
    end
    else
    begin
      Minutes := StrToIntDef(SchedPage.Values[2], -1);
      if (Minutes < 1) or (Minutes > 240) then
      begin
        MsgBox('Password-unlock minutes must be between 1 and 240.', mbError, MB_OK);
        Result := False;
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Stop a previous install before overwriting its files, so the service can be recreated
  // cleanly and no running .exe is locked during the copy.
  Exec('powershell.exe',
    '-ExecutionPolicy Bypass -NoProfile -Command "' +
    'sc.exe stop NightLockGuard; ' +
    'schtasks /end /tn NightLockGuardHelper; ' +
    'Get-Process NightLock.Helper,NightLock.Admin,NightLock.Service -ErrorAction SilentlyContinue | Stop-Process -Force"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  PwFile, Params: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    PwFile := ExpandConstant('{tmp}\nlpw.txt');
    SaveStringToFile(PwFile, PwPage.Values[0], False);
    Params :=
      '-ExecutionPolicy Bypass -NoProfile -File "' + ExpandConstant('{app}\post-install.ps1') + '"' +
      ' -InstallDir "' + ExpandConstant('{app}') + '"' +
      ' -DataDir "' + ExpandConstant('{commonappdata}\NightLockGuard') + '"' +
      ' -PasswordFile "' + PwFile + '"' +
      ' -Start "' + SchedPage.Values[0] + '"' +
      ' -End "' + SchedPage.Values[1] + '"' +
      ' -Minutes ' + SchedPage.Values[2];
    if not Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      MsgBox('Could not run the post-install configuration step.', mbError, MB_OK)
    else if ResultCode <> 0 then
      MsgBox('Post-install configuration reported an error (code ' + IntToStr(ResultCode) + ').'
        + #13#10 + 'You can re-run it from C:\Program Files\NightLockGuard.', mbError, MB_OK);
  end;
end;
