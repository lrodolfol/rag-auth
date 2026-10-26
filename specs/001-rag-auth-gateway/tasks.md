---

description: "Task list for 001-rag-auth-gateway"
---

# Tasks: API de Autenticação e Controle de Acesso (Gateway RAG)

**Input**: Design documents from `/specs/001-rag-auth-gateway/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBRIGATÓRIOS — Constituição VI exige testes unitários xUnit para toda regra de negócio
(cobertura de linhas ≥ 90%) e ao menos um teste de integração por endpoint. Em cada história, os
testes são escritos primeiro e devem falhar antes da implementação.

**Organization**: tarefas agrupadas por história de usuário para implementação e teste independentes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história de usuário da tarefa (US1…US5)

## Regras válidas para TODAS as tarefas

- Métodos com no máximo 20 linhas; uma responsabilidade por classe (Constituição II).
- Nomes autoexplicativos, sem abreviações (Constituição III).
- Endpoints só delegam a serviços e traduzem o resultado; nunca acessam `AppDbContext` nem
  repositórios (Constituição I).
- Entrada e saída só por DTOs (Constituição IV); dependências por interface e injeção, sem `new`
  para serviços (Constituição V).
- Mensagens de erro e de log em português (Constituição VII).
- Nunca registrar em log senha, token JWT, `x-api-key` nem corpo de resposta da API RAG (FR-024,
  SC-006).
- Respostas sempre no envelope de [contracts/api-envelope.md](./contracts/api-envelope.md).

## Path Conventions

- API: `src/RagAuth.Api/`
- Testes unitários: `tests/RagAuth.UnitTests/` (espelha as pastas da API)
- Testes de integração: `tests/RagAuth.IntegrationTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialização da solução, dos projetos e da configuração por ambiente

- [ ] T001 Criar `Directory.Build.props` na raiz com `TargetFramework` `net10.0`, `Nullable` `enable`, `ImplicitUsings` `enable`, `TreatWarningsAsErrors` `true` e `LangVersion` `latest`
- [ ] T002 Criar `RagAuth.slnx` na raiz e o projeto web vazio `src/RagAuth.Api/RagAuth.Api.csproj` (`dotnet new web`); apagar o `appsettings.Development.json` gerado; adicionar o projeto à solução
- [ ] T003 Adicionar a `src/RagAuth.Api/RagAuth.Api.csproj` os pacotes `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x, `Microsoft.EntityFrameworkCore.Design` 10.x (`PrivateAssets=all`) e `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x — únicas dependências permitidas na API (justificadas em plan.md)
- [ ] T004 [P] Criar `tests/RagAuth.UnitTests/RagAuth.UnitTests.csproj` (`dotnet new xunit`) referenciando `RagAuth.Api`, com os pacotes `Moq` e `coverlet.collector`; adicionar à solução
- [ ] T005 [P] Criar `tests/RagAuth.IntegrationTests/RagAuth.IntegrationTests.csproj` (`dotnet new xunit`) referenciando `RagAuth.Api`, com os pacotes `Microsoft.AspNetCore.Mvc.Testing` 10.x, `Testcontainers.PostgreSql` e `coverlet.collector`; adicionar à solução
- [ ] T006 [P] Criar as pastas da API conforme plan.md: `Endpoints/`, `Contracts/Auth/`, `Contracts/Business/`, `Contracts/Validation/`, `Domain/Entities/`, `Domain/Exceptions/`, `Services/Auth/`, `Services/Business/`, `Services/Clients/`, `Infrastructure/Persistence/`, `Infrastructure/RagApi/`, `Security/`, `Observability/`, `ErrorHandling/`, `Configuration/` em `src/RagAuth.Api/`
- [ ] T007 [P] Criar `src/RagAuth.Api/appsettings.json` (local) com as seções em camelCase: `database` (`host`: `localhost`, `port`: `5432`, `name`: `rag_auth`, `username`: `rag_auth`), `jwt` (`issuer`: `rag-auth`, `audience`: `rag-api`, `expirationMinutes`: `60`), `ragApi` (`baseUrl`: `http://localhost:8000`, `businessPath`: `/business`, `timeoutSeconds`: `10`) e `Logging` (nível mínimo `Information`, `Microsoft.AspNetCore`: `Warning`); nenhum segredo
- [ ] T008 [P] Criar `src/RagAuth.Api/appsettings.uat.json` e `src/RagAuth.Api/appsettings.prod.json` com as mesmas chaves de `database`, `jwt` e `ragApi` e valores de exemplo do ambiente remoto (marcados para substituição), sem nenhum segredo
- [ ] T009 [P] Criar `.editorconfig` na raiz com convenções C# (indentação de 4 espaços, `var` quando o tipo é aparente, namespaces com escopo de arquivo, campos privados `_camelCase`) e acrescentar `bin/`, `obj/`, `TestResults/` e `coverage*/` ao `.gitignore`

**Checkpoint**: `dotnet build RagAuth.slnx` compila sem avisos.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Configuração, persistência, envelope de erros, correlation-id, composição e infraestrutura
de testes de que todas as histórias dependem

**⚠️ CRITICAL**: nenhuma história começa antes do fim desta fase

### Configuração e segredos

- [ ] T010 [P] Criar as classes de options em `src/RagAuth.Api/Configuration/`: `DatabaseOptions.cs` (`Host`, `Port`, `Name`, `Username` obrigatórios; `Password` obrigatório, preenchido por variável de ambiente), `JwtOptions.cs` (`Issuer` e `Audience` obrigatórios; `ExpirationMinutes` `[Range(1, 1440)]` padrão 60; `SigningKey` obrigatório com no mínimo 32 caracteres, preenchido por variável de ambiente) e `RagApiOptions.cs` (`BaseUrl` obrigatório e URL absoluta; `BusinessPath` padrão `/business`; `TimeoutSeconds` `[Range(1, 120)]` padrão 10; `ApiKey` obrigatório, preenchido por variável de ambiente); todas as mensagens de validação em português
- [ ] T011 Criar `src/RagAuth.Api/Configuration/EnvironmentSecretNames.cs` (constantes `DATABASE_PASSWORD`, `JWT_SIGNING_KEY`, `RAG_API_KEY`) e `src/RagAuth.Api/Configuration/OptionsServiceExtensions.cs` com `AddApplicationOptions(IConfiguration)`: vincular `database`, `jwt` e `ragApi`, aplicar `PostConfigure` lendo cada segredo de `configuration[EnvironmentSecretNames.X]`, e registrar `ValidateDataAnnotations().ValidateOnStart()` — segredo ausente impede a inicialização e o motivo vai para o log (edge case da spec, research R12)

### Domínio

