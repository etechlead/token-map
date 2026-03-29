#define MyAppName GetEnv("TOKENMAP_APP_NAME")
#if MyAppName == ""
  #undef MyAppName
  #define MyAppName "TokenMap"
#endif

#define MyAppVersion GetEnv("TOKENMAP_APP_VERSION")
#if MyAppVersion == ""
  #undef MyAppVersion
  #define MyAppVersion "0.1.1-local"
#endif

#define MyAppPublisher GetEnv("TOKENMAP_APP_PUBLISHER")
#if MyAppPublisher == ""
  #undef MyAppPublisher
  #define MyAppPublisher "Clever.pro"
#endif

#define MyAppExeName GetEnv("TOKENMAP_APP_EXE")
#if MyAppExeName == ""
  #undef MyAppExeName
  #define MyAppExeName "Clever.TokenMap.App.exe"
#endif

#define MyArtifactBaseName GetEnv("TOKENMAP_ARTIFACT_BASENAME")
#if MyArtifactBaseName == ""
  #undef MyArtifactBaseName
  #define MyArtifactBaseName "TokenMap-win-x64-0.1.1-local-installer"
#endif

#define MyPublishDir AddBackslash(SourcePath) + "..\..\.artifacts\windows-installer\publish\win-x64"
#define MyOutputDir AddBackslash(SourcePath) + "..\..\.artifacts\windows-installer\installer"

[Setup]
AppId={{D6B37D4A-B4C7-4B7A-B8C7-6C46AC220001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesEnvironment=no
Compression=lzma2
SolidCompression=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyArtifactBaseName}
SetupIconFile=..\..\src\Clever.TokenMap.App\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  PurgeUserDataFlag = '/PURGEUSERDATA';

function GetUserDataRootPath(): string;
begin
  Result := ExpandConstant('{localappdata}\Clever\TokenMap');
end;

function GetUserDataVendorRootPath(): string;
begin
  Result := ExpandConstant('{localappdata}\Clever');
end;

procedure RemoveUserData();
var
  userDataRootPath: string;
  userDataVendorRootPath: string;
begin
  userDataRootPath := GetUserDataRootPath();
  if not DirExists(userDataRootPath) then
  begin
    Exit;
  end;

  if DelTree(userDataRootPath, True, True, True) then
  begin
    userDataVendorRootPath := GetUserDataVendorRootPath();
    if DirExists(userDataVendorRootPath) then
    begin
      RemoveDir(userDataVendorRootPath);
    end;
  end
  else
  begin
    SuppressibleMsgBox(
      'TokenMap settings and logs could not be removed completely.'#13#10#13#10 +
      'You can delete "' + userDataRootPath + '" manually later.',
      mbInformation,
      MB_OK,
      IDOK);
  end;
end;

function HasCommandLineFlag(const expectedFlag: string): Boolean;
var
  parameterIndex: Integer;
begin
  Result := False;

  for parameterIndex := 1 to ParamCount() do
  begin
    if CompareText(ParamStr(parameterIndex), expectedFlag) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usPostUninstall then
  begin
    Exit;
  end;

  if not DirExists(GetUserDataRootPath()) then
  begin
    Exit;
  end;

  if HasCommandLineFlag(PurgeUserDataFlag) then
  begin
    RemoveUserData();
    Exit;
  end;

  if UninstallSilent() then
  begin
    Exit;
  end;

  if SuppressibleMsgBox(
    'Remove TokenMap settings, folder history, and logs from your user profile?'#13#10#13#10 +
    'This will delete:'#13#10 +
    GetUserDataRootPath(),
    mbConfirmation,
    MB_YESNO or MB_DEFBUTTON2,
    IDNO) = IDYES then
  begin
    RemoveUserData();
  end;
end;
