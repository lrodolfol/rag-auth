---

description: "Lista de tarefas da feature 004 — liberação de CORS para o front-end"
---

# Tasks: Liberação de CORS para o front-end

**Input**: Design documents from `/specs/004-cors-frontend-access/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cors-policy.md, quickstart.md

**Tests**: obrigatórios pela constituição (princípio VI: unitários xUnit para toda regra, cobertura
≥ 90%, integração para comportamento HTTP). Escrever os testes de cada história **antes** da
implementação e confirmar que falham.

**Organization**: tarefas agrupadas por história de usuário (US1 = P1, US2 = P2 da spec).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história de usuário da tarefa (US1, US2)

## Regras gerais para todas as tarefas

- .NET 10, C#, Minimal API. **Nenhum pacote NuGet novo** (CORS já está em `Microsoft.AspNetCore.App`).
- Métodos com no máximo 20 linhas; nomes autoexplicativos; mensagens em português.
- Não usar o nome `CorsOptions` para a classe nova (conflita com
  `Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions`); usar `FrontendCorsOptions`.

---

## Phase 1: Setup (configuração compartilhada)

**Purpose**: chaves de configuração usadas por todas as histórias e pelos testes.

- [X] T001 [P] Em `src/RagAuth.Api/appsettings.json`, adicionar a seção
  `"cors": { "allowedOrigins": [ "http://localhost:4200" ] }` (depois da seção `email`).
- [X] T002 [P] Em `src/RagAuth.Api/appsettings.dev.json`, adicionar
  `"cors": { "allowedOrigins": [ "https://SUBSTITUIR-front-uat.ragauth.com.br" ] }`, seguindo o
  padrão de placeholders `SUBSTITUIR-...` já existente no arquivo.
- [X] T003 [P] Garantir que os ambientes de teste tenham origem explícita: adicionar
  `["cors:allowedOrigins:0"] = "http://localhost:4200"` em
  `RagAuthApiFactory.BuildSettings()` (`tests/RagAuth.IntegrationTests/Infrastructure/RagAuthApiFactory.cs`),
  em `StartupConfigurationTests.CreateValidSettings()` (`tests/RagAuth.IntegrationTests/StartupConfigurationTests.cs`)
  e no dicionário do construtor de `tests/RagAuth.UnitTests/Configuration/ApplicationCompositionTests.cs`.
  No `RagAuthApiFactory`, expor a constante `public const string FrontendOrigin = "http://localhost:4200";`
  e usá-la no dicionário.

---

## Phase 2: Foundational (pré-requisitos bloqueantes)

**Purpose**: options validadas e política registrada — base das duas histórias.

### Testes da fundação (escrever primeiro, devem falhar)

- [X] T004 [P] Criar `tests/RagAuth.UnitTests/Configuration/FrontendCorsOptionsTests.cs` validando
  `FrontendCorsOptions` com `Validator.TryValidateObject(options, context, results, validateAllProperties: true)`:
  - lista com `http://localhost:4200` → válida;
  - lista com `http://localhost:4200/` (barra final) → válida;
  - lista vazia → erro com a mensagem exata
    `A configuração 'cors:allowedOrigins' deve conter ao menos uma origem.`;
  - lista com `nao-e-uma-url`, `localhost:4200` ou `ftp://localhost:4200` → erro com a mensagem
    exata `A configuração 'cors:allowedOrigins' deve conter apenas URLs absolutas.`
- [X] T005 [P] Em `tests/RagAuth.IntegrationTests/StartupConfigurationTests.cs`, adicionar casos:
  aplicação não sobe (`OptionsValidationException`) quando `cors:allowedOrigins:0` é removido
  (lista vazia) e quando vale `nao-e-uma-url`; a mensagem da exceção contém as mensagens do T004.
  Reusar `StartAndCaptureValidationException` e `CreateValidSettings`. Atenção: o
  `appsettings.json` da API também é carregado; para simular lista vazia, remover a chave do
  dicionário **e** sobrescrever com `["cors:allowedOrigins:0"] = string.Empty` (string vazia deve
  falhar como URL não absoluta) — ajustar o caso conforme o comportamento observado e registrar no teste.

### Implementação da fundação

- [X] T006 Criar `src/RagAuth.Api/Configuration/FrontendCorsOptions.cs`:
  - `public sealed class FrontendCorsOptions : IValidatableObject`, `public const string SectionName = "cors";`
  - `public string[] AllowedOrigins { get; set; } = [];`
  - `Validate` retorna, com `MemberNames = [nameof(AllowedOrigins)]`:
    - se a lista estiver vazia: `A configuração 'cors:allowedOrigins' deve conter ao menos uma origem.`
    - se algum item não for `Uri.TryCreate(item, UriKind.Absolute, out var uri)` com esquema `http`
      ou `https`: `A configuração 'cors:allowedOrigins' deve conter apenas URLs absolutas.`
  - Método auxiliar privado `IsAbsoluteHttpUrl(string origin)`; cada método ≤ 20 linhas.
