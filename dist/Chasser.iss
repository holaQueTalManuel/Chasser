[Setup]
AppName=Chasser
AppVersion=1.0
DefaultDirName={pf}\Chasser
DefaultGroupName=Chasser
OutputBaseFilename=ChasserSetup
Compression=lzma
SolidCompression=yes

[Files]
; Ejecutables cliente y servidor
Source: "server\server\Chasser.Server.exe"; DestDir: "{app}\server\server"; Flags: ignoreversion
Source: "client\client\Chasser.exe"; DestDir: "{app}\client\client"; Flags: ignoreversion

; Script para lanzar ambos
Source: "start.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Accesos directos
Name: "{group}\Chasser Server"; Filename: "{app}\server\server\Chasser.Server.exe"
Name: "{group}\Chasser Client"; Filename: "{app}\client\client\Chasser.exe"
Name: "{group}\Chasser2"; Filename: "{app}\start.bat"; WorkingDir: "{app}"; IconFilename: "{app}\client\client\Chasser.exe"
Name: "{userdesktop}\Chasser2"; Filename: "{app}\start.bat"; WorkingDir: "{app}"; IconFilename: "{app}\client\client\Chasser.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Opciones adicionales:"
