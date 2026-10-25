#define AppVersion "0.1.4"
#ifndef PublishDir
#define PublishDir "..\dist\publish"
#endif
[Setup]
AppId={{29687483-44CB-453A-9278-778D1E5B58C0}
AppName=Datto
AppVersion={#AppVersion}
AppPublisher=Datto
DefaultDirName={localappdata}\Programs\Datto
DefaultGroupName=Datto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\src\Datto\Assets\Datto.ico
UninstallDisplayIcon={app}\Datto.exe
OutputDir=..\dist
OutputBaseFilename=Datto-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\Datto.Desktop
DisableProgramGroupPage=yes
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PublishDir}\Datto.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Datto.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Datto.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Datto.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{autoprograms}\Datto"; Filename: "{app}\Datto.exe"; Parameters: "--show"
Name: "{autodesktop}\Datto"; Filename: "{app}\Datto.exe"; Parameters: "--show"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Datto"; Flags: uninsdeletevalue
[Run]
Filename: "{app}\Datto.exe"; Description: "Start Datto in the system tray"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var
  Runtime: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Runtime);
  if Result then FindClose(Runtime)
  else MsgBox('Datto requires .NET 10 Desktop Runtime (x64).' + #13#10 + 'Download: https://dotnet.microsoft.com/download/dotnet/10.0', mbInformation, MB_OK);
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    { Remove only the obsolete custom uninstaller registration after success. }
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Datto');
    DeleteFile(ExpandConstant('{app}\Uninstall.ps1'));
  end;
end;


