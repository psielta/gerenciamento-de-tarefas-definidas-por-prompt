# Prompt Tasks

Prompt Tasks e uma aplicacao full-stack para organizar, versionar e acompanhar prompts em Markdown usados com agentes de desenvolvimento como Claude Code e Codex. O projeto foi construido como uma demonstracao de portfolio com foco em arquitetura limpa, experiencia de edicao produtiva, persistencia auditavel e atualizacoes em tempo real.

O caso de uso principal e simples: o usuario cadastra um diretorio de trabalho, escreve prompts referenciando arquivos reais desse diretorio com `@arquivo`, vincula planos Markdown gerados por agentes externos e acompanha suas mudancas sem sair do navegador.

## Principais Recursos

- Gerenciamento de diretorios de trabalho com validacao de caminho local.
- Editor de prompt em Markdown com busca de arquivos por `@`, mencoes estilizadas e validacao das referencias no backend.
- Persistencia de prompts, versoes, status e referencias de arquivos no PostgreSQL.
- Relacionamento entre prompts pai e prompts filhos, usado para gerar prompts auxiliares a partir de um plano vinculado.
- Vinculo de arquivos Markdown externos, como planos gerados pelo Claude Code.
- Monitoramento de alteracoes desses planos, versionamento automatico e atualizacao em tempo real via SignalR.
- Pausa, retomada, atualizacao manual e remocao de planos vinculados.
- Renderizacao de Markdown versionado no navegador com historico navegavel.
- Templates de prompts para fluxo de revisao e implementacao de planos.
- API REST documentada com OpenAPI/Scalar.
- Testes unitarios, testes de integracao com PostgreSQL em container e testes de frontend com Vitest.

## Stack

### Backend

- ASP.NET Core com .NET 10.
- Clean Architecture em projetos separados: `Domain`, `Application`, `Infrastructure` e `Api`.
- MediatR para comandos e consultas.
- FluentValidation para validacao de entrada.
- Entity Framework Core 10 com PostgreSQL via Npgsql.
- Newtonsoft.Json integrado ao ASP.NET Core.
- SignalR para eventos em tempo real.
- OpenAPI e Scalar para exploracao da API.
- xUnit, FluentAssertions, Testcontainers e `Microsoft.AspNetCore.Mvc.Testing` para testes.

### Frontend

- React 19 com Vite 8 e TypeScript 6.
- TanStack Router para rotas tipadas.
- TanStack Query para cache, sincronizacao e invalidacao de dados.
- React Hook Form e Zod para formularios e contratos de API.
- TipTap para editor Markdown com mencoes de arquivo.
- Tailwind CSS 4 e componentes no estilo shadcn/ui.
- SignalR client para atualizacoes em tempo real.
- Vitest, Testing Library e jsdom para testes.

### Infraestrutura Local

- Docker Compose com PostgreSQL 18.
- Migrations do EF Core aplicadas automaticamente em ambiente `Development`.
- API em `http://localhost:5080`.
- Frontend em `http://localhost:5173`.

## Arquitetura

```text
backend/
  src/
    PromptTasks.Domain/          Entidades, enums e regras de dominio
    PromptTasks.Application/     Casos de uso, DTOs, validadores e contratos
    PromptTasks.Infrastructure/  EF Core, PostgreSQL, filesystem e watchers
    PromptTasks.Api/             Controllers REST, SignalR, OpenAPI e DI
  tests/
    PromptTasks.Application.UnitTests/
    PromptTasks.Infrastructure.UnitTests/
    PromptTasks.Api.IntegrationTests/

frontend/
  src/
    api/                         Cliente HTTP, schemas Zod e query keys
    components/                  Componentes reutilizaveis de UI
    features/                    Modulos por area de produto
    realtime/                    Integracao SignalR
    routes/                      Rotas TanStack Router
```

O backend segue um fluxo orientado a casos de uso. Controllers chamam MediatR, handlers da camada `Application` aplicam regras e acessam contratos, enquanto a camada `Infrastructure` implementa banco de dados, servicos de arquivo e monitoramento de documentos. O frontend consome contratos validados por Zod e mantem a tela sincronizada com React Query e SignalR.

## Fluxo de Produto

1. O usuario cria um diretorio de trabalho apontando para um caminho local.
2. Dentro desse workspace, cria prompts em Markdown e referencia arquivos com mencoes como `@src/main.go`.
3. O backend valida se os arquivos existem dentro do diretorio permitido.
4. Um prompt de planejamento pode ser vinculado a um Markdown externo gerado pelo Claude Code.
5. O sistema renderiza esse plano, monitora alteracoes, cria versoes e envia eventos em tempo real.
6. A partir do plano vinculado, o usuario gera prompts filhos, como revisao do plano ou implementacao.
7. Prompts filhos permanecem associados ao prompt pai e nao poluem a listagem principal do workspace.

## Como Executar

### Pre-requisitos

- .NET SDK 10, conforme `backend/global.json`.
- Node.js compativel com o frontend e npm.
- Docker Desktop ou Docker Engine com Compose.

### 1. Subir o PostgreSQL

```powershell
docker compose up -d
```

O banco local usa as credenciais definidas em `docker-compose.yml`:

```text
Host=localhost
Port=5432
Database=prompttasks
Username=prompttasks
Password=prompttasks
```

### 2. Executar a API

```powershell
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

Servicos expostos:

- API REST: `http://localhost:5080/api`
- SignalR hub: `http://localhost:5080/hubs/prompts`
- Scalar/OpenAPI: `http://localhost:5080/scalar`

### 3. Executar o frontend

```powershell
cd frontend
npm install
npm run dev
```

Frontend:

```text
http://localhost:5173
```

Para apontar o frontend para outra API, defina:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5080/api"
```

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

Auditoria de dependencias:

```powershell
cd frontend
npm audit --audit-level=moderate
```

## Tarefas Futuras

- Implementar uma timeline operacional para cada prompt, permitindo acompanhar o estado atual da tarefa associada, eventos relevantes, mudancas de status e historico de execucao do fluxo.
- Evoluir o header da aplicacao para exibir informacoes dos agentes Claude e Codex, com destaque para disponibilidade, contexto operacional e limites de uso atuais.

## Decisoes de Produto Importantes

- A listagem principal do workspace mostra apenas prompts pai.
- Prompts gerados a partir de planos vinculados sao prompts filhos.
- Clicar em um prompt filho abre um drawer dentro da rota do prompt pai; nao redireciona para a tela de edicao do filho.
- Arquivos mencionados em prompts precisam existir dentro do diretorio de trabalho.
- Planos vinculados podem ser monitorados em background, pausados e retomados.
- Ao arquivar um prompt, os planos vinculados devem parar de ser monitorados.

## Documentacao para Agentes

Este repositorio possui guias especificos para agentes de codigo:

- `AGENT.md`: instrucoes gerais para agentes como Codex.
- `CLAUDE.md`: instrucoes especificas para Claude Code e fluxo de Plan Mode.

Antes de alterar codigo, leia esses arquivos para preservar as regras de arquitetura, produto e validacao.
