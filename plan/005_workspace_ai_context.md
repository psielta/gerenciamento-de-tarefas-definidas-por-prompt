# Contexto de Workspace para IA (README, CLAUDE, AGENT)

## Context

O assistente de IA (Gemini) hoje recebe **system instructions fixas e hardcoded**: `RefinePromptHandler.RefineSystemInstruction` (refino) e `SendChatMessageHandler.FallbackSystemInstruction` (chat). O modelo não conhece nada sobre o projeto que está sendo trabalhado, mesmo que cada workspace (`WorkingDirectory`) já tenha um `AbsolutePath` local validado e o backend rode localmente com acesso ao disco.

Objetivo: quando o usuário **ativar** a opção no workspace, o backend lê automaticamente `README.md`, `CLAUDE.md` e `AGENT.md` da raiz do diretório e injeta esse conteúdo na System Instruction do Gemini — tanto no **refino de prompts** quanto no **chat de suporte** — para respostas mais aderentes ao projeto.

**Decisões já tomadas (confirmadas com o usuário):**
- **Seleção de arquivos:** flag única `EnableAiContext` + nomes fixos (`README.md`, `CLAUDE.md`, `AGENT.md`). Sem caminhos customizados nesta versão → uma única coluna `bool`, sem novo padrão de persistência de listas.
- **Caching do chat:** injeta inline nos primeiros turnos e **embute o contexto no cache de sessão (30 min) já existente**. Custo mínimo; edições refletem em nova sessão / rebuild do cache. O refino é one-shot e sempre lê fresco.

**Nomenclatura importante:** o "Workspace" do enunciado é a entidade `WorkingDirectory` no código; o campo do diretório é `AbsolutePath` (não `DirectoryPath`). O provider de IA é o **Google Gemini** (apesar de o produto falar de Claude Code/Codex).

---

## Estratégia de caching com Gemini (requisito #4) — como fica

Mapeamento do que existe hoje em `GeminiApiClient.cs`:
- **Nível 1 (cache de sistema global):** `ResolveCachedContentNameAsync` usa **uma única chave estática** `"gemini:system-cache"` e cacheia **apenas `options.Value.SystemInstruction`** (config global, hoje vazia → o nível 1 está dormente). O `request.SystemInstruction` **nunca** passa por esse cache.
- **Nível 2 (cache de sessão):** criado por `EnsureSessionCacheAsync`, gravado em `AiChatSession` (`GeminiCacheName`, `CacheExpiresAt`, `CachedThroughSequence`), TTL 30 min, mínimo 4096 tokens.

Decisão de design:
- **NÃO** colocar o contexto do workspace no cache de Nível 1. Ele é por-workspace e mutável; a chave global única causaria (a) colisão entre workspaces/usuários e (b) staleness de até 1h. Mantemos `GeminiOptions.SystemInstruction` como está.
- **Refino:** injeta o contexto **inline** em `request.SystemInstruction` (já roda com `UseSystemCache: false`). Leitura sempre fresca, sem cache. Correto e simples.
- **Chat:** monta `systemInstruction = FallbackSystemInstruction + contexto`. Usa inline nos turnos iniciais **e** passa essa mesma instrução para `EnsureSessionCacheAsync` (hoje recebe `string.Empty`) para que persista no cache de sessão. Efeito colateral positivo: hoje, depois que o cache de sessão forma, a system instruction inline é descartada (`BuildGenerateBody` omite quando há `cachedContent`) — ou seja, o modelo perde as regras de formatação; ao semear o cache com a instrução, **corrigimos** isso de brinde.
- **Otimização opcional adiada:** um `cachedContent` dedicado por-workspace, com chave = hash do conteúdo (`SHA-256`) + modelo, reusaria o cache enquanto os arquivos não mudam e criaria um novo quando mudam. Não vale a pena agora (arquivos mudam com frequência; `MinCacheTokens` ≥ 1024 inviabiliza arquivos pequenos). Documentar e não implementar.

