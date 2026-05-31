# Plano: Limites de uso do Claude e Codex no header (v3 — aprovado p/ correcao final)

## Contexto

Item do roadmap do `README.md` ("Tarefas Futuras", linha ~180): exibir no header informacoes dos agentes Claude e Codex, com disponibilidade, contexto operacional e **limites de uso atuais**.

Objetivo: mostrar no header, por agente, quanto do limite ja foi usado e quanto resta (% + janelas + horario de reset), **replicando a estrategia do `nimbalyst`** — sem usar a CLI dos agentes. A API roda em `localhost`, na mesma maquina do usuario.

### Estrategia correta (confirmada nos fontes do nimbalyst)

Verificado em `packages/electron/src/main/services/ClaudeUsageService.ts` e `CodexUsageService.ts`. **A estrategia difere por agente:**

**Claude — token local + API OAuth (NAO soma transcricoes).**
- Le `claudeAiOauth.accessToken` de `~/.claude/.credentials.json` (Windows/Linux; override por env `CLAUDE_CREDENTIALS_PATH`; fallback `~/.config/claude/.credentials.json`). No macOS o nimbalyst usa o Keychain — alvo deste app e Windows, entao arquivo. **Nunca expor/logar o token.**
- Chama `GET https://api.anthropic.com/api/oauth/usage` com headers:
  - `Authorization: Bearer <accessToken>`
  - `Content-Type: application/json`
  - `Accept: application/json`
  - `anthropic-beta: oauth-2025-04-20`
  - `User-Agent: claude-code/<versao>` (UA configuravel)
  - timeout ~10s.
- Resposta mapeada: `five_hour`, `seven_day`, `seven_day_opus`, cada um `{ utilization: number (0–100), resets_at: string|null (ISO-8601) }`. Usado direto: `restante = 100 - utilization`; reset = `resets_at`.
- Erros viram motivo (`Status`): `NoToken`, `Unauthorized` (401/403), `RateLimited` (429), `HttpError` (outros status), `Timeout`, `NetworkError`. Cache/min-interval **60s**. Sem refresh de token (na expiracao retorna `Unauthorized`).
- A soma de tokens das transcricoes (`.claude/projects`) **NAO** e a fonte primaria. Pode existir apenas como **fallback opcional explicito, marcado como "estimado"** (desligado por padrao).

**Codex — JSONL locais (sem API/CLI).**
- Varre `~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl` (anos/meses/dias recentes); ordena por `mtime` desc; le os **mais recentes** (default 5, configuravel); em cada um le de tras pra frente ate achar o ultimo `rate_limits` valido. Campo em `payload.rate_limits ?? payload.info.rate_limits` (eventos `token_count`).
- Shape: `primary` (5h) e `secondary` (semanal), cada um `{ used_percent (0–100), window_minutes, resets_at (epoch SEGUNDOS) }`; `credits?` `{ has_credits, unlimited, balance }`; `plan_type`/`limit_id` quando presentes.
- **CRITICO — descartar buckets expirados:** janela ativa apenas se `resets_at` nao for numero ou `resets_at > now` (segundos). Se `resets_at*1000 < agora`, expirada -> ignorar (continuar lendo linhas/arquivos mais antigos). Sem nenhuma ativa -> `NoData`.
- Cache 30s. Sem fallback ao percentual (token_count e so contexto). Nao le `config.toml`/`auth.json`.

### Decisoes do usuario (mantidas, com ajuste de realtime)
- **UI:** dois indicadores compactos no header (Claude / Codex) com %, e **popover** ao clicar com as janelas, tokens (quando houver) e reset.
- **Realtime via SignalR**, com origem de dado distinta por agente:
  - **Codex:** watcher de arquivos em `~/.codex/sessions` (debounce) + reconcile periodico.
  - **Claude:** o dado vem da API; refresh por timer respeitando o min-interval (60s) e push por SignalR. Throttle pelo cache evita chamar a API da Anthropic em excesso quando muitos arquivos mudam.

---

## Backend (.NET 10 / Clean Architecture)

Nova feature `AgentUsage`, seguindo os padroes existentes.

