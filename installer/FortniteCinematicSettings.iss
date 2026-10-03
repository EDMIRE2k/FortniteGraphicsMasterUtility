#define MyAppName "Fortnite Graphics Master Utility"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "Fortnite Graphics Master Utility"
#define MyAppExeName "FortniteGraphicsMasterUtility.exe"

[Setup]
AppId={{0CC6A89E-8FB2-42A8-AE13-9C6CE3CA0C35}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Fortnite Graphics Master Utility
DefaultGroupName=Fortnite Graphics Master Utility
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=FortniteGraphicsMasterUtility-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName={#MyAppName}
SetupIconFile=..\src\FortniteCinematicSettings\Assets\app-icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
