# Data Model: Dados do painel (dashboard) do negócio

**Feature**: `005-business-dashboard` | **Date**: 2026-11-25

Nenhuma tabela, coluna, índice ou migração nova (FR-012). A feature só lê as entidades existentes
e as projeta em DTOs de saída.

## Entidades existentes (somente leitura)

### Client (`clients`)

| Campo | Tipo | Uso nesta feature |
|-------|------|-------------------|
| `Id` | int (PK) | filtro (= `clientId` da rota) e saída |
| `Name` | string(150) | saída |
| `Email` | string(254) | saída |
| `EmailConfirmed` | bool | filtro (`= true`) e saída |
| `CreatedAt` / `UpdatedAt` | timestamptz | saída |
| `DeletedAt` | timestamptz? | filtro global `IS NULL` (já existente) |
| `AccessCode`, `PasswordHash`, `EmailConfirmedAt`, `PasswordChangedAt`, links | — | **nunca** expostos (FR-007) |

### Business (`businesses`) — 0..1 por cliente

| Campo | Tipo | Uso |
|-------|------|-----|
| `ClientId` | int (FK, único) | condição do LEFT JOIN |
| `Name`, `Description` | string | saída |
| `Email`, `Telephone`, `Address`, `Details` | string? | saída (podem vir `null`, US1 cenário 3) |
| `CreatedAt`, `UpdatedAt` | timestamptz | saída |

### Consulta

```text
clients c LEFT JOIN businesses b ON b."clientId" = c.id
WHERE c.id = @clientId AND c."emailConfirmed" = true AND c."deletedAt" IS NULL
```

Gerada pelo EF Core a partir de `Include(client => client.Business)` com `AsNoTracking()`
(ver [research.md](./research.md) R4). Resultado vazio → `ClientNotFoundException` (401).

## DTOs de saída (novos, `Contracts/Dashboard`)

### BusinessDashboardResponse

| Campo JSON | Tipo | Regra |
|------------|------|-------|
| `client` | `ClientSummaryResponse` | sempre presente |
| `business` | `BusinessResponse` \| `null` | `null` quando o cliente não tem negócio (FR-006) |

### ClientSummaryResponse

| Campo JSON | Tipo |
|------------|------|
| `id` | int |
| `name` | string |
| `email` | string |
| `emailConfirmed` | bool |
| `createdAt` | DateTimeOffset (ISO 8601) |
| `updatedAt` | DateTimeOffset (ISO 8601) |

### BusinessResponse (existente, reutilizado)

`name`, `description`, `email?`, `telephone?`, `address?`, `details?`, `createdAt`, `updatedAt` —
mesmo DTO já devolvido pelo login e pelas operações de negócio.

## Tipo interno do serviço (novo, `Services/Dashboard`)

### BusinessDashboardResult

| Campo | Tipo | Regra |
|-------|------|-------|
| `Message` | string | `"Negócio encontrado com sucesso."` se há negócio; senão `"Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel."` |
| `Dashboard` | `BusinessDashboardResponse` | dados do painel |

## Estados

Não há transições: a consulta é só de leitura e idempotente (FR-009). Os dois estados observáveis
são "com negócio" e "sem negócio", decididos a cada consulta pelo que está no banco.
