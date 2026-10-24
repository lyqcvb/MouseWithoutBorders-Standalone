; Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#ifndef PublishDir
  #error PublishDir must point to the self-contained publish directory
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{F388A585-970F-420D-B761-22DDE93495DF}
AppName=Mouse Without Borders (Standalone)
AppVersion={#AppVersion}
AppPublisher=Mouse Without Borders standalone project
DefaultDirName={autopf}\Mouse Without Borders Standalone
DefaultGroupName=Mouse Without Borders Standalone
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=MouseWithoutBorders-Standalone-{#AppVersion}-win-x64-Setup
SetupIconFile=..\App\Logo.ico
UninstallDisplayIcon={app}\PowerToys.MouseWithoutBorders.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; The path-scoped maintenance script closes only this installation, including SYSTEM copies.
; Avoid Restart Manager closing unrelated applications that happen to share a library.
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: "autostart"; Description: "Start Mouse Without Borders when anyone signs in to Windows"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "Maintenance.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Mouse Without Borders"; Filename: "{app}\PowerToys.MouseWithoutBorders.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Mouse Without Borders"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Mouse Without Borders"; Filename: "{app}\PowerToys.MouseWithoutBorders.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{commonstartup}\Mouse Without Borders Standalone"; Filename: "{app}\PowerToys.MouseWithoutBorders.exe"; WorkingDir: "{app}"; Tasks: autostart

[InstallDelete]
; Remove a previous startup shortcut if this upgrade deselects the task.
Type: files; Name: "{commonstartup}\Mouse Without Borders Standalone.lnk"

[Run]
Filename: "{app}\PowerToys.MouseWithoutBorders.exe"; Description: "Launch Mouse Without Borders"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function RunMaintenance(Script, Operation: String): Boolean;
var
  ExitCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + Script +
    '" -Operation ' + Operation + ' -InstallDir "' + ExpandConstant('{app}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  Result := Result and (ExitCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  ExtractTemporaryFile('Maintenance.ps1');
  if not RunMaintenance(ExpandConstant('{tmp}\Maintenance.ps1'), 'Prepare') then
    Result := 'Could not stop this installation safely. Close Mouse Without Borders and retry. Details are in the setup log and installation-maintenance.log in the Windows temporary folder.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not RunMaintenance(ExpandConstant('{app}\Installer\Maintenance.ps1'), 'Configure') then
      RaiseException('The files were installed, but the firewall rule could not be configured. Run Setup again to repair it. Details are in installation-maintenance.log in the Windows temporary folder.');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  { Only change the machine after the user confirms uninstall. }
  if CurUninstallStep = usUninstall then
    if not RunMaintenance(ExpandConstant('{app}\Installer\Maintenance.ps1'), 'Uninstall') then
      RaiseException('Could not clean up this installation safely. No files were removed. Details are in installation-maintenance.log in the Windows temporary folder.');
end;