### 1. Contratos (Application)
- **DTO** em `backend/src/PromptTasks.Application/Common/Models/AgentUsageDto.cs`:
  - `AgentUsageDto(DateTimeOffset CapturedAtUtc, AgentUsageInfo Claude, AgentUsageInfo Codex)`
  - `AgentUsageInfo(string Agent, AgentUsageStatus Status, int? HttpStatusCode, string? StatusDetail, string? Plan, IReadOnlyList<AgentUsageWindow> Windows)` — `HttpStatusCode`/`StatusDetail` **sanitizados** (mensagem curta, sem token/segredo) para diagnosticar `HttpError` sem depender de log.
  - `AgentUsageWindow(string Key, string Label, double UsedPercent, DateTimeOffset? ResetsAtUtc, int? WindowMinutes, bool Estimated, long? UsedTokens, long? LimitTokens)`. Chaves: Claude -> `five_hour`, `seven_day`, `seven_day_opus`; Codex -> `primary`(5h), `secondary`(semanal). Lista flexivel (Claude ate 3, Codex 2).
  - `enum AgentUsageStatus { Ok, NoToken, Unauthorized, RateLimited, HttpError, Timeout, NetworkError, NoData, Disabled, Unavailable }`.
- **Interfaces** em `Common/Interfaces/`: `IClaudeUsageReader`, `ICodexUsageReader` (cada um `Task<AgentUsageInfo> ReadAsync(ct)`) e `IAgentUsageReader` (agregador, `Task<AgentUsageDto> ReadAsync(ct)`).

### 2. Claude (Infrastructure, via HTTP) — nova pasta `backend/src/PromptTasks.Infrastructure/AgentUsage/`
- **`ClaudeUsageReader : IClaudeUsageReader`** (`AddScoped`). Depende de `HttpClient` (typed/named via `IHttpClientFactory`), `IOptions<AgentUsageOptions>`, `IMemoryCache`, `IDateTimeProvider`.
  - Token: abrir `~/.claude/.credentials.json`, `Newtonsoft.Json` para extrair **somente** `claudeAiOauth.accessToken` (e opcionalmente `subscriptionType`/`rateLimitTier` p/ `Plan`). Sem token -> `Status=NoToken`. Nunca logar/serializar o token.
  - `GET` no `UsageEndpoint` com os headers acima (todos configuraveis). Mapear `five_hour`/`seven_day`/`seven_day_opus` -> `AgentUsageWindow` (`UsedPercent=utilization`, `ResetsAtUtc=DateTimeOffset.Parse(resets_at)`, `Estimated=false`).
  - Erros -> `Status` + `HttpStatusCode`/`StatusDetail` sanitizados (401/403=Unauthorized, 429=RateLimited, outros !ok=HttpError, Abort=Timeout, excecao de rede=NetworkError). Parsing defensivo.
  - **Cache/min-interval 60s** (`IMemoryCache`, chave "claude-usage").
  - **Fallback opcional** (`AgentUsageOptions.Claude.EnableTranscriptFallback`, default `false`): somar tokens de `~/.claude/projects/*/*.jsonl` nas janelas 5h/7d vs orcamento por tier, `Estimated=true`; so quando habilitado e a API falhar.

### 3. Codex (Infrastructure, via arquivos) — mesma pasta
- **`CodexUsageReader : ICodexUsageReader`** (`AddScoped`). Depende de `IOptions<AgentUsageOptions>`, `IMemoryCache`, `IDateTimeProvider`.
  - Resolver `~/.codex/sessions` (override `CODEX_HOME`/options). Listar `rollout-*.jsonl`, ordenar por `LastWriteTimeUtc` desc, pegar `MaxFilesToScan` (default 5). Ler tail-first ate o ultimo `rate_limits` valido; abrir com `FileShare.ReadWrite | FileShare.Delete` (reutilizar postura de `LinkedDocumentFileService.cs`).
  - **Filtro de expiracao** (`resets_at > now` em segundos via `IDateTimeProvider`); validos -> `AgentUsageWindow` (`UsedPercent=used_percent`, `WindowMinutes=window_minutes`, `ResetsAtUtc=FromUnixTimeSeconds(resets_at)`, `Estimated=false`); `Plan=plan_type`.
  - Sem janela valida -> `Status=NoData`. **Cache 30s**. Parsing defensivo por linha; falha geral -> `Status=Unavailable`.

### 4. Agregador + Query + Controller
- `AgentUsageReader : IAgentUsageReader` combina os dois readers (em paralelo) num `AgentUsageDto` com `CapturedAtUtc`. **Se `AgentUsage:Enabled=false`, retorna ambos com `Status=Disabled` sem acessar arquivos/API.**
- `Features/AgentUsage/Queries/GetAgentUsage/`: `GetAgentUsageQuery : IRequest<AgentUsageDto>` + `GetAgentUsageHandler` (injeta `IAgentUsageReader`) — padrao de `GetWorkflowBoardHandler`.
- `backend/src/PromptTasks.Api/Controllers/AgentUsageController.cs` (padrao `FilesController.cs`): `[HttpGet("agent-usage")] -> Ok(await sender.Send(new GetAgentUsageQuery(), ct))`.

