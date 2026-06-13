; =============================================================
;  Thoth - Instalador Windows
;
;  Compilado via build.ps1. Variaveis esperadas:
;    /DMyAppVersion=<versao>
;    /DMyStageDir=<caminho absoluto do staging publish>
;    /DMyOutputDir=<caminho absoluto de saida do setup.exe>
;
;  Identificadores legados mantidos de proposito para upgrades de
;  instalacoes existentes: AppId, nome interno do servico Windows
;  (PromptTasks), regra de firewall e pastas {app}\PromptTasks /
;  {commonappdata}\PromptTasks. Apenas textos de exibicao e o binario
;  (Thoth.Api.exe) usam o novo nome.
; =============================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyStageDir
  #define MyStageDir "..\build\publish"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\dist"
#endif

[Setup]
AppId={{B9B59443-D9B8-4A7E-A7C4-2B4A3F0C0477}
AppName=Thoth
AppVersion={#MyAppVersion}
AppVerName=Thoth {#MyAppVersion}
AppPublisher=Thoth
VersionInfoProductName=Thoth
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\PromptTasks
DefaultGroupName=Thoth
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#MyOutputDir}
OutputBaseFilename=Thoth-Setup-{#MyAppVersion}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Thoth
UninstallDisplayIcon={app}\PromptTasks\Thoth.Api.exe
CloseApplications=no
RestartApplications=no

[Languages]
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "{#MyStageDir}\PromptTasks\*"; DestDir: "{app}\PromptTasks"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "scripts\Stop-PromptTasksService.ps1"; Flags: dontcopy

[InstallDelete]
; Remove binarios da era PromptTasks.* em upgrades; o publish atual gera apenas Thoth.*.
Type: files; Name: "{app}\PromptTasks\PromptTasks.*"

[Run]
Filename: "http://localhost:8091"; Description: "Abrir Thoth"; Flags: postinstall nowait skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop PromptTasks"; Flags: runhidden; RunOnceId: "StopPromptTasks"
Filename: "{sys}\sc.exe"; Parameters: "delete PromptTasks"; Flags: runhidden; RunOnceId: "DeletePromptTasks"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Prompt Tasks (8091)"""; Flags: runhidden; RunOnceId: "DeletePromptTasksFirewall"

[Code]
var
  DbPage: TInputQueryWizardPage;
  AgentOptionsPage: TInputOptionWizardPage;
  AgentPathsPage: TInputQueryWizardPage;
  ConfigDefaultsLoaded: Boolean;

function JsonEscape(Value: String): String;
var
  I: Integer;
  Ch: String;
begin
  Result := '';
  for I := 1 to Length(Value) do
  begin
    Ch := Copy(Value, I, 1);
    if Ch = '\' then
      Result := Result + '\\'
    else if Ch = '"' then
      Result := Result + '\"'
    else if Ch = #13 then
      Result := Result + '\r'
    else if Ch = #10 then
      Result := Result + '\n'
    else if Ch = #9 then
      Result := Result + '\t'
    else
      Result := Result + Ch;
  end;
end;

function ReplaceString(Value: String; OldValue: String; NewValue: String): String;
begin
  Result := Value;
  StringChangeEx(Result, OldValue, NewValue, True);
end;

function NpgsqlEscape(Value: String): String;
begin
  if (Pos(';', Value) > 0) or (Pos('"', Value) > 0) or (Pos(' ', Value) > 0) or (Pos('=', Value) > 0) then
    Result := '"' + ReplaceString(Value, '"', '""') + '"'
  else
    Result := Value;
end;

function RemoveOuterQuotes(Value: String): String;
begin
  Result := Value;
  if (Length(Result) >= 2) and (Copy(Result, 1, 1) = '"') and (Copy(Result, Length(Result), 1) = '"') then
  begin
    Result := Copy(Result, 2, Length(Result) - 2);
    Result := ReplaceString(Result, '""', '"');
  end;
end;

function ExtractJsonString(Json: String; Key: String; DefaultValue: String): String;
var
  Token: String;
  Remainder: String;
  Value: String;
  KeyPos: Integer;
  ColonPos: Integer;
  I: Integer;
  Escaped: Boolean;
  Ch: String;
begin
  Result := DefaultValue;
  Token := '"' + Key + '"';
  KeyPos := Pos(Token, Json);
  if KeyPos = 0 then
    Exit;

  Remainder := Copy(Json, KeyPos + Length(Token), Length(Json));
  ColonPos := Pos(':', Remainder);
  if ColonPos = 0 then
    Exit;

  Remainder := Trim(Copy(Remainder, ColonPos + 1, Length(Remainder)));
  if Copy(Remainder, 1, 1) <> '"' then
    Exit;

  Remainder := Copy(Remainder, 2, Length(Remainder));
  Value := '';
  Escaped := False;
  for I := 1 to Length(Remainder) do
  begin
    Ch := Copy(Remainder, I, 1);
    if Escaped then
    begin
      if Ch = 'r' then
        Value := Value + #13
      else if Ch = 'n' then
        Value := Value + #10
      else if Ch = 't' then
        Value := Value + #9
      else
        Value := Value + Ch;
      Escaped := False;
    end
    else if Ch = '\' then
      Escaped := True
    else if Ch = '"' then
    begin
      Result := Value;
      Exit;
    end
    else
      Value := Value + Ch;
  end;
end;

function ExtractJsonBoolean(Json: String; Key: String; DefaultValue: Boolean): Boolean;
var
  Token: String;
  Remainder: String;
  KeyPos: Integer;
  ColonPos: Integer;
begin
  Result := DefaultValue;
  Token := '"' + Key + '"';
  KeyPos := Pos(Token, Json);
  if KeyPos = 0 then
    Exit;

  Remainder := Copy(Json, KeyPos + Length(Token), Length(Json));
  ColonPos := Pos(':', Remainder);
  if ColonPos = 0 then
    Exit;

  Remainder := Trim(Copy(Remainder, ColonPos + 1, Length(Remainder)));
  if CompareText(Copy(Remainder, 1, 4), 'true') = 0 then
    Result := True
  else if CompareText(Copy(Remainder, 1, 5), 'false') = 0 then
    Result := False;
end;

function ExtractConnectionValue(ConnectionString: String; Key: String; DefaultValue: String): String;
var
  I: Integer;
  Ch: String;
  Segment: String;
  InQuotes: Boolean;
  EqPos: Integer;
  SegmentKey: String;
  SegmentValue: String;
begin
  Result := DefaultValue;
  Segment := '';
  InQuotes := False;

  for I := 1 to Length(ConnectionString) + 1 do
  begin
    if I <= Length(ConnectionString) then
      Ch := Copy(ConnectionString, I, 1)
    else
      Ch := ';';

    if Ch = '"' then
      InQuotes := not InQuotes;

    if (Ch = ';') and (not InQuotes) then
    begin
      EqPos := Pos('=', Segment);
      if EqPos > 0 then
      begin
        SegmentKey := Trim(Copy(Segment, 1, EqPos - 1));
        SegmentValue := Trim(Copy(Segment, EqPos + 1, Length(Segment)));
        if CompareText(SegmentKey, Key) = 0 then
        begin
          Result := RemoveOuterQuotes(SegmentValue);
          Exit;
        end;
      end;
      Segment := '';
    end
    else
      Segment := Segment + Ch;
  end;
end;

function BoolToJson(Value: Boolean): String;
begin
  if Value then
    Result := 'true'
  else
    Result := 'false';
end;

function DefaultProfilePath(RelativePath: String): String;
var
  Profile: String;
begin
  Profile := GetEnv('USERPROFILE');
  if Profile = '' then
    Profile := GetEnv('HOMEDRIVE') + GetEnv('HOMEPATH');

  if Profile = '' then
    Result := ''
  else
    Result := AddBackslash(Profile) + RelativePath;
end;

function ValidateAgentPath(Value: String; LabelText: String): Boolean;
begin
  Result := False;
  if Trim(Value) = '' then
  begin
    MsgBox('Informe o caminho de ' + LabelText + '.', mbError, MB_OK);
    Exit;
  end;

  if Copy(Trim(Value), 1, 1) = '~' then
  begin
    MsgBox('Nao use ~ em ' + LabelText + '. Informe o caminho absoluto do usuario Windows.', mbError, MB_OK);
    Exit;
  end;

  Result := True;
end;

function BuildConnectionString(): String;
begin
  Result :=
    'Host=' + NpgsqlEscape(DbPage.Values[0]) + ';' +
    'Port=' + NpgsqlEscape(DbPage.Values[1]) + ';' +
    'Database=' + NpgsqlEscape(DbPage.Values[2]) + ';' +
    'Username=' + NpgsqlEscape(DbPage.Values[3]) + ';' +
    'Password=' + NpgsqlEscape(DbPage.Values[4]);
end;

procedure WriteProductionConfig();
var
  ConfigPath: String;
  LogPath: String;
  Content: String;
begin
  ConfigPath := ExpandConstant('{app}\PromptTasks\appsettings.Production.json');
  LogPath := ExpandConstant('{commonappdata}\PromptTasks\logs\thoth-.log');
  ForceDirectories(ExtractFileDir(ConfigPath));
  ForceDirectories(ExpandConstant('{commonappdata}\PromptTasks\logs'));

  Content :=
    '{' + #13#10 +
    '  "Urls": "http://0.0.0.0:8091",' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "DefaultConnection": "' + JsonEscape(BuildConnectionString()) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "AgentUsage": {' + #13#10 +
    '    "Enabled": ' + BoolToJson(AgentOptionsPage.Values[0]) + ',' + #13#10 +
    '    "Claude": {' + #13#10 +
    '      "CredentialsPath": "' + JsonEscape(AgentPathsPage.Values[0]) + '",' + #13#10 +
    '      "ProjectsDir": "' + JsonEscape(AgentPathsPage.Values[1]) + '",' + #13#10 +
    '      "EnableTranscriptFallback": true' + #13#10 +
    '    },' + #13#10 +
    '    "Codex": {' + #13#10 +
    '      "SessionsDir": "' + JsonEscape(AgentPathsPage.Values[2]) + '"' + #13#10 +
    '    }' + #13#10 +
    '  },' + #13#10 +
    '  "Terminals": {' + #13#10 +
    '    "Enabled": true,' + #13#10 +
    '    "DefaultShell": "powershell.exe"' + #13#10 +
    '  },' + #13#10 +
    '  "Gemini": {' + #13#10 +
    '    "ApiKey": "' + JsonEscape(DbPage.Values[5]) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "Serilog:WriteTo:1:Args:path": "' + JsonEscape(LogPath) + '"' + #13#10 +
    '}' + #13#10;

  if not SaveStringToFile(ConfigPath, Content, False) then
    RaiseException('Nao foi possivel gravar ' + ConfigPath);
end;

function ExecCommand(FileName: String; Parameters: String; Required: Boolean): Boolean;
var
  ResultCode: Integer;
begin
  Log('Executando: ' + FileName + ' ' + Parameters);
  Result := Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if (not Result) or (ResultCode <> 0) then
  begin
    Log('Falha ao executar comando. ExitCode=' + IntToStr(ResultCode));
    if Required then
      RaiseException('Falha ao executar comando: ' + FileName + ' ' + Parameters);
  end;
end;

function ScParameters(Command: String): String;
begin
  Result := Command;
end;

function ServiceBinPath(): String;
begin
  Result := '\"' + ExpandConstant('{app}\PromptTasks\Thoth.Api.exe') + '\" --environment Production';
end;

procedure ConfigureServiceAndFirewall();
var
  Sc: String;
  Netsh: String;
begin
  Sc := ExpandConstant('{sys}\sc.exe');
  Netsh := ExpandConstant('{sys}\netsh.exe');

  ExecCommand(Sc, ScParameters('stop PromptTasks'), False);
  ExecCommand(Sc, ScParameters('create PromptTasks binPath= "' + ServiceBinPath() + '" start= auto DisplayName= "Thoth"'), False);
  ExecCommand(Sc, ScParameters('config PromptTasks binPath= "' + ServiceBinPath() + '" start= auto DisplayName= "Thoth"'), True);
  ExecCommand(Sc, ScParameters('description PromptTasks "Thoth production service."'), True);
  ExecCommand(Sc, ScParameters('failure PromptTasks reset= 60 actions= restart/5000/restart/10000/restart/30000'), True);

  ExecCommand(Netsh, 'advfirewall firewall delete rule name="Prompt Tasks (8091)"', False);
  ExecCommand(Netsh, 'advfirewall firewall add rule name="Prompt Tasks (8091)" dir=in action=allow protocol=TCP localport=8091', True);

  ExecCommand(Sc, ScParameters('start PromptTasks'), True);
end;

procedure LoadExistingConfigDefaults();
var
  ConfigPath: String;
  JsonContent: AnsiString;
  Json: String;
  ConnectionString: String;
begin
  ConfigPath := AddBackslash(WizardDirValue()) + 'PromptTasks\appsettings.Production.json';
  if not LoadStringFromFile(ConfigPath, JsonContent) then
    Exit;

  Json := JsonContent;
  ConnectionString := ExtractJsonString(Json, 'DefaultConnection', '');
  if ConnectionString <> '' then
  begin
    DbPage.Values[0] := ExtractConnectionValue(ConnectionString, 'Host', DbPage.Values[0]);
    DbPage.Values[1] := ExtractConnectionValue(ConnectionString, 'Port', DbPage.Values[1]);
    DbPage.Values[2] := ExtractConnectionValue(ConnectionString, 'Database', DbPage.Values[2]);
    DbPage.Values[3] := ExtractConnectionValue(ConnectionString, 'Username', DbPage.Values[3]);
    DbPage.Values[4] := ExtractConnectionValue(ConnectionString, 'Password', DbPage.Values[4]);
  end;

  DbPage.Values[5] := ExtractJsonString(Json, 'ApiKey', DbPage.Values[5]);
  AgentOptionsPage.Values[0] := ExtractJsonBoolean(Json, 'Enabled', AgentOptionsPage.Values[0]);
  AgentPathsPage.Values[0] := ExtractJsonString(Json, 'CredentialsPath', AgentPathsPage.Values[0]);
  AgentPathsPage.Values[1] := ExtractJsonString(Json, 'ProjectsDir', AgentPathsPage.Values[1]);
  AgentPathsPage.Values[2] := ExtractJsonString(Json, 'SessionsDir', AgentPathsPage.Values[2]);
end;

procedure InitializeWizard();
begin
  DbPage := CreateInputQueryPage(
    wpSelectDir,
    'Configuracao do PostgreSQL',
    'Informe a conexao com o PostgreSQL instalado manualmente.',
    'O banco e o usuario devem existir antes de iniciar o servico. Use scripts\db-bootstrap.ps1 se necessario.');

  DbPage.Add('Host:', False);
  DbPage.Add('Porta:', False);
  DbPage.Add('Banco:', False);
  DbPage.Add('Usuario:', False);
  DbPage.Add('Senha:', True);
  DbPage.Add('Gemini API Key (opcional):', True);

  DbPage.Values[0] := 'localhost';
  DbPage.Values[1] := '5435';
  DbPage.Values[2] := 'prompttasks';
  DbPage.Values[3] := 'prompttasks';
  DbPage.Values[4] := 'prompttasks';
  DbPage.Values[5] := '';

  AgentOptionsPage := CreateInputOptionPage(
    DbPage.ID,
    'Monitoramento de uso dos agentes',
    'Configure se o servico deve monitorar uso do Claude e Codex.',
    'Quando o servico roda como LocalSystem, informe caminhos absolutos do seu usuario Windows.',
    False,
    False);
  AgentOptionsPage.Add('Habilitar Agent Usage');
  AgentOptionsPage.Values[0] := False;

  AgentPathsPage := CreateInputQueryPage(
    AgentOptionsPage.ID,
    'Caminhos do Agent Usage',
    'Informe os caminhos absolutos das credenciais e sessoes.',
    'Nao use ~ em producao. Use caminhos como C:\Users\seu-usuario\.claude e C:\Users\seu-usuario\.codex.');
  AgentPathsPage.Add('Claude credentials:', False);
  AgentPathsPage.Add('Claude projects:', False);
  AgentPathsPage.Add('Codex sessions:', False);
  AgentPathsPage.Values[0] := DefaultProfilePath('.claude\.credentials.json');
  AgentPathsPage.Values[1] := DefaultProfilePath('.claude\projects');
  AgentPathsPage.Values[2] := DefaultProfilePath('.codex\sessions');
  ConfigDefaultsLoaded := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = DbPage.ID) and (not ConfigDefaultsLoaded) then
  begin
    LoadExistingConfigDefaults();
    ConfigDefaultsLoaded := True;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageID = AgentPathsPage.ID) and (not AgentOptionsPage.Values[0]) then
    Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;
  if CurPageID = AgentPathsPage.ID then
  begin
    if not ValidateAgentPath(AgentPathsPage.Values[0], 'Claude credentials') then
    begin
      Result := False;
      Exit;
    end;

    if not ValidateAgentPath(AgentPathsPage.Values[1], 'Claude projects') then
    begin
      Result := False;
      Exit;
    end;

    if not ValidateAgentPath(AgentPathsPage.Values[2], 'Codex sessions') then
    begin
      Result := False;
      Exit;
    end;

    Exit;
  end;

  if CurPageID <> DbPage.ID then
    Exit;

  if Trim(DbPage.Values[0]) = '' then
  begin
    MsgBox('Informe o host do PostgreSQL.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  Port := StrToIntDef(DbPage.Values[1], 0);
  if (Port <= 0) or (Port > 65535) then
  begin
    MsgBox('Informe uma porta valida para o PostgreSQL.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if Trim(DbPage.Values[2]) = '' then
  begin
    MsgBox('Informe o nome do banco.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if Trim(DbPage.Values[3]) = '' then
  begin
    MsgBox('Informe o usuario do banco.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('Stop-PromptTasksService.ps1');

  Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Stop-PromptTasksService.ps1') + '"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  if ResultCode <> 0 then
    Result := 'Nao foi possivel parar o servico PromptTasks. Feche o aplicativo e tente novamente.'
  else
    Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteProductionConfig();
    ConfigureServiceAndFirewall();
  end;
end;
