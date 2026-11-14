# Implementation Plan: Recuperação de senha

**Branch**: `003-password-recovery` | **Date**: 2026-11-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-password-recovery/spec.md` + decisões técnicas de
`.mine/password recovery/plan.md`

## Summary

O cliente com e-mail confirmado pede a recuperação de senha em
`POST /api/v1/client/new-pass-recuvery-request`. A resposta é sempre 202 genérico. O pedido passa
pelos limites de 5 minutos entre pedidos e de 10 pedidos a cada 24 horas por conta.

Para atender o pedido, o sistema:
1. Revoga o link ativo do cliente, se houver.
2. Emite um link novo no padrão selector/validator. O validator é gravado só como hash
   `SHA-256(validator:accessCode:password-recovery)`. O link vale 24 horas e fica na tabela nova
   `password_recovery_links`.
3. Põe na fila o e-mail HTML com o link
   `https://ragauth.com.br/{selector}/pass-recuvery/{validator}`, enviado pelo Resend.

A página do front-end usa duas rotas:
- `GET /api/v1/client/{selector}/pass-recuvery/{validator}` verifica o link sem consumi-lo.
- `PUT` na mesma rota troca a senha. A troca acontece numa transação com a linha do cliente
  bloqueada e consome o link.

Depois da troca, o cliente recebe o aviso "senha alterada". Não há login automático. Os tokens já
emitidos continuam valendo até expirar.

A fila, o compositor e o remetente da feature 002 passam a ser genéricos por tipo de e-mail. O
gerador de links passa a receber a ação. Nenhuma dependência nova entra no projeto, e todas as
respostas usam o `ApiResponse` existente.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.400), ASP.NET Core Minimal APIs. Sem mudança.

**Primary Dependencies**: as que já existem (features 001 e 002), inclusive o SDK `Resend`. Nada
novo. Recursos nativos usados: `System.Security.Cryptography`, `System.Threading.Channels`,
`BackgroundService`, `TimeProvider`, `HtmlEncoder`, `System.ComponentModel.DataAnnotations`
(`[Compare]`) e `Microsoft.Extensions.Logging`.

**Storage**: PostgreSQL com EF Core code-first. Entra a coluna `clients.passwordChangedAt` e a
tabela `password_recovery_links`.

**Testing**: xUnit + Moq nos testes unitários. Nos testes de integração, `WebApplicationFactory` +
Testcontainers PostgreSQL. Cobertura com coverlet, mínimo de 90%. O fake de e-mail e o
`AdjustableTimeProvider` já existentes passam a ser genéricos.

**Target Platform**: servidor Linux em contêiner. Sem mudança.

**Project Type**: web-service (API REST).

**Performance Goals**: e-mail entregue ao Resend em menos de 1 minuto em 95% dos pedidos (SC-001).
As 3 rotas novas (POST, GET e PUT) seguem a meta da feature 001, com p95 abaixo de 500 ms. O envio
acontece em segundo plano.

**Constraints**: o validator nunca é persistido nem aparece em log. A resposta do pedido é
indistinguível para qualquer situação da conta, inclusive no tempo de resposta (SC-003): o pedido
dura no mínimo `passwordRecoveryMinimumResponseMilliseconds` (decisão 7). Métodos com no máximo 20 linhas. Mensagens em
português. Validação de entrada sempre retorna 400 com o campo.

**Scale/Scope**: mesma escala das features anteriores (cerca de 10.000 clientes). São 3 rotas
novas, 1 tabela nova, 1 coluna nova e 2 modelos de e-mail novos, além da generalização da
infraestrutura de e-mail.