### 5. Tempo real (SignalR)
- **Contrato:** adicionar `Task AgentUsageUpdated(AgentUsageDto usage);` na interface **`backend/src/PromptTasks.Application/Common/Realtime/IPromptClient.cs`** (todos os metodos desse contrato retornam `Task`; e aqui, NAO em `PromptHub.cs`). Broadcast global `Clients.All`.
- **Notifier:** `IAgentUsageNotifier` (Application `Common/Realtime/`) + `SignalRAgentUsageNotifier` (Api `Realtime/`) com `IHubContext<PromptHub, IPromptClient>`, no molde de `SignalRLinkedDocumentNotifier.cs`. `AddScoped` no `Api/DependencyInjection.cs`.
- **Servico de atualizacao:** `AgentUsageRefreshService : BackgroundService` (Infrastructure/AgentUsage), scoped via `IServiceScopeFactory` (padrao `LinkedDocumentWatcherService.cs`):
  - **Codex:** `FileSystemWatcher` recursivo em `~/.codex/sessions` (`*.jsonl`, debounce) -> recomputa Codex -> push.
  - **Claude:** `PeriodicTimer` no min-interval (>= cache 60s) -> recomputa Claude -> push.
  - **Reconcile** periodico (ex.: 30s) p/ refletir expiracao/reset sem mudanca de arquivo.
  - **Throttle:** cache (60s Claude / 30s Codex) impede rajadas de chamadas a API/IO.
  - **Se `Enabled=false`, o servico NAO inicia watcher/timer.** Registrar singleton + `IHostedService` no `Infrastructure/DependencyInjection.cs`.

### 6. Options + DI
- `AgentUsageOptions` (Infrastructure/AgentUsage), espelhando `LinkedDocumentOptions.cs`:
  - Geral: `Enabled` (default `true`). Com `Enabled=false` o endpoint retorna `Disabled` e o background service nao inicia.
  - Claude: `CredentialsPath?`, `UsageEndpoint` (default a URL acima), `AnthropicBetaHeader` (`oauth-2025-04-20`), `UserAgent` (`claude-code/<versao>`), `RequestTimeoutSeconds` (10), `CacheTtlSeconds` (60), `EnableTranscriptFallback` (false), `TierBudgets` (so fallback).
  - Codex: `SessionsDir?`, `MaxFilesToScan` (5), `CacheTtlSeconds` (30).
  - Realtime: `ReconcileSeconds`, `DebounceMilliseconds`.
- DI Infrastructure: `services.AddHttpClient("anthropic-usage", ...)` (BaseAddress/timeout), `Configure<AgentUsageOptions>(...)` (secao `"AgentUsage"`), `AddScoped` dos readers e do agregador, watcher singleton + hosted service. Api DI: `AddScoped<IAgentUsageNotifier, SignalRAgentUsageNotifier>()`.

### 7. Testes (backend)
- **Claude (`ClaudeUsageReaderTests`)** com `HttpMessageHandler` fake:
  - sucesso (mapeia `five_hour`/`seven_day`/`seven_day_opus`, utilization 0–100, resets ISO); verifica que os headers `Authorization`/`anthropic-beta`/`Accept`/`User-Agent` sao enviados;
  - `401`/`403` -> Unauthorized; `429` -> RateLimited; sem token -> NoToken; rede/timeout -> NetworkError/Timeout; `HttpError` popula `HttpStatusCode`/`StatusDetail`.
  - **Seguranca:** o `accessToken` NUNCA aparece no DTO serializado, em `StatusDetail`, nem em logs.
  - (Se habilitado) fallback de transcricao marca `Estimated=true`.
- **Codex (`CodexUsageReaderTests`)** com fixtures `.jsonl` temporarias:
  - snapshot valido -> percentuais/reset corretos;
  - **`rate_limits` expirado** (`resets_at` no passado) -> janela descartada, sem percentual velho;
  - `rate_limits` null/ausente em todos -> `NoData`;
  - escolhe o mais recente / ate `MaxFilesToScan`.
- **Disabled:** com `AgentUsage:Enabled=false`, `GET /api/agent-usage` retorna ambos `Status=Disabled` e nenhum acesso a arquivo/API ocorre.
- **Application** `GetAgentUsageHandlerTests` com `FakeAgentUsageReader` (padrao `CreatePromptHandlerTests.cs`).
- **Integration (Api)**: `GET /api/agent-usage` 200 + shape, com `AgentUsage:Codex:SessionsDir` em fixtures e Claude mockado/Disabled (padrao `PromptTasksApiFactory.cs`).

---

## Frontend (React 19 / TanStack Query / SignalR)

