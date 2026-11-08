#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef Payload
  #error Payload is required
#endif
#ifndef InstallId
  #define InstallId "NextThought.Windows.Preview"
#endif
#ifndef ShortcutGroup
  #define ShortcutGroup "NextThought Windows Preview"
#endif
#ifndef FailBeforeActivation
  #define FailBeforeActivation 0
#endif

[Setup]
AppId={#InstallId}
AppName=NextThought Windows Preview
AppVersion={#AppVersion}
AppPublisher=NextThought Windows Port
VersionInfoVersion={#AppVersion}
VersionInfoDescription=NextThought Windows Preview Installer
DefaultDirName={localappdata}\Programs\NextThought
DefaultGroupName={#ShortcutGroup}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
AppMutex=Local\NextThought.Windows.Preview.Running
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\versions\{#AppVersion}\NextThought.exe
UninstallDisplayName=NextThought Windows Preview
OutputDir={#OutputDirectory}
OutputBaseFilename={#OutputName}
Compression=lzma2/fast
SolidCompression=yes
LZMADictionarySize=32768
LZMANumBlockThreads=2
WizardStyle=modern
SetupLogging=yes
#ifdef SigningCommand
SignTool=ntsign
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "{#Payload}\*"; DestDir: "{app}\versions\{#AppVersion}"; Excludes: "release-manifest.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Payload}\release-manifest.json"; DestDir: "{app}\versions\{#AppVersion}"; Flags: ignoreversion; AfterInstall: ValidatePayload

[Run]
Filename: "{app}\versions\{#AppVersion}\NextThought.exe"; Description: "启动 NextThought"; Flags: nowait postinstall skipifsilent; Check: IsActivationSuccessful

[UninstallDelete]
Type: files; Name: "{app}\installed-release.json"
Type: files; Name: "{app}\installed-release.json.previous"
Type: files; Name: "{app}\.deployment.lock"
Type: files; Name: "{app}\Launch.cmd"
Type: files; Name: "{app}\Rollback.cmd"
Type: files; Name: "{userprograms}\{#ShortcutGroup}\NextThought.lnk"
Type: files; Name: "{userprograms}\{#ShortcutGroup}\一键自检.lnk"
Type: files; Name: "{userprograms}\{#ShortcutGroup}\回退上一版.lnk"
Type: files; Name: "{userdesktop}\{#ShortcutGroup}.lnk"
Type: dirifempty; Name: "{userprograms}\{#ShortcutGroup}"

[Code]
var
  PayloadVerified, ActivationSuccessful, HadPrevious: Boolean;
  PriorDisplayVersion, ActivationError: string;

function IsPayloadVerified: Boolean;
begin
  Result := PayloadVerified;
end;

function IsActivationSuccessful: Boolean;
begin
  Result := ActivationSuccessful;
end;

function GetFileAttributesW(lpFileName: string): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function IsInside(const Candidate, Parent: string): Boolean;
begin
  Result := Pos(Uppercase(AddBackslash(Parent)), Uppercase(AddBackslash(Candidate))) = 1;
end;

function IsEmptyDirectory(const Directory: string): Boolean;
var
  Search: TFindRec;
begin
  Result := True;
  if FindFirst(AddBackslash(Directory) + '*', Search) then
  begin
    try
      repeat
        if (Search.Name <> '.') and (Search.Name <> '..') then
        begin
          Result := False;
          Break;
        end;
      until not FindNext(Search);
    finally
      FindClose(Search);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  Target, Data, Current: string;
  Attributes: Cardinal;
begin
  Result := '';
  HadPrevious := RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallId}_is1', 'DisplayVersion', PriorDisplayVersion);
  Target := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  Data := ExpandConstant('{localappdata}\NextThought');
  if (Length(Target) < 4) or IsInside(Target, Data) or IsInside(Data, Target) then
  begin
    Result := '安装目录必须与会议和模型数据目录分离。';
    Exit;
  end;
  Data := GetEnv('NEXTTHOUGHT_DATA_DIR');
  if (Data <> '') and (IsInside(Target, Data) or IsInside(Data, Target)) then
  begin
    Result := '安装目录与自定义数据目录重叠，请改用独立程序目录。';
    Exit;
  end;
  Current := Target;
  repeat
    Attributes := GetFileAttributesW(Current);
    if (Attributes <> $FFFFFFFF) and ((Attributes and $400) <> 0) then
    begin
      Result := '安装目录不支持符号链接或联接点，请选择物理目录。';
      Exit;
    end;
    Data := ExtractFileDir(Current);
    if Data = Current then Break;
    Current := Data;
  until Length(Current) < 3;
  if DirExists(Target) and not IsEmptyDirectory(Target) and not FileExists(AddBackslash(Target) + 'installed-release.json') then
    Result := '此文件夹已有其他内容。请选空文件夹；已有便携版和会议数据不需要移动。';
end;

procedure ValidatePayload;
var
  Code: Integer;
  Tool, Arguments: string;
begin
  PayloadVerified := False;
  Tool := ExpandConstant('{app}\versions\{#AppVersion}\tools\NextThought.Cli.exe');
  Arguments := 'deployment-verify --install-root "' + ExpandConstant('{app}') + '" --version {#AppVersion}';
  if not Exec(Tool, Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    ActivationError := '无法运行发布包校验，未切换当前版本。'
  else if Code <> 0 then
    ActivationError := '发布包校验失败，未切换当前版本。'
  else PayloadVerified := True;
  #if FailBeforeActivation
  PayloadVerified := False;
  ActivationError := 'Controlled QA failure before activation.';
  #endif
  if not PayloadVerified then Log(ActivationError);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Arguments, Desktop: string;
begin
  if CurStep = ssPostInstall then
  begin
    if PayloadVerified then
    begin
      Desktop := 'false';
      if WizardIsTaskSelected('desktopicon') then Desktop := 'true';
      Arguments := 'deployment-activate --install-root "' + ExpandConstant('{app}') + '" --version {#AppVersion} --group "{#ShortcutGroup}" --desktop ' + Desktop + ' --uninstall-key {#InstallId}_is1';
      if not Exec(ExpandConstant('{app}\versions\{#AppVersion}\tools\NextThought.Cli.exe'), Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
        ActivationError := '版本切换未完成。'
      else if Code <> 0 then
        ActivationError := '版本切换失败，旧启动版本保持。'
      else ActivationSuccessful := True;
    end;
    if not ActivationSuccessful then
    begin
      Log('Activation failed: ' + ActivationError);
      if HadPrevious then
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallId}_is1', 'DisplayVersion', PriorDisplayVersion)
      else
      begin
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallId}_is1', 'DisplayName', 'NextThought Windows Preview (未激活)');
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallId}_is1', 'DisplayVersion', '0.0.0');
      end;
    end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and not ActivationSuccessful then
    WizardForm.FinishedLabel.Caption := '安装未激活：' + ActivationError + #13#10 + '已有版本和会议数据保持。请保留安装日志，使用旧快捷方式启动。';
end;

function GetCustomSetupExitCode: Integer;
begin
  if ActivationSuccessful then Result := 0 else Result := 21;
end;
