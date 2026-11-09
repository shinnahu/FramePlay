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
; An existing FramePlay folder (from an earlier install, say) is simply installed over.
DirExistsWarning=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
; Installs for the current user. "/ALLUSERS" on the command line installs for everyone instead.
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=..\dist
OutputBaseFilename=FramePlay-Setup-{#AppVersion}
SetupIconFile=..\src\FramePlay\Assets\FramePlay.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
; Dark like FramePlay's default theme, in the player's window colour, without divider lines. The buttons
; and the language picker are drawn in FramePlay's style by the [Code] section.
WizardStyle=modern dark hidebevels
WizardBackColor=#2b2d31
WizardImageFile=wizard-large.png,wizard-large-2x.png
WizardSmallImageFile=wizard-small.png,wizard-small-2x.png
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ChangesAssociations=yes
; FramePlay holds this mutex while it runs: Setup and the uninstaller ask to close it first.
AppMutex=FramePlay.Running
CloseApplications=yes
RestartApplications=no
; Setup shows its own language picker (English / Español) in FramePlay's style instead of Inno's dialog;
; the language picked last time comes preselected, and FramePlay uses it too.
ShowLanguageDialog=no
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
en.NeedDotNetTitle=.NET 8 is needed
es.NeedDotNetTitle=Hace falta .NET 8
en.UninstallTitle=Uninstall FramePlay
es.UninstallTitle=Desinstalar FramePlay
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

{ ============================================================================================
  FramePlay's look. Setup runs in Inno's dark style with the player's window colour. On top of that:
  flat, rounded buttons in the player's colours (accent for the main action, grey for the others,
  with hover), the player's slider as the progress bar, and dialogs in the same style (the language
  picker, questions and notices) instead of message boxes, so nothing shows a focus outline. The real
  buttons and progress bar keep working, just out of sight, so Enter and Esc behave as usual; the
  player-style ones copy their caption, state and position.
  ============================================================================================ }
type
  TWinRect = record
    Left, Top, Right, Bottom: Integer;
  end;
  TWinPoint = record
    X, Y: Integer;
  end;

function CreateRoundRectRgn(X1, Y1, X2, Y2, W, H: Integer): Longword;
  external 'CreateRoundRectRgn@gdi32.dll stdcall';
function SetWindowRgn(Wnd: HWND; Rgn: Longword; Redraw: Integer): Integer;
  external 'SetWindowRgn@user32.dll stdcall';
function GetWindowRect(Wnd: HWND; var R: TWinRect): Integer;
  external 'GetWindowRect@user32.dll stdcall';
function GetCursorPos(var P: TWinPoint): Integer;
  external 'GetCursorPos@user32.dll stdcall';
function SetTimer(Wnd: HWND; Id, Elapse: Longword; Proc: Longword): Longword;
  external 'SetTimer@user32.dll stdcall';
function KillTimer(Wnd: HWND; Id: Longword): Integer;
  external 'KillTimer@user32.dll stdcall';