- [X] T007 Em `src/RagAuth.Api/Configuration/OptionsServiceExtensions.cs`, registrar
  `services.AddOptions<FrontendCorsOptions>().Bind(configuration.GetSection(FrontendCorsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();`
  em um método privado novo `AddFrontendCorsOptions`, chamado a partir de `AddApplicationOptions`
  (manter `AddApplicationOptions` ≤ 20 linhas — encadear como já é feito com `AddEmailOptions`).
- [X] T008 Criar `src/RagAuth.Api/Security/CorsServiceExtensions.cs`:
  - `public const string FrontendPolicyName = "FrontendPolicy";`
  - `public static IServiceCollection AddFrontendCors(this IServiceCollection services)`: chama
    `services.AddCors()` e
    `services.AddOptions<CorsOptions>().Configure<IOptions<FrontendCorsOptions>>((corsOptions, frontendOptions) => corsOptions.AddPolicy(FrontendPolicyName, policy => BuildFrontendPolicy(policy, frontendOptions.Value)));`
  - `private static void BuildFrontendPolicy(CorsPolicyBuilder policy, FrontendCorsOptions options)`:
    `policy.WithOrigins(NormalizeOrigins(options.AllowedOrigins)).WithMethods("GET", "POST", "PUT", "DELETE").AllowAnyHeader();`
    — **não** chamar `AllowCredentials`, `SetPreflightMaxAge` nem `WithExposedHeaders` (research R5).
  - `private static string[] NormalizeOrigins(IEnumerable<string> origins)` → `origin.Trim().TrimEnd('/')` (research R4).
- [X] T009 Em `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs`:
  - em `AddApplication`, encadear `.AddFrontendCors()` (após `.AddJwtAuthentication()`);
  - em `MapApplication`, inserir `app.UseCors(CorsServiceExtensions.FrontendPolicyName);`
    **imediatamente após** `app.UseMiddleware<CorrelationIdMiddleware>();` e **antes** de
    `app.UseExceptionHandler();` (plan, "Pipeline resultante"; research R3).
- [X] T010 [P] Criar `tests/RagAuth.UnitTests/Configuration/FrontendCorsPolicyTests.cs` que monta a
  aplicação como `ApplicationCompositionTests` (mesmo dicionário, com
  `cors:allowedOrigins:0 = "http://localhost:4200/"` — com barra) e resolve
  `ICorsPolicyProvider.GetPolicyAsync(new DefaultHttpContext(), CorsServiceExtensions.FrontendPolicyName)`.
  Verificar: `Origins` == `["http://localhost:4200"]`; `Methods` == GET, POST, PUT, DELETE (exatamente
  esses quatro); `AllowAnyHeader` true; `SupportsCredentials` false; `AllowAnyOrigin` false;
  `AllowAnyMethod` false. Em `ApplicationCompositionTests`, adicionar asserção de que
  `IOptions<FrontendCorsOptions>.Value.AllowedOrigins` contém `http://localhost:4200`.

**Checkpoint**: `dotnet build` sem avisos novos; T004 e T010 passam; T005 passa.

---

## Phase 3: User Story 1 — Front-end local consome a API pelo navegador (Priority: P1) 🎯 MVP

**Goal**: página em `http://localhost:4200` consegue chamar qualquer rota com GET/POST/PUT/DELETE e
qualquer cabeçalho, inclusive lendo respostas de erro (contratos C1, C4, C5).

**Independent Test**: preflight e requisições reais com `Origin: http://localhost:4200` recebem
`Access-Control-Allow-Origin: http://localhost:4200`, com status e corpo inalterados.

### Testes da US1 (escrever primeiro)

- [X] T011 [US1] Criar `tests/RagAuth.IntegrationTests/CorsTests.cs` com
  `[Collection(IntegrationTestCollection.Name)]`, construtor `(RagAuthApiFactory factory)`,
  `IAsyncLifetime` com `factory.ResetDatabaseAsync()` (mesmo padrão de `ProtectedRouteTests`), e um
  auxiliar privado `CreatePreflightRequest(string path, string origin, string method, string? requestHeaders)`
  que monta `HttpRequestMessage(HttpMethod.Options, path)` com `Origin`,
  `Access-Control-Request-Method` e, se informado, `Access-Control-Request-Headers`. Casos US1:
  - `[Theory]` GET, POST, PUT, DELETE em preflight para `/api/v1/auth/login` com
    `Access-Control-Request-Headers: content-type,authorization,x-qualquer-coisa` → status 204;
    `Access-Control-Allow-Origin` == `http://localhost:4200`; `Access-Control-Allow-Methods`
    contém o método; `Access-Control-Allow-Headers` contém os três cabeçalhos; sem
    `Access-Control-Allow-Credentials` (C1, FR-001..FR-004).
  - preflight para rota protegida (ex.: `/api/v1/business/...` — usar uma rota protegida já
    exercitada em `ProtectedRouteTests`) **sem token** → 204 com os cabeçalhos (FR-004).
  - `POST /api/v1/auth/login` com `Origin: http://localhost:4200` e credenciais inexistentes →
    mesmo erro e mensagem de `AuthEndpointsTests` (`E-mail ou senha inválidos.`, via
    `ApiEnvelopeAssertions.AssertErrorAsync`) **e** `Access-Control-Allow-Origin` presente (C4, FR-008).
  - rota protegida sem token com `Origin` → 401 com a mesma mensagem de `ProtectedRouteTests`
    (`Token ausente, inválido ou expirado.`) **e** `Access-Control-Allow-Origin` presente (C4, FR-009).
  - `POST /api/v1/auth/register` com corpo inválido e `Origin` → 400 ValidationProblem inalterado
    **e** `Access-Control-Allow-Origin` presente.
  - `GET /api/v1/rota-inexistente` com `Origin` → 404 `Recurso não encontrado` **e** ACAO presente.
  - `POST /api/v1/auth/login` **sem** `Origin` → nenhum cabeçalho `Access-Control-*` (C5).