---

## Ajustes incorporados após revisão

Cinco correções pedidas na revisão, todas verificadas contra o checkout e refletidas nas fases abaixo:
1. **Fakes de teste** que implementam `IWorkspaceFileService` deixam de compilar ao adicionar `ReadWorkspaceContextAsync` — ex.: `backend/tests/PromptTasks.Application.UnitTests/CreatePromptHandlerTests.cs:290` e `UpdatePromptArchivalTests.cs:307`. Adicionar stub em todas as implementações (ver Fase 4).
2. **Estimativa do cache de sessão** ignora a system instruction (`GeminiApiClient.cs:129` soma só o histórico). Se o contexto do workspace for grande e o histórico pequeno, a criação do cache é pulada e a promessa "cache per session" quebra. Incluir `systemInstruction.Length / 4` no estimate (Fase 2.3).
3. **Query key das sessões de IA** só usa `promptId` (`query-keys.ts:62`), mas o filtro usa `promptId` **e** `workingDirectoryId`; com `promptId` undefined, históricos de workspaces diferentes se misturam. Incluir `workingDirectoryId` na key (Fase 3.5).
4. **Teste do chat** (além do refino): garantir que `SendChatMessageHandler` passe `systemInstruction` **não vazia** para `EnsureSessionCacheAsync` (hoje `string.Empty`, `SendChatMessageHandler.cs:152`). Exige um fake `IGeminiClient` que capture o argumento (Fase 4).
5. **Hardening:** incluir `NotSupportedException` no catch inicial de `ReadWorkspaceContextAsync`, seguindo `ValidatePathAsync` (Fase 2.1).

---

## Fase 1 — Domínio e Persistência (EF Core 10 / PostgreSQL)

**Entidade** — `backend/src/PromptTasks.Domain/WorkingDirectories/WorkingDirectory.cs`
Adicionar uma propriedade (mantendo o estilo flat-scalar atual):
```csharp
public bool EnableAiContext { get; set; }
```

**EF Config** — `backend/src/PromptTasks.Infrastructure/Persistence/Configurations/WorkingDirectoryConfiguration.cs`
```csharp
builder.Property(directory => directory.EnableAiContext)
    .HasDefaultValue(false)
    .IsRequired();
```

**Migration (comandos exatos, PowerShell na raiz):**
```powershell
dotnet ef migrations add AddWorkingDirectoryEnableAiContext `
  --project backend/src/PromptTasks.Infrastructure/PromptTasks.Infrastructure.csproj `
  --startup-project backend/src/PromptTasks.Api/PromptTasks.Api.csproj `
  --output-dir Persistence/Migrations

dotnet ef database update `
  --project backend/src/PromptTasks.Infrastructure/PromptTasks.Infrastructure.csproj `
  --startup-project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

**FluentValidation:** nenhuma regra nova. `EnableAiContext` é `bool` (sempre válido) e não há caminhos fornecidos pelo usuário para validar. `CreateWorkingDirectoryValidator`/`UpdateWorkingDirectoryValidator` ficam inalterados.

---

## Fase 2 — Aplicação e Infraestrutura (MediatR & File System)

### 2.1 Serviço de leitura seguro — estende `IWorkspaceFileService`
Reaproveitamos os helpers de path-safety **já existentes** em `WorkspaceFileService` (`CanonicalizeExistingPath`, `EnsureContained`) em vez de criar novos — uma única fonte de verdade para segurança de caminho.

`backend/src/PromptTasks.Application/Common/Interfaces/IWorkspaceFileService.cs` — adicionar:
```csharp
Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken);
```

