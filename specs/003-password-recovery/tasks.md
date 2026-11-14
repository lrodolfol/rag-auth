---

description: "Task list for 003-password-recovery"
---

# Tasks: Recuperação de senha

**Input**: Design documents from `/specs/003-password-recovery/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: os testes são obrigatórios. A Constituição VI exige teste unitário xUnit para toda regra
de negócio, com cobertura de linhas de no mínimo 90%, e pelo menos um teste de integração por
endpoint novo ou alterado. Em cada história, escreva os testes primeiro e confirme que eles falham
antes de implementar.

**Organization**: as tarefas estão agrupadas por história de usuário, para que cada história possa
ser implementada e testada de forma independente.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: a tarefa pode rodar em paralelo com outras, porque mexe em arquivos diferentes e não
  depende de tarefa incompleta.
- **[Story]**: história de usuário da tarefa (US1, US2 ou US3).

## Regras válidas para TODAS as tarefas

- Métodos com no máximo 20 linhas. Cada classe tem uma única responsabilidade (Constituição II).
- Nomes autoexplicativos, sem abreviações (Constituição III).
- Endpoints só delegam a serviços. Nunca acessam `AppDbContext` nem repositórios diretamente
  (Constituição I).
- Entrada e saída só por DTOs. Dependências chegam por interface e injeção, sem `new` para criar
  serviços (Constituição IV e V).
- Mensagens de erro, de resposta e de log em português (Constituição VII).
- Respostas sempre no envelope `ApiResponse` (`src/RagAuth.Api/Contracts/ApiResponse.cs`).
- Nunca registrar em log nem devolver em erro: selector, validator, URL do link, corpo do e-mail,
  senha (nova ou antiga), hash de senha nem a `RESEND_API_KEY` (FR-021, SC-007). E-mails só
  aparecem mascarados com `SensitiveDataMasker.MaskEmail`.
- Datas sempre obtidas pelo `TimeProvider` injetado, nunca por `DateTimeOffset.UtcNow` direto. Única
  exceção: o `StopwatchMinimumResponseDurationGuard` (T036), que mede duração e não data (plan,
  decisão 7).
- O segmento de rota e de URL `pass-recuvery` fica escrito **uma única vez**, na constante
  `PasswordRecoveryLinkIssuer.RecoveryPathSegment`. A grafia foi pedida na entrada e **precisa ser
  confirmada com o dono do produto antes da fase 3** (research R1, gate do T030).
- Os parâmetros de rota se chamam `selector` e `validator` (Constituição III). Não use
  `clientIdentificationHash` nem `keyHash`.
- **Nenhuma dependência nova** (NuGet ou serviço externo) entra nesta feature.

## Path Conventions

- API: `src/RagAuth.Api/`
- Testes unitários: `tests/RagAuth.UnitTests/`, com as mesmas pastas da API
- Testes de integração: `tests/RagAuth.IntegrationTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: modelos de e-mail e configuração nova

- [X] T001 [P] Criar `src/RagAuth.Api/Infrastructure/Email/Templates/PasswordRecoveryEmail.html`, copiando a estrutura e o estilo de `ConfirmationEmail.html`:
  - Título: `Redefina sua senha no {{APP_NAME}}`.
  - Saudação: `Olá, <strong>{{CLIENT_NAME}}</strong>!`.
  - Parágrafo: `Recebemos um pedido para redefinir a senha da sua conta.`
  - Botão `Redefinir minha senha` apontando para `{{ACTION_URL}}`.
  - Os textos `Este link é válido por <strong>24 horas</strong>.` e `Se o link expirar, solicite uma nova recuperação de senha na página de login.`
  - O link `{{ACTION_URL}}` também em texto, para quem não consegue usar o botão.
  - O texto `Se você não pediu a redefinição de senha, ignore este e-mail: sua senha atual continua valendo.`
  - Rodapé: `© {{YEAR}} {{APP_NAME}}. Todos os direitos reservados.`
  - Registrar o arquivo como `<EmbeddedResource>` em `src/RagAuth.Api/RagAuth.Api.csproj`, seguindo o padrão do template de confirmação.
- [X] T002 Criar `src/RagAuth.Api/Infrastructure/Email/Templates/PasswordChangedEmail.html`, no mesmo estilo. Fazer **depois do T001**, porque os dois editam o `RagAuth.Api.csproj`:
  - Título: `Sua senha foi alterada`.
  - Saudação com `{{CLIENT_NAME}}`.
  - O texto `A senha da sua conta no {{APP_NAME}} foi alterada em {{CHANGED_AT}}.`
  - O texto `Se não foi você, solicite imediatamente a recuperação de senha e entre em contato com o suporte.`
  - O mesmo rodapé do T001.
  - O e-mail **não pode ter** nenhum link de redefinição nem `{{ACTION_URL}}` (FR-018).
  - Registrar como `<EmbeddedResource>` em `src/RagAuth.Api/RagAuth.Api.csproj`.
- [X] T003 [P] Em `src/RagAuth.Api/Infrastructure/Email/Templates/ConfirmationEmail.html`, trocar as 3 ocorrências de `{{CONFIRMATION_URL}}` por `{{ACTION_URL}}` e não mudar nada além disso (research R12).
- [X] T004 [P] Adicionar à seção `email` de `src/RagAuth.Api/appsettings.json` as chaves `passwordRecoveryPageBaseUrl` (com o mesmo valor local de `confirmationPageBaseUrl`, hoje `http://localhost:1010`), `passwordRecoveryIntervalMinutes: 5`, `maximumPasswordRecoveryLinksPerDay: 10` e `passwordRecoveryMinimumResponseMilliseconds: 300`. Em `src/RagAuth.Api/appsettings.dev.json`, adicionar `passwordRecoveryPageBaseUrl` com o mesmo valor de `confirmationPageBaseUrl` desse arquivo. Só esses dois arquivos existem; não criar outros ambientes nesta feature (plan, decisão 8).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: generalizar a infraestrutura de e-mail e o gerador de links, e criar o domínio, a
persistência e a infraestrutura de testes usados por todas as histórias.

**⚠️ CRITICAL**: nenhuma história começa antes de esta fase terminar com **todos** os testes das
features 001 e 002 passando.

### Generalização da infraestrutura de e-mail (research R11), sem mudar o comportamento da feature 002

- [X] T005 Criar `src/RagAuth.Api/Infrastructure/Email/EmailKind.cs` com `public enum EmailKind { EmailConfirmation, PasswordRecovery, PasswordChanged }`.
  - Substituir `src/RagAuth.Api/Services/EmailConfirmation/ConfirmationEmailMessage.cs` por `src/RagAuth.Api/Infrastructure/Email/OutgoingEmailMessage.cs`, com `public sealed record OutgoingEmailMessage(EmailKind Kind, int ClientId, string RecipientEmail, string ClientName, string? ActionUrl, DateTimeOffset? OccurredAt = null, int AttemptNumber = 1);`.
  - Manter o comentário XML avisando que `ActionUrl` contém o validator e nunca vai para banco nem para log.
