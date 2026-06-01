# Plano: Comparação de Versões Lado a Lado (Diff View)

## Contexto

Hoje é possível **visualizar** uma versão por vez — tanto no histórico de prompts (`PromptVersions` → modal `PromptVersionPreview`) quanto no histórico de planos vinculados (`LinkedDocumentHistory` dentro de `LinkedDocumentViewer`). Não há forma de **comparar** duas versões. O objetivo é permitir selecionar exatamente duas versões e abrir um diff git-style (código-fonte Markdown bruto), com modos **lado a lado (split)** e **unificado**, reaproveitando o padrão visual e de dados já existente.

Resultado esperado: nas rotas `/workspaces/:workspaceId/prompts/:promptId` (aba Prompt) e `…?tab=linked-plan`, o usuário marca 2 versões, clica em **Comparar versões** e vê o diff em um modal.

## Decisões confirmadas com o usuário

- **Representação:** diff de **código-fonte Markdown bruto** (git-style), não Markdown renderizado.
- **Biblioteca:** **`diff` (jsdiff)** — leve, sem peer deps conflitantes com React 19.
- **Granularidade:** **linha + palavra** (`diffLines` + `diffWordsWithSpace`).

## Descobertas-chave (o que já existe — não recriar)

- **Backend: nenhuma mudança necessária.**
  - `GET /api/prompts/{id}/versions` já retorna `PromptVersionDto` **com `Content` completo** por versão.
  - `GET /api/linked-documents/{id}/content?version={n}` retorna o conteúdo completo de uma versão específica do plano (a lista `…/versions` traz só metadados).
- **Camada de API do frontend pronta** (`frontend/src/api/`):
  - `listPromptVersions(id)` → `PromptVersion[]` (conteúdo incluso) — `prompts.ts`.
  - `listLinkedDocumentVersions(id)` e `getLinkedDocumentContent(id, version?)` — `linked-documents.ts`.
  - `queryKeys.prompts.versions(id)`, `queryKeys.linkedDocuments.versions(id)`, `queryKeys.linkedDocuments.content(id, version?)` — `query-keys.ts`.
  - Cliente HTTP `ky`; TanStack Query v5 (`useQueries` ainda não usado no projeto); erros via `getErrorMessage` em `api/client.ts`.
- **UI/convenções:**
  - Modais são **overlays construídos à mão** (`fixed inset-0 z-50 bg-[#172126]/35 backdrop-blur-sm`, `role="dialog" aria-modal="true"`), ex.: `PromptVersionPreview` (`prompt-versions.tsx`) e `ChildPromptDrawer` (`prompt-children-panel.tsx`). **Não** existem `dialog.tsx`, `drawer.tsx`, `checkbox.tsx`, `toggle-group.tsx`, `skeleton.tsx` em `components/ui/`.
  - Componentes disponíveis: `Button` (variants `default/secondary/ghost/destructive`, sizes `default/sm/icon`), `Badge`, `Tabs`, `Switch`.
  - Paleta: `#254632` (primária), `#d9dfd5` (borda), `#eef2eb` (destaque), `#172126` (texto), `#66746b` (texto suave), `#f7f8f6` (hover).
  - Responsividade via classes Tailwind; **não** há `useMediaQuery`/`useIsMobile`.
  - `cn()` em `frontend/src/lib/utils.ts`. Testes: Vitest + jsdom, setup em `frontend/src/test/setup.ts`, padrão `describe/it` + Testing Library + `QueryClientProvider`.

## Arquitetura proposta

Nova feature isolada e reutilizável em `frontend/src/features/diff/`:

| Arquivo | Responsabilidade |
|---|---|
| `diff-engine.ts` | **Motor puro** (sem React). `computeLineDiff(old, new): DiffModel`. Testável. |
| `diff-viewer.tsx` | Componente **apresentacional**: recebe 2 strings + modo, renderiza split/unified com Tailwind. |
| `diff-viewer-modal.tsx` | **Modal** (overlay padrão do projeto): cabeçalho, toggle split/unified, responsividade, loading/erro/idêntico. |
| `use-linked-plan-compare.ts` | Hook com **`useQueries`** para buscar em paralelo o conteúdo das 2 versões de plano. |
| `diff-engine.test.ts` | Testes unitários do motor (Vitest). |