### 1. API + schema (atualizar contratos — exigido pelo CLAUDE.md)
- `frontend/src/api/schemas.ts`: `agentUsageStatusSchema` (enum), `agentUsageWindowSchema`, `agentUsageInfoSchema` (incl. `httpStatusCode`/`statusDetail` opcionais), `agentUsageSchema` + tipos `z.infer`.
- `frontend/src/api/agent-usage.ts` (novo): `getAgentUsage()` -> `api.get('agent-usage').json<unknown>()` -> `agentUsageSchema.parse(...)` (padrao `api/workflow.ts`).
- `frontend/src/api/query-keys.ts`: `agentUsage: { current: () => ['agent-usage','current'] as const }`.

### 2. Componentes UI (novos primitivos)
- `frontend/src/components/ui/progress.tsx`: barra com variantes CVA por status (`ok` <70, `warn` 70–90, `crit` >90), `cn()` + paleta existente.
- `frontend/src/components/ui/popover.tsx`: popover leve (`@floating-ui/dom`, ja nas deps) com click-outside + Escape.

### 3. Feature `frontend/src/features/agent-usage/`
- `usage-indicator.tsx`: header — duas mini-barras (Claude/Codex) com %. `useQuery({ queryKey: queryKeys.agentUsage.current(), queryFn: getAgentUsage, refetchInterval: 60_000 })` (fallback; tempo real via SignalR). Tratar `Status` != Ok com estados discretos ("sem token", "nao autorizado", "limite atingido", "sem dados", "desativado") sem quebrar o header.
- `usage-popover.tsx`: por agente, listar janelas (Claude `five_hour`/`seven_day`/`seven_day_opus`; Codex `primary`/`secondary`) com barra + % + reset ("reset em 2h13" / "reset sex 00:00"); marcar `Estimated`.
- `constants.ts`: rotulos pt-BR por chave de janela/status, thresholds e helpers de reset.
- `usage-indicator.test.tsx`: Vitest + Testing Library, mock `@/api/agent-usage`, render em `QueryClientProvider`, asserts de %, cor por threshold, abertura do popover e estados de `Status` (ex.: NoToken/Disabled).

### 4. Header + realtime
- `frontend/src/routes/__root.tsx`: substituir o pill placeholder "Codex e Claude Code" (div a direita, ~linha 34) por `<UsageIndicator />`.
- `frontend/src/realtime/prompt-hub.tsx`: `connection.on('AgentUsageUpdated', payload => queryClient.setQueryData(queryKeys.agentUsage.current(), agentUsageSchema.parse(payload)))` (broadcast global; sem entrar em grupo).

---

## Validacao

Backend:
```powershell
dotnet build backend/PromptTasks.sln
dotnet test backend/PromptTasks.sln
```
Frontend:
```powershell
cd frontend
npm run test
npm run lint
npm run build
```
Manual (end-to-end):
1. `docker compose up -d`; rodar API e `npm run dev`.
2. Header: duas barras; clicar -> popover. Claude com `five_hour`/`seven_day`/`seven_day_opus`; Codex com 5h/semanal.
3. Conferir Claude contra a API real (token valido) e Codex contra `~/.codex/.../rollout-*.jsonl`.
4. Realtime: alterar `.jsonl` em `~/.codex/sessions` -> update Codex; aguardar timer do Claude; ver countdown no reconcile.
5. Estados: renomear `~/.claude/.credentials.json` (NoToken), dir do Codex (NoData) e setar `Enabled=false` (Disabled) — header degradado sem quebrar.

---

## Riscos e mitigacoes

- **Endpoint OAuth da Anthropic e nao-documentado/beta** (`anthropic-beta: oauth-2025-04-20`). Mitigar: URL/headers/campos configuraveis, parsing defensivo, `Status`/`HttpStatusCode` sem quebrar.
- **Seguranca do token.** Ler `accessToken` so em memoria, usar no header, nunca logar/serializar/expor (incl. `StatusDetail`). Teste dedicado garante ausencia do token.
- **Excesso de chamadas a API (Claude).** Cache/min-interval 60s + timer; rajadas de arquivos nao disparam API.
- **Buckets expirados (Codex).** Filtro `resets_at > now` obrigatorio.
- **Performance** (Codex). `MaxFilesToScan=5` por mtime desc, leitura tail-first, cache 30s.
- **So faz sentido com API local.** Permitir desligar via `AgentUsage:Enabled=false` (retorna `Disabled`, sem watch/timer).
- **Regras do projeto:** contrato refletido em `schemas.ts`; `rootOnly` da listagem intacto; SignalR como canal de tempo real.

## Antes de implementar
- O repo tem alteracoes locais pendentes do ajuste anterior (workflow/409). Fechar/commitar antes, para nao misturar duas features.
