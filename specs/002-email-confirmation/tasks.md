---

description: "Task list for 002-email-confirmation"
---

# Tasks: Confirmação de e-mail no cadastro

**Input**: Design documents from `/specs/002-email-confirmation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBRIGATÓRIOS — Constituição VI exige testes unitários xUnit para toda regra de negócio
(cobertura de linhas ≥ 90%) e ao menos um teste de integração por endpoint novo ou alterado. Em cada
história, os testes são escritos primeiro e devem falhar antes da implementação.

**Organization**: tarefas agrupadas por história de usuário para implementação e teste independentes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história de usuário da tarefa (US1…US3)

## Regras válidas para TODAS as tarefas

- Métodos com no máximo 20 linhas; uma responsabilidade por classe (Constituição II).
- Nomes autoexplicativos, sem abreviações (Constituição III).
- Endpoints só delegam a serviços; nunca acessam `AppDbContext` nem repositórios (Constituição I).
- Entrada e saída só por DTOs; dependências por interface e injeção, sem `new` para serviços —
  inclusive o Resend: **não** usar `ResendClient.Create` (Constituição IV e V).
- Mensagens de erro, de resposta e de log em português (Constituição VII).
- Respostas sempre no envelope `ApiResponse` (`src/RagAuth.Api/Contracts/ApiResponse.cs`).
- Nunca registrar em log nem devolver em erro: selector, validator, URL do link, corpo do e-mail,
  `RESEND_API_KEY` (FR-021, SC-008). E-mails só mascarados com `SensitiveDataMasker.MaskEmail`.
- Datas sempre via `TimeProvider` injetado (nunca `DateTimeOffset.UtcNow` direto).

## Path Conventions

- API: `src/RagAuth.Api/`
- Testes unitários: `tests/RagAuth.UnitTests/` (espelha as pastas da API)
- Testes de integração: `tests/RagAuth.IntegrationTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependência nova, recurso embutido e configuração

- [X] T001 Adicionar `PackageReference` do pacote `Resend` (última versão estável, fixada) em `src/RagAuth.Api/RagAuth.Api.csproj`, com comentário XML curto citando a justificativa de [plan.md](./plan.md) (dependência nova) e a alternativa nativa descartada (research R7)
- [X] T002 Criar o template `src/RagAuth.Api/Infrastructure/Email/Templates/ConfirmationEmail.html` a partir do HTML de `.mine/email-confirmation/plan.md`, mantendo `{{CLIENT_NAME}}` e `{{CONFIRMATION_URL}}`, trocando o rodapé por `© {{YEAR}} {{APP_NAME}}. Todos os direitos reservados.` e acrescentando, após "Este link é válido por 24 horas.", o parágrafo `Se o link expirar, solicite um novo link de confirmação na página de login.`; registrar como `<EmbeddedResource>` em `src/RagAuth.Api/RagAuth.Api.csproj`
- [X] T003 [P] Adicionar a seção `email` em `src/RagAuth.Api/appsettings.json` (`fromAddress`, `appName: "RagAuth"`, `confirmationPageBaseUrl: "https://ragauth.com.br"`, `newLinkIntervalMinutes: 5`, `maximumNewLinksPerDay: 10`, `retryDelaysSeconds: [30, 120]`) e sobrescrever `fromAddress`/`confirmationPageBaseUrl` em `src/RagAuth.Api/appsettings.uat.json` e `src/RagAuth.Api/appsettings.prod.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: domínio, persistência, configuração, erros, logs e infraestrutura de teste usados por todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase terminar

### Configuração e segredos

- [X] T004 Adicionar `public const string ResendApiKey = "RESEND_API_KEY";` em `src/RagAuth.Api/Configuration/EnvironmentSecretNames.cs`
- [X] T005 Criar `src/RagAuth.Api/Configuration/EmailOptions.cs` (`SectionName = "email"`) com `FromAddress` (`[Required]`, `[EmailAddress]`), `AppName` (`[Required]`, padrão `RagAuth`), `ConfirmationPageBaseUrl` (`[Required]`, `[Url]`, padrão `https://ragauth.com.br`), `NewLinkIntervalMinutes` (`[Range(0, 1440)]`, padrão 5), `MaximumNewLinksPerDay` (`[Range(1, 100)]`, padrão 10), `RetryDelaysSeconds` (`int[]`, padrão `[30, 120]`) e `ResendApiKey` (`[Required]`); mensagens de validação em português no padrão de `JwtOptions.cs` (ex.: `A variável de ambiente RESEND_API_KEY é obrigatória.`)
- [X] T006 Registrar `EmailOptions` em `src/RagAuth.Api/Configuration/OptionsServiceExtensions.cs` com `Bind`, `PostConfigure` lendo `RESEND_API_KEY` via `ReadSecret`, `ValidateDataAnnotations()` e `ValidateOnStart()`