- [X] T006 Renomear `ConfirmationEmail.cs` para `ComposedEmail.cs`, com `ComposedEmail(string RecipientEmail, string Subject, string HtmlBody)`.
  - Renomear `IConfirmationEmailSender.cs`/`ResendConfirmationEmailSender.cs` para `IEmailSender.cs`/`ResendEmailSender.cs`, todos em `src/RagAuth.Api/Infrastructure/Email/`.
  - Renomear `ConfirmationEmailDeliveryException` para `EmailDeliveryException`, com a mensagem `O provedor de e-mail recusou o envio do e-mail.`
  - O `ResendEmailSender` continua sendo o único lugar que conhece o SDK do Resend.
- [X] T007 Renomear `IConfirmationEmailQueue.cs`/`ChannelConfirmationEmailQueue.cs` para `IEmailQueue.cs`/`ChannelEmailQueue.cs` em `src/RagAuth.Api/Infrastructure/Email/`, trocando o tipo da mensagem para `OutgoingEmailMessage`. A capacidade continua 1000 e o modo de fila cheia continua o mesmo.
- [X] T008 Criar `src/RagAuth.Api/Infrastructure/Email/EmailTemplateCatalog.cs` com o registro `EmailTemplate(string ResourceName, string SubjectTemplate)` e um `FrozenDictionary<EmailKind, EmailTemplate>` contendo:
  - `EmailConfirmation`: recurso `ConfirmationEmail.html`, assunto `Confirme seu cadastro no {0}`;
  - `PasswordRecovery`: recurso `PasswordRecoveryEmail.html`, assunto `Redefina sua senha no {0}`;
  - `PasswordChanged`: recurso `PasswordChangedEmail.html`, assunto `Sua senha foi alterada no {0}`.

  O conteúdo de cada template é carregado uma única vez (cache preguiçoso por tipo). Template
  ausente lança `InvalidOperationException` com mensagem em português.
- [X] T009 Substituir `IConfirmationEmailComposer.cs`/`HtmlConfirmationEmailComposer.cs` por `IEmailComposer.cs`/`HtmlEmailComposer.cs` em `src/RagAuth.Api/Infrastructure/Email/`.
  - O compositor escolhe o template pelo `EmailKind` usando o `EmailTemplateCatalog`.
  - Substitui `{{CLIENT_NAME}}`, `{{ACTION_URL}}` (texto vazio quando `ActionUrl` é nulo), `{{APP_NAME}}`, `{{YEAR}}` e `{{CHANGED_AT}}`. Este último recebe `OccurredAt` convertido para o fuso `America/Sao_Paulo` no formato `dd/MM/yyyy 'às' HH:mm` com `pt-BR`. Obter o fuso com `TimeZoneInfo.TryFindSystemTimeZoneById` e, se não existir (contêiner sem tzdata), usar um fuso fixo de UTC-3 criado com `TimeZoneInfo.CreateCustomTimeZone`. Resolver o fuso uma única vez.
  - Todo valor passa por `HtmlEncoder.Default.Encode`.
  - Divida a lógica em métodos de no máximo 20 linhas.
- [X] T010 Renomear `ConfirmationEmailDispatcher.cs` para `EmailDispatcher.cs` em `src/RagAuth.Api/Infrastructure/Email/`, usando `IEmailQueue`, `IEmailComposer` e `IEmailSender`. A lógica continua igual: 3 tentativas, atrasos de `email:retryDelaysSeconds`, espera fora do laço de leitura e nenhuma exceção escapa. Remover o `Console.WriteLine("fail")` que ficou no `catch` de cancelamento.
- [X] T011 Em `src/RagAuth.Api/Observability/LogMessages.cs`:
  1. Renomear os eventos 1500, 1501, 1502 e 1509 para `EmailSent`, `EmailAttemptFailed`, `EmailDeliveryExhausted` e `EmailQueueRejected`, acrescentando o parâmetro `EmailKind emailKind`. As mensagens passam a ser:
     - `E-mail {EmailKind} do cliente {ClientId} enviado na tentativa {AttemptNumber}.`
     - `Falha na tentativa {AttemptNumber} de envio do e-mail {EmailKind} do cliente {ClientId}: {FailureType}.`
     - `O envio do e-mail {EmailKind} do cliente {ClientId} esgotou as {AttemptCount} tentativas.`
     - `Fila de e-mails cheia: e-mail {EmailKind} do cliente {ClientId} descartado.`

     Os `EventId` não mudam.
  2. Adicionar os eventos novos:
     - 1600 Information `PasswordRecoveryLinkIssued(clientId)`: `Link de recuperação de senha emitido para o cliente {ClientId}.`
     - 1601 Warning `PasswordRecoveryThrottled(clientId)`: `Pedido de recuperação de senha do cliente {ClientId} recusado por limite de emissão.`
     - 1602 Information `PasswordRecoveryRequestedForIneligibleEmail()`: `Pedido de recuperação de senha para e-mail sem conta ativa e confirmada.`
     - 1603 Information `PasswordReset(clientId)`: `Senha do cliente {ClientId} redefinida com sucesso.`
     - 1604 Warning `ExpiredPasswordRecoveryLinkUsed(clientId)`: `Link de recuperação de senha expirado usado pelo cliente {ClientId}.`
     - 1605 Warning `InvalidPasswordRecoveryLinkUsed()`: `Tentativa de uso de link de recuperação de senha inválido.`
- [X] T012 Atualizar `src/RagAuth.Api/Infrastructure/Email/EmailServiceExtensions.cs` para registrar `IEmailQueue` → `ChannelEmailQueue` (singleton), `IEmailComposer` → `HtmlEmailComposer` (singleton), `IEmailSender` → `ResendEmailSender` (scoped) e o hosted service `EmailDispatcher`. Os registros do Resend por DI continuam como estão.
- [X] T013 Ajustar os chamadores da feature 002 aos tipos novos, sem mudar o comportamento:
  - `src/RagAuth.Api/Services/EmailConfirmation/ConfirmationLinkIssuer.cs`: cria `OutgoingEmailMessage(EmailKind.EmailConfirmation, …, ActionUrl: url)`.
  - `IssuedConfirmationLink.cs`: o campo `Message` passa a ser `OutgoingEmailMessage`.
  - `EmailConfirmationService.cs` e `src/RagAuth.Api/Services/Auth/AuthService.cs`: passam a usar `IEmailQueue` e `LogMessages.EmailQueueRejected(…, EmailKind.EmailConfirmation)`.