`backend/src/PromptTasks.Infrastructure/FileSystem/WorkspaceFileService.cs` — adicionar (precisa de `using System.Text;`):
```csharp
private static readonly string[] ContextFileNames = ["README.md", "CLAUDE.md", "AGENT.md"];
private const long MaxContextFileBytes = 64 * 1024;   // teto por arquivo
private const int MaxTotalContextChars = 48_000;      // ~12k tokens, guarda de custo

public async Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken)
{
    string rootCanonical;
    try
    {
        rootCanonical = CanonicalizeExistingPath(rootAbsolutePath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                 or ArgumentException or NotSupportedException or PathTraversalException)
    {
        return null; // resiliência: workspace inacessível não derruba a requisição de IA
    }

    var sections = new List<string>();
    var totalChars = 0;

    foreach (var fileName in ContextFileNames)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string content;
        try
        {
            var candidateLogical = Path.GetFullPath(Path.Combine(rootCanonical, fileName));
            if (!File.Exists(candidateLogical))
                continue; // arquivo ausente → ignora silenciosamente

            var candidateCanonical = CanonicalizeExistingPath(candidateLogical);
            EnsureContained(rootCanonical, candidateCanonical); // rejeita escape via symlink/junction

            var info = new FileInfo(candidateCanonical);
            if (info.Length == 0 || info.Length > MaxContextFileBytes)
                continue;

            await using var stream = new FileStream(
                candidateCanonical, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            content = (await reader.ReadToEndAsync(cancellationToken)).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                     or ArgumentException or NotSupportedException or PathTraversalException)
        {
            continue; // qualquer falha de leitura é tolerada
        }

        if (content.Length == 0 || totalChars + content.Length > MaxTotalContextChars)
            continue;

        sections.Add($"### {fileName}\n\n{content}");
        totalChars += content.Length;
    }

    if (sections.Count == 0)
        return null;

    return "## Contexto do workspace\n\n"
         + "Os arquivos abaixo descrevem o projeto e suas convenções; use-os como contexto.\n\n"
         + string.Join("\n\n", sections);
}
```
Segurança contra path traversal: `Path.GetFullPath` normaliza, `CanonicalizeExistingPath` resolve symlinks para o alvo real, e `EnsureContained` garante que o caminho final permanece dentro de `rootCanonical` (lança `PathTraversalException` caso escape — capturada e tratada como skip).

### 2.2 Refino de prompts (one-shot, contexto fresco)
- `RefinePromptCommand` (`.../Commands/RefinePrompt/RefinePromptCommand.cs`): adicionar `Guid? WorkingDirectoryId`.
- Controller `AiController.Refine` + record `RefineRequest`: adicionar `Guid? WorkingDirectoryId` e repassar ao command.
- `RefinePromptHandler`: injetar `IApplicationDbContext context`, `IWorkspaceFileService workspaceFiles`, `ICurrentUser currentUser`.
```csharp
var systemInstruction = RefineSystemInstruction;

if (request.WorkingDirectoryId is { } workspaceId)
{
    var workspace = context.WorkingDirectories
        .FirstOrDefault(w => w.Id == workspaceId && w.OwnerId == currentUser.UserId); // checa posse

    if (workspace is { EnableAiContext: true })
    {
        var workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(workspace.AbsolutePath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(workspaceContext))
            systemInstruction = $"{RefineSystemInstruction}\n\n{workspaceContext}";
    }
}
// ...usar `systemInstruction` no GeminiGenerationRequest (UseSystemCache permanece false)
```

