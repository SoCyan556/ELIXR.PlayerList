#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define RepoRoot AddBackslash(SourcePath) + ".."

[Setup]
AppId={{EC54DB34-5BDE-4CF0-923D-ED57F588B688}
AppName=ELIXR Player List
AppVersion={#AppVersion}
AppPublisher=SoCyan556
AppPublisherURL=https://github.com/SoCyan556/ELIXR.PlayerList
AppSupportURL=https://github.com/SoCyan556/ELIXR.PlayerList/issues
AppUpdatesURL=https://github.com/SoCyan556/ELIXR.PlayerList/releases
DefaultDirName={localappdata}\Programs\ELIXR Player List
DefaultGroupName=ELIXR Player List
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#RepoRoot}\artifacts\release
OutputBaseFilename=ELIXR-PlayerList-{#AppVersion}-Setup-x64
SetupIconFile={#RepoRoot}\src\ELIXR.PlayerList.Desktop\Assets\icon.ico
UninstallDisplayIcon={app}\ELIXR.PlayerList.exe
LicenseFile={#RepoRoot}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=ELIXR Player List
VersionInfoVersion={#AppVersion}.0
AppComments=Open-source VRChat player list. Uses Microsoft WebView2.

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#RepoRoot}\artifacts\publish-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\artifacts\dependencies\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\ELIXR Player List"; Filename: "{app}\ELIXR.PlayerList.exe"
Name: "{autodesktop}\ELIXR Player List"; Filename: "{app}\ELIXR.PlayerList.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ELIXR.PlayerList.exe"; Description: "Open ELIXR Player List"; Flags: nowait postinstall skipifsilent

[Code]
function HasWebView2: Boolean;
var Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
  if not Result then
    Result := (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
  if not HasWebView2 then begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Could not start Microsoft WebView2 setup. Install the WebView2 Evergreen Runtime from Microsoft and try again.'
    else if not HasWebView2 then
      Result := 'Microsoft WebView2 could not be installed. Check your internet connection, install the WebView2 Evergreen Runtime from Microsoft, and retry.';
  end;
end;

// Uninstall deliberately preserves settings, encrypted sessions and caches in LocalAppData/ELIXR.
