#define MyAppName "NetPulse"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Burhan Sahin"
#define MyAppExeName "NetPulse.App.exe"
#define SourceDir "..\temp_publish\AppBundle"

[Setup]
AppId={{8DD9039B-93E0-4CE9-9212-9E61BDBF3C19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\NetPulse
DefaultGroupName=NetPulse
DisableProgramGroupPage=yes
OutputDir=..\SetupOutput
OutputBaseFilename=NetPulseSetup
; Logo dosyan .jpg olduğu için setup ikonu olarak hata verebilir. İleride .ico yaparsan burayı güncelleyebilirsin.
; Şimdilik hata vermemesi için ikon satırını yoruma alabilir veya direkt bırakabilirsin.
; SetupIconFile=..\src\NetPulse.App\logo.jpg
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "startup"; Description: "NetPulse bilgisayar açıldığında otomatik başlsın (Başlangıçta Çalıştır)"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Kullanıcı başlangıçta çalıştır görevini seçerse Windows Registry'ye kayıt atıyoruz
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NetPulse"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Icons]
Name: "{group}\NetPulse"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\NetPulse"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "NetPulse uygulamasını başlat"; Flags: nowait postinstall skipifsilent