### Gerador de link compartilhado (research R2)

- [X] T014 Criar a pasta `src/RagAuth.Api/Services/SecureLinks/`. Mover `IConfirmationLinkGenerator`/`RandomConfirmationLinkGenerator` para `ISecureLinkGenerator.cs`/`RandomSecureLinkGenerator.cs` e `ConfirmationLinkFormat` para `SecureLinkFormat.cs`, todos nessa pasta.
  - As assinaturas passam a ser `string ComputeValidatorHash(string validator, string accessCode, string purpose)` e `bool IsValidatorMatch(string validator, string accessCode, string purpose, string storedHash)`.
  - A entrada do hash continua sendo `$"{validator}:{accessCode}:{purpose}"`, então os hashes já gravados continuam válidos.
  - Atualizar `ConfirmationLinkIssuer.cs` e `EmailConfirmationService.cs` para passar `EmailConfirmationLink.EmailConfirmationPurpose`.
  - Trocar o registro em `src/RagAuth.Api/Services/EmailConfirmationServiceExtensions.cs` para `services.AddSingleton<ISecureLinkGenerator, RandomSecureLinkGenerator>()`.

### Configuração

- [X] T015 Adicionar a `src/RagAuth.Api/Configuration/EmailOptions.cs`:
  - `PasswordRecoveryPageBaseUrl`: `[Required(ErrorMessage = "A configuração 'email:passwordRecoveryPageBaseUrl' é obrigatória.")]`, `[Url(ErrorMessage = "A configuração 'email:passwordRecoveryPageBaseUrl' deve ser uma URL absoluta.")]`, padrão `https://ragauth.com.br`.
  - `PasswordRecoveryIntervalMinutes`: `[Range(0, 1440, ErrorMessage = "A configuração 'email:passwordRecoveryIntervalMinutes' deve estar entre 0 e 1440.")]`, padrão 5.
  - `MaximumPasswordRecoveryLinksPerDay`: `[Range(1, 100, ErrorMessage = "A configuração 'email:maximumPasswordRecoveryLinksPerDay' deve estar entre 1 e 100.")]`, padrão 10.
  - `PasswordRecoveryMinimumResponseMilliseconds`: `[Range(0, 5000, ErrorMessage = "A configuração 'email:passwordRecoveryMinimumResponseMilliseconds' deve estar entre 0 e 5000.")]`, padrão 300 (SC-003, plan decisão 7).

### Domínio

- [X] T016 [P] Criar `src/RagAuth.Api/Domain/Entities/PasswordRecoveryLink.cs` com:
  - as constantes `public const string PasswordRecoveryPurpose = "password-recovery";` e `public static readonly TimeSpan ValidityPeriod = TimeSpan.FromHours(24);`;
  - as propriedades `long Id`, `int ClientId`, `Client Client`, `string Selector`, `string ValidatorHash`, `string Purpose` (padrão `PasswordRecoveryPurpose`), `DateTimeOffset IssuedAt`, `DateTimeOffset ExpiresAt`, `DateTimeOffset? UsedAt` e `DateTimeOffset? RevokedAt`.

  A entidade **não tem** `Origin` ([data-model.md](./data-model.md)).
- [X] T017 [P] Em `src/RagAuth.Api/Domain/Entities/Client.cs`, adicionar `DateTimeOffset? PasswordChangedAt` e `ICollection<PasswordRecoveryLink> PasswordRecoveryLinks { get; set; } = [];`.
- [X] T018 [P] Criar em `src/RagAuth.Api/Domain/Exceptions/` duas exceções derivadas de `DomainException`:
  - `InvalidPasswordRecoveryLinkException.cs`: status 400, título `Requisição inválida`, mensagem `Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.`
  - `ExpiredPasswordRecoveryLinkException.cs`: status 410, título `Link expirado`, mensagem `O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.`

### Persistência

- [X] T019 Criar `src/RagAuth.Api/Infrastructure/Persistence/Configurations/PasswordRecoveryLinkConfiguration.cs`, espelhando `EmailConfirmationLinkConfiguration.cs`:
  - tabela `password_recovery_links`;
  - `Id` `bigint` identity;
  - chave estrangeira `clientId` → `clients.id` com `ON DELETE CASCADE`, navegação `client.PasswordRecoveryLinks`;
  - `Selector` `varchar(22)` obrigatório, `ValidatorHash` `char(64)` obrigatório, `Purpose` `varchar(32)` obrigatório;
  - `IssuedAt` e `ExpiresAt` obrigatórios;
  - índices `ux_password_recovery_links_selector` (único em `selector`), `ux_password_recovery_links_active_per_client` (único em `clientId`, com filtro `"usedAt" IS NULL AND "revokedAt" IS NULL`) e `ix_password_recovery_links_client_issued` (`clientId`, `issuedAt`);
  - filtro global `link => link.Client.DeletedAt == null`.

  Adicionar `PasswordRecoveryLinkSelector` e `ActivePasswordRecoveryLinkPerClient` em `src/RagAuth.Api/Infrastructure/Persistence/UniqueIndexNames.cs`.
- [X] T020 Configurar `PasswordChangedAt` como coluna nula em `src/RagAuth.Api/Infrastructure/Persistence/Configurations/ClientConfiguration.cs` e adicionar `DbSet<PasswordRecoveryLink> PasswordRecoveryLinks` em `src/RagAuth.Api/Infrastructure/Persistence/AppDbContext.cs`.
- [X] T021 Gerar a migration com `dotnet ef migrations add PasswordRecovery --project src/RagAuth.Api --output-dir Infrastructure/Persistence/Migrations`. Conferir em `src/RagAuth.Api/Infrastructure/Persistence/Migrations/<timestamp>_PasswordRecovery.cs` que o `Up` só adiciona a coluna `clients."passwordChangedAt"` (nula, sem valor padrão) e cria a tabela com os 3 índices, **sem alterar dados**, e que o `Down` desfaz as duas coisas.
- [X] T022 Criar `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IPasswordRecoveryLinkRepository.cs` e `EntityFrameworkPasswordRecoveryLinkRepository.cs` com os métodos:
  - `Add(PasswordRecoveryLink)`;
  - `FindBySelectorWithClientAsync(string selector, CancellationToken)`;
  - `FindActiveForClientAsync(int clientId, CancellationToken)`;
  - `FindLatestIssuedAtAsync(int clientId, CancellationToken)`;
  - `CountIssuedSinceAsync(int clientId, DateTimeOffset since, CancellationToken)`;
  - `ReloadAsync(PasswordRecoveryLink link, CancellationToken)`, que relê `usedAt` e `revokedAt` do banco depois do bloqueio (research R9).

  Registrar em `src/RagAuth.Api/Infrastructure/Persistence/PersistenceServiceExtensions.cs`.
