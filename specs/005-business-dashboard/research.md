# Research: Dados do painel (dashboard) do negócio

**Feature**: `005-business-dashboard` | **Date**: 2026-11-25

Entradas: [spec.md](./spec.md), `.mine/dashboard-resource/plan.md` (decisões do autor) e o código
existente em `src/RagAuth.Api`. Não restou nenhum `NEEDS CLARIFICATION`.

## R1. Rota da consulta

- **Decision**: `GET /api/v1/{clientId}/business`, adicionada ao grupo de rotas já existente em
  `BusinessEndpoints` (o mesmo de `POST`/`PUT`/`DELETE`).
- **Rationale**: é a rota pedida em `.mine/dashboard-resource/plan.md`. O grupo já aplica a
  política `ClientRouteOwner` (401 sem token válido, 403 quando o `clientId` da rota difere do
  claim `client_id` do token), o que cobre FR-002, FR-003 e a US3 sem código novo de segurança. A
  política CORS global (`004-cors-frontend-access`) já libera `GET`, cobrindo FR-011.
- **Alternatives considered**: `GET /api/v1/{clientId}/dashboard` — nome mais fiel ao conceito de
  painel, mas diverge da decisão do autor e exigiria repetir a configuração do grupo.

## R2. Cliente sem negócio cadastrado

- **Decision**: HTTP 200 com `data.client` preenchido, `data.business = null` e a mensagem
  "Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel.".
- **Rationale**: confirmado pelo autor em 2026-11-25 (prevalece sobre o 404 do rascunho em
  `.mine`). Atende FR-006, US2 e SC-002 e combina com o LEFT JOIN pedido: o cliente sempre vem, o
  negócio pode faltar.
- **Alternatives considered**: 404 `BusinessNotFoundException` — descartado pelo autor; faria o
  front-end tratar como erro um estado normal de todo cliente novo.

## R3. Formato de erro

- **Decision**: manter o envelope atual (`ApiResponse`) com `error` = `ProblemDetails`
  (`type`, `title`, `status`, `detail`, `instance`, `correlationId`), gerado pelo
  `DomainExceptionHandler` + `ApiEnvelopeProblemDetailsWriter`.
- **Rationale**: confirmado pelo autor em 2026-11-25. Exigido pela constituição (VII) e por FR-010;
  nenhum código novo de tratamento de erro.
- **Alternatives considered**: `error = { code, message }` do rascunho em `.mine` — quebraria a
  consistência com os demais endpoints e exigiria mudar o writer global.

## R4. Consulta ao banco (LEFT JOIN, só e-mail confirmado)

- **Decision**: novo método `IClientRepository.FindConfirmedWithBusinessByIdAsync(clientId)`
  implementado com EF Core: `Clients.AsNoTracking().Include(client => client.Business)`
  filtrando `client.Id == clientId && client.EmailConfirmed`. O filtro global de `Client`
  (`DeletedAt == null`) continua valendo. O `Include` de navegação opcional 1:0..1 gera
  `LEFT JOIN` em uma única consulta.
- **Rationale**: atende ao "clients LEFT JOIN business" e ao "somente `emailConfirmed = true`"
  do rascunho, numa ida ao banco, só de leitura (`AsNoTracking`, FR-009), sem depender da API RAG
  (FR-008, SC-005). O índice da PK de `clients` e o índice único de `businesses.clientId` deixam a
  consulta trivialmente dentro de SC-004.
- **Alternatives considered**:
  - Duas consultas (`FindActiveByIdAsync` + `IBusinessRepository.FindByClientIdAsync`) — funciona,
    mas são duas idas ao banco e não segue o join pedido.
  - SQL bruto / Dapper — dependência ou código desnecessário; o EF já gera o join.

## R5. Cliente não encontrado / e-mail não confirmado

- **Decision**: se a consulta não encontra o cliente (removido ou com e-mail não confirmado), o
  serviço lança `ClientNotFoundException` → HTTP 401 "Token ausente, inválido ou expirado.".
- **Rationale**: mesmo comportamento das outras operações autenticadas (edge case "conta removida
  depois da emissão do token"). Um cliente sem e-mail confirmado não consegue fazer login
  (feature 002), então esse caminho é só defensivo e não vaza a existência da conta.
- **Alternatives considered**: 404 ou 403 — revelariam o estado da conta e destoariam do restante.

## R6. Campos do resumo da conta

- **Decision**: `id`, `name`, `email`, `emailConfirmed`, `createdAt`, `updatedAt`.
- **Rationale**: união do que pede a spec (FR-004: identificador, nome, e-mail, indicação de
  e-mail confirmado, data de cadastro) com o rascunho em `.mine` (id, name, email, createdAt,
  updatedAt). Nada sensível (FR-007): sem `passwordHash`, `accessCode`, `deletedAt`, links.
- **Alternatives considered**: devolver só os dados do negócio (exemplo 200 do rascunho) —
  descumpriria FR-004 e o pedido de campos de `clients` do próprio rascunho.

## R7. Onde fica a regra (constituição I e II)

- **Decision**: novo serviço `IBusinessDashboardService` / `BusinessDashboardService` em
  `Services/Dashboard`, com um mapper estático `BusinessDashboardMapper`. O serviço devolve um
  `BusinessDashboardResult` (mensagem + DTO); o endpoint só monta o envelope.
- **Rationale**: a escolha da mensagem (com ou sem negócio) é regra de negócio e não pode ficar no
  endpoint. Serviço separado de `BusinessService` mantém responsabilidade única (este é só
  leitura e não depende da API RAG).
- **Alternatives considered**: novo método em `IBusinessService` — misturaria a consulta do painel
  com as operações sincronizadas com a API RAG.

## Dependências

Nenhuma dependência nova. Tudo usa ASP.NET Core Minimal API, EF Core/Npgsql e xUnit já presentes.