Apoio: `frontend/src/hooks/use-media-query.ts` (novo, reutilizável) para forçar modo unificado em `< md`.

Modelo de dados do motor (ilustrativo):

```ts
type DiffSegment = { value: string; emphasis: boolean }      // emphasis = palavra alterada
type DiffRowType = 'unchanged' | 'added' | 'removed'
interface UnifiedRow { type: DiffRowType; oldLine: number | null; newLine: number | null; segments: DiffSegment[] }
interface SplitCell { type: DiffRowType; line: number; segments: DiffSegment[] }
interface SplitRow  { left: SplitCell | null; right: SplitCell | null }
interface DiffModel { unified: UnifiedRow[]; split: SplitRow[]; hasChanges: boolean; stats: { added: number; removed: number } }
```

Algoritmo: `diffLines(old, new)` → percorrer partes; uma parte `removed` imediatamente seguida de `added` é tratada como **bloco modificado** — alinhar linha a linha e, em cada par, rodar `diffWordsWithSpace` marcando `emphasis` nas palavras que mudaram (lado esquerdo = removidas, direito = adicionadas). Linhas sobrando viram add/remove puros; partes iguais viram `unchanged`. Contadores de número de linha old/new mantidos durante a varredura. `hasChanges` deriva de `stats`.

## Passo 1 — API e Contratos

- **Confirmação (sem código):** os endpoints e os schemas Zod **já cobrem** o caso de uso. **Nenhuma alteração** em `schemas.ts` nem em `query-keys.ts`.
- **Única mudança de "contrato":** declarar `diff` (jsdiff) como **dependência direta** do frontend:
  - `cd frontend; npm install diff`
  - O pacote **já está presente transitivamente** (`diff@8.0.4`, via TanStack Router) e a v8 **expõe os próprios tipos** no `package.json` — portanto **não** instalar `@types/diff` por padrão. Adicionar `@types/diff` apenas se o `npm run build` acusar falta de tipos.
- Reaproveitar `queryKeys.linkedDocuments.content(id, version)` para o fetch de planos — assim o cache é compartilhado com `LinkedDocumentViewer` (versão já vista não refaz request).

## Passo 2 — Componente de Seleção (UI)

Adicionar seleção de até **2** versões nos dois históricos, usando `<input type="checkbox">` nativo estilizado (`accent-[#254632]`) — evita nova dependência e segue o estilo leve do projeto.

**Prompts — `frontend/src/features/prompts/prompt-versions.tsx`:**
- Novo estado `compareIds: string[]` (máx. 2). Toggle: ao marcar a 3ª, desabilitar checkboxes não marcados (ou substituir a mais antiga — adotar **desabilitar** por clareza) + botão "Limpar".
- Reestruturar cada item: hoje a linha é um `<button>` (preview). Passar para `div` contendo o `checkbox` + o `<button>` de preview existente (não aninhar input dentro de button).
- Botão **"Comparar versões"** no topo da `aside`, habilitado só com 2 marcadas. Ao clicar, abre `DiffViewerModal` passando o conteúdo das duas (já disponível em `versionsQuery.data`).
- Ordem old→new pelo `versionNumber` (menor = esquerda/antiga), independente da ordem de marcação.

**Planos — `frontend/src/features/linked-documents/linked-document-history.tsx` + `linked-document-viewer.tsx`:**
- Estado de seleção mora no **pai** (`LinkedDocumentViewer`, que já controla versão); `LinkedDocumentHistory` recebe props novas (`compareSelection`, `onToggleCompare`) e renderiza checkbox + botão "Comparar versões".
- O modal é aberto pelo `LinkedDocumentViewer`, que aciona o hook `useLinkedPlanCompare`.

