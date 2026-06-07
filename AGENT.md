# AGENT.md

Guia operacional para agentes de codigo trabalhando neste repositorio.

## Contexto do Projeto

Prompt Tasks e um gerenciador local-first de prompts em Markdown para Claude Code e Codex. O produto organiza prompts por diretorio de trabalho, valida mencoes de arquivos, vincula planos Markdown externos, versiona alteracoes e usa SignalR para manter o navegador atualizado em tempo real.

Trate este repositorio como projeto de portfolio: alteracoes devem preservar clareza arquitetural, consistencia visual e boa cobertura de validacao.

## Regras de Produto

- A listagem do workspace deve mostrar somente prompts pai.
- Prompts gerados a partir de planos vinculados sao prompts filhos do prompt que possui o plano.
- Prompts filhos devem ser exibidos na tab `Prompts filhos` do prompt pai.
- Clicar em um prompt filho deve abrir drawer na rota do prompt pai; nao redirecione para edicao do filho.
- O drawer de geracao de prompt filho deve ter apenas a acao de criar; nao reintroduza `Criar e abrir`.
- Referencias `@arquivo` precisam ser validadas contra o diretorio de trabalho no backend.
- Planos vinculados devem ser versionados e refletidos em tempo real.
- Arquivar um prompt deve pausar o monitoramento de planos vinculados.

## Arquitetura Backend

- Preserve Clean Architecture:
  - `Domain`: entidades, enums e conceitos de dominio.
  - `Application`: comandos, consultas, handlers MediatR, validadores, DTOs e contratos.
  - `Infrastructure`: EF Core, PostgreSQL, filesystem, cache e watchers.
  - `Api`: controllers, SignalR, OpenAPI, DI e configuracao HTTP.
- Nao coloque regra de negocio em controllers.
- Para novos casos de uso, prefira command/query MediatR com validator quando houver entrada externa.
- Para mudancas de schema, gere migration EF Core e atualize os testes.
- Se mudar DTOs do backend, atualize schemas Zod e tipos do frontend.
- Mantenha `RowVersion`/concorrencia em updates de prompt.

## Arquitetura Frontend

- Use TanStack Router para novas rotas.
- Use TanStack Query para chamadas remotas, cache e invalidacao.
- Valide payloads de API com Zod em `frontend/src/api/schemas.ts`.
- Centralize query keys em `frontend/src/api/query-keys.ts`.
- Mantenha funcionalidades agrupadas em `frontend/src/features`.
- Use componentes existentes em `frontend/src/components` antes de criar novos.
- Use lucide-react para icones.
- Para navegar/visualizar arquivos do workspace, reutilize o visualizador Monaco e a arvore em `frontend/src/features/files` (`FileExplorer`, `WorkspaceFileTree`, `FileViewerPanel`).
- Evite telas de marketing dentro do produto; o app deve abrir direto na experiencia funcional.

## Comandos de Desenvolvimento

Banco:

```powershell
docker compose up -d
```

API:

```powershell
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

## Validacao Esperada

Execute conforme o tipo de mudanca.

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

Para mudancas visuais ou de fluxo, valide tambem no navegador. O frontend roda em `http://localhost:5190` e a API em `http://localhost:5191`.

## Observacoes de Ambiente

- Ambiente principal: Windows/PowerShell.
- Use `rg` para buscar arquivos e texto.
- Use migrations EF para alteracoes de banco.
- O build do frontend pode emitir avisos `INVALID_ANNOTATION` vindos de `@microsoft/signalr`; se o comando terminar com sucesso, eles sao nao bloqueantes.
- Nao versionar `node_modules`, `dist`, artefatos temporarios de Playwright ou arquivos locais de banco.

## Padrao de Entrega

- Mantenha as alteracoes pequenas e coerentes com o pedido.
- Nao reverta trabalho existente que nao faz parte da sua mudanca.
- Atualize testes quando alterar comportamento.
- Use Conventional Commits quando criar commits.