### Domínio

- [X] T007 [P] Alterar `src/RagAuth.Api/Domain/Entities/Client.cs`: adicionar `bool EmailConfirmed`, `DateTimeOffset? EmailConfirmedAt` e `ICollection<EmailConfirmationLink> ConfirmationLinks`
- [X] T008 [P] Criar `src/RagAuth.Api/Domain/Entities/ConfirmationLinkOrigin.cs` com constantes `Registration = "registration"` e `NewLinkRequest = "new-link-request"`, e `src/RagAuth.Api/Domain/Entities/EmailConfirmationLink.cs` com `long Id`, `int ClientId`, `Client Client`, `string Selector`, `string ValidatorHash`, `string Purpose` (constante `EmailConfirmationPurpose = "email-confirmation"`), `string Origin`, `DateTimeOffset IssuedAt`, `DateTimeOffset ExpiresAt`, `DateTimeOffset? UsedAt`, `DateTimeOffset? RevokedAt` e a constante `public static readonly TimeSpan ValidityPeriod = TimeSpan.FromHours(24);` — prazo fixo, sem configuração ([data-model.md](./data-model.md))
- [X] T009 [P] Criar as exceções em `src/RagAuth.Api/Domain/Exceptions/`: `EmailNotConfirmedException.cs` (403, título `Acesso negado`, mensagem `O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.`), `InvalidConfirmationLinkException.cs` (400, título `Requisição inválida`, mensagem `Link de confirmação inválido. Use o link do e-mail de confirmação mais recente.`), `ExpiredConfirmationLinkException.cs` (410, título `Link expirado`, mensagem `O link de confirmação expirou. Solicite um novo link de confirmação.`), todas derivando de `DomainException`; acrescentar o título de 410 em `src/RagAuth.Api/ErrorHandling/PortugueseProblemDetailsTitles.cs` se ainda não existir

### Persistência

- [X] T010 Alterar `src/RagAuth.Api/Infrastructure/Persistence/Configurations/ClientConfiguration.cs`: `EmailConfirmed` "não nulo, padrão `false`" (`HasDefaultValue(false)`) e `EmailConfirmedAt` nulo
- [X] T011 Criar `src/RagAuth.Api/Infrastructure/Persistence/Configurations/EmailConfirmationLinkConfiguration.cs`: tabela `email_confirmation_links`; `Id` `bigint` identity; FK `clientId` → `clients.id` `ON DELETE CASCADE`; `Selector` `varchar(22)` obrigatório; `ValidatorHash` `char(64)` obrigatório; `Purpose` `varchar(32)`; `Origin` `varchar(32)`; `IssuedAt`/`ExpiresAt` obrigatórios; índices `ux_email_confirmation_links_selector` (único em `selector`), `ux_email_confirmation_links_active_per_client` (único em `clientId` com filtro `"usedAt" IS NULL AND "revokedAt" IS NULL`) e `ix_email_confirmation_links_client_issued` (`clientId`, `issuedAt`); filtro global `link => link.Client.DeletedAt == null`. Adicionar os nomes `EmailConfirmationLinkSelector` e `ActiveEmailConfirmationLinkPerClient` em `src/RagAuth.Api/Infrastructure/Persistence/UniqueIndexNames.cs`
- [X] T012 Adicionar `DbSet<EmailConfirmationLink> EmailConfirmationLinks` em `src/RagAuth.Api/Infrastructure/Persistence/AppDbContext.cs`
- [X] T013 Gerar a migration `EmailConfirmation` com `dotnet ef migrations add EmailConfirmation --project src/RagAuth.Api --output-dir Infrastructure/Persistence/Migrations` e acrescentar no `Up`, após criar as colunas, `migrationBuilder.Sql("UPDATE clients SET \"emailConfirmed\" = true, \"emailConfirmedAt\" = \"createdAt\" WHERE \"deletedAt\" IS NULL;")` (FR-020); conferir o `Down` em `src/RagAuth.Api/Infrastructure/Persistence/Migrations/<timestamp>_EmailConfirmation.cs`
- [X] T014 Criar `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IEmailConfirmationLinkRepository.cs` e `EntityFrameworkEmailConfirmationLinkRepository.cs` com: `Add(EmailConfirmationLink)`, `FindBySelectorWithClientAsync(string selector, CancellationToken)`, `FindActiveForClientAsync(int clientId, CancellationToken)`, `FindLatestIssuedAtAsync(int clientId, CancellationToken)` e `CountIssuedSinceAsync(int clientId, string origin, DateTimeOffset since, CancellationToken)`; registrar em `src/RagAuth.Api/Infrastructure/Persistence/PersistenceServiceExtensions.cs`
- [X] T015 Adicionar `FindPendingByEmailAsync(string normalizedEmail, CancellationToken)` (não removida e `EmailConfirmed == false`) em `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IClientRepository.cs` e `EntityFrameworkClientRepository.cs`