### 2.3 Chat de suporte (contexto no cache de sessão)
`SendChatMessageHandler` já tem `IApplicationDbContext` e a `session.WorkingDirectoryId`. Injetar `IWorkspaceFileService workspaceFiles` e `ICurrentUser` (já presente).
```csharp
// resolve uma vez, antes de montar o geminiRequest
string? workspaceContext = null;
if (session.WorkingDirectoryId is { } wdId)
{
    var workspace = context.WorkingDirectories
        .FirstOrDefault(w => w.Id == wdId && w.OwnerId == currentUser.UserId);
    if (workspace is { EnableAiContext: true })
        workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(workspace.AbsolutePath, cancellationToken);
}

var systemInstruction = string.IsNullOrWhiteSpace(workspaceContext)
    ? FallbackSystemInstruction
    : $"{FallbackSystemInstruction}\n\n{workspaceContext}";
```
- No `GeminiGenerationRequest`: trocar `SystemInstruction: FallbackSystemInstruction` por `SystemInstruction: systemInstruction`.
- Na (re)criação do cache de sessão: trocar `EnsureSessionCacheAsync(session.Model, string.Empty, history, ...)` por `EnsureSessionCacheAsync(session.Model, systemInstruction, history, ...)` para o contexto persistir nos turnos cacheados.
- **Estimativa de tokens do cache (`GeminiApiClient.cs:129`):** hoje `var estimatedTokens = history.Sum(t => t.Text.Length / 4);` ignora a system instruction. Com contexto de workspace grande + histórico curto, o cache seria pulado (abaixo de `SessionCacheMinTokens`), furando a estratégia "cache per session". Corrigir para somar a instrução:
```csharp
var estimatedTokens = history.Sum(t => t.Text.Length / 4) + systemInstruction.Length / 4;
```

### 2.4 Contrato de saída do Workspace
- `WorkingDirectoryDto` (`Common/Models/WorkingDirectoryDto.cs`): adicionar `bool EnableAiContext`.
- `DtoMapper.ToDto` (`Common/Mappings/DtoMapper.cs`): mapear `workingDirectory.EnableAiContext`.
- `CreateWorkingDirectoryCommand` / `UpdateWorkingDirectoryCommand` (+ handlers): adicionar `bool EnableAiContext` e persistir no entity (`Create`: setar na criação; `Update`: `directory.EnableAiContext = request.EnableAiContext`).
- Controller `WorkingDirectoriesController`: records `CreateWorkingDirectoryRequest` / `UpdateWorkingDirectoryRequest` ganham `bool EnableAiContext` (default `false` no create) e repassam.

**SignalR:** não é necessário. A flag é configuração do usuário, sem broadcast em tempo real.

### 2.5 Invalidar cache de sessão ao mudar a configuração
O contexto fica embutido no cache de sessão (30 min) e `BuildGenerateBody` **omite** a `SystemInstruction` inline quando há `cachedContent` (`GeminiApiClient.cs:285`). Logo, mudar a configuração não teria efeito imediato em sessões já cacheadas — nas **duas** direções:
- **flag false→true:** a sessão cacheada continuaria **sem** o contexto até expirar/rebuildar;
- **flag true→false:** o contexto antigo continuaria sendo enviado;
- **`AbsolutePath` alterado (com contexto ligado):** o cache manteria o conteúdo do diretório antigo.

No `UpdateWorkingDirectoryHandler`, detectar essas transições e resetar o cache das sessões do workspace — reutilizando o padrão de `ReleasePromptAiSessionsHandler` (best-effort `IGeminiClient.DeleteCacheAsync` em try/catch + limpar `GeminiCacheName`/`CacheExpiresAt`/`CachedThroughSequence`). Injetar `IGeminiClient gemini` no handler. As comparações são feitas **antes** de mutar o entity:
```csharp
// antes de aplicar request no directory:
var contextFlagChanged = directory.EnableAiContext != request.EnableAiContext;       // qualquer direção
var pathChanged = !directory.AbsolutePath.Equals(path.CanonicalPath, StringComparison.OrdinalIgnoreCase);

// ... aplica Name/AbsolutePath/RespectGitignore/EnableAiContext ...

// reset se a flag mudou (on↔off) ou se o caminho mudou com o contexto ligado:
if (contextFlagChanged || (request.EnableAiContext && pathChanged))
{
    var sessions = context.AiChatSessions
        .Where(s => s.WorkingDirectoryId == directory.Id
                 && s.OwnerId == currentUser.UserId
                 && s.GeminiCacheName != null)
        .ToList();

    foreach (var s in sessions)
    {
        try { await gemini.DeleteCacheAsync(s.GeminiCacheName!, cancellationToken); } // best-effort
        catch { /* ignora falha de exclusão, igual ReleasePromptAiSessionsHandler */ }
        s.GeminiCacheName = null;
        s.CacheExpiresAt = null;
        s.CachedThroughSequence = 0;
    }
}
```
O próximo turno reconstrói o cache já com a configuração nova (injeta quando ligado, remove quando desligado, relê o novo caminho). A staleness de 30 min passa a valer **apenas** para edição de *conteúdo* dos arquivos com a flag ligada (tradeoff aceito na decisão de caching).

