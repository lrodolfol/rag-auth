# Implementation Plan: Confirmação de e-mail no cadastro

**Branch**: `002-email-confirmation` | **Date**: 2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-email-confirmation/spec.md` + decisões técnicas de
`.mine/email-confirmation/plan.md`

## Summary

O cadastro continua igual, mas cria a conta com `emailConfirmed = false` e, na mesma transação, um
link de confirmação no padrão selector/validator (selector aleatório em claro + hash SHA-256 do
validator vinculado ao código de acesso e à ação), válido por 24 h. Após o commit, a mensagem é posta
numa fila em memória (`Channel`) consumida por um `BackgroundService`, que envia o e-mail HTML pelo
Resend (SDK oficial, por DI) com até 3 tentativas. O link aponta para o front-end
(`https://ragauth.com.br/{selector}/confirmation-register/{validator}`), que chama
`GET /api/v1/client/{selector}/confirmation-register/{validator}` para ativar a conta. O login
recusa conta pendente com 403 depois de validar as credenciais. `POST
/api/v1/client/new-confirmation-register` emite novo link (revogando o anterior) respeitando 5 min de
intervalo e 10 por 24 h, sempre com resposta genérica 202. Todas as respostas usam o `ApiResponse`
existente.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.400), ASP.NET Core Minimal APIs (inalterado)

**Primary Dependencies**: existentes da feature 001 + **`Resend`** (SDK oficial, novo — research R7);
nativos: `System.Threading.Channels`, `BackgroundService`, `TimeProvider`,
`System.Security.Cryptography` (`RandomNumberGenerator`, `SHA256`, `CryptographicOperations`),
`System.Text.Encodings.Web.HtmlEncoder`, `Microsoft.Extensions.Logging`

**Storage**: PostgreSQL (EF Core code-first) — coluna nova em `clients` e tabela
`email_confirmation_links`

**Testing**: xUnit + Moq (unitários), `WebApplicationFactory` + Testcontainers PostgreSQL
(integração), coverlet ≥ 90%; `FakeConfirmationEmailSender` e `AdjustableTimeProvider` manuais nos
testes

**Target Platform**: servidor Linux/contêiner (inalterado)

**Project Type**: web-service (API REST)

**Performance Goals**: cadastro mantém p95 < 500 ms (SC-002) — o envio é em segundo plano; e-mail
entregue ao Resend em < 1 min em 95% dos casos (SC-001)

**Constraints**: validator nunca persistido nem logado; respostas do novo link indistinguíveis;
`RESEND_API_KEY` só por variável de ambiente; métodos ≤ 20 linhas; mensagens em português

**Scale/Scope**: mesma escala da feature 001 (~10.000 clientes); 2 rotas novas + 1 alterada; 1 tabela
nova; 1 serviço em segundo plano