- [X] T023 Adicionar `FindConfirmedByEmailAsync(string normalizedEmail, CancellationToken)` (conta não removida e `EmailConfirmed == true`) em `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IClientRepository.cs` e `EntityFrameworkClientRepository.cs`.

### Infraestrutura de testes

- [X] T024 Renomear `tests/RagAuth.IntegrationTests/Infrastructure/FakeConfirmationEmailSender.cs` para `FakeEmailSender.cs`, implementando `IEmailSender` e guardando `ComposedEmail`.
  - Acrescentar `EmailsTo(string recipient, string subjectStartsWith)`, para filtrar o tipo de e-mail pelo assunto.
  - Acrescentar `WaitForEmailAsync(recipient, subjectStartsWith, timeout, expectedCount)`.
  - Acrescentar `ExtractRecoveryPath(ComposedEmail)`, que converte `…/{selector}/pass-recuvery/{validator}` em `/api/v1/client/{selector}/pass-recuvery/{validator}` com `[GeneratedRegex]`.
  - Manter `ExtractConfirmationPath`.
  - Atualizar `RagAuthApiFactory.cs`, `TestClientAccounts.cs`, `AuthEndpointsTests.cs`, `EmailConfirmationEndpointsTests.cs` e `CrossCutting/SensitiveDataAuditTests.cs` para os nomes novos.
- [X] T025 Atualizar os testes unitários da feature 002 para os tipos renomeados, sem mudar o que cada teste verifica:
  - em `tests/RagAuth.UnitTests/Infrastructure/Email/`, renomear `ConfirmationEmailDispatcherTests.cs` → `EmailDispatcherTests.cs`, `HtmlConfirmationEmailComposerTests.cs` → `HtmlEmailComposerTests.cs` e `ResendConfirmationEmailSenderTests.cs` → `ResendEmailSenderTests.cs`;
  - em `tests/RagAuth.UnitTests/Services/EmailConfirmation/`, renomear `RandomConfirmationLinkGeneratorTests.cs` → `tests/RagAuth.UnitTests/Services/SecureLinks/RandomSecureLinkGeneratorTests.cs` e ajustar `ConfirmationLinkIssuerTests.cs`, `EmailConfirmationServiceFixture.cs` e `EmailConfirmationServiceRequestNewLinkTests.cs`;
  - ajustar também `tests/RagAuth.UnitTests/Services/Auth/AuthServiceTests.cs` e `tests/RagAuth.UnitTests/Configuration/ApplicationCompositionTests.cs`.

### Testes da fundação

- [X] T026 [P] Em `tests/RagAuth.UnitTests/Infrastructure/Email/HtmlEmailComposerTests.cs`, acrescentar casos para:
  - os três assuntos;
  - `{{ACTION_URL}}` codificado em HTML nos e-mails de confirmação e de recuperação;
  - o e-mail `PasswordChanged` sem `href` de redefinição e com `{{CHANGED_AT}}` no formato `dd/MM/yyyy 'às' HH:mm` em horário de Brasília;
  - nome com `<script>` codificado;
  - `{{CHANGED_AT}}` correto também quando o fuso `America/Sao_Paulo` não é encontrado (fuso fixo de UTC-3);
  - nenhum placeholder `{{…}}` restante em nenhum dos três templates.
- [X] T027 [P] Em `tests/RagAuth.UnitTests/Services/SecureLinks/RandomSecureLinkGeneratorTests.cs`, acrescentar casos mostrando que:
  - o hash calculado com `password-recovery` é diferente do hash calculado com `email-confirmation` para o mesmo validator e o mesmo código de acesso;
  - `IsValidatorMatch` com a ação errada devolve `false`;
  - o hash de `email-confirmation` é idêntico ao calculado antes da refatoração (valor fixo no teste).
- [X] T028 [P] Em `tests/RagAuth.IntegrationTests/PersistenceSchemaTests.cs`, verificar a existência dos índices `ux_password_recovery_links_selector`, `ux_password_recovery_links_active_per_client` (com o filtro parcial) e `ix_password_recovery_links_client_issued`, e da coluna nula `clients.passwordChangedAt`. Em `tests/RagAuth.UnitTests/Infrastructure/Persistence/PersistenceModelTests.cs`, verificar os tamanhos `varchar(22)`, `char(64)` e `varchar(32)` e o filtro global.
- [X] T029 [P] Em `tests/RagAuth.UnitTests/Infrastructure/Email/EmailDispatcherTests.cs`, acrescentar um caso mostrando que os logs 1500, 1501 e 1502 levam o `EmailKind` da mensagem (`PasswordRecovery`).
- [X] T030 Rodar `dotnet test` na solução inteira. **Checkpoint**: todos os testes das features 001 e 002 passam depois da refatoração, sem regressão.

**Checkpoint**: fundação pronta. As histórias podem começar.

**⚠️ Gate antes da fase 3**: confirmar com o dono do produto a grafia `pass-recuvery` (research
R1). Se mudar, alterar só a constante `RecoveryPathSegment` e o nome da rota do POST antes do T035.

---

## Phase 3: User Story 1 - Solicitar a recuperação de senha (Priority: P1) 🎯 MVP

**Goal**: o cliente com e-mail confirmado pede a recuperação e recebe o e-mail com um link válido
por 24 horas. A resposta é sempre 202 genérico. Um pedido novo revoga o link anterior.

**Independent Test**: pedir a recuperação para um e-mail confirmado e capturar o e-mail no
`FakeEmailSender`, conferindo o link no formato `…/{22}/pass-recuvery/{43}`, o texto de 24 horas e
o aviso "ignore". Em seguida, pedir para um e-mail inexistente, um pendente e um removido, e
verificar que a resposta é idêntica e que nenhum e-mail é enviado.

### Tests for User Story 1 ⚠️

> Escreva estes testes PRIMEIRO e confirme que falham antes da implementação.

- [X] T031 [P] [US1] Criar `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryLinkIssuerTests.cs`, cobrindo:
  - o issuer cria um `PasswordRecoveryLink` com `Purpose = "password-recovery"`, `ExpiresAt = IssuedAt + 24 h` (via `AdjustableTimeProvider`) e `ValidatorHash` igual a `ComputeValidatorHash(validator, accessCode, "password-recovery")`;
  - chama `Add` no repositório;
  - devolve uma `OutgoingEmailMessage` com `Kind = PasswordRecovery` e `ActionUrl = "{passwordRecoveryPageBaseUrl sem barra final}/{selector}/pass-recuvery/{validator}"`;
  - o validator nunca aparece na entidade.