---

## Fase 3 — Frontend (React 19, TanStack, Zod)

### 3.1 Schemas e API client
- `frontend/src/api/schemas.ts`: em `workingDirectorySchema`, adicionar `enableAiContext: z.boolean()`.
- `frontend/src/api/working-directories.ts`: em `WorkingDirectoryPayload`, adicionar `enableAiContext: boolean`.
- `frontend/src/api/ai.ts`: em `refinePrompt`, adicionar `workingDirectoryId?: string` aos params e ao `json` enviado.

### 3.2 Threading do workspace no refino
- `frontend/src/features/prompts/ai/refine-dialog.tsx`: aceitar prop `workingDirectoryId?: string`; passar em `refinePrompt({ ..., workingDirectoryId })`.
- `frontend/src/features/prompts/prompt-form.tsx`: passar `workingDirectoryId={workingDirectoryId}` ao `<RefineDialog />` (a prop já está no escopo, linha 32 / uso na ~170). O chat (`AiAssistantPanel`/`AiChatPanel`) já recebe `workingDirectoryId` e persiste na sessão — sem mudança.

### 3.3 Formulário de criação — `workspace-form.tsx`
Exemplo (react-hook-form + zodResolver, mantendo o padrão de checkbox já usado para `respectGitignore`):
```tsx
const workspaceFormSchema = z.object({
  name: z.string().trim().min(2, 'Informe um nome com pelo menos 2 caracteres.'),
  absolutePath: z.string().trim().min(3, 'Informe o caminho absoluto do diretorio.'),
  respectGitignore: z.boolean(),
  enableAiContext: z.boolean(),
})

const form = useForm<WorkspaceFormValues>({
  resolver: zodResolver(workspaceFormSchema),
  defaultValues: { name: '', absolutePath: '', respectGitignore: true, enableAiContext: false },
})

// no JSX, abaixo do checkbox de .gitignore:
<label className="flex items-center gap-2 text-sm text-[#425048]">
  <input type="checkbox" className="h-4 w-4" {...form.register('enableAiContext')} />
  Injetar README.md, CLAUDE.md e AGENT.md no contexto da IA
</label>
```
Lembrar de incluir `enableAiContext` no `form.reset({ ... })` do `onSuccess`.

### 3.4 Edição em workspace existente (a flag precisa ser editável)
Não existe form de edição hoje. Adicionar um card "Contexto de IA" no layout de detalhe `frontend/src/routes/workspaces/$workspaceId.tsx` (já tem `workspaceQuery.data`), com um toggle que chama `updateWorkingDirectory(id, payload)` (função já existe) e invalida o cache:
```tsx
const mutation = useMutation({
  mutationFn: (enableAiContext: boolean) =>
    updateWorkingDirectory(workspaceId, {
      name: workspaceQuery.data!.name,
      absolutePath: workspaceQuery.data!.absolutePath,
      respectGitignore: workspaceQuery.data!.respectGitignore,
      enableAiContext,
    }),
  onSuccess: async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.detail(workspaceId) })
    await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.all })
    toast.success('Configuração de contexto de IA atualizada.')
  },
})
```
Pode-se usar o componente `Switch` (`@/components/ui/switch`) aqui para um visual mais limpo de "configuração". `query-keys.ts` já tem `workingDirectories.all` e `.detail(id)` — sem novas chaves para o workspace.

