# Data Model: API de Autenticação e Controle de Acesso (Gateway RAG)

**Feature**: `001-rag-auth-gateway` | **Data**: 2026-10-25 | **Plano**: [plan.md](./plan.md)

Banco PostgreSQL, EF Core code-first com migrations. Convenções (ver [research.md](./research.md) R6):
IDs `int` identity; colunas em camelCase; `createdAt`/`updatedAt` em `timestamp with time zone`
(UTC) preenchidas por interceptor; entidades nunca expostas na API (Constituição IV).

## Diagrama

```text
clients (1) ──────────── (0..1) businesses
   id PK                         id PK
   accessCode UNIQUE             clientId FK UNIQUE → clients.id (ON DELETE CASCADE)
```

## Entidade `Client` → tabela `clients`

| Coluna | Tipo | Nulo | Regras |
|---|---|---|---|
| `id` | `integer` identity | não | PK; exposto como `clientId` no cadastro, no login e nas rotas autenticadas (FR-028); nunca reaproveitado |
| `accessCode` | `varchar(12)` | não | 12 caracteres `[A-Za-z0-9]`, CSPRNG; **índice único total** (inclui removidos, FR-025); imutável |
| `name` | `varchar(150)` | não | obrigatório, trim, 1–150 |
| `email` | `varchar(254)` | não | formato válido; normalizado (trim + minúsculas); **índice único parcial** `WHERE "deletedAt" IS NULL` (FR-003) |
| `passwordHash` | `text` | não | saída de `PasswordHasher<Client>`; nunca serializado nem logado (FR-005) |
| `createdAt` | `timestamptz` | não | definido na inserção |
| `updatedAt` | `timestamptz` | não | definido na inserção e a cada alteração |
| `deletedAt` | `timestamptz` | sim | soft delete (R9); global query filter `deletedAt IS NULL` |

**Validação da entrada** (`RegisterClientRequest`): nome obrigatório até 150; e-mail obrigatório,
formato válido, até 254; senha obrigatória, 8–128 caracteres, ao menos uma letra e um número
(FR-004).

**Estados**:

```text
[inexistente] --register--> ATIVO --DELETE /api/v1/{clientId}--> REMOVIDO (deletedAt preenchido)
```

- ATIVO: pode logar; tokens válidos.
- REMOVIDO: login → 401 genérico; tokens já emitidos → 401 (validados em `OnTokenValidated`);
  e-mail livre para novo cadastro; `id` e `accessCode` permanecem reservados. Estado final.
  A transição apaga antes o negócio (se houver), após 2xx da API RAG (FR-029).

## Entidade `Business` → tabela `businesses`

| Coluna | Tipo | Nulo | Regras (FR-016a) |
|---|---|---|---|
| `id` | `integer` identity | não | PK; nunca exposto na API |
| `clientId` | `integer` | não | FK → `clients.id`; **índice único** (1:1, FR-016); cascade delete |
| `name` | `varchar(150)` | não | obrigatório, até 150 |
| `description` | `varchar(1000)` | não | obrigatória, até 1.000 |
| `email` | `varchar(254)` | sim | opcional; formato válido quando informado |
| `telephone` | `varchar(11)` | sim | opcional; aceita máscara na entrada, persistido só com dígitos; 10 ou 11 dígitos com DDD |
| `address` | `varchar(300)` | sim | opcional; linha única (sem quebras de linha) |
| `details` | `varchar(2000)` | sim | opcional; texto livre |
| `createdAt` | `timestamptz` | não | definido na inserção |
| `updatedAt` | `timestamptz` | não | atualizado a cada alteração |

Campos opcionais vazios (`""` ou só espaços) são normalizados para `null`.

**Estados** (remoção definitiva, FR-016):

```text
[sem negócio] --POST (RAG 2xx)--> EXISTE --PUT (RAG 2xx)--> EXISTE
                                   EXISTE --DELETE (RAG 2xx)--> [sem negócio] (linha apagada)
```

Toda transição só é confirmada no banco após 2xx da API RAG (FR-022, R7). Falha da RAG → rollback,
estado anterior mantido.

## Token de acesso (não persistido)

| Claim | Valor |
|---|---|
| `sub` | `accessCode` do cliente |
| `client_id` | `id` do cliente (comparado com `{clientId}` da rota) |
| `jti` | GUID |
| `iat` / `exp` | emissão / emissão + `jwt:expirationMinutes` (padrão 60) |
| `iss` / `aud` | `jwt:issuer` / `jwt:audience` |

Assinatura HS256 com `JWT_SIGNING_KEY`.

## Exceções de domínio → status

| Exceção | Status | Mensagem (PT) |
|---|---|---|
| `EmailAlreadyRegisteredException` | 409 | "Já existe uma conta cadastrada com este e-mail." |
| `InvalidCredentialsException` | 401 | "E-mail ou senha inválidos." |
| `ClientNotFoundException` | 401 | "Token inválido ou conta inexistente." |
| `BusinessAlreadyExistsException` | 409 | "Você já possui um negócio cadastrado." |
| `BusinessNotFoundException` | 404 | "Não há negócio cadastrado para este cliente." |
| `AccessCodeGenerationException` | 500 | "Não foi possível concluir o cadastro. Tente novamente." |
| `RagApiRejectedException` | 4xx da RAG | mensagem por status (ver [contracts/rag-api-outbound.md](./contracts/rag-api-outbound.md)) |
| `RagApiIntegrationException` | 502 | "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde." |
| `RagApiUnavailableException` | 503 | "O serviço de recursos está indisponível no momento." |
| `RagApiTimeoutException` | 504 | "O serviço de recursos demorou demais para responder." |