### Gerador de link (usado por US1 e US3)

- [X] T016 Criar `src/RagAuth.Api/Services/EmailConfirmation/IConfirmationLinkGenerator.cs` e `RandomConfirmationLinkGenerator.cs`: `GenerateSelector()` (16 bytes de `RandomNumberGenerator`, Base64Url → 22 caracteres), `GenerateValidator()` (32 bytes, Base64Url → 43 caracteres), `ComputeValidatorHash(string validator, string accessCode)` = hexa minúsculo de `SHA256("{validator}:{accessCode}:email-confirmation")` (64 caracteres) e `IsValidatorMatch(string validator, string accessCode, string storedHash)` com `CryptographicOperations.FixedTimeEquals` (research R1)
- [X] T017 Criar `src/RagAuth.Api/Services/EmailConfirmation/ConfirmationEmailMessage.cs` (record `ClientId`, `RecipientEmail`, `ClientName`, `ConfirmationUrl`, `AttemptNumber`), `src/RagAuth.Api/Services/EmailConfirmation/IssuedConfirmationLink.cs` (record `EmailConfirmationLink Entity`, `string Validator`, `ConfirmationEmailMessage Message`; o `Validator` só vive em memória, nunca é persistido nem logado) e `IConfirmationLinkIssuer.cs`/`ConfirmationLinkIssuer.cs` com:
  - `IssuedConfirmationLink Issue(Client client, string origin)`: gera selector e validator, cria a entidade (`ValidatorHash = ComputeValidatorHash(validator, client.AccessCode)`, `Purpose = "email-confirmation"`, `IssuedAt = now`, `ExpiresAt = now + EmailConfirmationLink.ValidityPeriod`), adiciona via repositório e devolve o `IssuedConfirmationLink` com a mensagem cuja URL é `{ConfirmationPageBaseUrl}/{selector}/confirmation-register/{validator}` (sem barra dupla)
  - `void RehashForAccessCode(IssuedConfirmationLink issuedLink, string newAccessCode)`: recalcula só `issuedLink.Entity.ValidatorHash` com o mesmo `issuedLink.Validator` e o novo código de acesso; selector, validator e URL **não mudam**

### Observabilidade

- [X] T018 [P] Adicionar em `src/RagAuth.Api/Observability/LogMessages.cs` os eventos 1500–1509 de research R12 (`ConfirmationEmailSent`, `ConfirmationEmailAttemptFailed`, `ConfirmationEmailDeliveryExhausted`, `EmailConfirmed`, `ExpiredConfirmationLinkUsed`, `InvalidConfirmationLinkUsed`, `LoginRejectedEmailNotConfirmed`, `NewConfirmationLinkIssued`, `NewConfirmationLinkThrottled`, `ConfirmationEmailQueueRejected`), parâmetros só com `clientId`, número da tentativa ou e-mail mascarado

### Infraestrutura de testes