**Decisão de UX — botão "Atual vN":** a comparação opera **sempre por `versionNumber` concreto** vindo da lista `versions`. O botão "Atual v{n}" mantém seu papel atual (saltar para a versão corrente) e **não** ganha checkbox de comparação, para não duplicar a UX. Se quiser comparar a versão atual, ela também aparece na lista `versions`.

**Reset de estado da seleção** (vale para os dois históricos):
- Limpar `compareIds`/`compareSelection` e fechar o modal ao mudar `promptId` (prompts) ou `documentId` (planos) — via `useEffect` na dependência do id.
- Ao recarregar as versões, descartar ids selecionados que não existam mais na nova lista (evita comparar versão removida).

## Passo 3 — Motor de Diff e `DiffViewer`

- **`diff-engine.ts`**: implementar `computeLineDiff` conforme o algoritmo acima. **Normalizar fins de linha (`\r\n`/`\r` → `\n`) nas duas entradas antes de `diffLines`** — ambiente Windows; evita diff ruidoso por CRLF vs LF. Tratar bordas: string vazia, ausência de `\n` final, conteúdo idêntico (`hasChanges=false`).
- **`diff-viewer.tsx`**: `props { oldContent, newContent, oldLabel, newLabel, viewMode }`. `const model = useMemo(() => computeLineDiff(oldContent, newContent), [oldContent, newContent])`.
  - Render em `font-mono text-xs leading-relaxed`, `whitespace-pre-wrap break-words`, container com `overflow-auto max-h-[…]`.
  - **Unificado:** grid `grid-cols-[3rem_3rem_1fr]` (nº old, nº new + sinal, conteúdo).
  - **Split:** `grid-cols-2`; esquerda = removidas/iguais, direita = adicionadas/iguais (alinhadas por `SplitRow`).
  - Cores (conforme enunciado): removida `bg-red-500/20 text-red-700` (sinal `−`); adicionada `bg-green-500/20 text-green-700` (sinal `+`); palavra com `emphasis` recebe realce mais forte (`bg-red-500/40` / `bg-green-500/40 rounded-sm`).
  - **Acessibilidade:** sinal `+`/`−` textual + ícone `Plus`/`Minus` (lucide, `aria-hidden`) + `sr-only` "linha adicionada/removida" — não depender só de cor.

## Passo 4 — Integração nos Fluxos

- **`diff-viewer-modal.tsx`** (overlay padrão, fechar por Esc e clique no backdrop, como `ChildPromptDrawer`):
  - Cabeçalho: "Comparar versões" + labels `v{a} → v{b}` + botão fechar (`X`).
  - Toolbar: toggle **Lado a lado / Unificado** com dois `Button` (`variant` default/ghost), mesmo padrão dos botões de aba da rota.
  - Props: `oldContent, newContent, oldLabel, newLabel, isLoading, error, onClose` (apresentacional quanto a dados).
  - **Responsivo:** `const isDesktop = useMediaQuery('(min-width: 768px)')`; `effectiveMode = isDesktop ? mode : 'unified'`; ocultar o botão "Lado a lado" em `< md` (`hidden md:inline-flex`).
- **Prompts:** o conteúdo já está em memória → passar as 2 strings direto, **sem refetch** (alinhado a "evite estado duplicado" do CLAUDE.md). `isLoading=false`.
- **Planos:** usar o hook com **`useQueries`**, com fetch **disparado só quando o modal abre** (`isOpen`) — assim marcar 2 versões não busca conteúdo antes do clique em "Comparar versões":

```ts
// use-linked-plan-compare.ts
export function useLinkedPlanCompare(documentId: string, a?: number, b?: number, isOpen = false) {
  const results = useQueries({
    queries: [a, b].map((v) => ({
      queryKey: queryKeys.linkedDocuments.content(documentId, v),
      queryFn: () => getLinkedDocumentContent(documentId, v!),
      enabled: isOpen && v != null,   // só busca após abrir o modal de comparação
    })),
  })
  return {
    contents: results.map((r) => r.data?.content ?? ''),
    isLoading: results.some((r) => r.isLoading),
    error: results.find((r) => r.error)?.error ?? null,
  }
}
```

