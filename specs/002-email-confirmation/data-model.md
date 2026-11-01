# Data Model: Confirmação de e-mail no cadastro

**Feature**: `002-email-confirmation` | **Data**: 2026-10-31 | **Plano**: [plan.md](./plan.md)

Colunas em camelCase (convenção de `AppDbContext`). Decisões em [research.md](./research.md) R1, R5, R9.

## Cliente (`clients`) — alterada

| Coluna | Tipo | Regra |
|---|---|---|
| `emailConfirmed` | `boolean` | não nulo, padrão `false`; `true` após confirmação |
| `emailConfirmedAt` | `timestamptz` | nulo enquanto pendente; preenchido na confirmação (FR-008) |

Demais colunas inalteradas (feature 001). Entidade `Client` ganha `EmailConfirmed`,
`EmailConfirmedAt` e a navegação `ICollection<EmailConfirmationLink> ConfirmationLinks`.

### Situação (derivada)

```text
            cadastro                     confirmação (link válido)
 (nada) ───────────────► PENDENTE ─────────────────────────────► ATIVA
                            │                                      │
                            │ remoção de conta (exige token: só     │ remoção de conta
                            │ ocorre se ativa; pendente não loga)   ▼
                            └───────────────────────────────────► REMOVIDA
```

| Situação | Condição |
|---|---|
| Pendente de confirmação | `deletedAt IS NULL AND emailConfirmed = false` |
| Ativa | `deletedAt IS NULL AND emailConfirmed = true` |
| Removida | `deletedAt IS NOT NULL` |

Regras:
- O índice único de e-mail entre contas não removidas (`deletedAt IS NULL`) continua valendo: e-mail
  de conta pendente bloqueia novo cadastro (409), com link vencido ou não (Clarificação 2).
- Transição PENDENTE → ATIVA só pela confirmação; não há volta.
- Migração: contas não removidas existentes → `emailConfirmed = true`, `emailConfirmedAt = createdAt`
  (FR-020).

## Link de confirmação de e-mail (`email_confirmation_links`) — nova

| Coluna | Tipo | Regra |
|---|---|---|
| `id` | `bigint` identity | PK |
| `clientId` | `int` | FK → `clients.id`, `ON DELETE CASCADE`, não nulo |
| `selector` | `varchar(22)` | Base64Url de 16 bytes aleatórios; **índice único** |
| `validatorHash` | `char(64)` | hexa de `SHA-256(validator:accessCode:email-confirmation)`; validator nunca gravado |
| `purpose` | `varchar(32)` | fixo `email-confirmation` (ação vinculada, FR-004) |
| `origin` | `varchar(32)` | `registration` ou `new-link-request` (só `new-link-request` conta no limite diário, FR-015) |
| `issuedAt` | `timestamptz` | não nulo |
| `expiresAt` | `timestamptz` | não nulo; `issuedAt + 24 h` (FR-006) |
| `usedAt` | `timestamptz` | nulo até a confirmação (uso único, FR-007) |
| `revokedAt` | `timestamptz` | preenchido quando um novo link é emitido (FR-013) |

Índices:
- `ux_email_confirmation_links_selector` — único em `selector`.
- `ux_email_confirmation_links_active_per_client` — único em `clientId` com filtro
  `"usedAt" IS NULL AND "revokedAt" IS NULL` (no máximo um link ativo por cliente; protege a
  concorrência de pedidos de novo link, research R5).
- `ix_email_confirmation_links_client_issued` — em (`clientId`, `issuedAt`) para os limites.

Filtro global do EF: `link.Client.DeletedAt == null`, consistente com o filtro de `Client` (link de
conta removida é tratado como inexistente → inválido, FR-010).

### Estados do link

| Estado | Condição | Resultado na confirmação |
|---|---|---|
| Ativo | `usedAt`, `revokedAt` nulos e `now ≤ expiresAt` | confirma (200) |
| Expirado | `usedAt`, `revokedAt` nulos e `now > expiresAt` | 410 |
| Usado | `usedAt` preenchido | 200 "já confirmado" |
| Revogado | `revokedAt` preenchido | 400 inválido |

Qualquer resultado só é avaliado se `selector` existe **e** o hash do validator confere; caso
contrário → 400 inválido. Conta já confirmada com link revogado → 200 "já confirmado" (FR-011
prevalece: o e-mail já está confirmado).

### Regras de emissão

| Origem | Quando | Pré-condições |
|---|---|---|
| `registration` | no mesmo `SaveChanges` do cadastro | — |
| `new-link-request` | pedido de novo link | conta pendente; último link emitido há ≥ 5 min; < 10 links `new-link-request` nas últimas 24 h |

A emissão `new-link-request` revoga o link ativo (`revokedAt = now`) e insere o novo na mesma transação.

## Mensagem da fila (em memória, não persistida)

`ConfirmationEmailMessage`: `ClientId`, `RecipientEmail`, `ClientName`, `ConfirmationUrl`,
`AttemptNumber`. Vive só no `Channel` do processo; perdida em reinício (aceito pela spec).

## Configuração (`email`)

| Chave | Padrão | Uso |
|---|---|---|
| `fromAddress` | — (obrigatório) | remetente no Resend |
| `appName` | `RagAuth` | assunto e rodapé |
| `confirmationPageBaseUrl` | `https://ragauth.com.br` | base do link (front-end) |
| `newLinkIntervalMinutes` | 5 | FR-015 |
| `maximumNewLinksPerDay` | 10 | FR-015 |
| `retryDelaysSeconds` | `[30, 120]` | atrasos da 2ª e 3ª tentativas (R6) |

O prazo de validade do link é **fixo em 24 horas** (constante `EmailConfirmationLink.ValidityPeriod`, sem configuração), igual ao texto do e-mail.

Variável de ambiente: `RESEND_API_KEY` (obrigatória; ausência impede a inicialização, como os
demais segredos).
