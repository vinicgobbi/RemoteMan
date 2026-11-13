; Instalador do RemoteMan (Inno Setup 6.3+).
;
; Compilar (a partir da raiz do repositório, depois do dotnet publish):
;   ISCC.exe /DAppVersion=0.1.0 /DArch=x64 /DPublishDir=..\publish\win-x64 /DOutputDir=..\dist installer\RemoteMan.iss
;
; Arch: x64 ou arm64. Instala por usuário (sem pedir administrador) em %LOCALAPPDATA%\Programs\RemoteMan.
; Os dados do app (%LOCALAPPDATA%\RemoteMan) e as senhas no Gerenciador de Credenciais não são removidos
; ao desinstalar.

#ifndef AppVersion
  #error Informe a versão: /DAppVersion=X.Y.Z
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef PublishDir
  #error Informe a pasta publicada: /DPublishDir=...
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#if Arch == "x64"
  #define ArchAllowed "x64compatible"
#elif Arch == "arm64"
  #define ArchAllowed "arm64"
#else
  #error Arch deve ser x64 ou arm64
#endif

#define AppName "RemoteMan"
#define AppExe "RemoteMan.exe"

[Setup]
; não altere o AppId: é ele que permite atualizar por cima da versão instalada
AppId={{8C5E2F6A-3B1D-4F7E-9A42-6D0C1E7B5A93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=vinicgobbi
AppPublisherURL=https://github.com/vinicgobbi/RemoteMan
AppSupportURL=https://github.com/vinicgobbi/RemoteMan/issues
AppUpdatesURL=https://github.com/vinicgobbi/RemoteMan/releases
VersionInfoVersion={#AppVersion}

PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
MinVersion=10.0.17763

SetupIconFile=..\RemoteMan\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes

OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-Setup-{#AppVersion}-{#Arch}

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; ignoreversion: sempre substitui os arquivos (o app é self-contained)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; remove arquivos de versões anteriores que não existem mais na nova
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