- [ ] T012 [P] Criar `src/RagAuth.Api/Domain/Entities/IHasTimestamps.cs` (`CreatedAt`, `UpdatedAt` como `DateTimeOffset`) e `src/RagAuth.Api/Domain/Entities/Client.cs` (`Id` int, `AccessCode`, `Name`, `Email`, `PasswordHash`, `CreatedAt`, `UpdatedAt`, `DeletedAt` anulável, navegação `Business?`)
- [ ] T013 [P] Criar `src/RagAuth.Api/Domain/Entities/Business.cs` implementando `IHasTimestamps` (`Id` int, `ClientId` int, `Client`, `Name`, `Description`, `Email?`, `Telephone?`, `Address?`, `Details?`, `CreatedAt`, `UpdatedAt`)
- [ ] T014 [P] Criar `src/RagAuth.Api/Domain/Exceptions/DomainException.cs`: classe abstrata com `StatusCode` (int), `Title` (string, PT) e a mensagem em português em `Message`

### Persistência

- [ ] T015 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/Configurations/ClientConfiguration.cs` (`IEntityTypeConfiguration<Client>`): tabela `clients`; `id` identity; `accessCode` `varchar(12)` obrigatório com **índice único total** (inclui removidos, FR-025); `name` `varchar(150)` obrigatório; `email` `varchar(254)` obrigatório com **índice único parcial** `WHERE "deletedAt" IS NULL` (FR-003); `passwordHash` `text` obrigatório; `createdAt`/`updatedAt` `timestamptz` obrigatórios; `deletedAt` `timestamptz` anulável; global query filter `DeletedAt == null`; relação 1:0..1 com `Business`
- [ ] T016 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/Configurations/BusinessConfiguration.cs`: tabela `businesses`; `id` identity; `clientId` FK → `clients.id` com **índice único** (1:1, FR-016) e `ON DELETE CASCADE`; `name` `varchar(150)` obrigatório; `description` `varchar(1000)` obrigatório; `email` `varchar(254)` anulável; `telephone` `varchar(11)` anulável; `address` `varchar(300)` anulável; `details` `varchar(2000)` anulável; `createdAt`/`updatedAt` `timestamptz` obrigatórios; dar nomes fixos aos índices únicos (`ix_clients_accessCode`, `ix_clients_email_active`, `ix_businesses_clientId`) e expô-los como constantes em `src/RagAuth.Api/Infrastructure/Persistence/UniqueIndexNames.cs`
- [ ] T017 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/TimestampInterceptor.cs` (`SaveChangesInterceptor` com `TimeProvider` injetado): em `Added` preencher `CreatedAt` e `UpdatedAt`; em `Modified` atualizar só `UpdatedAt`, para toda entidade `IHasTimestamps`
- [ ] T018 Criar `src/RagAuth.Api/Infrastructure/Persistence/AppDbContext.cs`: `DbSet<Client> Clients`, `DbSet<Business> Businesses`, `ApplyConfigurationsFromAssembly` e convenção que nomeia cada coluna em camelCase a partir do nome da propriedade (sem pacote extra, research R6) (depende de T015, T016)
- [ ] T019 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/UniqueConstraintViolation.cs` com `IsViolationOf(DbUpdateException exception, string indexName)`: verdadeiro quando a causa é `PostgresException` com `SqlState` `23505` e `ConstraintName` igual ao índice
- [ ] T020 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/IUnitOfWork.cs` (`SaveChangesAsync`, `BeginTransactionAsync` retornando `IUnitOfWorkTransaction` com `CommitAsync`, `RollbackAsync` e `IAsyncDisposable`) e `EntityFrameworkUnitOfWork.cs` sobre `AppDbContext.Database`
- [ ] T021 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IClientRepository.cs` e `EntityFrameworkClientRepository.cs`: `FindActiveByEmailAsync(string normalizedEmail)`, `FindActiveByIdAsync(int clientId)` (entidade rastreada), `Add(Client client)`
- [ ] T022 [P] Criar `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IBusinessRepository.cs` e `EntityFrameworkBusinessRepository.cs`: `FindByClientIdAsync(int clientId)` (rastreada), `Add(Business business)`, `Remove(Business business)`
- [ ] T023 Criar `src/RagAuth.Api/Infrastructure/Persistence/PersistenceServiceExtensions.cs` com `AddPersistence()`: montar a connection string com `NpgsqlConnectionStringBuilder` a partir de `DatabaseOptions` (senha de `DATABASE_PASSWORD`), registrar `AppDbContext` com `TimestampInterceptor`, repositórios, `IUnitOfWork` e `TimeProvider.System` (depende de T017–T022)
- [ ] T024 Gerar a migration inicial com `dotnet ef migrations add InitialCreate --project src/RagAuth.Api --output-dir Infrastructure/Persistence/Migrations` e conferir no arquivo gerado as colunas camelCase, o índice parcial de e-mail e os três índices únicos nomeados (depende de T023)

### Envelope de resposta e erros

- [ ] T025 [P] Criar `src/RagAuth.Api/Contracts/ApiResponse.cs`: record `ApiResponse<TData>(int StatusCode, bool Success, string Message, TData? Data, ProblemDetails? Error)` e fábrica estática `ApiResponse.Succeeded(statusCode, message, data)`, conforme [contracts/api-envelope.md](./contracts/api-envelope.md)
- [ ] T026 [P] Criar `src/RagAuth.Api/ErrorHandling/ApiEnvelopeProblemDetailsWriter.cs` (`IProblemDetailsWriter`): escreve `application/json` com `statusCode` = status, `success` = `false`, `message` = `detail` ou `title`, `data` = `null` e `error` = o `ProblemDetails` recebido, acrescentando a extensão `correlationId` (research R1)
- [ ] T027 [P] Criar `src/RagAuth.Api/ErrorHandling/DomainExceptionHandler.cs` (`IExceptionHandler`): `DomainException` → `ProblemDetails` com status, título e detalhe da exceção; `BadHttpRequestException` → 400 "O corpo da requisição é inválido ou está mal formado."; qualquer outra → 500 "Ocorreu um erro inesperado. Tente novamente mais tarde." com log `Error` (sem detalhes internos na resposta); sempre escrevendo via `IProblemDetailsService`
- [ ] T028 Criar `src/RagAuth.Api/ErrorHandling/ErrorHandlingServiceExtensions.cs` com `AddErrorHandling()`: `AddProblemDetails`, registrar `ApiEnvelopeProblemDetailsWriter` como primeiro `IProblemDetailsWriter`, `AddExceptionHandler<DomainExceptionHandler>` e `RouteHandlerOptions.ThrowOnBadRequest = true` (depende de T026, T027)

### Observabilidade

- [ ] T029 [P] Criar `src/RagAuth.Api/Observability/ICorrelationIdAccessor.cs`, `HttpContextCorrelationIdAccessor.cs` (lê o id guardado em `HttpContext.Items` via `IHttpContextAccessor`) e `CorrelationIdMiddleware.cs`: gera um GUID no início de cada requisição, guarda no `HttpContext`, devolve no cabeçalho de resposta `X-Correlation-Id` e abre `ILogger.BeginScope` com `CorrelationId` até o fim da requisição (research R13)
- [ ] T030 [P] Criar `src/RagAuth.Api/Observability/LogMessages.cs` (classe estática parcial com `[LoggerMessage]`) com a mensagem inicial `UnexpectedErrorOccurred` (Error) — as histórias acrescentam as suas — e `src/RagAuth.Api/Observability/ObservabilityServiceExtensions.cs` com `AddObservability()`: `AddSimpleConsole` com `IncludeScopes = true`, `AddHttpContextAccessor`, registro do accessor

