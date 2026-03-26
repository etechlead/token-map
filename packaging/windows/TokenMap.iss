#define MyAppName GetEnv("TOKENMAP_APP_NAME")
#if MyAppName == ""
  #undef MyAppName
  #define MyAppName "TokenMap"
#endif

#define MyAppVersion GetEnv("TOKENMAP_APP_VERSION")
#if MyAppVersion == ""
  #undef MyAppVersion
  #define MyAppVersion "0.1.0-local"
#endif

#define MyAppPublisher GetEnv("TOKENMAP_APP_PUBLISHER")
#if MyAppPublisher == ""
  #undef MyAppPublisher
  #define MyAppPublisher "Clever"
#endif

#define MyAppExeName GetEnv("TOKENMAP_APP_EXE")
#if MyAppExeName == ""
  #undef MyAppExeName
  #define MyAppExeName "Clever.TokenMap.App.exe"
#endif

#define MyPublishDir AddBackslash(SourcePath) + "..\..\artifacts\windows-installer\publish\win-x64"
#define MyOutputDir AddBackslash(SourcePath) + "..\..\artifacts\windows-installer\installer"

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
OutputBaseFilename=TokenMap-Setup-win-x64
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
