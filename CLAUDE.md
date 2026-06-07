# CLAUDE.md

Instrucoes para Claude Code trabalhar neste projeto.

## Antes de Comecar

1. Leia `README.md` e `AGENT.md`.
2. Verifique `git status --short`.
3. Entenda se a tarefa pede somente plano, revisao ou implementacao.
4. Quando o usuario pedir Plan Mode, produza um plano pratico, validavel e alinhado com a arquitetura existente.

## Sobre o Projeto

Prompt Tasks e uma aplicacao ASP.NET Core + React para criar, versionar e acompanhar prompts em Markdown usados com Claude Code e Codex. O diferencial do produto e conectar prompts a diretorios locais, validar referencias de arquivos, acompanhar planos Markdown externos e gerar prompts filhos para revisao ou implementacao desses planos.

## Stack

- Backend: .NET 10, ASP.NET Core, MediatR, FluentValidation, EF Core, PostgreSQL, Newtonsoft.Json, SignalR.
- Frontend: React, Vite, TypeScript, TanStack Query, TanStack Router, React Hook Form, Zod, TipTap, Monaco Editor, Tailwind CSS, componentes estilo shadcn/ui.
- Testes: xUnit, FluentAssertions, Testcontainers, Vitest e Testing Library.

## Regras que Nao Devem Ser Quebradas

- Prompts criados a partir de plano vinculado sao filhos do prompt pai.
- A tela do workspace lista apenas prompts pai.
- A tab `Prompts filhos` mostra os filhos do prompt atual.
- Clicar em prompt filho abre drawer; nao navega para a rota de edicao do filho.
- O drawer de geracao deve criar o filho e permanecer no contexto do prompt pai.
- Mencoes de arquivos devem passar por validacao no backend.
- Planos vinculados devem manter historico versionado.
- Mudancas no Markdown monitorado devem chegar ao navegador por SignalR.
- Prompt arquivado nao deve continuar monitorando plano vinculado.

## Como Planejar Mudancas

Planos devem conter:

- Objetivo concreto da mudanca.
- Arquivos/camadas provaveis a alterar.
- Impacto em backend, frontend, banco e testes.
- Migracao EF quando houver mudanca de schema.
- Criterios de validacao com comandos exatos.
- Riscos conhecidos e como mitiga-los.

Evite planos genericos. Use os nomes reais dos projetos, pastas e fluxos existentes.

## Comandos Uteis

Subir banco:

```powershell
docker compose up -d
```

Rodar API:

```powershell
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

Rodar frontend:

```powershell
cd frontend
npm run dev
```

Validar backend:

```powershell
dotnet build backend/PromptTasks.sln
dotnet test backend/PromptTasks.sln
```

Validar frontend:

```powershell
cd frontend
npm run test
npm run lint
npm run build
```

## Pontos de Atencao

- Se alterar contratos da API, atualize `frontend/src/api/schemas.ts`.
- Se alterar filtros/listagens de prompts, preserve `rootOnly` para a lista principal do workspace.
- Se mexer em prompts filhos, valide a tab `Prompts filhos` no navegador.
- Se mexer em planos vinculados, valide pause/resume, versoes e SignalR.
- Se alterar persistencia, gere migration e rode testes de integracao.
- Se criar novo template de prompt, atualize backend, schemas e UI de geracao.
- O aviso de build `INVALID_ANNOTATION` em `@microsoft/signalr` e conhecido e nao bloqueia quando o comando finaliza com sucesso.

## Estilo de Codigo

- Prefira solucoes pequenas, consistentes com o codigo existente.
- Use DTOs e validators no backend para entradas externas.
- Use React Query para dados remotos e evite estado duplicado.
- Use componentes e padroes visuais ja existentes.
- Nao adicione dependencias sem necessidade clara.
- Nao altere arquivos gerados ou configuracoes globais sem justificar no plano.
