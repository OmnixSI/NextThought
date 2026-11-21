; NextThought for Windows — per-user installer for the Avalonia desktop build.
; Compile with the pinned Inno Setup compiler prepared by scripts/bootstrap_installer.ps1:
;   ISCC.exe /DAppVersion=0.9.0 /DPayload=<publish dir> /DOutputDirectory=<dir> /DOutputName=NextThought-0.9.0-windows-x64-setup NextThought.Desktop.iss
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef Payload
  #error Payload is required
#endif
#ifndef OutputDirectory
  #define OutputDirectory "."
#endif
#ifndef InstallId
  #define InstallId "NextThought.Desktop"
#endif
#ifndef OutputName
  #define OutputName "NextThought-" + AppVersion + "-windows-x64-setup"
#endif

[Setup]
AppId={#InstallId}
AppName=NextThought
AppVersion={#AppVersion}
AppVerName=NextThought {#AppVersion}
AppPublisher=NextThought Windows Port
AppPublisherURL=https://github.com/OmnixSI/NextThought
VersionInfoVersion={#AppVersion}
VersionInfoDescription=NextThought for Windows
DefaultDirName={localappdata}\Programs\NextThought
DefaultGroupName=NextThought
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\NextThought.exe
UninstallDisplayName=NextThought
OutputDir={#OutputDirectory}
OutputBaseFilename={#OutputName}
SetupIconFile=..\src\NextThought.Desktop\Assets\NextThought.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
DisableWelcomePage=no
SignedUninstaller=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式（可选）"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "{#Payload}\NextThought.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Payload}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Payload}\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Payload}\THIRD_PARTY_NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#Payload}\*.png"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#Payload}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#Payload}\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "..\PORTING_STATUS.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{userprograms}\NextThought\NextThought"; Filename: "{app}\NextThought.exe"; WorkingDir: "{app}"
Name: "{userprograms}\NextThought\卸载 NextThought"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NextThought"; Filename: "{app}\NextThought.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\NextThought.exe"; Description: "启动 NextThought"; Flags: nowait postinstall; Check: ShouldLaunchApp

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /IM NextThought.exe /F"; Flags: runhidden; RunOnceId: "StopNextThought"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: dirifempty; Name: "{userprograms}\NextThought"

[Code]
function ShouldLaunchApp(): Boolean;
begin
  // Silent in-app updates pass /LAUNCH=1; automated install tests stay headless.
  Result := (not WizardSilent) or (ExpandConstant('{param:LAUNCH|0}') = '1');
end;

// R1: the old WPF preview used AppId NextThought.Windows.Preview but installed into the same
// %LOCALAPPDATA%\Programs\NextThought folder. Refuse a mixed installation and ask the user to
// uninstall the old build first; uninstalling only removes that build's program files, the data
// directory %LOCALAPPDATA%\NextThought (meetings, settings, models) is kept.
function LegacyPreviewInstalled(): Boolean;
var
  Names: TArrayOfString;
  Index: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
    for Index := 0 to GetArrayLength(Names) - 1 do
      if Pos('NextThought.Windows.Preview', Names[Index]) = 1 then
      begin
        Result := True;
        Exit;
      end;
end;

function InitializeSetup(): Boolean;
begin
  Result := not LegacyPreviewInstalled();
  if not Result then
    MsgBox('An old NextThought preview (NextThought.Windows.Preview) is still installed in the same folder.'
           + #13#10 + #13#10 +
           'Please uninstall it first - your meetings, settings and models are kept - then run this installer again.',
           mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // The data directory lives in %LOCALAPPDATA%\NextThought and is intentionally
    // outside the install directory, so upgrading and uninstalling keep meetings.
  end;
end;