- [X] T019 [P] Criar `tests/RagAuth.UnitTests/TestDoubles/AdjustableTimeProvider.cs` (subclasse de `TimeProvider` com `SetUtcNow`/`Advance`) e uma cópia equivalente em `tests/RagAuth.IntegrationTests/Infrastructure/AdjustableTimeProvider.cs`
- [X] T020 Criar o contrato de envio na API — `src/RagAuth.Api/Infrastructure/Email/ConfirmationEmail.cs` (record `RecipientEmail`, `Subject`, `HtmlBody`) e `src/RagAuth.Api/Infrastructure/Email/IConfirmationEmailSender.cs` (`Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken)`, conforme [contracts/confirmation-email-outbound.md](./contracts/confirmation-email-outbound.md)) — e em seguida `tests/RagAuth.IntegrationTests/Infrastructure/FakeConfirmationEmailSender.cs` implementando `IConfirmationEmailSender`: guarda os e-mails enviados (thread-safe), permite configurar N falhas seguidas, expõe `WaitForEmailAsync(string recipient, TimeSpan timeout)` e `ExtractConfirmationPath(ConfirmationEmail)` que devolve `/api/v1/client/{selector}/confirmation-register/{validator}` a partir do `HtmlBody`; `Reset()`
- [X] T021 Alterar `tests/RagAuth.IntegrationTests/Infrastructure/RagAuthApiFactory.cs`: adicionar `RESEND_API_KEY`, `email:fromAddress`, `email:retryDelaysSeconds:0 = 0` e `:1 = 0` nas configurações; substituir `IConfirmationEmailSender` pelo `FakeConfirmationEmailSender` e `TimeProvider` pelo `AdjustableTimeProvider` (expostos como propriedades); incluir `email_confirmation_links` no `TRUNCATE` de `ResetDatabaseAsync` e resetar os fakes
- [X] T022 Alterar `tests/RagAuth.IntegrationTests/Infrastructure/TestClientAccounts.cs`: `RegisterAndLoginAsync` passa a marcar a conta como confirmada antes do login (via `factory.ExecuteInDatabaseAsync` com `UPDATE clients SET "emailConfirmed" = true` ou, se preferir, chamando o link do fake); criar `RegisterPendingAsync` que só cadastra; ajustar as chamadas existentes nos testes de `tests/RagAuth.IntegrationTests/` para a nova assinatura

### Testes da fundação

- [X] T023 [P] Testes unitários do gerador em `tests/RagAuth.UnitTests/Services/EmailConfirmation/RandomConfirmationLinkGeneratorTests.cs`: tamanhos 22/43/64, caracteres Base64Url, hash determinístico, hash muda com outro código de acesso, `IsValidatorMatch` verdadeiro/falso
- [X] T024 [P] Testes unitários do issuer em `tests/RagAuth.UnitTests/Services/EmailConfirmation/ConfirmationLinkIssuerTests.cs`: `ExpiresAt = IssuedAt + 24 h`, origem gravada, URL no formato do contrato com base com/sem barra final, validator ausente da entidade, `RehashForAccessCode` muda `ValidatorHash` para o hash do novo código de acesso e mantém selector, validator e URL iguais
- [X] T025 [P] Teste de inicialização em `tests/RagAuth.IntegrationTests/StartupConfigurationTests.cs`: sem `RESEND_API_KEY` ou sem `email:fromAddress` a aplicação não sobe e a mensagem cita o item ausente

**Checkpoint**: migration aplicada, testes existentes continuam verdes, fundação pronta

---

## Phase 3: User Story 1 - Receber e usar o link de confirmação (Priority: P1) 🎯 MVP

**Goal**: cadastro cria conta pendente + link, envia e-mail em segundo plano (até 3 tentativas) e a rota de confirmação ativa a conta

**Independent Test**: cadastrar, capturar o e-mail no `FakeConfirmationEmailSender`, chamar o `GET` de confirmação e verificar `emailConfirmed = true`

### Tests for User Story 1 ⚠️

- [X] T026 [P] [US1] Testes unitários do compositor em `tests/RagAuth.UnitTests/Infrastructure/Email/HtmlConfirmationEmailComposerTests.cs`: assunto `Confirme seu cadastro no {appName}`, nome codificado em HTML (`<script>` vira entidade), URL presente duas vezes no `href` e uma no texto, ano e nome da aplicação no rodapé, nenhum placeholder `{{` restante
- [X] T027 [P] [US1] Testes unitários do dispatcher em `tests/RagAuth.UnitTests/Infrastructure/Email/ConfirmationEmailDispatcherTests.cs`: sucesso na 1ª tentativa (log 1500); falha seguida de sucesso; 3 falhas → log 1502 e nenhuma 4ª tentativa; atrasos lidos de `RetryDelaysSeconds` com `AdjustableTimeProvider`; exceção do remetente nunca escapa; uma mensagem em espera não bloqueia a seguinte
- [X] T028 [P] [US1] Testes unitários do remetente em `tests/RagAuth.UnitTests/Infrastructure/Email/ResendConfirmationEmailSenderTests.cs` com `IResend` mockado: `From` = `FromAddress`, `To`, `Subject`, `HtmlBody` corretos; erro do SDK propaga como falha para o dispatcher
- [X] T029 [P] [US1] Testes unitários do cadastro em `tests/RagAuth.UnitTests/Services/Auth/AuthServiceTests.cs`: conta criada com `EmailConfirmed = false`; link `registration` adicionado antes do `SaveChanges`; mensagem enfileirada só após `SaveChanges` com sucesso; nada enfileirado se `SaveChanges` falhar (409); hash recalculado se o código de acesso for regenerado; fila cheia não impede o 201 (log 1509)
- [X] T030 [P] [US1] Testes unitários da confirmação em `tests/RagAuth.UnitTests/Services/EmailConfirmation/EmailConfirmationServiceConfirmTests.cs`: link ativo → `UsedAt`, `EmailConfirmed`, `EmailConfirmedAt` e resultado `confirmed`; link usado ou conta já confirmada → `alreadyConfirmed` sem alterações; expirado (instante = `ExpiresAt` ainda vale; `ExpiresAt + 1 tick` expira) → `ExpiredConfirmationLinkException` e conta pendente; selector inexistente, validator errado, link revogado com conta pendente → `InvalidConfirmationLinkException`; logs 1503/1504/1505
- [X] T031 [P] [US1] Testes de integração em `tests/RagAuth.IntegrationTests/EmailConfirmationEndpointsTests.cs` (rota `GET /api/v1/client/{clientIdentificationHash}/confirmation-register/{keyHash}`): cadastro gera exatamente 1 e-mail para o endereço informado; resposta do cadastro idêntica à feature 001; `GET` com link → 200 `confirmed` e envelope do contrato; repetir → 200 `alreadyConfirmed`; validator alterado, selector inexistente e valores com tamanho errado → 400 com a mesma mensagem; expirado (avançar o `AdjustableTimeProvider` 24 h + 1 s) → 410; dois clientes recebem links distintos e o link de um não confirma o outro; conta removida → 400; barra final aceita; rota acessível sem token; falha do remetente nas 3 tentativas mantém o 201 e a conta pendente