### Composição

- [ ] T031 Criar `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs` com `AddApplication(WebApplicationBuilder)` (chama `AddApplicationOptions`, `AddPersistence`, `AddErrorHandling`, `AddObservability`, `AddValidation()`, `AddAuthorization()`) e `MapApplication(WebApplication)` com o pipeline na ordem `CorrelationIdMiddleware` → `UseExceptionHandler` → `UseStatusCodePages` → `UseAuthentication` → `UseAuthorization` → mapeamento de endpoints; reescrever `src/RagAuth.Api/Program.cs` só com `builder.AddApplication()`, `app.MapApplication()`, `app.Run()` e `public partial class Program;` (depende de T011, T023, T028, T030)

### Infraestrutura de testes

- [ ] T032 Criar `tests/RagAuth.IntegrationTests/Infrastructure/RagAuthApiFactory.cs` (`WebApplicationFactory<Program>`, `IAsyncLifetime`): sobe `PostgreSqlContainer` (`postgres:17`), injeta via `UseSetting` as chaves `database:*`, `DATABASE_PASSWORD`, `JWT_SIGNING_KEY` e `RAG_API_KEY` de teste e `ragApi:timeoutSeconds` = `1`, aplica as migrations e expõe `CreateClient()` e um helper para limpar as tabelas entre testes; criar `tests/RagAuth.IntegrationTests/Infrastructure/IntegrationTestCollection.cs` (`[CollectionDefinition]` compartilhando a factory)
- [ ] T033 [P] Criar `tests/RagAuth.IntegrationTests/Infrastructure/LogCaptureProvider.cs` (`ILoggerProvider` que guarda nível, mensagem formatada e escopos de cada entrada) e registrá-lo na `RagAuthApiFactory`
- [ ] T034 [P] Criar `tests/RagAuth.IntegrationTests/Infrastructure/ApiEnvelopeAssertions.cs` com helpers para ler o envelope (`ApiResponse<JsonElement>`) e verificar `statusCode`, `success`, `message`, `data`/`error` e `error.errors[campo]`

### Testes da fundação

- [ ] T035 [P] Testes unitários em `tests/RagAuth.UnitTests/ErrorHandling/ApiEnvelopeProblemDetailsWriterTests.cs`: envelope com `success` = `false`, `data` = `null`, `message` vindo de `detail` (ou `title` se não houver), `error` igual ao `ProblemDetails` e `correlationId` presente
- [ ] T036 [P] Testes unitários em `tests/RagAuth.UnitTests/ErrorHandling/DomainExceptionHandlerTests.cs`: mapeamento de `DomainException` (exceção de teste derivada), `BadHttpRequestException` → 400 e exceção genérica → 500 com mensagem genérica e log `Error` (Moq em `IProblemDetailsService` e `ILogger`)
- [ ] T037 [P] Testes unitários em `tests/RagAuth.UnitTests/Observability/CorrelationIdMiddlewareTests.cs`: gera GUID novo a cada requisição, grava no `HttpContext`, escreve o cabeçalho `X-Correlation-Id` e chama o próximo middleware dentro do escopo de log
- [ ] T038 [P] Teste de integração em `tests/RagAuth.IntegrationTests/StartupConfigurationTests.cs`: para cada uma das variáveis `DATABASE_PASSWORD`, `JWT_SIGNING_KEY` e `RAG_API_KEY` ausente, a aplicação não inicia (`OptionsValidationException`) e a mensagem cita a variável
- [ ] T039 [P] Teste de integração em `tests/RagAuth.IntegrationTests/PersistenceSchemaTests.cs`: a migration cria `clients` e `businesses` com colunas camelCase; `createdAt`/`updatedAt` são preenchidos na inserção e só `updatedAt` muda na alteração (`TimestampInterceptor`); inserir dois clientes ativos com o mesmo e-mail viola `ix_clients_email_active`, mas é permitido se um deles tiver `deletedAt`

**Checkpoint**: fundação pronta — `dotnet test` passa e as histórias podem começar.

---

## Phase 3: User Story 1 - Cadastro e login do cliente (Priority: P1) 🎯 MVP

**Goal**: o cliente se cadastra (recebe `clientId` e código de acesso) e faz login (recebe JWT,
expiração, `clientId`, código de acesso e o seu negócio ou `null`).

**Independent Test**: cadastrar, fazer login e receber um token válido; login com senha errada é
rejeitado com 401 genérico ([contracts/auth-endpoints.md](./contracts/auth-endpoints.md)).

### Tests for User Story 1 ⚠️

> Escrever primeiro e garantir que falham antes da implementação.

- [ ] T040 [P] [US1] Testes unitários em `tests/RagAuth.UnitTests/Services/Auth/RandomAccessCodeGeneratorTests.cs`: código com exatamente 12 caracteres de `[A-Za-z0-9]`; 10.000 códigos gerados sem repetição
- [ ] T041 [P] [US1] Testes unitários em `tests/RagAuth.UnitTests/Services/Auth/IdentityPasswordHashingServiceTests.cs`: o hash é diferente da senha, duas chamadas com a mesma senha geram hashes diferentes (salt), verificação aceita a senha correta e recusa a errada
- [ ] T042 [P] [US1] Testes unitários em `tests/RagAuth.UnitTests/Services/Auth/JwtAccessTokenIssuerTests.cs` (`TimeProvider` via Moq): token HS256 com `sub` = código de acesso, `client_id` = id do cliente, `jti`, `iat`, `iss` e `aud` das options e `exp` = agora + `ExpirationMinutes`; `ExpiresAt` retornado igual ao `exp`
- [ ] T043 [P] [US1] Testes unitários em `tests/RagAuth.UnitTests/Contracts/Validation/AlphanumericPasswordAttributeTests.cs`: aceita `senhaSegura1`; recusa só letras, só números e vazio, com a mensagem "A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número."
- [ ] T044 [P] [US1] Testes unitários em `tests/RagAuth.UnitTests/Services/Auth/AuthServiceTests.cs` (Moq em repositório, unit of work, hasher, gerador, emissor e `IBusinessService`): cadastro normaliza o e-mail (trim + minúsculas) e o nome (trim); e-mail ativo existente → `EmailAlreadyRegisteredException`; violação de `ix_clients_email_active` no `SaveChanges` → `EmailAlreadyRegisteredException`; violação de `ix_clients_accessCode` → gera novo código e tenta de novo, até 5 tentativas, depois `AccessCodeGenerationException`; login com e-mail inexistente, conta removida ou senha errada → `InvalidCredentialsException`; login correto devolve token, expiração, `clientId`, código de acesso e o negócio (ou `null`) vindo de `IBusinessService`
- [ ] T045 [P] [US1] Testes de integração em `tests/RagAuth.IntegrationTests/AuthEndpointsTests.cs`: `POST /api/v1/auth/register` válido → 201 com `data.clientId`, `data.accessCode` (12 caracteres), `data.email` em minúsculas, sem campo de senha, e cabeçalho `X-Correlation-Id`; mesmo e-mail com outra caixa → 409; senha `abc` → 400 no envelope com `error.errors.password` (confirma a validação nativa passando pelo `IProblemDetailsService`, research R2); JSON malformado e corpo ausente → 400; dois cadastros simultâneos com o mesmo e-mail → um 201 e um 409; `POST /api/v1/auth/login` correto → 200 com `accessToken`, `tokenType` = `Bearer`, `expiresAt`, `clientId`, `accessCode` e `business` = `null`; senha errada e e-mail inexistente → 401 com a mesma mensagem "E-mail ou senha inválidos."