Todas as incógnitas foram resolvidas em [research.md](./research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Pré-design | Pós-design | Como é atendido |
|---|---|---|---|
| I. Camadas | ✅ | ✅ | `EmailConfirmationEndpoints` só delega a `IEmailConfirmationService`; acesso a dados por `IEmailConfirmationLinkRepository`/`IClientRepository` + `IUnitOfWork`; Resend só em `Infrastructure/Email` |
| II. Responsabilidade única / ≤ 20 linhas | ✅ | ✅ | Separação: gerador de link (`IConfirmationLinkGenerator`), verificador de hash, montagem do e-mail (`IConfirmationEmailComposer`), fila, dispatcher, remetente Resend, serviço de confirmação, política de limites (`ConfirmationLinkIssuePolicy`) |
| III. Nomenclatura | ✅ | ✅ | Nomes descritivos; parâmetros de rota `clientIdentificationHash`/`keyHash` como na entrada |
| IV. DTOs | ✅ | ✅ | `NewConfirmationLinkRequest`, `EmailConfirmationResultResponse`; `EmailConfirmationLink` nunca sai de `Services` |
| V. DI e abstrações | ✅ | ✅ | `IResend` por DI (o `ResendClient.Create` do exemplo **não** é usado); fila, remetente, compositor, gerador e `TimeProvider` injetados por interface |
| VI. Testes | ✅ | ✅ | Unitários para serviço, política de limites, gerador/hash, compositor, dispatcher (tentativas), remetente (com `IResend` mockado), `AuthService` (403); integração para as 2 rotas novas + login e cadastro alterados |
| VII. Erros PT + `ProblemDetails` | ✅ | ✅ | Novas `DomainException` (403, 400, 410) pelo `DomainExceptionHandler` existente; 400 de validação via validação nativa (`ValidationProblem` com campo) no corpo do novo link. **Decisão registrada**: o formato dos segmentos `clientIdentificationHash`/`keyHash` da rota de confirmação é **regra de domínio do link**, não validação de entrada — valor mal formado é tratado como link inexistente e gera `InvalidConfirmationLinkException` (400 `ProblemDetails`, sem detalhe de campo), para não diferenciar link mal formado de link inexistente (FR-010). Ver research R3 |
| Restrição: dependências justificadas | ⚠️ `Resend` | ✅ | Tabela abaixo (alternativas nativas avaliadas em R7) |

**Dependências novas** (motivo e alternativa nativa descartada):

| Pacote | Projeto | Motivo | Alternativa nativa descartada |
|---|---|---|---|
| `Resend` (SDK oficial, versão estável fixada no `.csproj`) | Api | Provedor de e-mail transacional escolhido em `.mine/email-confirmation/plan.md`; modelos tipados e erros do provedor mantidos pelo fornecedor; integra com `IHttpClientFactory` | `HttpClient` direto na API REST (reimplementa contrato e erros); `System.Net.Mail.SmtpClient` (não recomendado pela Microsoft para código novo) |

**Serviços externos novos**:

| Serviço | Uso | Motivo | Alternativa descartada |
|---|---|---|---|
| Resend (conta + domínio remetente verificado) | Envio dos e-mails | Escolha do dono do produto | SMTP próprio — operação e reputação de entrega por conta do time |

Nenhuma dependência nova nos projetos de teste (`AdjustableTimeProvider` e fake de e-mail manuais).

**Resultado do gate**: PASSA, sem violações a justificar em Complexity Tracking.

## Decisões sobre divergências entre entradas (resolvidas em 2026-10-31)

1. **Coluna `emailConfirmed` (bool)**: mantida, mais `emailConfirmedAt` exigida pela spec (FR-008)
   — research R9.
2. **"Hash que identifica o usuário + hash de chave de acesso"**: selector aleatório por link +
   validator cujo hash inclui código de acesso e ação; nada derivável dos dados do cliente (FR-005)
   — research R1.
3. **`GET` com efeito colateral**: mantido conforme a entrada; o link do e-mail aponta para o
   front-end (FR-007a) e a operação é idempotente — research R2.
4. **Código de exemplo do Resend** (`ResendClient.Create`): substituído por registro em DI
   (Constituição V) — research R7.
5. **Template HTML**: usado como está, com rodapé parametrizado e parágrafo de "como pedir novo link"
   (exigido por FR-002) — research R8. ⚠️ Confirmar o texto com o dono do produto.
6. **Tratamento de erros com try/catch**: segue o padrão de `DomainException` + handler global;
   `try/catch` só no dispatcher e na concorrência de emissão — research R11.
7. **Spec 001**: FR-012 (rotas abertas) e a premissa "sem verificação de e-mail" ficam superados
   por esta feature (FR-019); a spec 001 não é editada.

## Project Structure

### Documentation (this feature)

```text
specs/002-email-confirmation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── email-confirmation-endpoints.md
│   ├── auth-endpoints-changes.md
│   └── confirmation-email-outbound.md
├── checklists/requirements.md
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

Novos (`+`) e alterados (`~`) em `src/RagAuth.Api`:

```text
src/RagAuth.Api/
├── Endpoints/
│   ├── ~AuthEndpoints.cs                       # sem mudança de rota; login pode lançar 403
│   └── +EmailConfirmationEndpoints.cs          # GET confirmação, POST novo link (AllowAnonymous)
├── Contracts/
│   └── +EmailConfirmation/                     # NewConfirmationLinkRequest, EmailConfirmationResultResponse
├── Domain/
│   ├── Entities/ ~Client.cs, +EmailConfirmationLink.cs, +ConfirmationLinkOrigin.cs
│   └── Exceptions/ +EmailNotConfirmedException.cs, +InvalidConfirmationLinkException.cs,
│                   +ExpiredConfirmationLinkException.cs
├── Services/
│   ├── Auth/ ~AuthService.cs                   # cria link no cadastro, enfileira e-mail, 403 no login
│   ├── +EmailConfirmation/
│   │   ├── IEmailConfirmationService.cs / EmailConfirmationService.cs   # confirmar, pedir novo link
│   │   ├── IConfirmationLinkGenerator.cs / RandomConfirmationLinkGenerator.cs  # selector, validator, hash
│   │   ├── ConfirmationLinkIssuePolicy.cs      # 5 min / 10 por 24 h
│   │   ├── IConfirmationLinkIssuer.cs / ConfirmationLinkIssuer.cs  # cria entidade + mensagem (cadastro e reenvio)
│   │   └── ConfirmationEmailMessage.cs
│   └── +EmailConfirmationServiceExtensions.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── ~AppDbContext.cs                    # DbSet<EmailConfirmationLink>
│   │   ├── Configurations/ ~ClientConfiguration.cs, +EmailConfirmationLinkConfiguration.cs
│   │   ├── Repositories/ +IEmailConfirmationLinkRepository.cs, +EntityFrameworkEmailConfirmationLinkRepository.cs
│   │   ├── ~UniqueIndexNames.cs
│   │   └── Migrations/ +<timestamp>_EmailConfirmation.cs   # colunas, tabela, UPDATE das contas existentes
│   └── +Email/
│       ├── IConfirmationEmailQueue.cs / ChannelConfirmationEmailQueue.cs
│       ├── ConfirmationEmailDispatcher.cs      # BackgroundService, 3 tentativas
│       ├── IConfirmationEmailComposer.cs / HtmlConfirmationEmailComposer.cs
│       ├── IConfirmationEmailSender.cs / ResendConfirmationEmailSender.cs
│       ├── Templates/ConfirmationEmail.html    # EmbeddedResource
│       └── EmailServiceExtensions.cs           # AddHttpClient<ResendClient>, IResend, fila, hosted service
├── Configuration/
│   ├── +EmailOptions.cs                        # seção "email" + RESEND_API_KEY, ValidateOnStart
│   ├── ~EnvironmentSecretNames.cs              # ResendApiKey = "RESEND_API_KEY"
│   ├── ~OptionsServiceExtensions.cs
│   └── ~ApplicationServiceExtensions.cs        # AddEmailConfirmation(), MapEmailConfirmationEndpoints()
├── Observability/ ~LogMessages.cs              # eventos 1500–1509
├── ~appsettings*.json                          # seção "email"
└── ~RagAuth.Api.csproj                         # PackageReference Resend; EmbeddedResource do template

tests/
├── RagAuth.UnitTests/
│   ├── Services/EmailConfirmation/             # serviço, política, gerador, issuer
│   ├── Services/Auth/~AuthServiceTests.cs      # 403, link criado, enfileiramento
│   ├── Infrastructure/Email/                   # dispatcher (tentativas), composer (encode), sender (IResend mock)
│   └── TestDoubles/+AdjustableTimeProvider.cs
└── RagAuth.IntegrationTests/
    ├── +EmailConfirmationEndpointsTests.cs
    ├── ~AuthEndpointsTests.cs                  # login 403 / 401 em conta pendente
    ├── ~PersistenceSchemaTests.cs              # índices novos
    ├── CrossCutting/ +ConfirmationLinkUniquenessTests.cs (SC-005), ~SensitiveDataAuditTests.cs (SC-008)
    └── Infrastructure/ +FakeConfirmationEmailSender.cs, ~RagAuthApiFactory.cs, ~TestClientAccounts.cs (confirma antes do login)
```

**Structure Decision**: mantém o projeto único `src/RagAuth.Api` com camadas por pasta (feature 001).
A rota `/api/v1/client/...` tem 4+ segmentos e não conflita com `DELETE /api/v1/{clientId}`.

### Configuração

| Seção `email` | Padrão |
|---|---|
| `fromAddress` | obrigatório |
| `appName` | `RagAuth` |
| `confirmationPageBaseUrl` | `https://ragauth.com.br` |
| `newLinkIntervalMinutes` | 5 |
| `maximumNewLinksPerDay` | 10 |
| `retryDelaysSeconds` | `[30, 120]` |

| Variável de ambiente | Uso |
|---|---|
| `RESEND_API_KEY` | token do Resend (obrigatório; ausência impede a inicialização) |

### Fluxos

**Cadastro**: validação → e-mail livre → `Client` (`emailConfirmed = false`) + `EmailConfirmationLink`
(`registration`) → `SaveChanges` (com a retentativa de código de acesso existente; o hash do validator
é recalculado se o código de acesso mudar) → `TryWrite` na fila → 201.

**Envio**: `ConfirmationEmailDispatcher` lê a fila → `IConfirmationEmailComposer` → `IConfirmationEmailSender`
→ sucesso (log 1500) ou falha (log 1501, reagenda com `TimeProvider`) → após a 3ª falha, log 1502.

**Confirmação**: busca link por selector (filtro de conta removida) → compara hash em tempo constante
→ classifica (ativo / usado / expirado / revogado) → se ativo: `usedAt`, `emailConfirmed`,
`emailConfirmedAt` numa transação (atualização condicional `usedAt IS NULL` para concorrência) → 200.

**Novo link**: normaliza e-mail → busca conta pendente → `ConfirmationLinkIssuePolicy` → revoga ativo
+ cria novo (`new-link-request`) → `SaveChanges` (violação do índice "um ativo por cliente" = limite) →
`TryWrite` na fila → 202 genérico em todos os caminhos.

**Login**: credenciais (401) → `EmailConfirmed` (403, log 1506) → fluxo da feature 001.

## Complexity Tracking

Nenhuma violação da constituição a justificar.
