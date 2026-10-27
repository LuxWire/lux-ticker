; Per-user LuxWire Ticker setup. No administrator password.
; Installs under {localappdata}\LuxTicker and registers with Apps & features for this user.

#define MyAppName "LuxWire Ticker"
#define MyAppVersion "1.4.14"
#define MyAppPublisher "Lux"
#define MyAppExeName "LuxTicker.exe"

[Setup]
AppId={{8F4E2C1A-6B7D-4E5F-9A1C-2D3E4F506172}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.4.14.0
VersionInfoProductVersion=1.4.14.0
VersionInfoProductName=LuxWire Ticker Setup
VersionInfoCompany=Lux
VersionInfoDescription=LuxWire Ticker Setup
DefaultDirName={localappdata}\LuxTicker
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
AppMutex=Local\LuxTicker.SingleInstance
AllowNoIcons=no
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=LuxTicker-Setup-1.4.14
SetupIconFile=..\src\LuxTicker.App\Assets\LuxTicker.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This installs LuxWire Ticker for your Windows account. No administrator password is required.%n%nRunning it again upgrades the copy already installed and keeps your settings. The ticker sits above the taskbar. Settings, prices, and @LuxWire posts stay on this PC under your profile.
FinishedLabel=LuxWire Ticker is installed for your account.%n%nIt is in the Start Menu and in Apps & features. Windows 11 does not let setup pin to the taskbar. Open Start, right-click LuxWire Ticker, and choose Pin to taskbar. LuxWire Ticker Settings is also in the Start Menu.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "pintostart"; Description: "Pin LuxWire Ticker to Start (Windows 11 may require you to pin it from the Start Menu instead)"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "pintotaskbar"; Description: "Pin LuxWire Ticker to the taskbar (Windows 11 usually blocks this — if it does not pin, right-click LuxWire Ticker in Start and choose Pin to taskbar)"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startwithwindows"; Description: "Start LuxWire Ticker when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\src\LuxTicker.App\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\LuxTicker.exe"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{userprograms}\Lux Ticker.lnk"
Type: files; Name: "{userprograms}\Lux Ticker Settings.lnk"
Type: files; Name: "{userdesktop}\Lux Ticker.lnk"

[Icons]
Name: "{userprograms}\LuxWire Ticker"; Filename: "{app}\{#MyAppExeName}"; Comment: "Stock and crypto ticker"; AppUserModelID: "Lux.LuxTicker"
Name: "{userprograms}\LuxWire Ticker Settings"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"; Comment: "Open LuxWire Ticker settings"; AppUserModelID: "Lux.LuxTicker"
Name: "{userdesktop}\LuxWire Ticker"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "Stock and crypto ticker"; AppUserModelID: "Lux.LuxTicker"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LuxTicker"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startwithwindows; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup on"; Tasks: startwithwindows; StatusMsg: "Saving startup setting..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup off"; Tasks: not startwithwindows; Check: NoSavedSettings; StatusMsg: "Saving startup setting..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"; Description: "Launch LuxWire Ticker"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup off"; RunOnceId: "StartupOff"; Flags: runhidden waituntilterminated

[Code]
function NoSavedSettings: Boolean;
begin
  { An upgrade must not clear settings.json. The startup box only writes a default when that file was never created. }
  Result := not FileExists(ExpandConstant('{userappdata}\LuxTicker\settings.json'));
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  { The running single-file exe keeps the installed file locked. Close it before the files are replaced. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM LuxTicker.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

procedure TryShellVerb(const VerbName: string);
var
  Shell, Folder, Item: Variant;
  Shortcut, Dir: string;
begin
  try
    Shortcut := ExpandConstant('{userprograms}\LuxWire Ticker.lnk');
    if not FileExists(Shortcut) then
      Exit;
    Dir := ExtractFilePath(Shortcut);
    Shell := CreateOleObject('Shell.Application');
    Folder := Shell.NameSpace(Dir);
    if VarIsClear(Folder) then
      Exit;
    Item := Folder.ParseName(ExtractFileName(Shortcut));
    if VarIsClear(Item) then
      Exit;
    Item.InvokeVerb(VerbName);
  except
    { Windows 11 often refuses installer pinning. The finish page explains the Start Menu. }
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;
  if WizardIsTaskSelected('pintostart') then
    TryShellVerb('startpin');
  if WizardIsTaskSelected('pintotaskbar') then
    TryShellVerb('taskbarpin');
end;