### Implementation for User Story 1

- [ ] T046 [P] [US1] Criar em `src/RagAuth.Api/Domain/Exceptions/` as exceções `EmailAlreadyRegisteredException` (409, "Já existe uma conta cadastrada com este e-mail."), `InvalidCredentialsException` (401, "E-mail ou senha inválidos.") e `AccessCodeGenerationException` (500, "Não foi possível concluir o cadastro. Tente novamente."), derivadas de `DomainException`
- [ ] T047 [P] [US1] Criar `src/RagAuth.Api/Contracts/Validation/AlphanumericPasswordAttribute.cs`: válido quando há ao menos uma letra e um dígito
- [ ] T048 [P] [US1] Criar `src/RagAuth.Api/Contracts/Auth/RegisterClientRequest.cs` (`Name` `[Required]` `[MaxLength(150)]`; `Email` `[Required]` `[EmailAddress]` `[MaxLength(254)]`; `Password` `[Required]` `[MinLength(8)]` `[MaxLength(128)]` `[AlphanumericPassword]`) e `LoginRequest.cs` (`Email` `[Required]` `[EmailAddress]`; `Password` `[Required]`), todas as mensagens em português
- [ ] T049 [P] [US1] Criar `src/RagAuth.Api/Contracts/Auth/RegisteredClientResponse.cs` (`ClientId`, `AccessCode`, `Name`, `Email`, `CreatedAt`), `src/RagAuth.Api/Contracts/Auth/LoginResponse.cs` (`AccessToken`, `TokenType`, `ExpiresAt`, `ClientId`, `AccessCode`, `Business` anulável) e `src/RagAuth.Api/Contracts/Business/BusinessResponse.cs` (`Name`, `Description`, `Email`, `Telephone`, `Address`, `Details`, `CreatedAt`, `UpdatedAt`)
- [ ] T050 [P] [US1] Criar `src/RagAuth.Api/Services/Auth/IAccessCodeGenerator.cs` e `RandomAccessCodeGenerator.cs` usando `RandomNumberGenerator.GetString` com o alfabeto `A-Z a-z 0-9` e tamanho 12 (research R5)
- [ ] T051 [P] [US1] Criar `src/RagAuth.Api/Services/Auth/IPasswordHashingService.cs` e `IdentityPasswordHashingService.cs` sobre `PasswordHasher<Client>` (research R3)
- [ ] T052 [P] [US1] Criar `src/RagAuth.Api/Security/ClientClaimTypes.cs` (constante `ClientId` = `client_id`) e `src/RagAuth.Api/Services/Auth/IAccessTokenIssuer.cs` + `JwtAccessTokenIssuer.cs` com `JsonWebTokenHandler`, HS256 com `JwtOptions.SigningKey`, claims `sub`, `client_id`, `jti`, `iat`, `exp`, `iss`, `aud` e `TimeProvider` injetado; retorna o token e `ExpiresAt` (research R4)
- [ ] T053 [P] [US1] Criar `src/RagAuth.Api/Services/Business/BusinessMapper.cs` (`ToResponse(Business)` → `BusinessResponse`) e `src/RagAuth.Api/Services/Business/IBusinessService.cs` + `BusinessService.cs` com, por enquanto, só `FindOwnedBusinessAsync(int clientId)` → `BusinessResponse?` lido de `IBusinessRepository` (a US3 acrescenta criar, alterar e remover)
- [ ] T054 [US1] Acrescentar a `src/RagAuth.Api/Observability/LogMessages.cs` as mensagens `ClientRegistered` (Information, só o `clientId`) e `LoginFailed` (Warning, e-mail mascarado como `a***@exemplo.com`, nunca a senha)
- [ ] T055 [US1] Criar `src/RagAuth.Api/Services/Auth/IAuthService.cs` e `AuthService.cs`: `RegisterAsync` (normaliza, checa e-mail ativo, gera código, faz hash, grava, trata violações de índice único com novas tentativas para o código) e `LoginAsync` (busca conta ativa, verifica a senha, emite o token, busca o negócio via `IBusinessService`), em métodos curtos por etapa (depende de T046–T054)
- [ ] T056 [US1] Criar `src/RagAuth.Api/Endpoints/AuthEndpoints.cs` com `MapAuthEndpoints`: grupo `/api/v1/auth` com `AllowAnonymous`; `POST /register` → 201 `ApiResponse` "Cadastro realizado com sucesso."; `POST /login` → 200 `ApiResponse` "Login realizado com sucesso."; só delega ao `IAuthService`
- [ ] T057 [US1] Criar `src/RagAuth.Api/Services/AuthServiceExtensions.cs` com `AddAuthServices()` (gerador, hasher, emissor, `IAuthService`, `IBusinessService`) e chamá-lo em `AddApplication`; mapear `MapAuthEndpoints` em `MapApplication` em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs`

**Checkpoint**: US1 funciona e é testável sozinha (cenários 1–5 do quickstart).

---

## Phase 4: User Story 2 - Proteção de rotas por token (Priority: P1)

**Goal**: toda rota exceto cadastro e login exige JWT válido de conta ativa; `{clientId}` da rota
precisa ser igual à claim `client_id`, senão 403.

**Independent Test**: em uma rota protegida de teste, verificar 401 sem token, com token adulterado,
expirado ou de conta removida; 403 com `{clientId}` de outro cliente ou não numérico; 200 com token
válido.

### Tests for User Story 2 ⚠️

- [ ] T058 [P] [US2] Testes unitários em `tests/RagAuth.UnitTests/Services/Clients/ClientSessionValidatorTests.cs`: válido quando a conta do `client_id` existe, está ativa e tem o mesmo código de acesso do `sub`; inválido quando a conta não existe, foi removida, o código difere ou a claim falta / não é numérica
- [ ] T059 [P] [US2] Testes unitários em `tests/RagAuth.UnitTests/Security/ClientRouteMatchesTokenHandlerTests.cs`: sucesso quando o valor bruto de `clientId` da rota é igual à claim `client_id`; falha quando é de outro cliente, não numérico (`abc`), ausente ou quando falta a claim
- [ ] T060 [US2] Criar `tests/RagAuth.IntegrationTests/Infrastructure/ProtectedProbeStartupFilter.cs`: `IStartupFilter` registrado só na `RagAuthApiFactory` que mapeia `GET /api/v1/{clientId}/probe` com a policy `ClientRouteOwner`, retornando 200 — rota de teste usada para validar a proteção sem depender da US3
- [ ] T061 [US2] Testes de integração em `tests/RagAuth.IntegrationTests/ProtectedRouteTests.cs` sobre a rota de teste: sem `Authorization` → 401; `Authorization` sem prefixo `Bearer` → 401; assinatura adulterada → 401; token malformado → 401; token expirado, emissor ou público errados (gerados por helper com `JsonWebTokenHandler` e a chave de teste) → 401; conta com `deletedAt` preenchido direto no banco → 401; `{clientId}` de outro cliente → 403; `{clientId}` = `abc` → 403; token válido e próprio `{clientId}` → 200; todas as falhas no envelope com mensagem em português e registradas em log `Warning`

### Implementation for User Story 2

- [ ] T062 [P] [US2] Criar `src/RagAuth.Api/Services/Clients/IClientSessionValidator.cs` e `ClientSessionValidator.cs`: recebe o `ClaimsPrincipal`, lê `client_id` e `sub`, e confirma via `IClientRepository.FindActiveByIdAsync` que a conta está ativa e o código de acesso confere (FR-010)
- [ ] T063 [P] [US2] Criar `src/RagAuth.Api/Security/AuthorizationPolicies.cs` (constante `ClientRouteOwner`), `src/RagAuth.Api/Security/ClientRouteMatchesTokenRequirement.cs` e `src/RagAuth.Api/Security/ClientRouteMatchesTokenHandler.cs`: compara o valor bruto do parâmetro de rota `clientId` (via `HttpContext` do recurso de autorização) com a claim `client_id` usando comparação ordinal (research R10)
- [ ] T064 [US2] Acrescentar a `src/RagAuth.Api/Observability/LogMessages.cs` as mensagens `AuthenticationRejected` (Warning, motivo sem o token) e `AccessForbidden` (Warning, `clientId` do token e caminho da requisição)
- [ ] T065 [US2] Criar `src/RagAuth.Api/Security/JwtBearerEventsSetup.cs`: `OnTokenValidated` chama `IClientSessionValidator` e falha a autenticação se inválido; `OnChallenge` escreve 401 "Token ausente, inválido ou expirado." via `IProblemDetailsService` e registra log; `OnForbidden` escreve 403 "Você não tem permissão para acessar este recurso." via `IProblemDetailsService` e registra log (depende de T062, T064)
- [ ] T066 [US2] Criar `src/RagAuth.Api/Security/AuthenticationServiceExtensions.cs` com `AddJwtAuthentication()`: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer` com `MapInboundClaims = false`, `TokenValidationParameters` (valida emissor, público, tempo de vida e chave de assinatura das `JwtOptions`, `ClockSkew` de 30 s) e os eventos de T065; `AddAuthorization` com `FallbackPolicy` = usuário autenticado (FR-012) e a policy `ClientRouteOwner`; registrar `ClientRouteMatchesTokenHandler` e `IClientSessionValidator`; substituir o `AddAuthorization()` de `AddApplication` em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs` por `AddJwtAuthentication()` (depende de T063, T065)

**Checkpoint**: US1 e US2 funcionam; nenhuma rota além de cadastro e login aceita requisição sem token.

---

## Phase 5: User Story 3 - Gerenciamento do negócio do cliente (Priority: P2)

**Goal**: o cliente autenticado cria, altera e remove o seu único negócio; cada operação só é
confirmada no banco após 2xx da API RAG; o login devolve o negócio.

**Independent Test**: com a API RAG simulada, executar criar → login (negócio na resposta) →
alterar → remover, verificando que cada chamada à RAG levou `x-api-key`, `Authorization` e
`user_code` ([contracts/business-endpoints.md](./contracts/business-endpoints.md),
[contracts/rag-api-outbound.md](./contracts/rag-api-outbound.md)).

### Tests for User Story 3 ⚠️

- [ ] T067 [US3] Criar `tests/RagAuth.IntegrationTests/Infrastructure/FakeRagApiHandler.cs` (`HttpMessageHandler` que registra cada requisição — método, caminho, cabeçalhos e corpo — e responde conforme configuração por teste: status, atraso ou exceção) e substituir o handler primário do cliente tipado `IRagApiClient` na `RagAuthApiFactory` por ele
- [ ] T068 [P] [US3] Testes unitários em `tests/RagAuth.UnitTests/Contracts/Validation/BrazilianPhoneAttributeTests.cs` e `SingleLineAttributeTests.cs`: telefone aceita `(11) 98765-4321`, `1132654321` e nulo; recusa 9 ou 12 dígitos e letras com "Informe um telefone com DDD, com 10 ou 11 dígitos."; linha única recusa `\r` e `\n` com "O endereço deve ser informado em uma única linha."
- [ ] T069 [P] [US3] Testes unitários em `tests/RagAuth.UnitTests/Services/Business/BusinessInputNormalizerTests.cs`: telefone fica só com dígitos; textos sofrem trim; opcionais vazios ou só com espaços viram `null`
- [ ] T070 [P] [US3] Testes unitários em `tests/RagAuth.UnitTests/Infrastructure/RagApi/RagApiClientTests.cs` (handler fake local): `POST`, `PUT` e `DELETE` em `BusinessPath`; corpo em snake_case `{ "user_code", "request": { name, description, email, telephone, address, details } }` e, no DELETE, só `{ "user_code" }`; 2xx conclui sem exceção; não-2xx lança `RagApiIntegrationException`
- [ ] T071 [P] [US3] Testes unitários em `tests/RagAuth.UnitTests/Infrastructure/RagApi/RagApiHeaderHandlersTests.cs`: `ApiKeyHeaderHandler` adiciona `x-api-key` com o valor das options; `ClientTokenForwardingHandler` repassa o `Authorization: Bearer` da requisição atual; `CorrelationIdHeaderHandler` adiciona `X-Correlation-Id`
- [ ] T072 [P] [US3] Testes unitários em `tests/RagAuth.UnitTests/Services/Business/BusinessServiceTests.cs` (Moq em repositórios, unit of work, transação e `IRagApiClient`): criar com negócio existente → `BusinessAlreadyExistsException` sem chamar a RAG; violação de `ix_businesses_clientId` no `SaveChanges` → `BusinessAlreadyExistsException` sem chamar a RAG; criar com sucesso → grava, chama a RAG com o código de acesso e os dados normalizados, faz commit e devolve `BusinessResponse`; RAG lança → rollback e a exceção é propagada; alterar ou remover sem negócio → `BusinessNotFoundException` sem chamar a RAG; alterar e remover com sucesso → commit após a RAG; RAG falha → rollback
- [ ] T073 [US3] Testes de integração em `tests/RagAuth.IntegrationTests/BusinessEndpointsTests.cs`: `POST /api/v1/{clientId}/business` válido → 201 com `BusinessResponse`, telefone só com dígitos, e a RAG recebeu `x-api-key`, `Authorization: Bearer <token do cliente>`, `X-Correlation-Id` igual ao da resposta e `user_code` = código de acesso (SC-004); segundo POST → 409 sem chamada à RAG; cada regra violada (nome vazio ou > 150, descrição vazia ou > 1.000, e-mail inválido, endereço > 300 ou com quebra de linha, telefone com 9 dígitos, detalhes > 2.000) → 400 com o campo em `error.errors` e sem chamada à RAG (SC-003); `PUT` e `DELETE` antes de criar → 404 sem chamada à RAG; `PUT` válido → 200 e novo login mostra os dados alterados; `DELETE` → 200 "Negócio removido com sucesso." e novo login mostra `business` = `null`; novo POST após remover → 201; RAG respondendo 500 no POST → nada gravado (login com `business` = `null`); cliente A usando o `{clientId}` do cliente B em POST, PUT e DELETE → 403 e o negócio de B intacto (SC-002)

### Implementation for User Story 3

- [ ] T074 [P] [US3] Criar em `src/RagAuth.Api/Domain/Exceptions/` as exceções `BusinessAlreadyExistsException` (409, "Você já possui um negócio cadastrado."), `BusinessNotFoundException` (404, "Não há negócio cadastrado para este cliente.") e `RagApiIntegrationException` (502, "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.")
- [ ] T075 [P] [US3] Criar `src/RagAuth.Api/Contracts/Validation/BrazilianPhoneAttribute.cs` (nulo é válido; após remover não-dígitos, exige 10 ou 11 dígitos) e `SingleLineAttribute.cs` (recusa `\r` e `\n`)
- [ ] T076 [P] [US3] Criar `src/RagAuth.Api/Contracts/Business/BusinessRequest.cs`: `Name` `[Required]` `[MaxLength(150)]`; `Description` `[Required]` `[MaxLength(1000)]`; `Email` `[EmailAddress]` `[MaxLength(254)]` opcional; `Telephone` `[BrazilianPhone]` opcional; `Address` `[MaxLength(300)]` `[SingleLine]` opcional; `Details` `[MaxLength(2000)]` opcional — mensagens em português (FR-016a)
- [ ] T077 [P] [US3] Criar `src/RagAuth.Api/Services/Business/BusinessInputNormalizer.cs` (`IBusinessInputNormalizer`): devolve um `NormalizedBusinessData` (record em `src/RagAuth.Api/Services/Business/NormalizedBusinessData.cs`) com trim, telefone só com dígitos e opcionais vazios como `null`
- [ ] T078 [P] [US3] Criar `src/RagAuth.Api/Infrastructure/RagApi/RagBusinessRequest.cs` e `RagBusinessDeletionRequest.cs` com `[JsonPropertyName]` `user_code` e `request` (campos internos `name`, `description`, `email`, `telephone`, `address`, `details`)
- [ ] T079 [P] [US3] Criar `src/RagAuth.Api/Infrastructure/RagApi/ICurrentRequestTokenAccessor.cs` e `HttpContextRequestTokenAccessor.cs` (lê o cabeçalho `Authorization` da requisição atual via `IHttpContextAccessor`)
- [ ] T080 [P] [US3] Criar em `src/RagAuth.Api/Infrastructure/RagApi/` os `DelegatingHandler`s `ApiKeyHeaderHandler.cs` (`x-api-key` de `RagApiOptions.ApiKey`), `ClientTokenForwardingHandler.cs` (usa `ICurrentRequestTokenAccessor`) e `CorrelationIdHeaderHandler.cs` (usa `ICorrelationIdAccessor`)
- [ ] T081 [US3] Criar `src/RagAuth.Api/Infrastructure/RagApi/IRagApiClient.cs` e `RagApiClient.cs`: `CreateBusinessAsync(string accessCode, NormalizedBusinessData data)`, `UpdateBusinessAsync(...)` e `DeleteBusinessAsync(string accessCode)` usando `RagApiOptions.BusinessPath`; DELETE envia corpo JSON via `HttpRequestMessage`; não-2xx lança `RagApiIntegrationException` (a US4 detalha a tradução); acrescentar `RagApiCallSucceeded` (Information, operação e status) a `src/RagAuth.Api/Observability/LogMessages.cs` (depende de T078, T080)
- [ ] T082 [US3] Criar `src/RagAuth.Api/Infrastructure/RagApi/RagApiServiceExtensions.cs` com `AddRagApiClient()`: `AddHttpClient<IRagApiClient, RagApiClient>` com `BaseAddress` = `RagApiOptions.BaseUrl`, `Timeout` = `TimeoutSeconds`, os três handlers via `AddHttpMessageHandler` e o accessor de token; chamá-lo em `AddApplication` (depende de T081)
- [ ] T083 [US3] Estender `src/RagAuth.Api/Services/Business/IBusinessService.cs` e `BusinessService.cs` com `CreateOwnedBusinessAsync(int clientId, BusinessRequest)`, `UpdateOwnedBusinessAsync(int clientId, BusinessRequest)` e `DeleteOwnedBusinessAsync(int clientId)` seguindo research R7: checar pré-condições (409/404) → abrir transação → aplicar e `SaveChanges` (violação de `ix_businesses_clientId` → 409) → chamar a RAG → commit; qualquer exceção → rollback e propagação; um método privado curto por etapa (depende de T074, T077, T081)
- [ ] T084 [US3] Criar `src/RagAuth.Api/Endpoints/BusinessEndpoints.cs` com `MapBusinessEndpoints`: grupo `/api/v1/{clientId}/business` com `RequireAuthorization(AuthorizationPolicies.ClientRouteOwner)`, sem restrição `:int` na rota; `POST` → 201 "Negócio cadastrado com sucesso."; `PUT` → 200 "Negócio atualizado com sucesso."; `DELETE` → 200 "Negócio removido com sucesso." com `data` = `null`; só delega ao `IBusinessService`; mapear em `MapApplication` (depende de T083)

**Checkpoint**: US1, US2 e US3 funcionam; ciclo completo do negócio com a RAG simulada.

---

## Phase 6: User Story 4 - Tratamento de falhas da API de recursos RAG (Priority: P3)

**Goal**: cada falha da RAG vira status e mensagem em português adequados, sem detalhes internos, e
nunca deixa dados locais divergentes.

**Independent Test**: simular a RAG respondendo 4xx, 401/403, 5xx, recusando conexão e
estourando o tempo limite; conferir status, mensagem e dados locais inalterados
([contracts/rag-api-outbound.md](./contracts/rag-api-outbound.md)).

### Tests for User Story 4 ⚠️

- [ ] T085 [P] [US4] Testes unitários em `tests/RagAuth.UnitTests/Infrastructure/RagApi/RagApiResponseTranslatorTests.cs` cobrindo a tabela do contrato: 400 → `RagApiRejectedException(400)` "O serviço de recursos recusou os dados enviados."; 404 → 404 "O negócio não foi encontrado no serviço de recursos."; 409 → 409 "O serviço de recursos indicou conflito com os dados enviados."; 422 → 422 "O serviço de recursos recusou a operação."; 401 e 403 → `RagApiIntegrationException` (502) com log `Error` de chave rejeitada; 500 e 503 → `RagApiIntegrationException` (502)
- [ ] T086 [P] [US4] Testes unitários em `tests/RagAuth.UnitTests/Infrastructure/RagApi/RagApiClientFailureTests.cs`: `HttpRequestException` → `RagApiUnavailableException`; `TaskCanceledException` com `TimeoutException` interna → `RagApiTimeoutException`; cancelamento pelo chamador não é convertido em timeout
- [ ] T087 [P] [US4] Testes unitários em `tests/RagAuth.UnitTests/Services/Business/BusinessServiceCompensationTests.cs`: na criação, se o commit falhar depois do 2xx da RAG → log `Error` e chamada de compensação `DeleteBusinessAsync`; na alteração e remoção → só log `Error`; a falha de commit é propagada como erro 500 genérico
- [ ] T088 [US4] Testes de integração em `tests/RagAuth.IntegrationTests/RagFailureTests.cs` para POST, PUT e DELETE de negócio: RAG 400/404/409/422 → mesmo status com a mensagem em português; RAG 401/403 → 502 e log `Error`; RAG 500 → 502; `FakeRagApiHandler` lançando `HttpRequestException` → 503; atraso acima de `timeoutSeconds` (1 s nos testes) → 504; em todos os casos o envelope não contém o corpo nem detalhes da RAG e o login seguinte mostra os dados locais anteriores (FR-022, SC-007); com a RAG fora do ar, o login ainda devolve o negócio

### Implementation for User Story 4

- [ ] T089 [P] [US4] Criar em `src/RagAuth.Api/Domain/Exceptions/` as exceções `RagApiRejectedException` (status 4xx recebido e mensagem por status), `RagApiUnavailableException` (503, "O serviço de recursos está indisponível no momento.") e `RagApiTimeoutException` (504, "O serviço de recursos demorou demais para responder.")
- [ ] T090 [P] [US4] Acrescentar a `src/RagAuth.Api/Observability/LogMessages.cs`: `RagApiRejectedRequest` (Warning, operação e status), `RagApiKeyRejected` (Error), `RagApiFailed` (Error, operação e status ou tipo da falha) e `CommitFailedAfterRagSuccess` (Error, operação e `clientId`)
- [ ] T091 [US4] Criar `src/RagAuth.Api/Infrastructure/RagApi/IRagApiResponseTranslator.cs` e `RagApiResponseTranslator.cs`: recebe operação e `HttpResponseMessage` não-2xx, registra o log adequado e devolve a exceção de domínio conforme a tabela do contrato (depende de T089, T090)
- [ ] T092 [US4] Atualizar `src/RagAuth.Api/Infrastructure/RagApi/RagApiClient.cs`: usar `IRagApiResponseTranslator` para não-2xx; `try/catch` convertendo `HttpRequestException` em `RagApiUnavailableException` e `TaskCanceledException` por timeout em `RagApiTimeoutException`, com log; registrar o tradutor em `RagApiServiceExtensions.cs` (depende de T091)
- [ ] T093 [US4] Atualizar `src/RagAuth.Api/Services/Business/BusinessService.cs`: se o commit falhar após o 2xx da RAG, registrar `CommitFailedAfterRagSuccess` e, na criação, tentar `DeleteBusinessAsync` como compensação, sem mascarar a falha original (research R7) (depende de T092)

**Checkpoint**: US1–US4 funcionam; toda falha da RAG tem resposta adequada e estado local consistente.

---

## Phase 7: User Story 5 - Remoção da própria conta (Priority: P3)

**Goal**: o cliente remove a própria conta; o negócio (se houver) é removido também na RAG; tokens e
login deixam de funcionar; o e-mail fica livre; `id` e código de acesso nunca são reutilizados.

**Independent Test**: remover uma conta com negócio e outra sem; conferir a chamada DELETE à RAG só
no primeiro caso, 401 no token antigo e no login, e novo cadastro com o mesmo e-mail recebendo
`clientId` e código diferentes ([contracts/client-endpoints.md](./contracts/client-endpoints.md)).

### Tests for User Story 5 ⚠️

- [ ] T094 [P] [US5] Testes unitários em `tests/RagAuth.UnitTests/Services/Clients/ClientAccountServiceTests.cs` (Moq): com negócio → abre transação, remove o negócio, chama `DeleteBusinessAsync` com o código de acesso, preenche `DeletedAt` com o `TimeProvider`, grava e faz commit; sem negócio → não chama a RAG; RAG falha → rollback, `DeletedAt` não é gravado e a exceção é propagada; conta inexistente → `ClientNotFoundException`
- [ ] T095 [US5] Testes de integração em `tests/RagAuth.IntegrationTests/ClientEndpointsTests.cs`: `DELETE /api/v1/{clientId}` sem negócio → 200 "Conta removida com sucesso." sem chamada à RAG; com negócio → 200 e a RAG recebeu `DELETE` com `{ "user_code" }`; depois disso o token antigo → 401 e o login → 401; novo cadastro com o mesmo e-mail → 201 com `clientId` e `accessCode` diferentes dos da conta removida; `{clientId}` de outro cliente → 403; RAG fora do ar → 503 e a conta continua ativa (login funciona e mostra o negócio)

### Implementation for User Story 5

- [ ] T096 [P] [US5] Criar `src/RagAuth.Api/Domain/Exceptions/ClientNotFoundException.cs` (401, "Token inválido ou conta inexistente.") e acrescentar `ClientAccountDeleted` (Information, só o `clientId`) a `src/RagAuth.Api/Observability/LogMessages.cs`
- [ ] T097 [US5] Criar `src/RagAuth.Api/Services/Clients/IClientAccountService.cs` e `ClientAccountService.cs` com `DeleteAccountAsync(int clientId)`: buscar a conta ativa → abrir transação → se houver negócio, removê-lo e chamar `IRagApiClient.DeleteBusinessAsync` → preencher `DeletedAt` → `SaveChanges` → commit; rollback em qualquer falha (FR-029, FR-030); métodos curtos por etapa (depende de T096)
- [ ] T098 [US5] Criar `src/RagAuth.Api/Endpoints/ClientEndpoints.cs` com `MapClientEndpoints`: `DELETE /api/v1/{clientId}` com `RequireAuthorization(AuthorizationPolicies.ClientRouteOwner)`, sem restrição `:int`, → 200 `ApiResponse` "Conta removida com sucesso." com `data` = `null`; registrar `IClientAccountService` e mapear em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs` (depende de T097)