- [X] T032 [P] [US1] Criar `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryServiceFixture.cs`, com mocks de `IPasswordRecoveryLinkRepository`, `IClientRepository`, `ISecureLinkGenerator`, `IPasswordRecoveryLinkIssuer`, `IPasswordRecoveryLinkIssuePolicy` (por padrão permite emitir), `IEmailQueue`, `IPasswordHashingService`, `IUnitOfWork` (reaproveitando `UnitOfWorkMocks`), `AdjustableTimeProvider` e um logger capturável (`LoggerMocks`). Criar também `PasswordRecoveryServiceRequestTests.cs`, cobrindo:
  - conta confirmada: o serviço bloqueia a linha (`LockForUpdateAsync`), revoga o link ativo (`RevokedAt = agora`), emite o novo, faz commit, enfileira `PasswordRecovery` e grava o log 1600;
  - conta pendente, removida ou inexistente (`FindConfirmedByEmailAsync` devolve `null`): o serviço não emite link, não enfileira nada e grava o log 1602 **sem o e-mail**;
  - o e-mail é normalizado com `Trim` e minúsculas;
  - fila cheia: log 1509 com `EmailKind.PasswordRecovery`, sem exceção;
  - o método nunca lança exceção nem devolve valor que diferencie as situações;
  - política recusa: não emite, não enfileira e grava o log 1601;
  - o pedido inteiro, nos dois caminhos (conta confirmada e e-mail desconhecido), roda dentro de `IMinimumResponseDurationGuard.RunAsync` (mock que executa a operação recebida).

  Criar também `tests/RagAuth.UnitTests/Services/PasswordRecovery/StopwatchMinimumResponseDurationGuardTests.cs`, cobrindo:
  - com mínimo de 100 ms e operação instantânea, o tempo total é de pelo menos 100 ms;
  - com operação mais demorada que o mínimo, não há espera extra além de uma margem;
  - mínimo 0: sem espera;
  - exceção da operação é propagada.
- [X] T033 [P] [US1] Criar `tests/RagAuth.IntegrationTests/PasswordRecoveryEndpointsTests.cs` com a seção do `POST /api/v1/client/new-pass-recuvery-request`, cobrindo:
  - conta confirmada: 202 no envelope `ApiResponse` com a mensagem `Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes.` e e-mail com assunto `Redefina sua senha no …` contendo o link, `24 horas` e o aviso "ignore";
  - e-mail inexistente, conta pendente e conta removida: resposta com `statusCode`, `success`, `message` e `data` **idênticos** e nenhum e-mail;
  - `email` ausente, `"invalido"` ou com 255 caracteres: 400 com `error.errors.email`;
  - dois clientes recebem links diferentes;
  - a rota funciona sem token e com barra no fim.

### Implementation for User Story 1

- [X] T034 [P] [US1] Criar `src/RagAuth.Api/Contracts/PasswordRecovery/PasswordRecoveryRequest.cs` com `Email`: `[Required(ErrorMessage = "Informe o e-mail.")]`, `[EmailAddress(ErrorMessage = "Informe um e-mail válido.")]` e `[MaxLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]`.
- [X] T035 [US1] Criar em `src/RagAuth.Api/Services/PasswordRecovery/`:
  - `IssuedPasswordRecoveryLink.cs`: `record(PasswordRecoveryLink Entity, OutgoingEmailMessage Message)`, com comentário XML avisando que a mensagem leva o validator só em memória;
  - `IPasswordRecoveryLinkIssuer.cs` e `PasswordRecoveryLinkIssuer.cs`, com `public const string RecoveryPathSegment = "pass-recuvery";` e `Issue(Client client)`, que gera selector e validator pelo `ISecureLinkGenerator`, monta a entidade, chama `Add` e monta a URL a partir de `EmailOptions.PasswordRecoveryPageBaseUrl` (T031).
- [X] T036 [US1] Criar `src/RagAuth.Api/Services/PasswordRecovery/IPasswordRecoveryService.cs`, com `Task RequestRecoveryAsync(string email, CancellationToken)` e um comentário XML dizendo que o método nunca revela a situação da conta.
  - Criar `PasswordRecoveryService.cs` com a parte de pedido:
    1. Normalizar o e-mail e buscar com `FindConfirmedByEmailAsync`; se não encontrar, gravar o log 1602 e retornar.
    2. Executar `TryIssueLinkAsync`: abrir transação, `LockForUpdateAsync`, verificar `issuePolicy.CanIssueAsync`, revogar o link ativo, `Issue`, `SaveChanges` e `Commit`. Capturar `DbUpdateException` quando `UniqueConstraintViolation.IsViolationOf(…, UniqueIndexNames.ActivePasswordRecoveryLinkPerClient)` e devolver `null`.
    3. Gravar o log 1600 e enfileirar a mensagem.
  - Seguir a estrutura de `EmailConfirmationService.RequestNewLinkAsync`, com métodos de no máximo 20 linhas.
  - Quando a política recusa, ou quando ocorre a violação de unicidade, gravar o log 1601 (`PasswordRecoveryThrottled`) e não enfileirar nada.
  - Criar já nesta história a interface `src/RagAuth.Api/Services/PasswordRecovery/IPasswordRecoveryLinkIssuePolicy.cs` (`Task<bool> CanIssueAsync(int clientId, CancellationToken)`) e uma implementação provisória `AllowAllPasswordRecoveryLinkIssuePolicy.cs`, que sempre devolve `true`. Ela será substituída e apagada no T052.
  - Criar `src/RagAuth.Api/Services/PasswordRecovery/IMinimumResponseDurationGuard.cs` (`Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)`) e `StopwatchMinimumResponseDurationGuard.cs`. A implementação mede com `Stopwatch`, executa a operação e, se ela terminar antes de `EmailOptions.PasswordRecoveryMinimumResponseMilliseconds`, espera o tempo que falta com `Task.Delay`. `RequestRecoveryAsync` executa todo o pedido dentro de `RunAsync` (SC-003, plan decisão 7).
- [X] T037 [US1] Criar `src/RagAuth.Api/Services/PasswordRecoveryServiceExtensions.cs` com `AddPasswordRecovery()`, registrando `IPasswordRecoveryLinkIssuer`, `IPasswordRecoveryLinkIssuePolicy` (por enquanto com `AllowAllPasswordRecoveryLinkIssuePolicy`) e `IPasswordRecoveryService` como scoped, e `IMinimumResponseDurationGuard` → `StopwatchMinimumResponseDurationGuard` como singleton. Chamá-lo em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs`.
- [X] T038 [US1] Criar `src/RagAuth.Api/Endpoints/PasswordRecoveryEndpoints.cs` com `MapPasswordRecoveryEndpoints()`:
  - grupo `/api/v1/client` com `.AllowAnonymous()`;
  - `MapPost("/new-pass-recuvery-request", RequestPasswordRecoveryAsync)`, que chama o serviço e devolve `TypedResults.Json(ApiResponse.Succeeded<object?>(202, RecoveryRequestAcceptedMessage, null), statusCode: 202)`;
  - a constante `RecoveryRequestAcceptedMessage = "Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes."`.

  Mapear em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs`, ao lado de `MapEmailConfirmationEndpoints()`.
