; Adapted from WPods v0.4.2, GPL-3.0. See CREDITS.md.
; Experimental companion fork: no boot-security changes, no automatic updates.
; WPods installer (Inno Setup 6). Built by ..\build.ps1, which passes /DAppVersion.
#ifndef AppVersion
  #error AppVersion is not defined: build with build.ps1
#endif
; Fetched by ..\native\fetch-usbip.ps1 into ..\.build\deps.
#define UsbipSetup "USBip-0.9.8.1-x64.exe"
#ifndef Payload
#error Payload must point to the checked release payload
#endif
#ifndef UsbipFile
#error UsbipFile must point to the hash-checked original dependency
#endif

[Setup]
AppId={{EB653D78-A99D-4E7D-A72C-D766C6F38C85}
AppName=AirPods Companion
AppVersion={#AppVersion}
AppPublisher=WendelinProfiler; Bluetooth engine by WPods contributors
AppCopyright=Copyright (C) 2026 fpndev (rimjoeh) <rimjoeh@gmail.com>
LicenseFile=..\LICENSE
InfoBeforeFile=ALPHA.txt
VersionInfoVersion={#AppVersion}
UninstallDisplayName=AirPods Companion
UninstallDisplayIcon={app}\AirPodsCompanion.exe
DefaultDirName={autopf}\AirPods Companion
DisableProgramGroupPage=yes
DisableDirPage=yes
UsePreviousAppDir=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Restart Manager closes only the app and never restarts it (it would run elevated). The service keeps running
; through the copy (see MoveServiceFilesAside); stopped by Restart Manager, it would leave the adapter on WinUSB.
CloseApplications=yes
CloseApplicationsFilter=AirPodsCompanion.exe,IslandAirPodsHost.exe
RestartApplications=no
RestartIfNeededByRun=no
SetupLogging=yes
OutputDir=..\out\release
OutputBaseFilename=AirPodsCompanion-Setup

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: startup; Description: "Start AirPods Companion when I sign in"; Flags: unchecked

[InstallDelete]
; Service files the previous update moved aside while the service used them (see MoveServiceFilesAside).
Type: files; Name: "{app}\*.old-*"
; Left by an older service version that rotated its log; nothing writes it any more.
Type: files; Name: "{commonappdata}\WPods\logs\service.1.log"
; Folders fully owned by the app: drop files a previous version had (e.g. a removed locale would stay selectable).
Type: filesandordirs; Name: "{app}\web"
Type: filesandordirs; Name: "{app}\locales"
Type: filesandordirs; Name: "{app}\licenses"

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; Extracted to {tmp} and run by PrepareToInstall, before any file is copied.
Source: "{#UsbipFile}"; Flags: dontcopy

[UninstallDelete]
Type: files; Name: "{app}\drivers\installed.txt"
; Updates the service downloaded.
Type: filesandordirs; Name: "{commonappdata}\WPods\updates"

[Icons]
Name: "{autoprograms}\AirPods Companion"; Filename: "{app}\AirPodsCompanion.exe"
Name: "{autodesktop}\AirPods Companion"; Filename: "{app}\AirPodsCompanion.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AirPodsCompanion.exe"; Parameters: "--register-startup"; Tasks: startup; Flags: runasoriginaluser waituntilterminated
Filename: "{app}\AirPodsCompanion.exe"; Description: "Open AirPods Companion"; Flags: postinstall runasoriginaluser nowait skipifsilent

[UninstallRun]
; Runs before the service and usbip-win2 are removed (usPostUninstall); the RunOnceId matches the
; entry of WPods 0.2.x, so it runs once.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\drivers\drivers.ps1"" -Uninstall"; Flags: runhidden waituntilterminated logoutput; RunOnceId: "Drivers"

[Code]
const
  DotNetUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';
  DotNetFile = 'windowsdesktop-runtime-win-x64.exe';
  WebView2Url = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';
  WebView2File = 'MicrosoftEdgeWebview2Setup.exe';
  WebView2Key = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  UsbipSetup = '{#UsbipSetup}';
  ServiceName = 'WPodsService';
  WPodsKey = 'SOFTWARE\WPods';
  // Exit code of RunMicrosoftInstaller when the file is not validly signed by Microsoft Corporation.
  NotMicrosoftSigned = 22352;


var
  DownloadPage: TDownloadWizardPage;
  BluetoothPage: TOutputMsgWizardPage;
  BluetoothConsent: TNewCheckBox;
  AdapterChecked: Boolean;
  RemoveSettings: Boolean;
  FirstInstall: Boolean;
  // Uninstall: the Bluetooth adapter is back on Windows' own driver, so usbip-win2 may go.
  AdapterRestored: Boolean;
  // Uninstall: usbip-win2 was installed by Setup but stays, so its marker must stay too.
  UsbipKept: Boolean;
  // Uninstall: this run removed usbip-win2 (and with it the virtual adapter's device nodes).
  UsbipRemoved: Boolean;
  // Uninstall: the service files were kept aside, so the service is removed at the end (usPostUninstall).
  ServiceKeptForLater: Boolean;
  // This run installed usbip-win2, which works only after a restart.
  UsbipJustInstalled: Boolean;
  // The stamp of the service files MoveServiceFilesAside renamed ('' = none moved, or moved back).
  MovedAsideStamp: String;
  // The files are installed (ssPostInstall reached): no rollback can take the new service files away any more.
  FilesInstalled: Boolean;
  // The Select Tasks page was shown (it shows the current auto-update choice, see CurPageChanged).
  TasksPageShown: Boolean;

function InitializeSetup: Boolean;
var ExistingPath: String;
begin
  Result := False;
  if WizardSilent and (ExpandConstant('{param:ACCEPTBLUETOOTH|0}') <> '1') then begin
    Log('Silent install requires explicit /ACCEPTBLUETOOTH=1 consent.'); Exit;
  end;
  if RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Services\WPodsService', 'ImagePath', ExistingPath) then
    if Pos(Lowercase(ExpandConstant('{autopf}\AirPods Companion\')), Lowercase(ExistingPath)) = 0 then begin
      SuppressibleMsgBox('Another WPods service is installed. Remove it with its own uninstaller before installing this alpha. Your existing service has not been changed.', mbError, MB_OK, IDOK);
      Exit;
    end;
  // Read once, before this install registers itself: an update must not touch the user's startup choice.
  FirstInstall := not RegKeyExists(HKLM64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{EB653D78-A99D-4E7D-A72C-D766C6F38C85}_is1');
  Result := True;
end;

// Startup is owned by the app; Setup offers it only on a first install.
function IsFirstInstall: Boolean;
begin
  Result := FirstInstall;
end;

// Started silently as SYSTEM by the WPods service to install an update; the service reopens the app.
function FromService: Boolean;
begin
  Result := ExpandConstant('{param:FROMSERVICE|0}') = '1';
end;


function PowerShell: String;
begin
  Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
end;

// PowerShell single-quoted string literal.
function PSString(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '''', '''''', True);
  Result := '''' + Result + '''';
end;

// 'Setup' or 'Uninstall', for messages shared by both.
function Who: String;
begin
  if IsUninstaller then
    Result := 'Uninstall'
  else
    Result := 'Setup';
end;

function WPodsFlag(const Name: String): Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, WPodsKey, Name, Value) and (Value = 1);
end;

function DotNetInstalled: Boolean;
var
  Root: String;
  FindRec: TFindRec;
begin
  Result := False;
  if not RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', Root) or (Root = '') then begin
    Root := ExpandConstant('{commonpf64}\dotnet');
  end;
  if FindFirst(AddBackslash(Root) + 'shared\Microsoft.WindowsDesktop.App\10.*', FindRec) then begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and (Pos('-', FindRec.Name) = 0) then begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function WebView2InstalledIn(RootKey: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, WebView2Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

function WebView2Installed: Boolean;
begin
  // HKLM32 = HKLM\SOFTWARE\WOW6432Node (per-machine install); HKCU = per-user install.
  Result := WebView2InstalledIn(HKLM32) or WebView2InstalledIn(HKCU);
end;

function UsbipInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf64}\USBip\usbip.exe'));
end;

// usbip-win2 installed by an earlier run still waits for a restart (the service clears the value when it starts).
function ServiceWaitsForRestart: Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\WPods\Service', 'WaitsForRestart', Value) and (Value = 1);
end;

function ServiceRegistered: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\' + ServiceName);
end;

// Runs a downloaded installer only if validly signed with O=Microsoft Corporation (.NET's signer is CN=.NET). The
// file is held open, write and delete denied, until the installer exits, so it cannot be swapped after the check.
function RunMicrosoftInstaller(const FileName, Params: String; var ResultCode: Integer): Boolean;
var
  Script: String;
begin
  Script :=
    '$f = [IO.File]::Open(' + PSString(FileName) + ', ''Open'', ''Read'', ''Read''); ' +
    'try { ' +
    '$s = Get-AuthenticodeSignature -LiteralPath ' + PSString(FileName) + '; ' +
    'if ($s.Status -ne ''Valid'' -or ($s.SignerCertificate.SubjectName.Format($true) -split ''\r?\n'') ' +
    '-cnotcontains ''O=Microsoft Corporation'') { exit ' + IntToStr(NotMicrosoftSigned) + ' } ' +
    '$p = [Diagnostics.Process]::Start(' + PSString(FileName) + ', ' + PSString(Params) + '); ' +
    '$p.WaitForExit(); exit $p.ExitCode ' +
    '} finally { $f.Dispose() }';
  Result := Exec(PowerShell, '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Returns '' on success, else the error to show.
function InstallPrerequisite(const Name, FileName, Params: String): String;
var
  Code: Integer;
begin
  DownloadPage.SetText('Installing ' + Name + '...', '');
  if not RunMicrosoftInstaller(ExpandConstant('{tmp}\' + FileName), Params, Code) then
    Result := 'Setup could not verify the ' + Name + ' installer: ' + SysErrorMessage(Code)
  else if Code = NotMicrosoftSigned then
    Result := 'The downloaded ' + Name + ' installer is not signed by Microsoft Corporation. Setup did not run it.'
  else begin
    Log(Format('%s installer exit code: %d', [Name, Code]));
    Result := '';
    if ((FileName = DotNetFile) and not DotNetInstalled) or ((FileName = WebView2File) and not WebView2Installed) then
      Result := Format('%s could not be installed (installer exit code %d).', [Name, Code]);
  end;
end;

// Installs the bundled usbip-win2 and records it for Uninstall. Returns '' on success, else the error to show.
function InstallUsbip: String;
var
  Code, I: Integer;
  LogFile: String;
  Lines: TArrayOfString;
begin
  Result := '';
  DownloadPage.SetText('Preparing Bluetooth...', '');
  DownloadPage.Show;
  try
    ExtractTemporaryFile(UsbipSetup);
    LogFile := ExpandConstant('{tmp}\usbip-setup.log');
    // No desktop shortcut: usbip-win2 is a part of WPods, not an app the user opens.
    if not Exec(ExpandConstant('{tmp}\' + UsbipSetup), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /MERGETASKS="!desktopicon" ' +
      '/LOG="' + LogFile + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := 'Setup could not install a part WPods needs for Bluetooth (usbip-win2): ' + SysErrorMessage(Code) +
        ' Nothing was installed.'
    else begin
      Log(Format('usbip-win2 installer exit code: %d', [Code]));
      if UsbipInstalled then begin
        UsbipJustInstalled := True;
        if not RegWriteDWordValue(HKLM64, WPodsKey, 'UsbipInstalledByWPods', 1) then
          Log('Could not record HKLM64\SOFTWARE\WPods\UsbipInstalledByWPods.');
      end else begin
        // Its log lives in {tmp}, which goes when Setup exits: keep it in this log.
        if LoadStringsFromFile(LogFile, Lines) then
          for I := 0 to GetArrayLength(Lines) - 1 do
            Log('usbip-win2: ' + Lines[I]);
        Result := Format('Setup could not install a part WPods needs for Bluetooth (usbip-win2, error %d). ' +
          'Nothing was installed.', [Code]);
      end;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

// Stops the old service with the adapter kept on WinUSB, so InstallService starts the new one without a driver switch.
procedure StopService;
var
  Code: Integer;
  Exe: String;
begin
  Exe := ExpandConstant('{app}\WPods.Service.exe');
  if not FileExists(Exe) then
    Exit;
  if not ExecAndLogOutput(Exe, '--stop', '', SW_HIDE, ewWaitUntilTerminated, Code, nil) then
    Log('Could not run WPods.Service.exe --stop: ' + SysErrorMessage(Code))
  else if Code <> 0 then
    Log(Format('WPods.Service.exe --stop exit code: %d', [Code]));
end;

// Files the running service keeps open. Files in use can be renamed, so they are moved aside before the copy or
// delete and the service (and Bluetooth) keeps running until the end; if a rename fails, it is stopped first.
function ServiceFiles: TArrayOfString;
begin
  SetArrayLength(Result, 6);
  Result[0] := 'WPods.Service.exe';
  Result[1] := 'WPods.Service.dll';
  Result[2] := 'System.ServiceProcess.ServiceController.dll';
  Result[3] := 'System.Diagnostics.EventLog.dll';
  Result[4] := 'System.Diagnostics.EventLog.Messages.dll';
  // The mic's shared jitter buffer.
  Result[5] := 'wpods_native.dll';
end;

// Puts moved-aside files back where nothing replaced them: after a partial move or a cancelled or failed install.
procedure MoveServiceFilesBack;
var
  Names: TArrayOfString;
  I: Integer;
  Path: String;
begin
  if MovedAsideStamp = '' then
    Exit;
  Names := ServiceFiles;
  for I := 0 to GetArrayLength(Names) - 1 do begin
    Path := ExpandConstant('{app}\') + Names[I];
    if FileExists(Path + '.old-' + MovedAsideStamp) and not FileExists(Path) then
      if RenameFile(Path + '.old-' + MovedAsideStamp, Path) then
        Log('Moved ' + Path + ' back.')
      else
        Log('Could not move ' + Path + ' back.');
  end;
  MovedAsideStamp := '';
end;

// False when a file could not be moved aside; the files already moved are put back then.
function MoveServiceFilesAside: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Path: String;
begin
  Result := True;
  Names := ServiceFiles;
  MovedAsideStamp := GetDateTimeString('yyyymmddhhnnss', #0, #0);
  for I := 0 to GetArrayLength(Names) - 1 do begin
    Path := ExpandConstant('{app}\') + Names[I];
    if FileExists(Path) and not RenameFile(Path, Path + '.old-' + MovedAsideStamp) then begin
      Log('Could not move ' + Path + ' aside.');
      MoveServiceFilesBack;
      Result := False;
      Exit;
    end;
  end;
end;

// Deletes the files moved aside, once the service that used them is gone (retries while its process exits).
procedure DeleteServiceFilesAside;
var
  F: TFindRec;
begin
  if FindFirst(ExpandConstant('{app}\*.old-*'), F) then
    try
      repeat
      begin
        DelayDeleteFile(ExpandConstant('{app}\') + F.Name, 20);
        if FileExists(ExpandConstant('{app}\') + F.Name) then
          Log('Could not delete ' + F.Name);
      end
      until not FindNext(F);
    finally
      FindClose(F);
    end;
end;

// Logs the Bluetooth LE, input and paired classic devices to the Setup log and C:\ProgramData\WPods-bluetooth.txt,
// which is kept after uninstall.
procedure LogBluetoothDevices(const Stage: String);
begin
  Log(Stage);
end;

function AddText(const Page: TWizardPage; const Top: Integer; const Text: String): TNewStaticText;
begin
  Result := TNewStaticText.Create(Page);
  Result.Parent := Page.Surface;
  Result.Top := Top;
  Result.Width := Page.SurfaceWidth;
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.Caption := Text;
  Result.AdjustHeight;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  BluetoothPage := CreateOutputMsgPage(wpLicense, 'Bluetooth', 'What happens to your Bluetooth.',
    'The WPods service routes your USB-connected Bluetooth adapter through the signed USB/IP driver.' + #13#10#13#10 +
    'Bluetooth devices may disconnect briefly. Keep a touchpad or wired keyboard available. Setup may ' +
    'ask you to restart later. It does not change Secure Boot or Windows Test Mode.' + #13#10#13#10 +
    'Uninstall attempts to restore the original Bluetooth driver. Installation and recovery have not ' +
    'been tested with this alpha package.' + #13#10#13#10 +
    'Includes usbip-win2 (c) 2021-2026 Vadym Hrynchyshyn, BSD 2-Clause License');
  BluetoothConsent := TNewCheckBox.Create(BluetoothPage);
  BluetoothConsent.Parent := BluetoothPage.Surface;
  BluetoothConsent.Top := BluetoothPage.SurfaceHeight - ScaleY(42);
  BluetoothConsent.Width := BluetoothPage.SurfaceWidth;
  BluetoothConsent.Height := ScaleY(38);
  BluetoothConsent.Caption := 'Allow Bluetooth service and USB/IP driver setup';
  BluetoothConsent.Checked := False;
  BluetoothPage.MsgLabel.AutoSize := False;
  BluetoothPage.MsgLabel.Height := BluetoothConsent.Top - ScaleY(8);
end;

// The Bluetooth adapter page explains the service, so it is shown until the service is registered.
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
end;

// The PC's Bluetooth adapters: 0 = a USB adapter WPods can use (or none at all), 3 = only adapters not connected over
// USB (UART, SDIO). Same rule as the service (code B3).
function AdapterProblem: Integer;
begin
  if not Exec(PowerShell, '-NoProfile -NonInteractive -Command "' +
    '$r = @(Get-PnpDevice -PresentOnly -Class Bluetooth -ErrorAction Ignore | Where-Object { $_.InstanceId -notmatch ''^BTH'' } | ForEach-Object InstanceId); ' +
    'if (($r.Count -eq 0) -or ($r | Where-Object { $_ -match ''^USB\\VID_'' })) { exit 0 }; exit 3"', '', SW_HIDE,
    ewWaitUntilTerminated, Result) then
    Result := 0;
  Log(Format('Bluetooth adapter check: %d', [Result]));
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Problem: Integer;
  AutoUpdate: Cardinal;
begin
  // An update shows the auto-update choice as it is now (the app may have changed it), not the one Setup remembers.
  if CurPageID = wpFinished then
    if WizardForm.NoRadio.Visible then WizardForm.NoRadio.Checked := True;
  // A warning only: Setup goes on either way (a USB dongle plugged in later works).
  if (CurPageID = BluetoothPage.ID) and not AdapterChecked then begin
    AdapterChecked := True;
    Problem := AdapterProblem;
    if Problem = 3 then
      BluetoothPage.MsgLabel.Caption := 'Warning: your PC''s Bluetooth adapter isn''t connected over USB, so WPods ' +
        'can''t work with it (error B3). A USB Bluetooth adapter, such as a small USB dongle, works.' + #13#10#13#10 +
        BluetoothPage.MsgLabel.Caption;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  NeedDotNet, NeedWebView2: Boolean;
begin
  Result := '';
  NeedDotNet := False; // The matching .NET runtime is bundled self-contained.
  NeedWebView2 := not WebView2Installed;
  if NeedDotNet or NeedWebView2 then begin
    DownloadPage.Clear;
    if NeedDotNet then
      DownloadPage.Add(DotNetUrl, DotNetFile, '');
    if NeedWebView2 then
      DownloadPage.Add(WebView2Url, WebView2File, '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        if DownloadPage.AbortedByUser then
          Result := 'The download was canceled.'
        else
          Result := 'Setup could not download ' + DownloadPage.LastBaseNameOrUrl + ': ' + GetExceptionMessage;
      end;
      if (Result = '') and NeedDotNet then
        Result := InstallPrerequisite('the .NET 10 Desktop Runtime', DotNetFile, '/install /quiet /norestart');
      if (Result = '') and NeedWebView2 then
        Result := InstallPrerequisite('the Microsoft Edge WebView2 Runtime', WebView2File, '/silent /install');
    finally
      DownloadPage.Hide;
    end;
  end;
  if (Result = '') and not UsbipInstalled then
    Result := InstallUsbip;
end;

// Ends WPods.exe from this install folder if Restart Manager did not close it.
procedure StopWPods;
var
  Code: Integer;
begin
  Exec(PowerShell, '-NoProfile -NonInteractive -Command "Get-Process WPods -ErrorAction Ignore | ' +
    'Where-Object Path -eq ' + PSString(ExpandConstant('{app}\IslandAirPodsHost.exe')) + ' | ' +
    'ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null }"', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

// WPods 0.2.x left its driver packages or their record behind (drivers.ps1 removes them once it succeeds).
function LegacyDriversLeft: Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\drivers\NTAPD')) or DirExists(ExpandConstant('{app}\drivers\NTA2DPVM')) or
    FileExists(ExpandConstant('{app}\drivers\installed.txt'));
end;

procedure RemoveLegacyDrivers;
var
  Code: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Removing the drivers older versions of WPods used...';
  if not ExecAndLogOutput(PowerShell, '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    ExpandConstant('{app}\drivers\drivers.ps1') + '" -Uninstall', '', SW_HIDE, ewWaitUntilTerminated, Code, nil) then
    SuppressibleMsgBox('Setup could not start removing the drivers older versions of WPods used: ' +
      SysErrorMessage(Code) + #13#10#13#10 + 'Run Setup again to retry.', mbError, MB_OK, IDOK)
  else if Code = 3010 then
    Log('Old WPods drivers removed; Windows finishes removing them at the next restart.')
  else if Code <> 0 then
    SuppressibleMsgBox(Format('Setup could not remove all the drivers older versions of WPods used (error %d). ' +
      'Run Setup again to retry. Details are in the setup log:' + #13#10 + '%s', [Code,
      ExpandConstant('{log}')]), mbError, MB_OK, IDOK);
end;

// A failure keeps the app installed. Until the restart usbip-win2 needs, the service only starts with Windows, as a
// takeover before it would leave every Bluetooth device without a working adapter.
procedure InstallService;
var
  Code: Integer;
  Params: String;
begin
  WizardForm.StatusLabel.Caption := 'Setting up the Bluetooth adapter...';
  Params := '--install';
  if UsbipJustInstalled or ServiceWaitsForRestart then
    Params := '--install --no-start';
  if not ExecAndLogOutput(ExpandConstant('{app}\WPods.Service.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated,
    Code, nil) then
    SuppressibleMsgBox('AirPods Companion was installed, but Setup could not start setting up its background service: ' +
      SysErrorMessage(Code) + #13#10#13#10 + 'Run Setup again to retry.', mbError, MB_OK, IDOK)
  else if Code <> 0 then
    SuppressibleMsgBox(Format('AirPods Companion was installed, but its background service could not be set up (error %d). ' +
      'WPods cannot connect to your AirPods until it is.' + #13#10#13#10 +
      'Run Setup again to retry. Details are in the setup log:' + #13#10 + '%s', [Code, ExpandConstant('{log}')]),
      mbError, MB_OK, IDOK);
end;

// The service reads this value and the app's switch changes it; a silent update keeps the current choice.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    StopWPods;
    LogBluetoothDevices('install: before');
    if not MoveServiceFilesAside then
      StopService;
  end else if CurStep = ssPostInstall then begin
    FilesInstalled := True;
    if LegacyDriversLeft then
      RemoveLegacyDrivers;
    // The old service still runs from the renamed files: restart it as the new one.
    StopService;
    InstallService;
    RegWriteDWordValue(HKLM64, 'SOFTWARE\WPods\Service', 'AutoUpdate', 0);
  end;
end;

// A cancelled or failed install rolled back the new service files: the old service keeps running from the files moved
// aside, and gets them back under their names, or it would not start again after a restart.
procedure DeinitializeSetup;
begin
  if not FilesInstalled then
    MoveServiceFilesBack;
end;

// Offers the restart usbip-win2 needs before Bluetooth works through WPods; never for an update the service runs.
function NeedRestart: Boolean;
begin
  Result := (UsbipJustInstalled or ServiceWaitsForRestart) and not FromService;
end;

// Keeps the service running until the end of uninstall: its files are copied to {tmp}\svc, which UninstallService
// runs at the end, and the open ones moved aside. False when that failed; the service is then removed first.
function KeepServiceForLater: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Dir: String;
begin
  Result := FileExists(ExpandConstant('{app}\WPods.Service.exe'));
  if not Result then
    Exit;
  Dir := ExpandConstant('{tmp}\svc');
  ForceDirectories(Dir);
  Names := ServiceFiles;
  SetArrayLength(Names, GetArrayLength(Names) + 2);
  Names[GetArrayLength(Names) - 2] := 'WPods.Service.deps.json';
  Names[GetArrayLength(Names) - 1] := 'WPods.Service.runtimeconfig.json';
  for I := 0 to GetArrayLength(Names) - 1 do
    if FileExists(ExpandConstant('{app}\') + Names[I]) and
      not FileCopy(ExpandConstant('{app}\') + Names[I], Dir + '\' + Names[I], False) then begin
      Log('Could not copy ' + Names[I] + ' for the late service removal.');
      Result := False;
      Exit;
    end;
  Result := MoveServiceFilesAside;
end;

// Stops and deletes the WPods service, which gives the Bluetooth adapter back to Windows' own driver. If that fails,
// it still tries to give the adapter back (--restore-native). AdapterRestored stays False when both fail.
procedure UninstallService(const Exe: String);
var
  Code, RestoreCode: Integer;
begin
  AdapterRestored := True;
  if not FileExists(Exe) then begin
    // No service program but a registered service: nothing here can give the adapter back.
    AdapterRestored := not ServiceRegistered;
    if not AdapterRestored then begin
      Log('WPods.Service.exe is missing but the service is registered: the adapter cannot be given back.');
      SuppressibleMsgBox('Uninstall could not give your Bluetooth adapter back to Windows: a WPods file is missing. ' +
        'It kept usbip-win2. Install WPods again, then uninstall it.', mbError, MB_OK, IDOK);
    end;
    Exit;
  end;
  if Exec(Exe, '--uninstall', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
    Exit;
  Log(Format('WPods.Service.exe --uninstall failed (code %d).', [Code]));
  if not Exec(Exe, '--restore-native', '', SW_HIDE, ewWaitUntilTerminated, RestoreCode) then
    RestoreCode := 1;
  AdapterRestored := RestoreCode = 0;
  if AdapterRestored then
    SuppressibleMsgBox(Format('Uninstall could not remove the WPods background service (error %d), but it gave your ' +
      'Bluetooth adapter back to Windows.', [Code]), mbError, MB_OK, IDOK)
  else begin
    Log(Format('WPods.Service.exe --restore-native failed (code %d).', [RestoreCode]));
    SuppressibleMsgBox(Format('Uninstall could not give your Bluetooth adapter back to Windows (error %d), so it ' +
      'kept usbip-win2. If Bluetooth does not work after you restart Windows, install WPods again.', [Code]),
      mbError, MB_OK, IDOK);
  end;
end;

// Removes usbip-win2 when Setup installed it, and only once the adapter is back on Windows' own driver.
procedure UninstallUsbip;
var
  Code: Integer;
  Uninstaller: String;
begin
  if not WPodsFlag('UsbipInstalledByWPods') then
    Exit;
  if not AdapterRestored then begin
    UsbipKept := True;
    Exit;
  end;
  Uninstaller := ExpandConstant('{commonpf64}\USBip\unins000.exe');
  if not FileExists(Uninstaller) then
    Exit;
  UsbipRemoved := Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated,
    Code) and (Code = 0);
  if not UsbipRemoved then begin
    UsbipKept := True;
    Log(Format('Could not uninstall usbip-win2 (code %d).', [Code]));
    SuppressibleMsgBox(Format('Uninstall could not remove usbip-win2 (error %d). You can remove it yourself in ' +
      'Windows Settings, Apps.', [Code]), mbError, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Startup: String;
  ReplugCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    RegDeleteValue(HKCU, RunKey, 'AirPodsCompanion');
    RemoveSettings := SuppressibleMsgBox('Also remove your AirPods Companion and host settings?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2,
      IDNO) = IDYES;
    StopWPods;
    LogBluetoothDevices('uninstall: before');
    // The service (and Bluetooth through it) keeps running until the end; if its files cannot be kept for that, it
    // goes now, before [UninstallRun] and the files.
    ServiceKeptForLater := KeepServiceForLater;
    if not ServiceKeptForLater then
      UninstallService(ExpandConstant('{app}\WPods.Service.exe'));
  end else if CurUninstallStep = usPostUninstall then begin
    if ServiceKeptForLater then begin
      UninstallService(ExpandConstant('{tmp}\svc\WPods.Service.exe'));
      DeleteServiceFilesAside;
    end;
    LogBluetoothDevices('uninstall: adapter back on its own driver');
    // {localappdata} and HKCU belong to the account Uninstall runs as (the signed-in user when they approve the
    // UAC prompt themselves; the admin's when another account elevates it).
    if RegQueryStringValue(HKCU, RunKey, 'WPods', Startup) and
      (Pos(Lowercase(ExpandConstant('{app}\IslandAirPodsHost.exe')), Lowercase(Startup)) > 0) then
      RegDeleteValue(HKCU, RunKey, 'WPods');
    if RemoveSettings then begin
      DelTree(ExpandConstant('{localappdata}\WPods'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\AirPodsCompanion'), True, True, True);
      RegDeleteValue(HKCU, 'SOFTWARE\WPods', 'AutoUpdate');
      RegDeleteKeyIfEmpty(HKCU, 'SOFTWARE\WPods');
    end;
    UninstallUsbip;
    // The native driver sets paired devices up while usbip-win2 still removes the virtual adapter's nodes, which can
    // leave one failed ("driver error"); a re-plug sets them all up again.
    if UsbipRemoved and ServiceKeptForLater then
      Exec(ExpandConstant('{tmp}\svc\WPods.Service.exe'), '--replug', '', SW_HIDE, ewWaitUntilTerminated, ReplugCode);
    LogBluetoothDevices('uninstall: after usbip-win2');
    // Preserve upstream state markers; this fork does not change boot configuration.
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = BluetoothPage.ID) and not BluetoothConsent.Checked then begin
    MsgBox('Please accept Bluetooth setup to continue, or cancel without changing your Bluetooth configuration.', mbInformation, MB_OK);
    Result := False;
  end;
end;

function InitializeUninstall: Boolean;
var ExistingPath: String;
begin
  Result := True;
  if RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Services\WPodsService', 'ImagePath', ExistingPath) then
    if Pos(Lowercase(ExpandConstant('{app}\')), Lowercase(ExistingPath)) = 0 then begin
      SuppressibleMsgBox('The Bluetooth service now belongs to another installation. Uninstall stopped to protect that service. Remove the other installation first, then retry.', mbError, MB_OK, IDOK);
      Result := False;
    end;
end;