### 3.5 Corrigir a query key das sessões de IA (escopo por workspace)
`queryKeys.ai.sessions` (`query-keys.ts:62`) hoje é `(promptId?) => ['ai','sessions',{ promptId }]`, mas `listChatSessions` filtra por `promptId` **e** `workingDirectoryId`. Com `promptId` undefined (chat aberto a partir do workspace, sem prompt), dois workspaces compartilham a mesma key e o histórico se mistura — problema que esta feature torna visível. Corrigir a key e os call sites:
```ts
sessions: (promptId?: string, workingDirectoryId?: string) =>
  ['ai', 'sessions', { promptId, workingDirectoryId }] as const,
```
Atualizar **todos os 6 call sites** para passar ambos (`workingDirectoryId` já está no escopo dos três componentes — `AiChatPanel` recebe a prop em `ai-assistant-panel.tsx:151`): `ai-session-list.tsx` (linhas 27, 42), `ai-assistant-panel.tsx` (linhas 83, 157) e `ai-chat-panel.tsx` (linhas 88, 97) — `queryKeys.ai.sessions(promptId, workingDirectoryId)`. A invalidação após criar/deletar sessão precisa bater na key nova, senão a lista de histórico não atualiza.

---

## Arquivos a alterar (resumo)

**Backend**
- `Domain/WorkingDirectories/WorkingDirectory.cs` — campo `EnableAiContext`
- `Infrastructure/Persistence/Configurations/WorkingDirectoryConfiguration.cs` — mapeamento + default
- `Infrastructure/Persistence/Migrations/*` — nova migration
- `Application/Common/Interfaces/IWorkspaceFileService.cs` + `Infrastructure/FileSystem/WorkspaceFileService.cs` — `ReadWorkspaceContextAsync`
- `Application/Common/Models/WorkingDirectoryDto.cs` + `Common/Mappings/DtoMapper.cs`
- `Application/Features/WorkingDirectories/Commands/{Create,Update}WorkingDirectory/*` — command + handler
- `Application/Features/Ai/Commands/RefinePrompt/{RefinePromptCommand,RefinePromptHandler}.cs`
- `Application/Features/Ai/Commands/SendChatMessage/SendChatMessageHandler.cs`
- `Infrastructure/Ai/GeminiApiClient.cs` — somar `systemInstruction` na estimativa de tokens do cache de sessão
- `Api/Controllers/WorkingDirectoriesController.cs` e `Api/Controllers/AiController.cs` (records de request)
- `tests/PromptTasks.Application.UnitTests/*` — stub nos fakes de `IWorkspaceFileService` + novo teste de chat (fake `IGeminiClient` que captura args)

**Frontend**
- `api/schemas.ts`, `api/working-directories.ts`, `api/ai.ts`
- `api/query-keys.ts` (`ai.sessions` por workspace) + callers `features/prompts/ai/{ai-session-list,ai-assistant-panel,ai-chat-panel}.tsx` (6 call sites)
- `features/prompts/ai/refine-dialog.tsx`, `features/prompts/prompt-form.tsx`
- `features/workspaces/workspace-form.tsx`
- `routes/workspaces/$workspaceId.tsx` (card de edição)

---

## Fase 4 — Testes (xUnit / FluentAssertions)

**Correção de compilação (obrigatória):** ao adicionar `ReadWorkspaceContextAsync` à interface, todas as implementações de `IWorkspaceFileService` nos testes param de compilar. Localizar com grep `: IWorkspaceFileService` e adicionar o stub — confirmados em `CreatePromptHandlerTests.cs:290` (`FakeWorkspaceFileService`) e `UpdatePromptArchivalTests.cs:307`:
```csharp
public Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken) =>
    Task.FromResult<string?>(null);
```