- [X] T039 [P] [US1] Criar `tests/RagAuth.UnitTests/Endpoints/PasswordRecoveryEndpointHandlersTests.cs`, seguindo o padrão de `EmailConfirmationEndpointHandlersTests.cs`: o handler do POST delega ao serviço e devolve 202 com a mensagem genérica.

**Checkpoint**: a US1 funciona sozinha (cenários 1 a 3 do quickstart).

---

## Phase 4: User Story 2 - Redefinir a senha pelo link (Priority: P1)

**Goal**: com o link válido, a página verifica o link (GET, sem consumir) e envia a nova senha com
a confirmação (PUT). A senha é trocada, o link é consumido, o aviso "senha alterada" é enviado e
não há login automático. A senha antiga deixa de valer e os tokens antigos continuam válidos.

**Independent Test**: pedir a recuperação (US1), chamar o GET duas vezes (200 nas duas), enviar o
PUT com confirmação diferente (400, link intacto) e depois o PUT válido (200, sem token). Fazer
login com a senha nova (200) e com a antiga (401), repetir o PUT (400) e verificar que o e-mail
"Sua senha foi alterada" não tem link.

### Tests for User Story 2 ⚠️

- [X] T040 [P] [US2] Criar `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryServiceVerifyTests.cs` cobrindo `VerifyLinkAsync`:
  - link ativo dentro do prazo, inclusive com `agora == ExpiresAt`: retorna sem alterar nada e sem `SaveChanges`;
  - `agora > ExpiresAt`: lança `ExpiredPasswordRecoveryLinkException` e grava o log 1604;
  - formato inválido (selector com 21 caracteres, validator com caractere fora de Base64Url), selector inexistente, validator errado, link `UsedAt != null`, link `RevokedAt != null`, link de conta com `EmailConfirmed == false`, ou hash calculado com a ação `email-confirmation`: lança `InvalidPasswordRecoveryLinkException` e grava o log 1605 sem dados do link;
  - a ordem das checagens: o hash é conferido **antes** de revelar se o link expirou.
- [X] T041 [P] [US2] Criar `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryServiceResetTests.cs` cobrindo `ResetPasswordAsync`:
  - link válido: abre transação, `LockForUpdateAsync`, `ReloadAsync`, define `PasswordHash = passwordHashing.HashPassword(client, newPassword)`, `PasswordChangedAt = agora` e `link.UsedAt = agora`, faz `SaveChanges` e `Commit`, grava o log 1603 e enfileira `OutgoingEmailMessage(Kind = PasswordChanged, ActionUrl = null, OccurredAt = agora)`;
  - `AccessCode` do cliente inalterado (FR-019);
  - depois do `ReloadAsync`, o link aparece com `UsedAt` preenchido (PUT concorrente): lança `InvalidPasswordRecoveryLinkException`, não grava a senha e não enfileira nada;
  - link expirado, inválido, usado ou revogado: as mesmas exceções do T040 e a senha não muda;
  - fila cheia: log 1509 com `PasswordChanged`, a redefinição continua com sucesso e não há exceção.
- [X] T042 [P] [US2] Acrescentar a `tests/RagAuth.IntegrationTests/PasswordRecoveryEndpointsTests.cs` as seções do GET e do PUT `/api/v1/client/{selector}/pass-recuvery/{validator}`:
  - GET válido: 200 com `data.status == "valid"` e mensagem `Link válido. Informe a nova senha.`; o `data` não traz nome, e-mail nem id; repetir o GET dá 200;
  - PUT com `newPasswordConfirmation` diferente: 400 com `error.errors.newPasswordConfirmation` e mensagem `A confirmação da senha deve ser igual à nova senha.`; o GET seguinte ainda dá 200;
  - PUT com `newPassword` `"curta"`, `"somenteletras"` ou com 129 caracteres: 400 com `error.errors.newPassword`; o link continua válido;
  - PUT válido: 200 com a mensagem `Senha alterada com sucesso. Você já pode fazer login com a nova senha.`, `data == null` e nenhum token no corpo;
  - `POST /api/v1/auth/login` com a senha nova dá 200; com a senha antiga dá 401 genérico;
  - um token obtido **antes** da troca continua acessando uma rota protegida (FR-019);
  - o e-mail `Sua senha foi alterada no …` chega sem `pass-recuvery` no corpo;
  - um segundo PUT com o mesmo link dá 400 inválido;
  - validator adulterado (último caractere trocado) no GET e no PUT dá 400 sem `errors`;
  - selector e validator de um link de **confirmação de e-mail** usados na rota de recuperação dão 400;
  - link de conta removida depois da emissão dá 400;
  - dois PUT simultâneos (`Task.WhenAll`) com o mesmo link: exatamente um 200 e um 400, e o login funciona só com a senha do PUT vencedor;
  - o `error.instance` dos erros não contém o selector nem o validator.
- [X] T043 [P] [US2] Acrescentar a `tests/RagAuth.UnitTests/Endpoints/PasswordRecoveryEndpointHandlersTests.cs`: o GET devolve 200 com `PasswordRecoveryLinkStatusResponse("valid")`, e o PUT devolve 200 com `data` nulo e a mensagem de sucesso.

### Implementation for User Story 2

- [X] T044 [P] [US2] Criar em `src/RagAuth.Api/Contracts/PasswordRecovery/`:
  - `ResetPasswordRequest.cs`, com:
    - `NewPassword`: `[Required(ErrorMessage = "Informe a nova senha.")]`, `[MinLength(8, ErrorMessage = AlphanumericPasswordAttribute.DefaultErrorMessage)]`, `[MaxLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]`, `[AlphanumericPassword]`;
    - `NewPasswordConfirmation`: `[Required(ErrorMessage = "Informe a confirmação da nova senha.")]`, `[Compare(nameof(NewPassword), ErrorMessage = "A confirmação da senha deve ser igual à nova senha.")]`;
  - `PasswordRecoveryLinkStatusResponse.cs`: `record(string Status)` com a constante `ValidStatus = "valid"`.

  Conferir que o `ValidationErrorsFormatter` existente devolve as chaves em camelCase (`newPasswordConfirmation`).
