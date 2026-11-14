# Data Model: Recuperação de senha

**Feature**: `003-password-recovery` | **Data**: 2026-11-08 | **Plano**: [plan.md](./plan.md)

Os nomes de coluna seguem o camelCase usado em `AppDbContext`. As decisões que justificam este
modelo estão em [research.md](./research.md), seções R1, R3, R7, R8 e R9.

## Cliente (`clients`): tabela alterada

| Coluna | Tipo | Regra |
|---|---|---|
| `passwordChangedAt` | `timestamptz` | Aceita nulo. Fica nulo enquanto a senha nunca foi redefinida e recebe a data de cada redefinição. |

As demais colunas não mudam (features 001 e 002). A entidade `Client` ganha a propriedade
`PasswordChangedAt` e a navegação `ICollection<PasswordRecoveryLink> PasswordRecoveryLinks`.

A redefinição grava `passwordHash`, `passwordChangedAt` e `updatedAt`. O `updatedAt` é preenchido
pelo `TimestampInterceptor` que já existe. O `accessCode` **não muda**, por isso os tokens emitidos
antes continuam válidos (R10).

### Quem pode pedir a recuperação

| Situação da conta | Condição | Recebe o link? |
|---|---|---|
| Ativa (e-mail confirmado) | `deletedAt IS NULL AND emailConfirmed = true` | Sim |
| Pendente de confirmação | `deletedAt IS NULL AND emailConfirmed = false` | Não. Recebe a resposta genérica e nenhum e-mail. |
| Removida | `deletedAt IS NOT NULL` | Não |
| Inexistente | nenhuma linha | Não |

## Link de recuperação de senha (`password_recovery_links`): tabela nova

| Coluna | Tipo | Regra |
|---|---|---|
| `id` | `bigint` identity | Chave primária |
| `clientId` | `integer` | Chave estrangeira para `clients.id`, com `ON DELETE CASCADE`. Não nulo. |
| `selector` | `varchar(22)` | Base64Url com 16 bytes aleatórios. Único. |
| `validatorHash` | `char(64)` | `SHA-256(validator:accessCode:password-recovery)` em hexadecimal minúsculo. O validator **nunca** é gravado. |
| `purpose` | `varchar(32)` | Sempre `password-recovery`. Fica explícito para auditoria e porque faz parte do hash. |
| `issuedAt` | `timestamptz` | Instante da emissão (`TimeProvider`). |
| `expiresAt` | `timestamptz` | `issuedAt + 24 h`. Valor fixo, definido em `PasswordRecoveryLink.ValidityPeriod`. |
| `usedAt` | `timestamptz` | Aceita nulo. Recebe a data da redefinição feita com este link. |
| `revokedAt` | `timestamptz` | Aceita nulo. Recebe a data em que um pedido mais recente invalidou o link. |

A coluna `origin`, que existe na tabela de confirmação, não entra aqui: todos os links de
recuperação vêm de um pedido do cliente.

### Índices

| Nome | Colunas | Tipo | Finalidade |
|---|---|---|---|
| `ux_password_recovery_links_selector` | `selector` | Único | Localizar o link (R1) |
| `ux_password_recovery_links_active_per_client` | `clientId` | Único e parcial, com `"usedAt" IS NULL AND "revokedAt" IS NULL` | Garantir no máximo um link válido por cliente e servir de barreira contra emissões simultâneas (R8) |
| `ix_password_recovery_links_client_issued` | `clientId`, `issuedAt` | Comum | Aplicar os limites de 5 minutos e de 10 links em 24 h (FR-005) |

Os nomes dos índices únicos ficam em `UniqueIndexNames`.

Há um filtro global de consulta, `link.Client.DeletedAt == null`, o mesmo usado na tabela de
confirmação. Com ele, o link de uma conta removida é tratado como inexistente, ou seja, inválido.

### Estados do link (derivados)

```text
                emissão (pedido atendido)
     (nada) ──────────────────────────────► ATIVO
                                             │  │  │
         redefinição com sucesso (PUT)       │  │  │  novo pedido atendido
       ┌─────────────────────────────────────┘  │  └──────────────────────┐
       ▼                                        │                         ▼
     USADO                     agora > expiresAt│                     REVOGADO
  (usedAt != null)                              ▼                 (revokedAt != null)
                                            EXPIRADO
                                    (sem coluna: só pelo relógio)
```

| Estado | Condição | GET de verificação / PUT |
|---|---|---|
| Ativo | `usedAt IS NULL AND revokedAt IS NULL AND agora <= expiresAt` | 200 |
| Expirado | `usedAt IS NULL AND revokedAt IS NULL AND agora > expiresAt` | 410 |
| Usado | `usedAt IS NOT NULL` | 400 (inválido) |
| Revogado | `revokedAt IS NOT NULL` | 400 (inválido) |

Os estados só são avaliados depois que o validator confere. Um selector inexistente, um validator
errado ou um formato inválido dão sempre 400 (inválido).

Um link expirado continua contando como "ativo" para o índice parcial, porque `usedAt` e
`revokedAt` estão nulos. O próximo pedido atendido o revoga antes de inserir o novo, da mesma forma
que a feature 002 faz.

### Validações

| Regra | Onde fica |
|---|---|
| E-mail do pedido: obrigatório, formato válido, até 254 caracteres | `PasswordRecoveryRequest` (validação nativa, 400 com o campo) |
| Nova senha: de 8 a 128 caracteres, com ao menos uma letra e um número | `ResetPasswordRequest` (validação nativa, as mesmas regras do cadastro) |
| Confirmação igual à nova senha | `ResetPasswordRequest` (`[Compare]`, 400 com o campo) |
| Selector com 22 caracteres e validator com 43, ambos em Base64Url | Regra de domínio em `SecureLinkFormat` (400 inválido, sem indicar o campo) |
| Intervalo mínimo de 5 minutos e no máximo 10 links em 24 h por conta | `PasswordRecoveryLinkIssuePolicy` |

## Configuração (seção `email`): chaves novas

| Chave | Padrão | Validação |
|---|---|---|
| `passwordRecoveryPageBaseUrl` | `https://ragauth.com.br` | Obrigatória, URL absoluta |
| `passwordRecoveryIntervalMinutes` | 5 | Entre 0 e 1440 |
| `maximumPasswordRecoveryLinksPerDay` | 10 | Entre 1 e 100 |

## Migration

A migration `<timestamp>_PasswordRecovery` faz três coisas:

1. Adiciona `clients."passwordChangedAt"`, que aceita nulo e não tem valor padrão.
2. Cria `password_recovery_links` com os índices acima e a chave estrangeira com cascata.
3. Não altera nenhum dado existente.
