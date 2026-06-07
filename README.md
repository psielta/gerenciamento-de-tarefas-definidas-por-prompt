<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/logo-dark.svg" />
    <img src="docs/logo-light.svg" alt="Prompt Tasks" width="320" />
  </picture>
</p>

Prompt Tasks e uma aplicacao full-stack para organizar, versionar e acompanhar prompts em Markdown usados com agentes de desenvolvimento como Claude Code e Codex. O projeto foi construido como uma demonstracao de portfolio com foco em arquitetura limpa, experiencia de edicao produtiva, persistencia auditavel, workflow operacional e atualizacoes em tempo real.

O caso de uso principal e simples: o usuario cadastra um diretorio de trabalho, escreve prompts referenciando arquivos reais desse diretorio com `@arquivo`, vincula planos Markdown gerados por agentes externos, acompanha suas mudancas e gerencia a fase atual da tarefa sem sair do navegador.

## Principais Recursos

- Gerenciamento de diretorios de trabalho com validacao de caminho local.
- Editor de prompt em Markdown com busca de arquivos por `@`, mencoes estilizadas e validacao das referencias no backend.
- Persistencia de prompts, versoes, status e referencias de arquivos no PostgreSQL.
- Relacionamento entre prompts pai e prompts filhos, usado para gerar prompts auxiliares a partir de um plano vinculado.
- Board global de tarefas, agrupado por fase, com modos Kanban e vertical, drag-and-drop, arquivamento direto pelo cartao e filtros por diretorio, status do prompt e status do workflow.
- Workflow por prompt raiz, com fases configuraveis, responsavel atual, transicoes manuais, transicoes automaticas por prompts filhos, concluir e reabrir.
- Timeline append-only por prompt, registrando inicio do fluxo, mudancas de fase, troca de responsavel, notas, conclusao, reabertura, edicao de fases e transicoes originadas por templates.
- Selos de fase e responsavel nas listas de prompts do workspace.
- Vinculo de arquivos Markdown externos, como planos gerados pelo Claude Code.
- Monitoramento de alteracoes desses planos, versionamento automatico e atualizacao em tempo real via SignalR.
- Pausa, retomada, atualizacao manual e remocao de planos vinculados.
- Renderizacao de Markdown versionado no navegador com historico navegavel.
- Navegador de arquivos do diretorio de trabalho: arvore lazy-loaded e busca por nome, disponivel na aba `Arquivos` do workspace e em uma pagina global `Arquivos` no header, com seletor de diretorio cuja ultima escolha e persistida no navegador.
- Visualizador de codigo somente leitura baseado no Monaco Editor, com realce de sintaxe, tema sincronizado com o app e atualizacao em tempo real via SignalR quando o arquivo muda no disco.
- Templates de prompts para fluxo de revisao, implementacao, rebase e merge de planos, com atualizacao automatica da fase da tarefa pai quando aplicavel.
- Indicadores no header para limites atuais de Claude Code e Codex, lendo as fontes locais dos agentes e sincronizando atualizacoes via SignalR.
- **Assistente IA com Gemini:** refinamento de prompts, chat de suporte e configuracao de modelo diretamente na tela de criacao e edicao.
- Contexto de workspace opcional na IA, lendo automaticamente `README.md`, `CLAUDE.md` e `AGENT.md` da raiz do diretorio de trabalho.
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
- Leitura local de uso dos agentes: Claude via OAuth da instalacao local e API de uso da Anthropic; Codex via snapshots `rate_limits` dos JSONL em `~/.codex/sessions`.
- Integracao com a Gemini API via `HttpClient` tipado: refinamento de prompts, chat com streaming SSE e context caching de dois niveis (instrucao de sistema e historico de sessao).
- Leitura segura de contexto do workspace para IA, restrita a arquivos Markdown conhecidos na raiz canonica do diretorio.
- OpenAPI e Scalar para exploracao da API.
- xUnit, FluentAssertions, Testcontainers e `Microsoft.AspNetCore.Mvc.Testing` para testes.

### Frontend

- React 19 com Vite 8 e TypeScript 6.
- TanStack Router para rotas tipadas.
- TanStack Query para cache, sincronizacao e invalidacao de dados.
- React Hook Form e Zod para formularios e contratos de API.
- TipTap para editor Markdown com mencoes de arquivo.
- Monaco Editor (`@monaco-editor/react`) para o visualizador de arquivos do workspace com realce de sintaxe.
- Tailwind CSS 4 e componentes no estilo shadcn/ui.
- SignalR client para atualizacoes em tempo real.
- `react-markdown` com `remark-gfm` para renderizacao de respostas do assistente IA.
- Vitest, Testing Library e jsdom para testes.

### Infraestrutura Local