### Implementation for User Story 1

- [X] T032 [P] [US1] Criar `src/RagAuth.Api/Contracts/EmailConfirmation/EmailConfirmationResultResponse.cs` (record com `string Status`, valores `confirmed`/`alreadyConfirmed`) e o enum/constantes de resultado em `src/RagAuth.Api/Services/EmailConfirmation/EmailConfirmationOutcome.cs`
- [X] T033 [P] [US1] Criar `src/RagAuth.Api/Infrastructure/Email/IConfirmationEmailQueue.cs` e `ChannelConfirmationEmailQueue.cs` (singleton, `Channel.CreateBounded<ConfirmationEmailMessage>` com capacidade 1000, `TryEnqueue` retorna `bool`, `ReadAllAsync` para o consumidor)
- [X] T034 [P] [US1] Criar `src/RagAuth.Api/Infrastructure/Email/IConfirmationEmailComposer.cs` e `HtmlConfirmationEmailComposer.cs` (devolve o `ConfirmationEmail` criado em T020; lê o recurso embutido uma vez, substitui `{{CLIENT_NAME}}` com `HtmlEncoder.Default.Encode`, `{{CONFIRMATION_URL}}`, `{{APP_NAME}}`, `{{YEAR}}` pelo `TimeProvider`)
- [X] T035 [P] [US1] Criar `src/RagAuth.Api/Infrastructure/Email/ResendConfirmationEmailSender.cs`, implementando o `IConfirmationEmailSender` criado em T020 (injeta `IResend` e `IOptions<EmailOptions>`; monta `EmailMessage` com `From`, `To`, `Subject`, `HtmlBody`; chama `EmailSendAsync`)
- [X] T036 [US1] Criar `src/RagAuth.Api/Infrastructure/Email/ConfirmationEmailDispatcher.cs` (`BackgroundService`): lê a fila, compõe e envia; em falha captura a exceção, registra 1501 e reagenda a próxima tentativa com `Task.Delay(delay, timeProvider, token)` em tarefa separada que reenfileira com `AttemptNumber + 1`; após a 3ª falha registra 1502; usa escopo de DI por mensagem se necessário; nunca propaga exceção
- [X] T037 [US1] Criar `src/RagAuth.Api/Infrastructure/Email/EmailServiceExtensions.cs`: `services.AddOptions(); services.AddHttpClient<ResendClient>(); services.Configure<ResendClientOptions>(o => o.ApiToken = <EmailOptions.ResendApiKey>); services.AddTransient<IResend, ResendClient>();` + fila (singleton), compositor, remetente e `AddHostedService<ConfirmationEmailDispatcher>()`
- [X] T038 [US1] Criar `src/RagAuth.Api/Services/EmailConfirmation/IEmailConfirmationService.cs` e `EmailConfirmationService.cs` com `ConfirmAsync(string selector, string validator, CancellationToken)`: valida formato (22/43 caracteres Base64Url, senão inválido) → `FindBySelectorWithClientAsync` → `IsValidatorMatch` → classifica conforme a tabela "Estados do link" de [data-model.md](./data-model.md) → no caso ativo grava `UsedAt`, `EmailConfirmed = true`, `EmailConfirmedAt` via `IUnitOfWork`; registra logs 1503–1505; devolve `EmailConfirmationOutcome`
- [X] T039 [US1] Alterar `src/RagAuth.Api/Services/Auth/AuthService.cs` (`RegisterAsync`): criar o cliente com `EmailConfirmed = false`; chamar `var issuedLink = IConfirmationLinkIssuer.Issue(client, ConfirmationLinkOrigin.Registration)` antes do `SaveChanges`; passar `issuedLink` para `SaveWithUniqueAccessCodeAsync` e, sempre que o código de acesso for regenerado (`client.AccessCode = accessCodeGenerator.Generate()`), chamar `IConfirmationLinkIssuer.RehashForAccessCode(issuedLink, client.AccessCode)` antes da nova tentativa; após salvar, chamar `IConfirmationEmailQueue.TryEnqueue(issuedLink.Message)` (log 1509 se falso); manter cada método ≤ 20 linhas, extraindo métodos privados
- [X] T040 [US1] Criar `src/RagAuth.Api/Endpoints/EmailConfirmationEndpoints.cs` com grupo `/api/v1/client` `.AllowAnonymous()` e `MapGet("/{clientIdentificationHash}/confirmation-register/{keyHash}", ConfirmEmailAsync)` (aceitando barra final), delegando a `IEmailConfirmationService` e respondendo `ApiResponse.Succeeded(200, <mensagem do contrato>, new EmailConfirmationResultResponse(...))` conforme [contracts/email-confirmation-endpoints.md](./contracts/email-confirmation-endpoints.md)
- [X] T041 [US1] Criar `src/RagAuth.Api/Services/EmailConfirmationServiceExtensions.cs` (gerador singleton, issuer e serviço scoped) e registrar `AddEmailConfirmation()`/`AddEmailServices()` e `MapEmailConfirmationEndpoints()` em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs`; garantir `TimeProvider.System` registrado como singleton se ainda não estiver

**Checkpoint**: cadastro envia e-mail e o link ativa a conta; US1 testável isoladamente

---

## Phase 4: User Story 2 - Login bloqueado antes da confirmação (Priority: P1)

**Goal**: conta pendente com credenciais corretas recebe 403; credenciais erradas continuam 401 genérico

**Independent Test**: cadastrar com `RegisterPendingAsync`, logar com senha correta → 403 sem token; senha errada → 401; confirmar e logar → 200

### Tests for User Story 2 ⚠️

- [X] T042 [P] [US2] Testes unitários em `tests/RagAuth.UnitTests/Services/Auth/AuthServiceTests.cs`: pendente + senha correta → `EmailNotConfirmedException`, nenhum token emitido (`IAccessTokenIssuer` não chamado), log 1506; pendente + senha errada → `InvalidCredentialsException`; confirmada → fluxo atual
- [X] T043 [P] [US2] Testes de integração em `tests/RagAuth.IntegrationTests/AuthEndpointsTests.cs`: conta pendente + senha correta → 403 com a mensagem e o envelope de [contracts/auth-endpoints-changes.md](./contracts/auth-endpoints-changes.md) e sem `accessToken`; pendente + senha errada → 401 `E-mail ou senha inválidos.`; e-mail de conta pendente em novo cadastro → 409 (também depois de avançar o relógio além das 24 h); conta criada com `emailConfirmed = true` via banco (simula migração, FR-020) loga normalmente

### Implementation for User Story 2

- [X] T044 [US2] Alterar `src/RagAuth.Api/Services/Auth/AuthService.cs` (`LoginAsync`): após `FindClientWithValidCredentialsAsync`, chamar novo método privado `EnsureEmailIsConfirmed(client)` que registra 1506 e lança `EmailNotConfirmedException` se `EmailConfirmed == false`, antes de emitir o token

**Checkpoint**: US1 + US2 entregam o MVP (confirmação obrigatória para acessar)

---

## Phase 5: User Story 3 - Solicitar novo link de confirmação (Priority: P2)

**Goal**: pedido de novo link por e-mail com resposta genérica 202, revogação do link anterior e limites de 5 min / 10 por 24 h

**Independent Test**: cadastrar, avançar o relógio 5 min, pedir novo link → novo e-mail; link antigo → 400; novo link → 200

### Tests for User Story 3 ⚠️

- [X] T045 [P] [US3] Testes unitários da política em `tests/RagAuth.UnitTests/Services/EmailConfirmation/ConfirmationLinkIssuePolicyTests.cs`: último link há 4 min 59 s → bloqueia; há exatamente 5 min → permite; 9 links `new-link-request` nas últimas 24 h → permite; 10 → bloqueia; link `registration` não conta no limite diário mas conta no intervalo; link `new-link-request` emitido há 24 h + 1 s sai da janela
- [X] T046 [P] [US3] Testes unitários do pedido em `tests/RagAuth.UnitTests/Services/EmailConfirmation/EmailConfirmationServiceRequestNewLinkTests.cs`: pendente dentro dos limites → revoga o ativo, emite `new-link-request`, salva e enfileira (log 1507); bloqueado pela política → nada salvo nem enfileirado (log 1508); e-mail inexistente, removido ou já confirmado → nada feito; violação de `ActiveEmailConfirmationLinkPerClient` no `SaveChanges` → tratada como limite (sem exceção); e-mail normalizado (trim + minúsculas)
- [X] T047 [P] [US3] Testes de integração em `tests/RagAuth.IntegrationTests/EmailConfirmationEndpointsTests.cs` (rota `POST /api/v1/client/new-confirmation-register`): pedido logo após o cadastro → 202 e nenhum e-mail novo; após avançar 5 min → 202 e novo e-mail; link anterior → 400 e novo link → 200; após 10 reenvios em 24 h o 11º não envia e-mail; e-mail inexistente, confirmado e removido → 202 com corpo **byte a byte idêntico** ao da conta pendente (SC-007) e nenhum e-mail; link expirado + novo link → novo link confirma; `email` ausente, mal formado ou com mais de 254 caracteres → 400 com `error.errors.email`; rota acessível sem token; dois pedidos simultâneos geram no máximo um novo link

### Implementation for User Story 3

- [X] T048 [P] [US3] Criar `src/RagAuth.Api/Contracts/EmailConfirmation/NewConfirmationLinkRequest.cs` (record `Email` com `[Required]`, `[EmailAddress]`, `[MaxLength(254)]` e mensagens em português no padrão de `RegisterClientRequest`)
- [X] T049 [P] [US3] Criar `src/RagAuth.Api/Services/EmailConfirmation/ConfirmationLinkIssuePolicy.cs` (interface `IConfirmationLinkIssuePolicy`): `CanIssueAsync(int clientId, CancellationToken)` usando `FindLatestIssuedAtAsync` (intervalo `NewLinkIntervalMinutes`) e `CountIssuedSinceAsync(clientId, ConfirmationLinkOrigin.NewLinkRequest, now - 24 h)` (limite `MaximumNewLinksPerDay`)
- [X] T050 [US3] Adicionar `RequestNewLinkAsync(string email, CancellationToken)` em `src/RagAuth.Api/Services/EmailConfirmation/IEmailConfirmationService.cs` e `EmailConfirmationService.cs`: normaliza → `FindPendingByEmailAsync` → política → `FindActiveForClientAsync` e `RevokedAt = now` → `IConfirmationLinkIssuer.Issue(client, ConfirmationLinkOrigin.NewLinkRequest)` → `SaveChangesAsync` com `try/catch` de `DbUpdateException` quando `UniqueConstraintViolation.IsViolationOf(..., UniqueIndexNames.ActiveEmailConfirmationLinkPerClient)` (log 1508) → `TryEnqueue`; nunca lança para casos de negócio
- [X] T051 [US3] Adicionar `MapPost("/new-confirmation-register", RequestNewConfirmationLinkAsync)` (aceitando barra final) em `src/RagAuth.Api/Endpoints/EmailConfirmationEndpoints.cs`, respondendo sempre `ApiResponse.Succeeded<object?>(202, "Se o e-mail estiver cadastrado e aguardando confirmação, enviaremos um novo link em instantes.", null)` com status 202

**Checkpoint**: as três histórias funcionam de forma independente

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T052 [P] Teste em massa em `tests/RagAuth.IntegrationTests/CrossCutting/ConfirmationLinkUniquenessTests.cs`: gerar ≥ 10.000 selectors/validators com `RandomConfirmationLinkGenerator` e cadastrar um lote no banco sem nenhuma colisão (SC-005)
- [X] T053 [P] Ampliar `tests/RagAuth.IntegrationTests/CrossCutting/SensitiveDataAuditTests.cs`: após cadastro, confirmação, link inválido, novo link e falha de envio, nenhum log capturado contém selector, validator, URL do link, HTML do e-mail nem o valor de `RESEND_API_KEY` (SC-008)
- [X] T054 [P] Ampliar `tests/RagAuth.IntegrationTests/PersistenceSchemaTests.cs`: colunas `emailConfirmed` (padrão `false`) e `emailConfirmedAt`; índices `ux_email_confirmation_links_selector` e `ux_email_confirmation_links_active_per_client` (com filtro); a migração marca como confirmadas as contas não removidas existentes
- [X] T055 [P] Ampliar `tests/RagAuth.IntegrationTests/ProtectedRouteTests.cs` (ou teste equivalente de rotas abertas): somente cadastro, login, confirmação de e-mail e novo link são acessíveis sem token (FR-019)
- [X] T056 [P] Ampliar `tests/RagAuth.IntegrationTests/CrossCutting/ResponseTimeTests.cs`: p95 do cadastro continua < 500 ms com o envio de e-mail ativo (SC-002)
- [X] T057 [P] Ampliar `tests/RagAuth.UnitTests/Configuration/ApplicationCompositionTests.cs`: `IResend`, `IConfirmationEmailQueue`, `IConfirmationEmailSender`, `IEmailConfirmationService` e o hosted service resolvem pelo contêiner
- [X] T058 Atualizar `README.md` com `RESEND_API_KEY`, a seção `email` e as duas rotas novas
- [X] T059 Rodar `dotnet test --settings coverlet.runsettings`, garantir todos verdes e cobertura de linhas ≥ 90%; revisar que nenhum método novo passa de 20 linhas
- [X] T060 Executar os cenários de [quickstart.md](./quickstart.md) (1–15) contra a aplicação local com uma chave real do Resend — **Executado sem envio real (decisão do usuário em 2026-11-01)**: cenários 1–3, 7, 8, 10–13 e 15 contra a API local com chave fictícia; 4–6, 9 e 14 (dependem do link do e-mail) cobertos pelos testes de integração com `FakeConfirmationEmailSender`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: depende do Setup; bloqueia todas as histórias
- **US1 (Phase 3)**: depende da fundação
- **US2 (Phase 4)**: depende só da fundação (usa `RegisterPendingAsync`/banco); o teste "confirmar e logar" usa o `GET` da US1 — se a US1 não estiver pronta, confirmar via banco
- **US3 (Phase 5)**: depende da fundação e da fila/dispatcher/endpoint-grupo da US1 (T033–T037, T040)
- **Polish (Phase 6)**: depois das histórias desejadas

### Within Each User Story

- Testes primeiro (devem falhar) → DTOs/infra → serviço → endpoint → composição
- T036 depende de T033–T035; T039 depende de T033 e T017; T040 depende de T038; T041 depende de T037–T040
- T050 depende de T049; T051 depende de T048 e T050

### Parallel Opportunities

- Setup: T003 em paralelo com T001–T002
- Fundação: T007, T008, T009, T018, T019 em paralelo; T020 antes de T021; T023–T025 em paralelo após T016–T017
- US1: testes T026–T031 em paralelo; T032–T035 em paralelo
- US2 pode ser feita em paralelo com a US1 por outra pessoa (só toca `LoginAsync`)
- US3: T045–T047 em paralelo; T048 e T049 em paralelo
- Polish: T052–T057 em paralelo

## Parallel Example: User Story 1

```text
# Testes juntos:
T026 HtmlConfirmationEmailComposerTests
T027 ConfirmationEmailDispatcherTests
T028 ResendConfirmationEmailSenderTests
T030 EmailConfirmationServiceConfirmTests

