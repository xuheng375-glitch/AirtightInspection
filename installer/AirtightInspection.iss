#define MyAppName "气密检测数据采集系统"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "本地采集节点"
#define MyAppExeName "AirtightInspection.exe"

[Setup]
AppId={{A35CA95E-0883-4F0C-8DC3-5E77FBC7F88A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\AirtightInspection
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=气密检测数据采集系统_安装程序_v{#MyAppVersion}_x64
SetupIconFile=..\AirtightInspection.WinForms\Icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Local\AirtightInspection.DataAcquisition
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName}安装程序
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Excludes: "Config.ini,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\publish\win-x64\Config.ini"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Dirs]
Name: "{app}\Data"; Flags: uninsneveruninstall
Name: "{app}\Logs"; Flags: uninsneveruninstall
Name: "{app}\ProductManual"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
