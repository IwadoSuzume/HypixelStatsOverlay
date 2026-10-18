; Inno Setup script for WitheredRose (Hypixel Bedwars stats overlay).
; Rebuild first:
;   dotnet publish HypixelStatsOverlay.csproj -c Release -r win-x64 --self-contained true
; then compile this with ISCC.exe (or open it in the Inno Setup IDE and hit Build).
; PublishDir below must match dotnet's default self-contained publish output path.

#define AppVersion "1.0.0"
#define RepoDir "E:\桌面项目\HypixelStatsOverlay"
#define ProjDir RepoDir + "\HypixelStatsOverlay"
#define PublishDir ProjDir + "\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{6D6E75A5-98B7-4759-9A6C-81F50E7B8D30}
AppName=WitheredRose
AppVersion={#AppVersion}
AppPublisher=Suzume
AppPublisherURL=https://suzume.cloud
DefaultDirName={autopf}\WitheredRose
DefaultGroupName=WitheredRose
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#RepoDir}\installer\output
OutputBaseFilename=WitheredRose-Setup
SetupIconFile={#ProjDir}\Assets\logo.ico
UninstallDisplayIcon={app}\WitheredRose.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\WitheredRose"; Filename: "{app}\WitheredRose.exe"; IconFilename: "{app}\WitheredRose.exe"
Name: "{autodesktop}\WitheredRose"; Filename: "{app}\WitheredRose.exe"; IconFilename: "{app}\WitheredRose.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WitheredRose.exe"; Description: "{cm:LaunchProgram,WitheredRose}"; Flags: nowait postinstall skipifsilent