**Checkpoint**: todas as histórias funcionam de forma independente.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: critérios de sucesso transversais e gates da constituição

- [ ] T099 [P] Teste de integração em `tests/RagAuth.IntegrationTests/CrossCutting/AccessCodeUniquenessTests.cs` (SC-008): gerar códigos com `IAccessCodeGenerator` e inserir 10.000 clientes direto pelo `AppDbContext` (com hash fixo, para não pagar o custo do PBKDF2) sem nenhuma violação de `ix_clients_accessCode`
- [ ] T100 [P] Teste de integração em `tests/RagAuth.IntegrationTests/CrossCutting/SensitiveDataAuditTests.cs` (SC-006): executar cadastro, login com senha errada e certa, criação, falha da RAG (401 e 500) e remoção de conta; afirmar que nenhuma entrada do `LogCaptureProvider` nem nenhum corpo de resposta de erro contém a senha usada, o token JWT emitido ou o valor de `RAG_API_KEY`
- [ ] T101 [P] Teste de integração em `tests/RagAuth.IntegrationTests/CrossCutting/CorrelationIdTests.cs`: cada resposta traz `X-Correlation-Id`; requisições diferentes têm ids diferentes; todas as entradas de log de uma requisição têm o mesmo `CorrelationId` no escopo; a RAG recebe o mesmo id; o envelope de erro traz o id em `error.correlationId`
- [ ] T102 [P] Criar `coverlet.runsettings` na raiz excluindo da cobertura `**/Infrastructure/Persistence/Migrations/*.cs` e `Program.cs`, e documentar no `README.md` o comando `dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"`
- [ ] T103 Rodar `dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"` e confirmar cobertura de linhas ≥ 90%; se faltar, acrescentar testes unitários nas classes descobertas em `tests/RagAuth.UnitTests/` (gate da Constituição VI)
- [ ] T104 Revisar `src/RagAuth.Api/` contra os gates da constituição: nenhum método com mais de 20 linhas, nenhum endpoint em `Endpoints/` referenciando `AppDbContext`, repositórios ou regra de negócio, nenhuma entidade exposta em DTO, nenhum `new` de serviço, nenhum pacote além dos justificados em plan.md; corrigir o que for encontrado
- [ ] T105 [P] Atualizar `README.md` com objetivo da API, pré-requisitos, variáveis de ambiente (`ASPNETCORE_ENVIRONMENT`, `DATABASE_PASSWORD`, `JWT_SIGNING_KEY`, `RAG_API_KEY`), como rodar migrations e a aplicação, e link para `specs/001-rag-auth-gateway/quickstart.md`
- [ ] T106 Executar todos os cenários manuais de `specs/001-rag-auth-gateway/quickstart.md` contra a aplicação local com um stub da RAG e registrar qualquer divergência

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: depende do Setup — BLOQUEIA todas as histórias
- **US1 (Phase 3)**: depende só da fundação
- **US2 (Phase 4)**: depende da fundação e da US1 (os testes precisam do token emitido no login)
- **US3 (Phase 5)**: depende da US2 (rotas protegidas pela policy `ClientRouteOwner`)
- **US4 (Phase 6)**: depende da US3 (refina o `RagApiClient` e o `BusinessService`)
- **US5 (Phase 7)**: depende da US2 e do `IRagApiClient` da US3; pode rodar em paralelo com a US4
- **Polish (Phase 8)**: depende das histórias desejadas