**Testes a adicionar** (seguir o harness de contexto in-memory já usado em `CreatePromptHandlerTests`):
- **`ReadWorkspaceContextAsync`** (unit, FS real em diretório temporário): lê os 3 arquivos presentes; **ignora ausentes** silenciosamente; respeita o teto por arquivo; **rejeita symlink** que aponta para fora da raiz (sem aquela seção, sem exceção); retorna `null` quando nenhum arquivo existe.
- **Refino:** o handler injeta o contexto **apenas** quando `EnableAiContext` e o workspace é do usuário; **não** injeta quando a flag está off, o `WorkingDirectoryId` é nulo, ou o workspace é de outro `OwnerId`.
- **Chat (crítico — ponto 4 da revisão):** criar um fake `IGeminiClient` que **captura** o `systemInstruction` recebido por `EnsureSessionCacheAsync` (e emite um chunk em `StreamAsync`). Primeira mensagem numa sessão com `WorkingDirectoryId` de um workspace `EnableAiContext=true` + fake `IWorkspaceFileService` retornando contexto não-nulo ⇒ asserir que o `systemInstruction` capturado **não é vazio** e contém o contexto do workspace (regressão direta do `string.Empty` atual em `SendChatMessageHandler.cs:152`).
- **Update de workspace (Fase 2.5):** com uma sessão do workspace tendo `GeminiCacheName` preenchido, alternar `EnableAiContext` (false→true **e** true→false) e trocar o `AbsolutePath` com a flag ligada ⇒ asserir que `GeminiCacheName`/`CacheExpiresAt`/`CachedThroughSequence` são limpos e `DeleteCacheAsync` é chamado; uma mudança irrelevante (ex.: só `Name`, ou path mudando com a flag desligada) **não** mexe no cache.

## Verificação

**Backend**
```powershell
docker compose up -d
dotnet build backend/PromptTasks.sln
dotnet test backend/PromptTasks.sln
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

**Frontend**
```powershell
cd frontend
npm run test
npm run lint
npm run build
```

**E2E manual**
1. Criar/abrir um workspace cujo `AbsolutePath` contenha `README.md`, `CLAUDE.md`, `AGENT.md`; ativar a flag.
2. **Refino:** abrir um prompt do workspace, refinar — o resultado deve refletir convenções dos arquivos. Conferir no payload do Gemini (log/DevTools) que `systemInstruction` contém "## Contexto do workspace".
3. **Chat:** abrir o Assistente IA no prompt, perguntar algo específico do projeto; validar aderência. Estender a conversa além de ~4096 tokens para formar o cache de sessão e confirmar que o contexto **persiste** nos turnos seguintes.
4. **Resiliência:** apagar/renomear um dos arquivos e repetir — sem erro, apenas sem aquela seção.
5. **Toggle de estado:** com uma sessão de chat já cacheada, alternar a flag (on↔off) ou trocar o `AbsolutePath` com a flag ligada — o cache das sessões do workspace é resetado (Fase 2.5); confirmar que o próximo turno reflete a nova configuração **imediatamente** (passa a injetar ao ligar; deixa de injetar ao desligar), sem esperar 30 min.

---

## Riscos e mitigações
- **Custo de tokens:** arquivos grandes inflam a entrada. Mitigado por tetos (`MaxContextFileBytes` 64KB/arquivo, `MaxTotalContextChars` ~48k) e por ser opt-in.
- **Staleness no chat:** vale só para *edição de conteúdo* dos arquivos com a flag ligada (cache 30 min) — aceito na decisão de caching. Alternar a flag (on↔off) ou trocar o `AbsolutePath` com contexto ligado reseta o cache das sessões na hora (Fase 2.5), sem espera de 30 min.
- **Segurança:** leitura restrita à raiz canônica via `EnsureContained`; checagem de posse (`OwnerId == currentUser.UserId`) antes de ler qualquer arquivo.
- **Mudança de comportamento do cache de sessão:** semear `EnsureSessionCacheAsync` com a system instruction passa a embutir também as regras de formatação no cache (hoje perdidas após o cache formar) — efeito desejável; cobrir com teste de chat.
- **Migration:** coluna `bool NOT NULL DEFAULT false` é aditiva e segura para linhas existentes.