- [X] T045 [US2] Em `src/RagAuth.Api/Services/PasswordRecovery/IPasswordRecoveryService.cs` e `PasswordRecoveryService.cs`, adicionar `Task VerifyLinkAsync(string selector, string validator, CancellationToken)`, usando estes métodos privados reutilizáveis:
  - `FindMatchingLinkAsync`: `SecureLinkFormat.IsValid`, `FindBySelectorWithClientAsync` e `IsValidatorMatch(…, PasswordRecoveryLink.PasswordRecoveryPurpose, …)`; qualquer falha gera `InvalidPasswordRecoveryLinkException` com log 1605;
  - `EnsureLinkIsUsable`: `UsedAt`, `RevokedAt` ou `!Client.EmailConfirmed` geram inválido; `agora > ExpiresAt` gera `ExpiredPasswordRecoveryLinkException` com log 1604.
- [X] T046 [US2] Adicionar `Task ResetPasswordAsync(string selector, string validator, string newPassword, CancellationToken)` em `PasswordRecoveryService.cs`, com os passos:
  1. `FindMatchingLinkAsync` e `EnsureLinkIsUsable`.
  2. Abrir transação, `LockForUpdateAsync(link.ClientId)` e `ReloadAsync(link)`. Se `UsedAt` ou `RevokedAt` estiverem preenchidos, gerar inválido.
  3. `ApplyNewPassword`: grava o hash, `PasswordChangedAt` e `UsedAt`.
  4. `SaveChanges`, `Commit` e log 1603.
  5. Enfileirar `PasswordChanged`.

  Injetar `IPasswordHashingService`. Métodos de no máximo 20 linhas.
- [X] T047 [US2] Em `src/RagAuth.Api/Endpoints/PasswordRecoveryEndpoints.cs`, adicionar duas rotas, ambas com `.WithMetadata(new SensitiveRouteValuesMetadata())`:
  - `MapGet("/{selector}/pass-recuvery/{validator}", VerifyPasswordRecoveryLinkAsync)`: devolve 200 com `ValidLinkMessage = "Link válido. Informe a nova senha."` e `PasswordRecoveryLinkStatusResponse(ValidStatus)`;
  - `MapPut("/{selector}/pass-recuvery/{validator}", ResetPasswordAsync)`: recebe `ResetPasswordRequest`, devolve 200 com `PasswordResetMessage = "Senha alterada com sucesso. Você já pode fazer login com a nova senha."` e `data` nulo.

  Montar o modelo da rota com `PasswordRecoveryLinkIssuer.RecoveryPathSegment`. Os handlers só delegam ao serviço. Conferir que o `SensitiveRouteValuesMetadata` mascara os valores `selector` e `validator` (ele foi feito para os nomes da feature 002) e ajustar se necessário.

**Checkpoint**: US1 + US2 entregam o fluxo completo de recuperação (cenários 1 a 10 e 13 a 14 do
quickstart).

---

## Phase 5: User Story 3 - Link expirado e novo pedido (Priority: P2)

**Goal**: o link expirado é recusado com 410 e orientação. Um pedido novo gera link novo e
invalida os anteriores, respeitando 5 minutos entre emissões e no máximo 10 links em 24 horas por
conta, sempre com a mesma resposta genérica.

**Independent Test**: com o `AdjustableTimeProvider`, emitir um link e avançar 24 h + 1 s; o GET e
o PUT dão 410. Pedir de novo: o link antigo dá 400 e o novo dá 200. Dois pedidos com menos de 5
minutos de intervalo enviam só um e-mail, e o 11º pedido em 24 horas não envia e-mail.

### Tests for User Story 3 ⚠️

- [X] T048 [P] [US3] Criar `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryLinkIssuePolicyTests.cs`:
  - sem links anteriores: permite;
  - último link há 4 min 59 s: recusa; há exatamente 5 min: permite;
  - 9 links nas últimas 24 h: permite; 10 links: recusa;
  - um link emitido há 24 h + 1 s não conta;
  - os valores vêm de `EmailOptions.PasswordRecoveryIntervalMinutes` e `MaximumPasswordRecoveryLinksPerDay`, não de `NewLinkIntervalMinutes` nem de `MaximumNewLinksPerDay`.
- [X] T049 [P] [US3] Acrescentar a `tests/RagAuth.UnitTests/Services/PasswordRecovery/PasswordRecoveryServiceRequestTests.cs`:
  - política recusa: não emite, não enfileira, grava o log 1601 e não lança exceção;
  - `DbUpdateException` de violação de `ux_password_recovery_links_active_per_client`: tratada como limite, sem exceção;
  - outra `DbUpdateException`: é propagada.
- [X] T050 [P] [US3] Acrescentar a `tests/RagAuth.IntegrationTests/PasswordRecoveryEndpointsTests.cs`:
  - link expirado: avançar o relógio para `ExpiresAt` dá 200 no GET; avançar mais 1 s dá 410 no GET e no PUT com a mensagem `O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.`, e a senha não muda;
  - novo pedido depois de 5 min: o link anterior dá 400 no GET e o novo dá 200;
  - pedido dentro de 5 min: 202 idêntico e nenhum e-mail novo;
  - 11 pedidos espaçados de 5 min dentro de 24 h: só 10 e-mails; depois de a janela andar, um pedido novo volta a enviar;
  - o limite de recuperação é independente do limite de novo link de confirmação: pedir confirmação e recuperação para contas diferentes não interfere.

### Implementation for User Story 3

- [X] T051 [US3] Criar `src/RagAuth.Api/Services/PasswordRecovery/PasswordRecoveryLinkIssuePolicy.cs` (a interface já existe desde o T036), espelhando `ConfirmationLinkIssuePolicy`:
  - `HasMinimumIntervalElapsedAsync`: compara com `FindLatestIssuedAtAsync`;
  - `IsUnderDailyLimitAsync`: compara `CountIssuedSinceAsync(clientId, agora - 24 h)` com `MaximumPasswordRecoveryLinksPerDay`. Todos os links contam, porque não existe origem de cadastro.
- [X] T052 [US3] Em `src/RagAuth.Api/Services/PasswordRecoveryServiceExtensions.cs`, substituir o registro provisório do T036 por `PasswordRecoveryLinkIssuePolicy` e apagar `AllowAllPasswordRecoveryLinkIssuePolicy.cs`. O log 1601 já é gravado desde o T036.

