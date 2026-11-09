; =====================================================================
; TempGuard - Inno Setup 6 installer script
;
; Build (CI):
;   ISCC.exe /DAppVersion=1.0.0 /DSourceExe=..\src\TempGuard\bin\Release\TempGuard.exe installer\TempGuard.iss
;
; Design notes:
;   * PrivilegesRequired=lowest  -> per-user install, NO UAC prompt.
;   * InstallDir = %LOCALAPPDATA%\Programs\TempGuard
;   * Auto-start uses HKCU Run with the same value name as the legacy
;     PowerShell script, so upgrading silently replaces the old entry.
;   * PrepareToInstall stops a running instance before replacing the exe,
;     so upgrades never fail with "file in use".
; =====================================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\src\TempGuard\bin\Release\TempGuard.exe"
#endif
#ifndef AppIcon
  #define AppIcon "..\src\TempGuard\TempGuard.ico"
#endif

#define AppName       "TempGuard"
#define AppPublisher  "shenge-work"
#define AppURL        "https://github.com/shenge-work/TempGuard"
#define AppExeName    "TempGuard.exe"

[Setup]
AppId={{9D2F6B41-7C8E-4A53-B0D1-3E5F8A2C6B94}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription=TempGuard - Lightweight Windows tray temperature monitor
DefaultDirName={localappdata}\Programs\TempGuard
DefaultGroupName=TempGuard
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts
OutputBaseFilename=TempGuard-Setup-{#AppVersion}
SetupIconFile={#AppIcon}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle=安装 TempGuard
SetupWindowTitle=安装 - TempGuard
WelcomeLabel1=欢迎使用 TempGuard 安装程序
WelcomeLabel2=这将在你的电脑上安装 [name/ver]。%n%nTempGuard 是一款轻量后台温度监控工具，托盘图标会实时显示 CPU 温度数字。%n%n建议先关闭其它安装程序再继续。
FinishedHeadingLabel=TempGuard 安装完成
FinishedLabelNoIcons=安装已完成。%n%nTempGuard 已在后台启动，任务栏右下角会显示实时温度数字。
FinishedLabel=安装已完成。%n%nTempGuard 已在后台启动，任务栏右下角会显示实时温度数字。
ButtonNext=下一步(&N) >
ButtonBack=< 上一步(&B)
ButtonInstall=安装(&I)
ButtonFinish=完成(&F)
ButtonCancel=取消
ButtonBrowse=浏览(&R)...
ClickNext=点击"下一步"继续，或点击"取消"退出安装程序。

[Tasks]
Name: "autostart"; Description: "开机自动启动 TempGuard"; GroupDescription: "附加选项："; Flags: checkedonce
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项："; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; DestName: "CHANGELOG.md"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Registry]
; HKCU Run - same value name as the legacy PowerShell version => clean upgrade
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "TempGuard"; \
    ValueData: """{app}\{#AppExeName}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Icons]
Name: "{group}\TempGuard"; Filename: "{app}\{#AppExeName}"; Parameters: "--minimized"; Comment: "轻量后台温度监控"
Name: "{group}\卸载 TempGuard"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TempGuard"; Filename: "{app}\{#AppExeName}"; Parameters: "--minimized"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--minimized"; \
    Description: "安装完成后启动 TempGuard"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden; RunOnceId: "StopTempGuard"

[UninstallDelete]
Type: files; Name: "{app}\README.md"
Type: files; Name: "{app}\CHANGELOG.md"
Type: files; Name: "{app}\LICENSE.txt"

[Code]
// Stop any running instance before replacing the exe.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  Exec('taskkill.exe', '/F /IM {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