Todas as incógnitas foram resolvidas em [research.md](./research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Pré-design | Pós-design | Como é atendido |
|---|---|---|---|
| I. Camadas | ✅ | ✅ | `PasswordRecoveryEndpoints` só delega a `IPasswordRecoveryService`. Dados passam por `IPasswordRecoveryLinkRepository`, `IClientRepository` e `IUnitOfWork`. O Resend fica só em `Infrastructure/Email`. |
| II. Responsabilidade única / no máximo 20 linhas | ✅ | ✅ | Responsabilidades separadas em: emissor (`PasswordRecoveryLinkIssuer`), política de limites (`PasswordRecoveryLinkIssuePolicy`), serviço de pedido, verificação e redefinição, gerador genérico (`ISecureLinkGenerator`), compositor por catálogo de modelos e dispatcher único. A redefinição é dividida em localizar, classificar, bloquear, gravar e enfileirar. |
| III. Nomenclatura | ✅ | ✅ | O gerador e a fila ganham nomes genéricos, porque deixam de ser só de confirmação (R2, R11). Os parâmetros de rota se chamam `selector` e `validator`, que descrevem o valor que recebem. Os nomes `clientIdentificationHash`/`keyHash` da feature 002 não são repetidos, porque não são hashes. |
| IV. DTOs | ✅ | ✅ | `PasswordRecoveryRequest`, `ResetPasswordRequest` e `PasswordRecoveryLinkStatusResponse`. A entidade `PasswordRecoveryLink` nunca sai da camada `Services`. |
| V. DI e abstrações | ✅ | ✅ | Todas as novas dependências entram por interface injetada: serviço, repositório, política, emissor, gerador, fila, compositor, remetente, `TimeProvider` e `IPasswordHashingService`. |
| VI. Testes | ✅ | ✅ | Testes unitários cobrem o serviço (pedido, verificação, redefinição e concorrência), a política, o emissor, o gerador com a ação, o compositor (2 modelos novos, codificação) e o dispatcher generalizado. Testes de integração cobrem as 3 rotas novas, os cenários de login após a troca e o esquema do banco. |
| VII. Erros em português + `ProblemDetails` | ✅ | ✅ | As novas `DomainException` (400 e 410) passam pelo `DomainExceptionHandler`. A validação nativa dos corpos usa `ValidationProblem` com o campo. O formato do selector e do validator é regra de domínio, como na feature 002 (R5). |
| Restrição: dependências justificadas | ✅ | ✅ | Nenhum pacote novo e nenhum serviço externo novo. O Resend já foi justificado na feature 002 (R15). |

**Resultado do gate**: passa, sem violações a justificar.

## Decisões sobre divergências entre entradas

Estas decisões foram resolvidas em 2026-11-08.

1. **Rota de verificação** (não estava na entrada): a clarificação 4 da spec exige a rota. Ela fica
   como `GET` no mesmo caminho do `PUT` (R4).
2. **Grafia `pass-recuvery`**: mantida exatamente como na entrada, com o valor centralizado num só
   lugar. ⚠️ Confirmar com o dono do produto se é erro de digitação (R1) **antes de começar a fase
   3**, porque a grafia vira URL pública da qual o front-end depende.
3. **HTML vazio na entrada**: foram criados 2 modelos (recuperação e senha alterada) no estilo do
   modelo de confirmação. ⚠️ Os textos precisam ser validados (R12).
4. **"Use try/catch"**: segue o padrão de `DomainException` com handler global, que já garante que
   nenhuma exceção chegue sem tratamento. Há `try/catch` só na emissão concorrente e no dispatcher
   (R13).
5. **Tabela própria em vez de reaproveitar `email_confirmation_links`**: o índice "um ativo por
   cliente" daquela tabela não distingue a ação (R3).
6. **Generalização da infraestrutura de e-mail da feature 002**: refatoração sem mudança de
   comportamento, protegida pelos testes que já existem (R11).
7. **Tempo de resposta do pedido (SC-003)**: para conta confirmada, o pedido abre transação,
   bloqueia, emite e faz commit. Para e-mail desconhecido, faz só uma consulta. Sem tratamento, a
   diferença de tempo revelaria quais e-mails estão cadastrados. Solução: o
   `IMinimumResponseDurationGuard` executa o pedido e completa a espera até
   `passwordRecoveryMinimumResponseMilliseconds` (padrão 300 ms). A medição usa `Stopwatch`, que
   mede duração e não data, então é exceção à regra do `TimeProvider`. A outra opção, processar o
   pedido em segundo plano, foi descartada porque exigiria refazer o fluxo.
8. **Endereço da página de redefinição**: a chave é separada da confirmação, mas pode ter o mesmo
   valor, porque o caminho `pass-recuvery` distingue as páginas.

## Project Structure

### Documentation (this feature)

```text
specs/003-password-recovery/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── password-recovery-endpoints.md
│   └── password-recovery-emails-outbound.md
├── checklists/requirements.md
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

Arquivos novos marcados com `+`, alterados com `~` e renomeados ou movidos com `→`, todos em
`src/RagAuth.Api`:

```text
src/RagAuth.Api/
├── Endpoints/
│   └── +PasswordRecoveryEndpoints.cs           # POST pedido, GET verificação, PUT redefinição (AllowAnonymous)
├── Contracts/
│   └── +PasswordRecovery/                      # PasswordRecoveryRequest, ResetPasswordRequest,
│                                               # PasswordRecoveryLinkStatusResponse
├── Domain/
│   ├── Entities/ ~Client.cs (PasswordChangedAt, PasswordRecoveryLinks), +PasswordRecoveryLink.cs
│   └── Exceptions/ +InvalidPasswordRecoveryLinkException.cs (400), +ExpiredPasswordRecoveryLinkException.cs (410)
├── Services/
│   ├── +SecureLinks/
│   │   ├── ISecureLinkGenerator.cs / RandomSecureLinkGenerator.cs   # → de EmailConfirmation; recebe a ação
│   │   └── SecureLinkFormat.cs                                      # → de ConfirmationLinkFormat
│   ├── EmailConfirmation/ ~ConfirmationLinkIssuer.cs, ~EmailConfirmationService.cs
│   │                      # passam a usar ISecureLinkGenerator + OutgoingEmailMessage
│   ├── Auth/ ~AuthService.cs                   # só troca IConfirmationEmailQueue → IEmailQueue
│   ├── +PasswordRecovery/
│   │   ├── IPasswordRecoveryService.cs / PasswordRecoveryService.cs  # pedir, verificar, redefinir
│   │   ├── IPasswordRecoveryLinkIssuer.cs / PasswordRecoveryLinkIssuer.cs
│   │   ├── IPasswordRecoveryLinkIssuePolicy.cs / PasswordRecoveryLinkIssuePolicy.cs
│   │   ├── IMinimumResponseDurationGuard.cs / StopwatchMinimumResponseDurationGuard.cs  # SC-003
│   │   └── IssuedPasswordRecoveryLink.cs
│   ├── ~EmailConfirmationServiceExtensions.cs  # registra ISecureLinkGenerator
│   └── +PasswordRecoveryServiceExtensions.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── ~AppDbContext.cs                    # DbSet<PasswordRecoveryLink>
│   │   ├── Configurations/ ~ClientConfiguration.cs, +PasswordRecoveryLinkConfiguration.cs
│   │   ├── Repositories/ ~IClientRepository.cs + ~EntityFramework… (FindConfirmedByEmailAsync),
│   │   │                 +IPasswordRecoveryLinkRepository.cs, +EntityFrameworkPasswordRecoveryLinkRepository.cs
│   │   ├── ~UniqueIndexNames.cs
│   │   └── Migrations/ +<timestamp>_PasswordRecovery.cs
│   └── Email/                                  # generalização (R11)
│       ├── +EmailKind.cs, +OutgoingEmailMessage.cs (→ ConfirmationEmailMessage), +ComposedEmail.cs (→ ConfirmationEmail)
│       ├── IEmailQueue.cs / ChannelEmailQueue.cs             # → IConfirmationEmailQueue / Channel…
│       ├── IEmailComposer.cs / HtmlEmailComposer.cs / +EmailTemplateCatalog.cs
│       ├── IEmailSender.cs / ResendEmailSender.cs            # → IConfirmationEmailSender / Resend…
│       ├── EmailDispatcher.cs                                # → ConfirmationEmailDispatcher
│       ├── Templates/ ~ConfirmationEmail.html ({{ACTION_URL}}), +PasswordRecoveryEmail.html, +PasswordChangedEmail.html
│       └── ~EmailServiceExtensions.cs
├── Configuration/
│   ├── ~EmailOptions.cs                        # passwordRecoveryPageBaseUrl, passwordRecoveryIntervalMinutes,
│   │                                           # maximumPasswordRecoveryLinksPerDay
│   └── ~ApplicationServiceExtensions.cs        # AddPasswordRecovery(), MapPasswordRecoveryEndpoints()
├── Observability/ ~LogMessages.cs              # 1600–1605; 1500–1502/1509 com EmailKind
├── ~appsettings*.json                          # chaves novas da seção "email"
└── ~RagAuth.Api.csproj                         # EmbeddedResource dos 2 templates novos

tests/
├── RagAuth.UnitTests/
│   ├── +Services/PasswordRecovery/             # serviço (pedido/verificação/redefinição), política, emissor
│   ├── ~Services/EmailConfirmation/            # gerador com a ação, tipos de mensagem novos
│   └── ~Infrastructure/Email/                  # compositor (3 modelos), dispatcher e remetente generalizados
└── RagAuth.IntegrationTests/
    ├── +PasswordRecoveryEndpointsTests.cs      # 3 rotas, login após troca, expiração, concorrência
    ├── ~PersistenceSchemaTests.cs              # índices novos
    ├── CrossCutting/ +PasswordRecoveryLinkUniquenessTests.cs (SC-005), ~SensitiveDataAuditTests.cs (SC-007)
    └── Infrastructure/ ~FakeConfirmationEmailSender.cs → FakeEmailSender.cs, ~RagAuthApiFactory.cs
```

**Structure Decision**: o projeto continua único (`src/RagAuth.Api`), com camadas por pasta, como
nas features 001 e 002. As rotas `/api/v1/client/...` com 4 ou mais segmentos não conflitam com
`DELETE /api/v1/{clientId}` nem com as rotas de confirmação.

### Configuração

Chaves novas na seção `email`:

| Chave | Padrão |
|---|---|
| `passwordRecoveryPageBaseUrl` | `https://ragauth.com.br` |
| `passwordRecoveryIntervalMinutes` | 5 |
| `maximumPasswordRecoveryLinksPerDay` | 10 |
| `passwordRecoveryMinimumResponseMilliseconds` | 300 |

Em `appsettings.json` (ambiente local), `passwordRecoveryPageBaseUrl` usa o mesmo valor local de
`confirmationPageBaseUrl`. O padrão `https://ragauth.com.br` fica só no código (`EmailOptions`).

Nenhuma variável de ambiente nova. A `RESEND_API_KEY` continua a mesma.

### Fluxos

**Pedido** (`POST`):
1. Valida o corpo (400 com o campo).
2. Normaliza o e-mail.
3. Busca com `FindConfirmedByEmailAsync`. Se não encontrar, grava o log 1602 e responde 202.
4. Abre transação e bloqueia a linha do cliente.
5. Avalia `PasswordRecoveryLinkIssuePolicy`. Se recusar, grava o log 1601 e responde 202.
6. Revoga o link ativo e emite o novo com `PasswordRecoveryLinkIssuer`.
7. Faz commit. Uma violação do índice de link ativo aqui equivale a limite atingido.
8. Grava o log 1600, põe na fila o e-mail `PasswordRecovery` e responde 202.

Os passos 2 a 8 rodam dentro do `IMinimumResponseDurationGuard`, então a resposta nunca sai antes
de `passwordRecoveryMinimumResponseMilliseconds`, qualquer que seja o caminho (decisão 7).

**Verificação** (`GET`):
1. Confere o formato com `SecureLinkFormat`.
2. Busca o link pelo selector, já com o cliente.
3. Compara o hash do validator em tempo constante, usando a ação `password-recovery`.
4. Classifica o link:
   - usado, revogado ou de conta não confirmada: 400 e log 1605;
   - expirado: 410 e log 1604;
   - válido: 200.

**Redefinição** (`PUT`):
1. Valida o corpo (400 com o campo, sem tocar no link).
2. Faz a mesma verificação do `GET`.
3. Abre transação e bloqueia a linha do cliente.
4. Relê o link e confere que `usedAt` continua nulo. Se não estiver, responde 400.
5. Grava `PasswordHash` (com `IPasswordHashingService`), `PasswordChangedAt` e o `usedAt` do link.
6. Faz commit e grava o log 1603.
7. Põe na fila o e-mail `PasswordChanged` e responde 200, sem token.

**Envio**: o `EmailDispatcher` lê a fila e passa cada mensagem pelo `HtmlEmailComposer`, que escolhe
o modelo pelo `EmailKind`, e depois pelo `ResendEmailSender`. São até 3 tentativas, com os logs
1500, 1501 e 1502 indicando o tipo do e-mail.

## Complexity Tracking

Nenhuma violação da constituição a justificar.