const
  { FramePlay's dark theme, as $BBGGRR colours. }
  ColAccent = $F26558;       { #5865F2 }
  ColAccentHover = $D5594D;  { #4D59D5 }
  ColAccentOff = $88463F;    { the accent at 45 %, for a disabled button }
  ColGrey = $413C39;         { #393C41 }
  ColGreyHover = $494240;    { #404249 }
  ColGreyOff = $383431;
  ColText = $F5F3F2;         { #F2F3F5 }
  ColTextMuted = $A49B94;    { #949BA4 }
  ColTextOnAccent = $FFFFFF;
  ColTextOff = $898684;
  ColBack = $312D2B;         { #2B2D31, the player's window }
  ColTrack = $221F1E;        { #1E1F22, the player's slider track }

var
  PbPanels: array of TPanel;
  PbReal: array of TButton;
  PbPrimary: array of Boolean;
  PbState: array of Integer;
  PbTimer: Longword;
  PbTimerWnd: HWND;
  PbActive: Boolean;
  PgReal: TNewProgressBar;
  PgTrack, PgFill: TPanel;
  PgW, PgFillW: Integer;
  DlgForm: TSetupForm;
  DlgResult: Integer;
  HaveWizard: Boolean;

function NoAccel(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '&&', #1, True);
  StringChangeEx(Result, '&', '', True);
  StringChangeEx(Result, #1, '&', True);
end;

procedure PaintPlayerButton(I: Integer; Enabled, Hover: Boolean);
var
  State: Integer;
  P: TPanel;
begin
  State := 0;
  if Enabled then State := State + 2;
  if Hover then State := State + 1;
  if PbState[I] = State then Exit;
  PbState[I] := State;
  P := PbPanels[I];
  if not Enabled then
  begin
    if PbPrimary[I] then P.Color := ColAccentOff else P.Color := ColGreyOff;
    P.Font.Color := ColTextOff;
  end
  else if PbPrimary[I] then
  begin
    if Hover then P.Color := ColAccentHover else P.Color := ColAccent;
    P.Font.Color := ColTextOnAccent;
  end
  else
  begin
    if Hover then P.Color := ColGreyHover else P.Color := ColGrey;
    P.Font.Color := ColText;
  end;
end;

{ Copies caption, visibility and enabled state from the real buttons, and shows hover. }
procedure SyncPlayerButtons;
var
  I: Integer;
  R: TWinRect;
  Pt: TWinPoint;
  Over: Boolean;
  Caption: String;
begin
  if not PbActive then Exit;
  GetCursorPos(Pt);
  for I := 0 to GetArrayLength(PbPanels) - 1 do
  begin
    if PbPanels[I].Visible <> PbReal[I].Visible then PbPanels[I].Visible := PbReal[I].Visible;
    Caption := NoAccel(PbReal[I].Caption);
    if PbPanels[I].Caption <> Caption then PbPanels[I].Caption := Caption;
    Over := False;
    if PbPanels[I].Visible and (GetWindowRect(PbPanels[I].Handle, R) <> 0) then
      Over := (Pt.X >= R.Left) and (Pt.X < R.Right) and (Pt.Y >= R.Top) and (Pt.Y < R.Bottom);
    PaintPlayerButton(I, PbReal[I].Enabled, Over);
  end;
end;

{ Follows the real progress bar: the accent fill grows over the dark track, both with round ends. }
procedure SyncPlayerProgress;
var
  W, H: Integer;
  F: Extended;
begin
  if not PbActive or (PgReal = nil) then Exit;
  H := PgTrack.Height;
  if PgTrack.Width <> PgW then
  begin
    PgW := PgTrack.Width;
    SetWindowRgn(PgTrack.Handle, CreateRoundRectRgn(0, 0, PgW + 1, H + 1, H, H), 1);
    PgFillW := -1;
  end;
  W := 0;
  if PgReal.Max > PgReal.Min then
  begin
    F := PgReal.Position - PgReal.Min;
    F := F / (PgReal.Max - PgReal.Min);
    W := Round(F * PgW);
  end;
  if W > PgW then W := PgW;
  if (W > 0) and (W < H) then W := H;
  if W = PgFillW then Exit;
  PgFillW := W;
  if W > 0 then
  begin
    PgFill.Width := W;
    SetWindowRgn(PgFill.Handle, CreateRoundRectRgn(0, 0, W + 1, H + 1, H, H), 1);
  end;
  PgFill.Visible := W > 0;
end;

procedure PlayerButtonTimer(Wnd: HWND; Msg: Longword; IdEvent: Longword; Time: Longword);
begin
  SyncPlayerButtons;
  SyncPlayerProgress;
end;

procedure PlayerButtonClick(Sender: TObject);
var
  I: Integer;
begin
  for I := 0 to GetArrayLength(PbPanels) - 1 do
    if (Sender = PbPanels[I]) and PbReal[I].Visible and PbReal[I].Enabled then
    begin
      PbReal[I].OnClick(PbReal[I]);
      Exit;
    end;
end;

{ Puts a player-style button where a real one is; the real one moves out of sight but keeps working. }
procedure AddPlayerButton(Owner: TComponent; Parent: TWinControl; Real: TButton; Primary: Boolean);
var
  I: Integer;
  P: TPanel;
begin
  I := GetArrayLength(PbPanels);
  SetArrayLength(PbPanels, I + 1);
  SetArrayLength(PbReal, I + 1);
  SetArrayLength(PbPrimary, I + 1);
  SetArrayLength(PbState, I + 1);
  P := TPanel.Create(Owner);
  P.Parent := Parent;
  P.SetBounds(Real.Left, Real.Top, Real.Width, Real.Height);
  P.Anchors := Real.Anchors;
  P.BevelOuter := bvNone;
  P.ParentBackground := False;
  P.StyleElements := [];
  P.Cursor := crHand;
  P.Caption := NoAccel(Real.Caption);
  P.Visible := Real.Visible;
  P.OnClick := @PlayerButtonClick;
  SetWindowRgn(P.Handle, CreateRoundRectRgn(0, 0, P.Width + 1, P.Height + 1, ScaleX(10), ScaleY(10)), 1);
  PbPanels[I] := P;
  PbReal[I] := Real;
  PbPrimary[I] := Primary;
  PbState[I] := -1;
  PaintPlayerButton(I, Real.Enabled, False);
  Real.Left := Real.Left - ScaleX(4000);
end;

{ FramePlay's slider in place of the real progress bar, which moves out of sight but keeps counting. }
procedure AddPlayerProgress(Owner: TComponent; Real: TNewProgressBar);
var
  H: Integer;
begin
  H := ScaleY(8);
  PgTrack := TPanel.Create(Owner);
  PgTrack.Parent := Real.Parent;
  PgTrack.SetBounds(Real.Left, Real.Top + (Real.Height - H) div 2, Real.Width, H);
  PgTrack.Anchors := Real.Anchors;
  PgTrack.BevelOuter := bvNone;
  PgTrack.ParentBackground := False;
  PgTrack.StyleElements := [];
  PgTrack.Caption := '';
  PgTrack.Color := ColTrack;
  PgFill := TPanel.Create(Owner);
  PgFill.Parent := PgTrack;
  PgFill.SetBounds(0, 0, H, H);
  PgFill.BevelOuter := bvNone;
  PgFill.ParentBackground := False;
  PgFill.StyleElements := [];
  PgFill.Caption := '';
  PgFill.Color := ColAccent;
  PgFill.Visible := False;
  PgReal := Real;
  PgW := -1;
  PgFillW := -1;
  Real.Left := Real.Left - ScaleX(4000);
end;

{ Wnd: the form the timer belongs to, so that it ends with the form; 0 for a timer that runs until stopped. }
procedure StartPlayerButtons(Wnd: HWND);
begin
  PbActive := True;
  if PbTimer = 0 then
  begin
    PbTimerWnd := Wnd;
    PbTimer := SetTimer(Wnd, 1, 40, CreateCallback(@PlayerButtonTimer));
  end;
end;

procedure StopPlayerButtons;
begin
  PbActive := False;
  if PbTimer <> 0 then
    KillTimer(PbTimerWnd, PbTimer);
  PbTimer := 0;
  PbTimerWnd := 0;
  SetArrayLength(PbPanels, 0);
  SetArrayLength(PbReal, 0);
  SetArrayLength(PbPrimary, 0);
  SetArrayLength(PbState, 0);
  PgReal := nil;
  PgTrack := nil;
  PgFill := nil;
end;

{ ---- Dialogs in FramePlay's style: the language picker, questions and notices ---- }
procedure DlgButton1Click(Sender: TObject);
begin
  DlgResult := 1;
  DlgForm.Close;
end;

procedure DlgButton2Click(Sender: TObject);
begin
  DlgResult := 2;
  DlgForm.Close;
end;

procedure DlgCloseClick(Sender: TObject);
begin
  DlgForm.Close;
end;

function NewDialogButton(const Caption: String; X, Y, W, H: Integer; Click: TNotifyEvent): TNewButton;
begin
  Result := TNewButton.Create(DlgForm);
  Result.Parent := DlgForm;
  Result.SetBounds(X, Y, W, H);
  Result.Caption := Caption;
  Result.OnClick := Click;
end;

{ A dialog with no focus outlines: a bold title (optional), the text, and one or two buttons (Button2 '' for one).
  Primary is the button in the accent colour, which Enter clicks; Esc and the window's X close the dialog.
  Wide puts two big buttons side by side across the dialog (the language picker); otherwise they sit at the
  bottom right, in the wizard's size. Returns the button clicked (1 or 2), or 0 when the dialog was closed. }
function PlayerDialog(const Caption, Title, Text, Button1, Button2: String; Primary: Integer; Wide: Boolean): Integer;
var
  TitleLabel, TextLabel: TNewStaticText;
  Real1, Real2, RealClose: TNewButton;
  Margin, Gap, BtnW, BtnH, Y, First: Integer;
  OwnTimer: Boolean;
begin
  DlgResult := 0;
  First := GetArrayLength(PbPanels);
  OwnTimer := PbTimer = 0;
  DlgForm := CreateCustomForm(ScaleX(380), ScaleY(150), False, False);
  try
    DlgForm.Caption := Caption;
    DlgForm.Color := ColBack;
    DlgForm.StyleElements := [seBorder];
    Margin := ScaleX(24);
    Y := ScaleY(20);
    if Title <> '' then
    begin
      TitleLabel := TNewStaticText.Create(DlgForm);
      TitleLabel.Parent := DlgForm;
      TitleLabel.StyleElements := [];
      TitleLabel.ShowAccelChar := False;
      TitleLabel.Font.Size := 12;
      TitleLabel.Font.Style := [fsBold];
      TitleLabel.Font.Color := ColText;
      TitleLabel.Caption := Title;
      TitleLabel.Left := Margin;
      TitleLabel.Top := Y;
      Y := Y + TitleLabel.Height + ScaleY(4);
    end;
    TextLabel := TNewStaticText.Create(DlgForm);
    TextLabel.Parent := DlgForm;
    TextLabel.StyleElements := [];
    TextLabel.ShowAccelChar := False;
    TextLabel.AutoSize := False;
    TextLabel.WordWrap := True;
    if Wide then TextLabel.Font.Color := ColTextMuted else TextLabel.Font.Color := ColText;
    TextLabel.SetBounds(Margin, Y, DlgForm.ClientWidth - Margin * 2, ScaleY(15));
    TextLabel.Caption := Text;
    Y := TextLabel.AdjustHeight;

    if Wide then
    begin
      Gap := ScaleX(12);
      BtnH := ScaleY(38);
      BtnW := (DlgForm.ClientWidth - Margin * 2 - Gap) div 2;
      Y := DlgForm.ClientHeight - Margin - BtnH;
      Real1 := NewDialogButton(Button1, Margin, Y, BtnW, BtnH, @DlgButton1Click);
      Real2 := NewDialogButton(Button2, Margin + BtnW + Gap, Y, BtnW, BtnH, @DlgButton2Click);
    end
    else
    begin
      Gap := ScaleX(8);
      BtnH := ScaleY(23);
      if Button2 <> '' then
        BtnW := DlgForm.CalculateButtonWidth([Button1, Button2])
      else
        BtnW := DlgForm.CalculateButtonWidth([Button1]);
      DlgForm.ClientHeight := TextLabel.Top + TextLabel.Height + ScaleY(28) + BtnH + ScaleY(20);
      Y := DlgForm.ClientHeight - ScaleY(20) - BtnH;
      if Button2 <> '' then
      begin
        Real1 := NewDialogButton(Button1, DlgForm.ClientWidth - Margin - BtnW * 2 - Gap, Y, BtnW, BtnH, @DlgButton1Click);
        Real2 := NewDialogButton(Button2, DlgForm.ClientWidth - Margin - BtnW, Y, BtnW, BtnH, @DlgButton2Click);
      end
      else
        Real1 := NewDialogButton(Button1, DlgForm.ClientWidth - Margin - BtnW, Y, BtnW, BtnH, @DlgButton1Click);
    end;
    Real1.Default := Primary = 1;
    if Button2 <> '' then
      Real2.Default := Primary = 2;
    RealClose := NewDialogButton('', -ScaleX(4000), Y, BtnW, BtnH, @DlgCloseClick);
    RealClose.Cancel := True;
    AddPlayerButton(DlgForm, DlgForm, Real1, Primary = 1);
    if Button2 <> '' then
      AddPlayerButton(DlgForm, DlgForm, Real2, Primary = 2);

    { Over the wizard when there is one, else in the middle of the screen. }
    if HaveWizard then
    begin
      DlgForm.Position := poDesigned;
      DlgForm.CenterOnShow := False;
      DlgForm.FlipAndCenterIfNeeded(True, WizardForm, False);
    end
    else
      DlgForm.Position := poScreenCenter;
    if OwnTimer then
      StartPlayerButtons(0);
    DlgForm.ShowModal();
    Result := DlgResult;
  finally
    { The dialog's buttons leave the list before the dialog goes; the wizard's stay. }
    if OwnTimer then
      StopPlayerButtons
    else
    begin
      SetArrayLength(PbPanels, First);
      SetArrayLength(PbReal, First);
      SetArrayLength(PbPrimary, First);
      SetArrayLength(PbState, First);
    end;
    DlgForm.Free;
    DlgForm := nil;
  end;
end;

function HasParam(const Prefix: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if Pos(Uppercase(Prefix), Uppercase(ParamStr(I))) = 1 then
      Result := True;
end;

{ Setup's own internal parameters, which a restarted Setup must not get. }
function IsInternalParam(const P: String): Boolean;
var
  U: String;
begin
  U := Uppercase(P);
  Result := (Pos('/SL5=', U) = 1) or (Pos('/SPAWNWND=', U) = 1) or (Pos('/NOTIFYWND=', U) = 1) or (Pos('/DEBUGWND=', U) = 1);
end;

{ English or Español, before the wizard. Picking the other language restarts Setup in it.
  False: Setup ends here (closed, or restarted in the other language). }
function PickLanguage(): Boolean;
var
  Picked, Primary, Code, I: Integer;
  Lang, Params: String;
begin
  if ActiveLanguage() = 'es' then Primary := 2 else Primary := 1;
  Picked := PlayerDialog('{#AppName}', 'Language  ·  Idioma', 'For Setup and FramePlay  ·  Para la instalación y FramePlay',
    'English', 'Español', Primary, True);
  Result := Picked <> 0;
  if Picked = 2 then Lang := 'es' else Lang := 'en';
  if Result and (Lang <> ActiveLanguage()) then
  begin
    Params := '/LANG=' + Lang;
    for I := 1 to ParamCount do
      if not IsInternalParam(ParamStr(I)) then
        Params := Params + ' ' + AddQuotes(ParamStr(I));
    Log('Restarting Setup in another language: ' + ExpandConstant('{srcexe}') + ' ' + Params);
    if not Exec(ExpandConstant('{cmd}'), '/c start "" ' + AddQuotes(ExpandConstant('{srcexe}')) + ' ' + Params, '', SW_HIDE, ewNoWait, Code) then
      Log('Restart failed: ' + SysErrorMessage(Code));
    Result := False;
  end;
end;

{ An update: say first that it goes over the installed version (no need to uninstall it). }
procedure InitializeWizard();
var
  Prev, Text: String;
begin
  AddPlayerButton(WizardForm, WizardForm.BackButton.Parent, WizardForm.BackButton, False);
  AddPlayerButton(WizardForm, WizardForm.NextButton.Parent, WizardForm.NextButton, True);
  AddPlayerButton(WizardForm, WizardForm.CancelButton.Parent, WizardForm.CancelButton, False);
  AddPlayerButton(WizardForm, WizardForm.DirBrowseButton.Parent, WizardForm.DirBrowseButton, False);
  AddPlayerProgress(WizardForm, WizardForm.ProgressGauge);
  StartPlayerButtons(0);
  HaveWizard := True;
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

{ Pages open with the keyboard on the (hidden) Next button rather than in a field, so no field starts out
  with its text selected. Enter still means Next, and Tab still reaches every field. }
procedure CurPageChanged(CurPageID: Integer);
begin
  if WizardForm.NextButton.Visible and WizardForm.NextButton.Enabled then
    WizardForm.ActiveControl := WizardForm.NextButton;
end;

{ "Exit Setup?" in FramePlay's style. }
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if Cancel and Confirm then
  begin
    Confirm := False;
    Cancel := PlayerDialog(WizardForm.Caption, SetupMessage(msgExitSetupTitle), SetupMessage(msgExitSetupMessage),
      SetupMessage(msgButtonYes), SetupMessage(msgButtonNo), 1, False) = 1;
  end;
end;

{ Windows' Apps settings start the uninstaller with /SILENT /FRAMEPLAYUI: it then asks and reports in FramePlay's
  style (see InitializeUninstall) instead of with Windows message boxes. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Key: String;
begin
  if CurStep = ssPostInstall then
  begin
    Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#AppGuid}}_is1';
    if RegValueExists(HKA, Key, 'UninstallString') then
      RegWriteStringValue(HKA, Key, 'UninstallString', '"' + ExpandConstant('{uninstallexe}') + '" /SILENT /FRAMEPLAYUI');
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
  Download: Boolean;
begin
  Result := True;
  { The language picker, unless Setup is silent or already got a language (/LANG=, also after a restart). }
  if not WizardSilent() and not HasParam('/LANG=') then
    if not PickLanguage() then
    begin
      Result := False;
      Exit;
    end;
  if not HasDotNetDesktop8() then
  begin
    if WizardSilent() then
      Download := SuppressibleMsgBox(CustomMessage('NeedDotNet'), mbConfirmation, MB_YESNO, IDNO) = IDYES
    else
      Download := PlayerDialog('{#AppName}', CustomMessage('NeedDotNetTitle'), CustomMessage('NeedDotNet'),
        SetupMessage(msgButtonYes), SetupMessage(msgButtonNo), 1, False) = 1;
    if Download then
      ShellExec('open', '{#DotNetUrl}', '', '', SW_SHOWNORMAL, ewNoWait, Code);
  end;
end;

procedure DeinitializeSetup();
begin
  StopPlayerButtons;
end;

{ Started from Windows' Apps settings (/FRAMEPLAYUI, see CurStepChanged), the uninstaller asks first in FramePlay's
  style. Started plainly or silently, it behaves as usual. }
function InitializeUninstall(): Boolean;
begin
  Result := True;
  if HasParam('/FRAMEPLAYUI') then
    Result := PlayerDialog(FmtMessage(SetupMessage(msgUninstallAppFullTitle), ['{#AppName}']), CustomMessage('UninstallTitle'),
      FmtMessage(SetupMessage(msgConfirmUninstall), ['{#AppName}']), SetupMessage(msgButtonYes), SetupMessage(msgButtonNo), 1, False) = 1;
end;

{ The uninstaller's progress window in the same style. Its timer belongs to the window and ends with it. }
procedure InitializeUninstallProgressForm();
begin
  AddPlayerButton(UninstallProgressForm, UninstallProgressForm.CancelButton.Parent, UninstallProgressForm.CancelButton, False);
  AddPlayerProgress(UninstallProgressForm, UninstallProgressForm.ProgressBar);
  StartPlayerButtons(UninstallProgressForm.Handle);
end;

procedure DeinitializeUninstall();
begin
  StopPlayerButtons;
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
var
  Answer: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveAssociations();
    { The progress window is gone by now (its timer went with it): forget its buttons. }
    StopPlayerButtons;
  end
  else if (CurUninstallStep = usDone) and HasParam('/FRAMEPLAYUI') then
    Answer := PlayerDialog(FmtMessage(SetupMessage(msgUninstallAppFullTitle), ['{#AppName}']), '',
      FmtMessage(SetupMessage(msgUninstalledAll), ['{#AppName}']), SetupMessage(msgButtonOK), '', 1, False);
end;