### User Story Dependencies

```text
Setup → Foundational → US1 → US2 → US3 ─┬→ US4 ─┬→ Polish
                                        └→ US5 ─┘
```

### Within Each User Story

- Testes primeiro, falhando antes da implementação
- Exceções e DTOs → serviços → endpoints → registro na composição
- História concluída e validada antes de avançar de prioridade

### Parallel Opportunities

- Setup: T004–T009 em paralelo depois de T002
- Fundação: T010, T012–T017, T019–T022, T025–T027, T029–T030 em paralelo; T033–T039 em paralelo depois de T032
- Em cada história, todos os testes marcados [P] e todas as exceções/DTOs/serviços independentes marcados [P]
- US4 e US5 em paralelo, por pessoas diferentes, depois da US3 (arquivos distintos, exceto `LogMessages.cs`, que precisa de merge)

---

## Parallel Example: User Story 1

```bash
# Testes da US1 juntos:
Task: "T040 RandomAccessCodeGeneratorTests em tests/RagAuth.UnitTests/Services/Auth/"
Task: "T041 IdentityPasswordHashingServiceTests em tests/RagAuth.UnitTests/Services/Auth/"
Task: "T042 JwtAccessTokenIssuerTests em tests/RagAuth.UnitTests/Services/Auth/"
Task: "T043 AlphanumericPasswordAttributeTests em tests/RagAuth.UnitTests/Contracts/Validation/"
Task: "T044 AuthServiceTests em tests/RagAuth.UnitTests/Services/Auth/"
Task: "T045 AuthEndpointsTests em tests/RagAuth.IntegrationTests/"

# Blocos independentes da US1 juntos:
Task: "T050 RandomAccessCodeGenerator em src/RagAuth.Api/Services/Auth/"
Task: "T051 IdentityPasswordHashingService em src/RagAuth.Api/Services/Auth/"
Task: "T052 JwtAccessTokenIssuer em src/RagAuth.Api/Services/Auth/"
Task: "T053 BusinessMapper e BusinessService (consulta) em src/RagAuth.Api/Services/Business/"
```