### Implementação da US1

- [X] T012 [US1] Rodar `CorsTests` contra a implementação da Phase 2. Se algum caso de erro (T011)
  vier sem `Access-Control-Allow-Origin`, investigar a ordem do pipeline em
  `src/RagAuth.Api/Configuration/ApplicationServiceExtensions.cs` (o CORS deve continuar antes do
  `UseExceptionHandler`) e corrigir sem mudar regras de negócio; registrar o achado em
  `specs/004-cors-frontend-access/research.md` (R3). Garantir que os testes existentes de 405/404
  em `AuthEndpointsTests` continuam passando.

**Checkpoint**: US1 completa — front-end local consome a API.

---

## Phase 4: User Story 2 — Origens não autorizadas continuam bloqueadas (Priority: P2)

**Goal**: qualquer outra origem e qualquer método fora da lista continuam sem autorização
(contratos C2, C3; FR-005, FR-006).

**Independent Test**: requisições com origens diferentes de `http://localhost:4200` ou preflight
com PATCH não recebem autorização de CORS.

### Testes da US2

- [X] T013 [US2] Em `tests/RagAuth.IntegrationTests/CorsTests.cs`, adicionar:
  - `[Theory]` com `http://localhost:3000`, `https://localhost:4200`, `http://127.0.0.1:4200`:
    preflight POST para `/api/v1/auth/login` → resposta **sem** `Access-Control-Allow-Origin`;
    e `POST /api/v1/auth/login` real com essa origem → mesmo status/corpo de erro de credenciais
    inválidas, **sem** `Access-Control-Allow-Origin` (C3).
  - preflight com `Access-Control-Request-Method: PATCH` a partir de `http://localhost:4200` →
    `Access-Control-Allow-Methods` ausente ou sem `PATCH` (C2).
- [X] T014 [US2] Se algum caso do T013 falhar, ajustar apenas `BuildFrontendPolicy` em
  `src/RagAuth.Api/Security/CorsServiceExtensions.cs` (nunca usar `AllowAnyOrigin`/`AllowAnyMethod`).

**Checkpoint**: US1 e US2 passam juntas.

---

## Phase 5: Polish & Cross-Cutting

- [X] T015 Rodar `dotnet test RagAuth.slnx --settings coverlet.runsettings`; todos os testes passam
  e a cobertura de linhas é ≥ 90%. Conferir que nenhum método novo passa de 20 linhas.
- [X] T016 [P] Atualizar `README.md` com a seção de configuração `cors:allowedOrigins` (valor padrão,
  como adicionar a origem de produção/UAT, e que a aplicação não sobe com lista vazia ou URL inválida).
- [ ] T017 Executar a validação manual de `specs/004-cors-frontend-access/quickstart.md` (seção 2) e
  marcar as tarefas concluídas neste arquivo.

---

## Dependencies & Execution Order

- **Phase 1 (T001–T003)**: sem dependências; as três em paralelo.
- **Phase 2**: T004 e T005 (testes) podem começar após Phase 1; T006 → T007 → T008 → T009 em
  sequência (T009 depende de T008; T008 depende de T006); T010 após T009.
- **US1 (T011–T012)**: depende da Phase 2 completa.
- **US2 (T013–T014)**: depende da Phase 2; edita o mesmo `CorsTests.cs` da US1, então fazer após T011.
- **Polish (T015–T017)**: após US1 e US2.

```text
T001,T002,T003 ─► T004,T005 (testes) ─► T006 ─► T007 ─► T008 ─► T009 ─► T010
                                                                   └─► T011 ─► T012 ─► T013 ─► T014 ─► T015 ─► T016,T017
```

## Parallel Example

```text
# Phase 1
T001 appsettings.json | T002 appsettings.dev.json | T003 configurações dos testes

# Phase 2 (testes antes da implementação)
T004 FrontendCorsOptionsTests.cs | T005 StartupConfigurationTests.cs

# Polish
T016 README.md (em paralelo com T015)
```

## Implementation Strategy

1. **MVP**: Phases 1 e 2 + US1 (T001–T012) — já libera o front-end em `http://localhost:4200`.
2. **Incremento**: US2 (T013–T014) — prova que nada além do pedido foi liberado.
3. **Fechamento**: Polish (T015–T017) — cobertura, documentação e validação manual.