- Docker Compose com PostgreSQL 18.
- Migrations do EF Core aplicadas automaticamente em ambiente `Development`.
- API em `http://localhost:5191`.
- Frontend em `http://localhost:5190`.

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
6. A partir do plano vinculado, o usuario gera prompts filhos, como revisao do plano, implementacao, revisao de PR, rebase ou merge.
7. Prompts filhos permanecem associados ao prompt pai e nao poluem a listagem principal do workspace.
8. Cada prompt raiz representa uma tarefa no board global.
9. Ao criar uma tarefa nao arquivada, o workflow inicia automaticamente em `Engenharia de prompt` com responsavel humano.
10. Ao criar prompts filhos a partir de templates mapeados, a tarefa pai avanca automaticamente para a fase correspondente, como revisao do plano, implementacao, revisao de codigo, rebase ou merge.
11. Re-reviews de plano ou PR incrementam a iteracao da fase, exibem selo no cartao e ficam registrados na timeline.
12. No board, o usuario pode arrastar tarefas entre fases, concluir, reabrir ou arquivar a tarefa.
13. Na aba `Timeline`, o usuario acompanha o historico, adiciona notas, muda fase/responsavel, conclui ou reabre o fluxo.
14. O template de fases pode ser editado em `Configuracoes`; tarefas existentes mantem um snapshot proprio das fases.
15. Na tela de criacao ou edicao de um prompt, o botao **Refinar** envia o conteudo atual para o Gemini e exibe uma previa do prompt otimizado antes de aplicar.
16. O botao **IA** abre um drawer lateral com chat de suporte especializado em engenharia de prompts; o usuario pode incluir o conteudo do prompt atual como contexto da conversa.
17. O painel de **Configuracao** do drawer permite escolher o modelo Gemini, ajustar a temperatura e definir o nivel de raciocinio. As configuracoes sao salvas por usuario.
18. Em cada workspace, o usuario pode ativar o **Contexto de IA** para injetar `README.md`, `CLAUDE.md` e `AGENT.md` nas instrucoes de sistema do Gemini durante o refinamento e o chat.
19. A qualquer momento, o usuario pode navegar e visualizar os arquivos do diretorio de trabalho pela pagina global `Arquivos` no header ou pela aba `Arquivos` do workspace, com busca por nome e visualizacao somente leitura no Monaco Editor.

## Como Executar

### Pre-requisitos

- .NET SDK 10, conforme `backend/global.json`.
- Node.js compativel com o frontend e npm.
- Docker Desktop ou Docker Engine com Compose.

### 1. Configurar a chave Gemini

Crie um arquivo `.env` na raiz do repositorio (nunca commitado — ja esta no `.gitignore`):

```text
GEMINI_API_KEY=sua_chave_aqui
```

Obtenha a chave em [https://aistudio.google.com/apikey](https://aistudio.google.com/apikey). Sem ela, os endpoints `/api/ai/*` retornam `503`; o restante do app funciona normalmente.

### 2. Subir o PostgreSQL

```powershell
docker compose up -d
```

O banco local usa as credenciais definidas em `docker-compose.yml`:

```text
Host=localhost
Port=5459
Database=prompttasks
Username=prompttasks
Password=prompttasks
```

### 3. Executar a API

```powershell
dotnet run --project backend/src/PromptTasks.Api/PromptTasks.Api.csproj
```

Servicos expostos:

- API REST: `http://localhost:5191/api`
- SignalR hub: `http://localhost:5191/hubs/prompts`
- Scalar/OpenAPI: `http://localhost:5191/scalar`

### 4. Executar o frontend

```powershell
cd frontend
npm install
npm run dev
```

Frontend:

```text
http://localhost:5190
```

Para apontar o frontend para outra API, defina:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5191/api"
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

- Adicionar visualizacao de diferencas entre versoes de prompts e entre versoes de planos Markdown vinculados, permitindo revisar exatamente o que mudou no prompt ou o que o Claude alterou no plano.
- Permitir salvar uma copia do Markdown de um plano vinculado em um diretorio definido pelo usuario, preservando historico local fora do arquivo monitorado original.

## Decisoes de Produto Importantes

- A listagem principal do workspace mostra apenas prompts pai.
- Prompts gerados a partir de planos vinculados sao prompts filhos.
- Clicar em um prompt filho abre um drawer dentro da rota do prompt pai; nao redireciona para a tela de edicao do filho.
- Cada prompt pai e tratado como uma tarefa; prompts filhos sao artefatos auxiliares e nao entram no board como tarefas independentes.
- O workflow combina acoes manuais com transicoes automaticas geradas por templates de prompt filho; concluir e reabrir continuam dependendo de acao explicita do usuario.
- Concluir o workflow nao arquiva o prompt; `Prompt.Status` e `PromptWorkflow.Status` sao estados separados.
- Arquivos mencionados em prompts precisam existir dentro do diretorio de trabalho.
- O visualizador de arquivos do workspace e somente leitura; a edicao continua sendo feita no editor local do usuario.
- Planos vinculados podem ser monitorados em background, pausados e retomados.
- Ao arquivar um prompt, os planos vinculados devem parar de ser monitorados.
- Ao arquivar ou excluir um prompt, os caches Gemini das sessoes associadas sao liberados proativamente para evitar custo de armazenamento desnecessario.
- A chave `GEMINI_API_KEY` nunca trafega pelo navegador; todas as chamadas a API Gemini sao feitas exclusivamente pelo backend.
- O chat usa context caching em dois niveis: instrucao de sistema compartilhada por modelo (TTL 1h) e cache de historico por sessao criado apos atingir o limite de tokens configurado.
- O contexto de IA do workspace e opt-in por diretorio de trabalho e le apenas `README.md`, `CLAUDE.md` e `AGENT.md` da raiz canonica. Arquivos ausentes, vazios, inacessiveis ou grandes demais sao ignorados sem falhar a chamada de IA.
- Contexto especifico de workspace nao entra no cache global de system instruction do Gemini; quando usado no chat, ele e embutido no cache de sessao e protegido por hash para evitar reutilizar contexto antigo.
- O nivel de raciocinio padrao e `high`; usuarios podem reduzir para `medium` ou `low` na aba Configuracao do drawer de IA.

## Documentacao para Agentes

Este repositorio possui guias especificos para agentes de codigo:

- `AGENT.md`: instrucoes gerais para agentes como Codex.
- `CLAUDE.md`: instrucoes especificas para Claude Code e fluxo de Plan Mode.

Antes de alterar codigo, leia esses arquivos para preservar as regras de arquitetura, produto e validacao.
