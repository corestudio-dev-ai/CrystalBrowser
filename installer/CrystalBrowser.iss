; Inno Setup script for Crystal Browser.
; Produces a single bundled installer .exe. .NET is already baked into the published
; binaries (self-contained), so the target machine needs no .NET runtime.

#define AppName "Crystal Browser"
#define AppVersion "1.2"
#define AppPublisher "Crystal"
#define AppExe "CrystalBrowser.App.exe"

[Setup]
AppId={{B6F1C2A4-7E3D-49A8-9C2B-CrystalBrowser01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Crystal Browser
DefaultGroupName=Crystal Browser
AllowNoIcons=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=.
OutputBaseFilename=CrystalBrowserSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "quicklaunchicon"; Description: "Create a &Quick Launch shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Bundle the entire published app folder (app + native dlls + the .NET runtime).
Source: "..\build\CrystalBrowser\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
; Start Menu
Name: "{group}\Crystal Browser"; Filename: "{app}\{#AppExe}"; Comment: "Launch Crystal Browser"
Name: "{group}\Uninstall Crystal Browser"; Filename: "{uninstallexe}"
; Desktop & Quick Launch (optional, chosen on the tasks page)
Name: "{autodesktop}\Crystal Browser"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Crystal Browser"; Filename: "{app}\{#AppExe}"; Tasks: quicklaunchicon

[Registry]
; Register Crystal Browser with Windows so it shows up as a selectable default browser
; in Settings > Default apps. Written to HKCU (HKA maps to HKCU for per-user installs).
; A ProgId that handles http/https links by launching the app with the URL.
Root: HKA; Subkey: "Software\Classes\CrystalHTM"; ValueType: string; ValueName: ""; ValueData: "Crystal Browser Document"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\CrystalHTM\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\CrystalHTM\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
; Capabilities block that declares the http/https URL associations.
Root: HKA; Subkey: "Software\Crystal Browser\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Crystal Browser\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "A fast, dark, private Windows browser with bundled Tor."
Root: HKA; Subkey: "Software\Crystal Browser\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "CrystalHTM"
Root: HKA; Subkey: "Software\Crystal Browser\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "CrystalHTM"
; Advertise the capabilities to Windows.
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\Crystal Browser\Capabilities"; Flags: uninsdeletevalue

[Run]
; Offer to launch the app from the final wizard page.
Filename: "{app}\{#AppExe}"; Description: "Run Crystal Browser now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Bundled Tor keeps its working state under LocalAppData; remove it on uninstall.
Type: filesandordirs; Name: "{localappdata}\CrystalBrowser\tor-data"

[Code]
{ ---- Automatic Microsoft Edge WebView2 Runtime installation ---- }
{ Crystal Browser needs the WebView2 (Chromium) runtime. If it isn't already on the
  machine, the installer downloads Microsoft's Evergreen bootstrapper and installs it
  silently — so the whole thing stays hands-off. }

const
  WV2_GUID = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  WV2_BOOTSTRAP_URL = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

function PvSet(const RootStr: String; Root: Integer): Boolean;
var
  v: String;
begin
  Result := RegQueryStringValue(Root, RootStr, 'pv', v) and (v <> '') and (v <> '0.0.0.0');
end;

function WebView2Installed: Boolean;
begin
  { System-wide (64-bit and 32-bit views) or per-user install all count. }
  Result :=
    PvSet('SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WV2_GUID, HKLM) or
    PvSet('SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID, HKLM) or
    PvSet('SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID, HKCU);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and (not WebView2Installed) then
  begin
    try
      { Saved into the temp dir; raises on failure such as no internet. }
      DownloadTemporaryFile(WV2_BOOTSTRAP_URL, 'MicrosoftEdgeWebview2Setup.exe', '', nil);
      Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'),
        '/silent /install', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    except
      MsgBox('Crystal Browser could not install the Microsoft Edge WebView2 Runtime '
        + 'automatically (no internet connection?).' + #13#10#13#10
        + 'The browser will still install, but you may need to install WebView2 manually '
        + 'from Microsoft before pages will display.', mbInformation, MB_OK);
    end;
  end;
end;