> Nota de engenharia: `useQueries` é aplicado **onde há fetch real em paralelo** (planos). Para prompts o conteúdo já vem em `listPromptVersions`, então passamos as strings diretamente para não duplicar requisições.

## Passo 5 — Casos de Borda e Resiliência

- **Loading (planos):** enquanto `isLoading`, exibir skeleton (linhas cinza animadas `animate-pulse bg-[#eef2eb]`) no corpo do modal.
- **Erro de API:** caixa de erro reutilizando o padrão de `LinkedDocumentViewer` (`border-[#f8b4aa] bg-[#fff3f0] text-[#8a241b]`) com `getErrorMessage(error)`.
- **Versões idênticas:** `model.hasChanges === false` → estado vazio amigável ("As versões selecionadas são idênticas.") em vez de diff vazio.
- **Seleção inválida:** impedir comparar versão consigo mesma; botão só habilita com 2 ids distintos.
- **Conteúdo grande:** diff de palavra só nos pares modificados (limita custo); corpo com scroll próprio.
- **Seleção obsoleta:** ao trocar de prompt/plano ou recarregar versões, resetar seleção e fechar modal (ver Passo 2) para não comparar versão inexistente.

## Passo 6 — Validação e Testes

**Testes (Vitest) — foco no motor puro** `frontend/src/features/diff/diff-engine.test.ts` (padrão `describe/it`, import explícito de `vitest`):
- idênticos → `hasChanges=false`, todas `unchanged`;
- adição pura, remoção pura, linha modificada (verifica `segments` com `emphasis` só nas palavras alteradas);
- old vazio / new vazio;
- numeração de linhas old/new correta;
- `\n` final e múltiplas linhas;
- **normalização CRLF/LF:** mesmo conteúdo com `\r\n` vs `\n` → `hasChanges=false` (sem diff ruidoso no Windows).
- (Opcional, secundário) `diff-viewer.test.tsx` para presença de linhas add/removed e labels acessíveis; se testar o modal, mockar `window.matchMedia` no `test/setup.ts`.

**Comandos:**

```powershell
# Frontend
cd frontend
npm run test
npm run lint
npm run build

# Backend (sanity check — sem alterações de código nesta feature)
dotnet build backend/PromptTasks.sln
dotnet test backend/PromptTasks.sln
```

## Verificação end-to-end (manual)

```powershell
docker compose up -d
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
cd frontend; npm run dev
```

1. Prompt com ≥2 versões → aba **Prompt** → marcar 2 versões → **Comparar versões** → conferir split, alternar para unificado, redimensionar para `< 768px` e confirmar que força unificado.
2. **Plano vinculado** (`?tab=linked-plan`) → marcar 2 versões → comparar → no DevTools/Network confirmar **duas** chamadas `…/content?version=` em paralelo; ver skeleton → diff.
3. Selecionar duas versões idênticas → confirmar mensagem "idênticas".
4. (Opcional) capturar screenshots via MCP `chrome-devtools`.

## Riscos e mitigação

- **Tipos do `diff`:** `diff@8.x` (já presente transitivamente) traz tipos próprios → **não** planejar `@types/diff`; adicionar só se o `npm run build` acusar falta.
- **HTML inválido (checkbox dentro de button):** resolver reestruturando a linha do histórico (checkbox e botão de preview como irmãos).
- **`matchMedia` no jsdom:** só afeta testes de componente do modal → mockar no setup; testes do motor não dependem disso.
- **Aviso `INVALID_ANNOTATION` (`@microsoft/signalr`)** no build é conhecido e não bloqueia.

## Conformidade com CLAUDE.md

- Sem mudança de contrato de API → `schemas.ts` intacto (regra atendida).
- Não afeta filhos/`rootOnly`, arquivamento, mentions, SignalR ou planos pausados (feature somente de leitura/visualização).
- Reusa `Button`, `cn`, `getErrorMessage`, `queryKeys`, padrão de modal e paleta existentes; única dependência nova justificada (`diff`).
