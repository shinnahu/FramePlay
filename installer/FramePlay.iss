; FramePlay installer (Inno Setup 6.7).
; Build: installer\build-installer.ps1 (publishes the app, then runs ISCC.exe on this file).
; Per-user install by default, no admin rights needed. Settings, history and screenshots are never
; touched by the installer or the uninstaller.

#define AppName "FramePlay"
#define AppExe "FramePlay.exe"
#define AppGuid "45B21EE5-C7AD-47C5-91D6-E38996D18D35"
#define AppUrl "https://github.com/shinnahu/FramePlay"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\FramePlay"
#endif
; Microsoft's permanent link to the latest .NET 8 Desktop Runtime installer (x64).
#define DotNetUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

; File types FramePlay registers (same list as MediaFormats.Associations in the app).
#define ExtCount 11
#dim Exts[ExtCount] {"mp4", "mkv", "webm", "mov", "avi", "mp3", "wav", "m4a", "flac", "mid", "midi"}
#dim Kinds[ExtCount] {"Video", "Video", "Video", "Video", "Video", "Audio", "Audio", "Audio", "Audio", "Midi", "Midi"}
#define ExtList "mp4,mkv,webm,mov,avi,mp3,wav,m4a,flac,mid,midi"

[Setup]
AppId={{{#AppGuid}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=shinnahu
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppComments=Fast, frame-exact media player
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=FramePlay-Setup-{#AppVersion}
SetupIconFile=..\src\FramePlay\Assets\FramePlay.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
; Follows Windows' light or dark mode.
WizardStyle=modern dynamic windows11
WizardImageFile=wizard-large.png,wizard-large-2x.png
WizardSmallImageFile=wizard-small.png,wizard-small-2x.png
; Without these, dark mode swaps in Inno's own stock images.
WizardImageFileDynamicDark=wizard-large.png,wizard-large-2x.png
WizardSmallImageFileDynamicDark=wizard-small.png,wizard-small-2x.png
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ChangesAssociations=yes
; FramePlay holds this mutex while it runs: Setup and the uninstaller ask to close it first.
AppMutex=FramePlay.Running
CloseApplications=yes
RestartApplications=no
; Always ask (English / Español). The language picked last time comes preselected, and FramePlay uses it too.
ShowLanguageDialog=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
en.TypeFull=Full (with the MIDI soundfont)
es.TypeFull=Completa (con el soundfont MIDI)
en.TypeCompact=Compact (no MIDI soundfont, 90 MB smaller)
es.TypeCompact=Compacta (sin el soundfont MIDI, 90 MB menos)
en.TypeCustom=Custom
es.TypeCustom=Personalizada
en.CompMain=FramePlay
es.CompMain=FramePlay
en.CompSoundfont=MIDI soundfont "Classic Station" (90 MB), used to play .mid files
es.CompSoundfont=Soundfont MIDI "Classic Station" (90 MB), para reproducir archivos .mid
en.GroupFiles=File types:
es.GroupFiles=Tipos de archivo:
en.TaskAssoc=Add FramePlay to "Open with" and to Default apps for video, audio and MIDI files
es.TaskAssoc=Agregar FramePlay a "Abrir con" y a Aplicaciones predeterminadas para videos, audio y MIDI
en.RunDefaultApps=Make FramePlay the default player (opens Windows Settings)
es.RunDefaultApps=Elegir FramePlay como reproductor predeterminado (abre Configuración de Windows)
en.FileVideo=%1 video
es.FileVideo=Video %1
en.FileAudio=%1 audio
es.FileAudio=Audio %1
en.FileMidi=MIDI file
es.FileMidi=Archivo MIDI
en.NeedDotNet=FramePlay needs the Microsoft .NET 8 Desktop Runtime (x64), which is free and isn't installed on this PC.%n%nDownload it now? Your browser downloads the official installer from Microsoft (about 58 MB): run it, then open FramePlay.%n%nSetup continues either way.
es.NeedDotNet=FramePlay necesita Microsoft .NET 8 Desktop Runtime (x64), que es gratis y no está instalado en esta PC.%n%n¿Descargarlo ahora? El navegador descarga el instalador oficial de Microsoft (unos 58 MB). Después de instalarlo, FramePlay ya se puede abrir.%n%nLa instalación sigue de todos modos.
en.UpdateTitle=Update FramePlay
es.UpdateTitle=Actualizar FramePlay
en.UpdateSubtitle=FramePlay %1 is already installed on this PC.
es.UpdateSubtitle=FramePlay %1 ya está instalado en esta PC.
en.UpdateText=This setup updates it to version %2, in the same folder.%n%nThere's no need to uninstall the current version first.%n%nYour settings, the queue, per-file memory and screenshots are not touched.
es.UpdateText=Este instalador lo actualiza a la versión %2, en la misma carpeta.%n%nNo hace falta desinstalar la versión actual antes.%n%nLa configuración, la cola, lo que FramePlay recuerda de cada archivo y las capturas no se tocan.
en.ReinstallText=This setup installs version %2 again over it, in the same folder (to repair it, for example).%n%nThere's no need to uninstall it first.%n%nYour settings are not touched.
es.ReinstallText=Este instalador vuelve a instalar la versión %2 encima, en la misma carpeta (por ejemplo, para repararla).%n%nNo hace falta desinstalarla antes.%n%nLa configuración no se toca.

[Types]
Name: "full"; Description: "{cm:TypeFull}"
Name: "compact"; Description: "{cm:TypeCompact}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "main"; Description: "{cm:CompMain}"; Types: full compact custom; Flags: fixed
Name: "soundfont"; Description: "{cm:CompSoundfont}"; Types: full

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "assoc"; Description: "{cm:TaskAssoc}"; GroupDescription: "{cm:GroupFiles}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml,*.sf2"; Flags: recursesubdirs ignoreversion; Components: main
Source: "{#SourceDir}\*.sf2"; DestDir: "{app}"; Flags: ignoreversion; Components: soundfont
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion; Components: main
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; DestName: "THIRD-PARTY-NOTICES.txt"; Flags: ignoreversion; Components: main

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The language picked in Setup. FramePlay adopts it on its next start (and again only if a later Setup picks another).
Root: HKA; Subkey: "Software\{#AppName}"; ValueType: string; ValueName: "Language"; ValueData: "{language}"; Flags: uninsdeletekey
; "Open with" and Default apps, the same entries FramePlay writes from Settings › About.
#define I 0
#sub AssocEntries
Root: HKA; Subkey: "Software\Classes\FramePlay.{#Exts[I]}"; ValueType: string; ValueData: "{cm:File{#Kinds[I]},{#UpperCase(Exts[I])}}"; Flags: uninsdeletekey; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\FramePlay.{#Exts[I]}\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\FramePlay.{#Exts[I]}\shell\open"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\FramePlay.{#Exts[I]}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\.{#Exts[I]}\OpenWithProgids"; ValueType: none; ValueName: "FramePlay.{#Exts[I]}"; Flags: uninsdeletevalue; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".{#Exts[I]}"; ValueData: ""; Tasks: assoc
Root: HKA; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".{#Exts[I]}"; ValueData: "FramePlay.{#Exts[I]}"; Tasks: assoc
#endsub
#for {I = 0; I < ExtCount; I++} AssocEntries
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assoc
Root: HKA; Subkey: "Software\{#AppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Tasks: assoc
Root: HKA; Subkey: "Software\{#AppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "A fast, frame-exact media player"; Tasks: assoc
Root: HKA; Subkey: "Software\{#AppName}\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\{#AppExe}"",0"; Tasks: assoc
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\{#AppName}\Capabilities"; Flags: uninsdeletevalue; Tasks: assoc

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
Filename: "{code:DefaultAppsUri}"; Description: "{cm:RunDefaultApps}"; Flags: shellexec nowait postinstall skipifsilent unchecked; Tasks: assoc

[Code]
{ The version already installed in this install mode (Setup reuses the previous mode and folder), or ''. }
function PreviousVersion(): String;
var
  V: String;
begin
  Result := '';
  if RegQueryStringValue(HKA64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#AppGuid}}_is1', 'DisplayVersion', V) then
    Result := V;
end;

{ An update: say first that it goes over the installed version (no need to uninstall it). }
procedure InitializeWizard();
var
  Prev, Text: String;
begin
  Prev := PreviousVersion();
  if Prev <> '' then
  begin
    if Prev = '{#AppVersion}' then
      Text := CustomMessage('ReinstallText')
    else
      Text := CustomMessage('UpdateText');
    CreateOutputMsgPage(wpWelcome, CustomMessage('UpdateTitle'), FmtMessage(CustomMessage('UpdateSubtitle'), [Prev]), FmtMessage(Text, [Prev, '{#AppVersion}']));
  end;
end;

{ Windows Settings › Default apps, opened on FramePlay's own page. }
function DefaultAppsUri(Param: String): String;
begin
  if IsAdminInstallMode() then
    Result := 'ms-settings:defaultapps?registeredAppMachine={#AppName}'
  else
    Result := 'ms-settings:defaultapps?registeredAppUser={#AppName}';
end;

function HasDotNetDesktop8(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup(): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if not HasDotNetDesktop8() then
    if SuppressibleMsgBox(CustomMessage('NeedDotNet'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', '{#DotNetUrl}', '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

{ Uninstall also removes the entries FramePlay may have written itself from Settings › About. }
procedure RemoveAssociations();
var
  Exts, Ext: String;
  P: Integer;
begin
  Exts := '{#ExtList},';
  while Exts <> '' do
  begin
    P := Pos(',', Exts);
    Ext := Copy(Exts, 1, P - 1);
    Delete(Exts, 1, P);
    RegDeleteKeyIncludingSubkeys(HKA, 'Software\Classes\FramePlay.' + Ext);
    RegDeleteValue(HKA, 'Software\Classes\.' + Ext + '\OpenWithProgids', 'FramePlay.' + Ext);
  end;
  RegDeleteKeyIncludingSubkeys(HKA, 'Software\Classes\Applications\{#AppExe}');
  RegDeleteValue(HKA, 'Software\RegisteredApplications', '{#AppName}');
  RegDeleteKeyIncludingSubkeys(HKA, 'Software\{#AppName}');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveAssociations();
end;