**Checkpoint**: as três histórias funcionam de forma independente.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T053 [P] Criar `tests/RagAuth.IntegrationTests/CrossCutting/PasswordRecoveryLinkUniquenessTests.cs`, seguindo `ConfirmationLinkUniquenessTests.cs`: emitir pelo menos 10.000 links de recuperação e confirmar que não há nenhuma colisão de selector nem de URL (SC-005).
- [X] T054 [P] Em `tests/RagAuth.IntegrationTests/CrossCutting/SensitiveDataAuditTests.cs`, rodar o fluxo pedido, GET, PUT inválido, PUT válido e link expirado, e verificar nos logs capturados (`LogCaptureProvider`) e nos corpos de erro que não aparecem selector, validator, URL `pass-recuvery`, a senha nova, a senha antiga nem o e-mail sem máscara (SC-007, FR-021).
- [X] T055 [P] Em `tests/RagAuth.IntegrationTests/ProtectedRouteTests.cs`, ou no teste que já lista as rotas abertas, incluir as 3 rotas novas como acessíveis sem token e conferir que as demais continuam exigindo token (FR-020).
- [X] T056 [P] Em `tests/RagAuth.IntegrationTests/CrossCutting/ResponseTimeTests.cs`:
  - `POST /api/v1/client/new-pass-recuvery-request` para e-mail confirmado (uma conta por pedido, para não cair no limite) e para e-mail inexistente: p95 abaixo de 500 ms nos dois casos, **e** diferença entre as medianas dos dois casos abaixo de 50 ms (SC-003);
  - `GET` e `PUT /api/v1/client/{selector}/pass-recuvery/{validator}`: p95 abaixo de 500 ms (meta da feature 001);
  - em 20 pedidos para contas confirmadas, pelo menos 19 e-mails chegam ao `FakeEmailSender` em menos de 60 s (`WaitForEmailAsync`) (SC-001).
- [X] T057 [P] Em `tests/RagAuth.IntegrationTests/StartupConfigurationTests.cs`, verificar que `email:passwordRecoveryPageBaseUrl` vazio ou que não seja URL impede a inicialização com a mensagem em português do T015.
- [X] T058 Atualizar `README.md`: incluir as 3 rotas novas na lista de rotas, as chaves novas da seção `email` e uma referência a `specs/003-password-recovery/quickstart.md`.
- [X] T059 Rodar `dotnet test --settings coverlet.runsettings` e confirmar cobertura de linhas de pelo menos 90%. Revisar os arquivos novos e alterados, confirmando que nenhum método passa de 20 linhas e que nenhum endpoint acessa repositório ou `AppDbContext` (gate de PR da constituição).
- [ ] T060 Executar manualmente os cenários 1 a 14 de [quickstart.md](./quickstart.md) com o Resend real e anotar as divergências encontradas. Pendência para o dono do produto: validar os textos dos dois templates (research R12). A grafia `pass-recuvery` já foi confirmada no gate antes da fase 3.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (fase 1)**: não depende de nada. T001, T003 e T004 podem rodar em paralelo. O T002 vem
  depois do T001, porque os dois editam o csproj.
- **Foundational (fase 2)**: depende do Setup e bloqueia todas as histórias.
  - Ordem interna da generalização do e-mail: T005 → T006 → T007 → T008 → T009 → T010 → T011 → T012 → T013.
  - Gerador: T014.
  - Configuração, domínio e persistência: T015, depois T016–T018 [P], depois T019 → T020 → T021, e T022 e T023.
  - Testes: T024 e T025, depois T026–T029 [P], depois o checkpoint T030.
- **US1 (fase 3)**: depende da fase 2 e da confirmação da grafia `pass-recuvery` (gate depois do
  T030).
- **US2 (fase 4)**: depende da fase 2. Nos testes de integração, usa a US1 para obter um link. Nos
  testes unitários, é independente.
- **US3 (fase 5)**: depende da US1 (substitui a política provisória do pedido) e reaproveita o GET
  e o PUT da US2 nos testes de expiração.
- **Polish (fase 6)**: depende das histórias que forem entregues.

### Within Each User Story

- Os testes são escritos primeiro e falham antes da implementação.
- Ordem: DTOs → issuer e política → serviço → registro em DI → endpoints → testes de handler.
- Cada checkpoint é validado antes de passar para a próxima história.

### Parallel Opportunities

- Fase 1: T001, T003 e T004 (o T002 vem depois do T001).
- Fase 2: T016, T017 e T018; depois T026, T027, T028 e T029.
- US1: T031, T032 e T033 (testes); T034 em paralelo com T035.
- US2: T040, T041, T042 e T043 (testes); T044 em paralelo com o início do T045.
- US3: T048, T049 e T050.
- Polish: T053 a T058.

---

## Parallel Example: User Story 1

```bash
# Testes primeiro, em paralelo:
Task: "T031 PasswordRecoveryLinkIssuerTests em tests/RagAuth.UnitTests/Services/PasswordRecovery/"
Task: "T032 PasswordRecoveryServiceFixture + PasswordRecoveryServiceRequestTests"
Task: "T033 Seção POST em tests/RagAuth.IntegrationTests/PasswordRecoveryEndpointsTests.cs"

# Depois, em paralelo:
Task: "T034 PasswordRecoveryRequest em src/RagAuth.Api/Contracts/PasswordRecovery/"
Task: "T035 PasswordRecoveryLinkIssuer em src/RagAuth.Api/Services/PasswordRecovery/"
```

## Parallel Example: User Story 2

```bash
Task: "T040 PasswordRecoveryServiceVerifyTests"
Task: "T041 PasswordRecoveryServiceResetTests"
Task: "T042 Seções GET/PUT em PasswordRecoveryEndpointsTests.cs"
Task: "T043 Handlers GET/PUT em PasswordRecoveryEndpointHandlersTests.cs"
```

---

## Implementation Strategy

### MVP First (US1 + US2)

As duas histórias têm prioridade P1 e só entregam valor juntas: pedir o link sem conseguir usá-lo
não devolve o acesso ao cliente.

1. Concluir as fases 1 e 2, incluindo o checkpoint T030 sem regressões nas features 001 e 002.
2. Concluir a fase 3 (US1) e validar os cenários 1 a 3 do quickstart.
3. Concluir a fase 4 (US2) e validar os cenários 4 a 10, 13 e 14.
4. **Parar e validar.** Neste ponto o MVP pode ser publicado. Os limites de emissão ainda são
   provisórios, então publicar em produção só depois da US3.

### Incremental Delivery

1. Fundação: refatoração do e-mail e do gerador, testada contra as features 001 e 002.
2. US1 + US2: fluxo completo de recuperação.
3. US3: expiração orientada e limites de 5 minutos e de 10 links em 24 horas. Necessária antes de
   produção, porque protege contra envio abusivo de e-mails.
4. Polish: auditoria de dados sensíveis, unicidade, desempenho, README e cobertura.

---

## Notes

- [P] significa arquivos diferentes e nenhuma dependência de tarefa incompleta.
- A refatoração da fase 2 (T005 a T014) muda nomes de tipos usados pela feature 002. Faça um commit
  separado só com ela, depois do T030, para facilitar a revisão.
- Faça um commit por tarefa ou por grupo lógico.
- Não use `try/catch` em endpoints. Os erros vêm de `DomainException` e passam pelo handler global
  (research R13).