## Parallel Example: User Story 3

```bash
Task: "T068 BrazilianPhone/SingleLine attribute tests"
Task: "T069 BusinessInputNormalizerTests"
Task: "T070 RagApiClientTests"
Task: "T071 RagApiHeaderHandlersTests"
Task: "T072 BusinessServiceTests"

Task: "T075 BrazilianPhoneAttribute e SingleLineAttribute"
Task: "T078 RagBusinessRequest e RagBusinessDeletionRequest"
Task: "T079 HttpContextRequestTokenAccessor"
Task: "T080 Delegating handlers da API RAG"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Fase 1: Setup
2. Fase 2: Fundação (bloqueia tudo)
3. Fase 3: US1 — cadastro e login
4. Fase 4: US2 — proteção por token (também P1; sem ela a API não cumpre o papel de camada de
   segurança)
5. **PARAR E VALIDAR**: cenários 1–8 do quickstart

### Incremental Delivery

1. Setup + Fundação → base pronta
2. US1 → cadastro e login demonstráveis
3. US2 → MVP de segurança
4. US3 → valor de negócio (ciclo do negócio com a RAG)
5. US4 → robustez diante de falhas da RAG
6. US5 → remoção de conta
7. Polish → gates de cobertura, auditoria de logs e revisão da constituição

---

## Notes

- [P] = arquivos diferentes, sem dependência de tarefa incompleta
- [Story] liga a tarefa à história para rastreabilidade
- `LogMessages.cs` e `ApplicationServiceExtensions.cs` são editados por várias histórias: tarefas que
  os alteram não são [P] entre si
- Commit após cada tarefa ou grupo lógico
- Testes de integração exigem Docker em execução (Testcontainers)