# Infra juntos:
T033 ChannelConfirmationEmailQueue
T034 HtmlConfirmationEmailComposer
T035 ResendConfirmationEmailSender
```

## Parallel Example: User Story 3

```text
T045 ConfirmationLinkIssuePolicyTests  |  T046 EmailConfirmationServiceRequestNewLinkTests
T048 NewConfirmationLinkRequest        |  T049 ConfirmationLinkIssuePolicy
```

## Implementation Strategy

### MVP First (US1 + US2)

1. Phase 1 + Phase 2 (testes existentes continuam verdes graças a T022)
2. Phase 3 (US1) → validar cenários 1, 4, 5, 7 do quickstart
3. Phase 4 (US2) → validar cenários 2, 3, 6
4. **Parar e validar**: só contas confirmadas acessam o sistema — MVP entregável

### Incremental Delivery

1. MVP (US1 + US2)
2. US3 → validar cenários 10–14 (sem ela, quem perde o prazo fica bloqueado: entregar logo em seguida)
3. Polish → auditorias, cobertura, README, quickstart completo

## Notes

- [P] = arquivos diferentes, sem dependência de tarefa incompleta
- Commit após cada tarefa ou grupo lógico
- A spec 001 não é editada; FR-012 dela fica superado por FR-019 desta feature
