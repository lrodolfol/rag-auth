# Implementation Plan: API de Autenticação e Controle de Acesso (Gateway RAG)

**Branch**: `001-rag-auth-gateway` | **Date**: 2026-10-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-rag-auth-gateway/spec.md` + decisões técnicas de
`.mine/plan.md`

## Summary

API .NET 10 (Minimal APIs) que cadastra e autentica clientes (e-mail + senha → JWT com o código de
acesso de 12 caracteres e o `id` do cliente; o login já devolve o negócio do cliente), protege as
rotas por token — `{clientId}` da rota precisa ser o `id` do cliente do token — e gerencia o único
negócio de cada cliente e a remoção da própria conta, persistindo uma cópia local em PostgreSQL
(EF Core, code-first) e encaminhando criação, alteração e remoção à API de recursos RAG com
`x-api-key`, o token do cliente e `user_code`. Toda validação
acontece antes da chamada à RAG; a gravação local fica numa transação que só é confirmada depois do
2xx da RAG. Todas as respostas seguem o envelope `{ statusCode, success, message, data, error }`, com
`error` no formato `ProblemDetails` nativo.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.400), ASP.NET Core Minimal APIs

**Primary Dependencies**: EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`,
`Microsoft.EntityFrameworkCore.Design` (migrations), `Microsoft.AspNetCore.Authentication.JwtBearer`;
nativos: validação de Minimal APIs (`AddValidation`), `PasswordHasher<T>`, `IHttpClientFactory`,
`IProblemDetailsService`, `IExceptionHandler`, `Microsoft.Extensions.Logging` (console)

**Storage**: PostgreSQL (local via Docker; remoto em UAT/produção)

**Testing**: xUnit + Moq (unitários), `WebApplicationFactory` + Testcontainers PostgreSQL
(integração), coverlet (cobertura ≥ 90%)

**Target Platform**: servidor Linux/contêiner; desenvolvimento em Windows

**Project Type**: web-service (API REST)

**Performance Goals**: p95 < 500 ms em cadastro, login e validação de token, sem contar a RAG
(SC-005); timeout da RAG 10 s (FR-021)

**Constraints**: nenhum segredo em código ou arquivo de configuração; mensagens em português;
métodos ≤ 20 linhas; nenhuma chamada à RAG em caso de falha local; dados locais nunca divergem da RAG
por falha (FR-022)

**Scale/Scope**: até ~10.000 clientes (SC-008), baixo volume concorrente; 6 rotas em 3 arquivos de
endpoint

Todas as incógnitas foram resolvidas em [research.md](./research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Pré-design | Pós-design | Como é atendido |
|---|---|---|---|
| I. Camadas | ✅ | ✅ | Endpoints só delegam a serviços; serviços usam repositórios/`IUnitOfWork`; `DbContext` só em `Infrastructure` |
| II. Responsabilidade única / ≤ 20 linhas | ✅ | ✅ | `Program.cs` dividido em extensões `Add*`/`Map*`; RAG separada em cliente + 3 handlers + tradutor de resposta; fluxo transacional em métodos por etapa |
| III. Nomenclatura | ✅ | ✅ | Nomes descritivos (`ClientTokenForwardingHandler`, `BusinessAlreadyExistsException`); sem abreviações |
| IV. DTOs | ✅ | ✅ | `RegisterClientRequest`, `LoginRequest`, `BusinessRequest`, `*Response`, `ApiResponse<T>`; entidades nunca saem de `Services` |
| V. DI e abstrações | ✅ | ✅ | Tudo por interface e injeção; `HttpClient` via `IHttpClientFactory`; `TimeProvider` injetado |
| VI. Testes (≥ 90%, integração por endpoint) | ✅ | ✅ | Projetos de teste unitário e de integração; 6 endpoints com testes de integração; gate de cobertura no `dotnet test` |
| VII. Erros em PT + `ProblemDetails`/`ValidationProblem` | ⚠️ conflito com o envelope de `.mine/plan.md` | ✅ | Resolvido em research R1: erros são `ProblemDetails`/`ValidationProblem` nativos, embrulhados no campo `error` pelo `IProblemDetailsWriter` |
| Restrição: dependências justificadas | ✅ | ✅ | Tabela abaixo |

**Dependências novas** (motivo e alternativa nativa descartada):

| Pacote | Projeto | Motivo | Alternativa nativa descartada |
|---|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Api | Provider EF Core do PostgreSQL, exigido em `.mine/plan.md` | Não há provider PostgreSQL no .NET |
| `Microsoft.EntityFrameworkCore.Design` | Api | Gerar migrations (code-first exigido) | Scripts SQL manuais — contraria code-first |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Api | Validação de JWT (assinatura, `iss`, `aud`, `exp`) | Validação manual do token — insegura e verbosa |
| `Moq` | UnitTests | Exigido em `.mine/plan.md` | Fakes manuais |
| `coverlet.collector` | ambos de teste | Medir o gate de 90% | Sem medição nativa sem pacote |
| `Microsoft.AspNetCore.Mvc.Testing` | IntegrationTests | Host em memória por endpoint | Processo separado + HTTP real |
| `Testcontainers.PostgreSql` | IntegrationTests | Índice parcial, transação e erro `23505` reais | EF InMemory/SQLite não reproduzem esse comportamento |

`xunit`, `xunit.runner.visualstudio` e `Microsoft.NET.Test.Sdk` vêm do template de teste e são
exigidos pela constituição. `Microsoft.EntityFrameworkCore` entra como dependência do provider.

**Resultado do gate**: PASSA, sem violações a justificar em Complexity Tracking.

## Decisões sobre divergências entre entradas (resolvidas em 2026-10-25)

1. **Remoção de conta** (`DELETE /api/v1/{clientId}`): incorporada à spec (User Story 5, FR-029,
   FR-030); soft delete (research R9).
2. **Envelope**: acrescentado `data`; `error` é `ProblemDetails` (research R1) — aprovado.
3. **Consulta do negócio**: sem rota própria; o login devolve o negócio (FR-014, research R11).
4. **Contrato com a RAG**: `x-api-key` + token do cliente em `Authorization` (FR-018, R8) — aprovado.
5. **`{clientId}`** = `id` (int) da tabela `clients`; divergência com a claim `client_id` → 403
   (FR-011, FR-028, R10).

## Project Structure

### Documentation (this feature)

```text
specs/001-rag-auth-gateway/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api-envelope.md
│   ├── auth-endpoints.md
│   ├── business-endpoints.md
│   ├── client-endpoints.md
│   └── rag-api-outbound.md
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

```text
RagAuth.slnx
src/RagAuth.Api/
├── Program.cs                         # só composição: builder.AddApplication(); app.MapApplication()
├── appsettings.json                   # local (padrão)
├── appsettings.uat.json
├── appsettings.prod.json
├── Endpoints/                         # um arquivo por recurso
│   ├── AuthEndpoints.cs               # /api/v1/auth/register, /login (login devolve o negócio)
│   ├── BusinessEndpoints.cs           # /api/v1/{clientId}/business (POST, PUT, DELETE)
│   └── ClientEndpoints.cs             # /api/v1/{clientId} (DELETE)
├── Contracts/                         # DTOs
│   ├── ApiResponse.cs
│   ├── Auth/                          # RegisterClientRequest, LoginRequest, RegisteredClientResponse, LoginResponse
│   ├── Business/                      # BusinessRequest, BusinessResponse
│   └── Validation/                    # AlphanumericPasswordAttribute, BrazilianPhoneAttribute, SingleLineAttribute
├── Domain/
│   ├── Entities/                      # Client, Business
│   └── Exceptions/                    # DomainException + exceções específicas
├── Services/                          # regras de negócio (interfaces + implementações)
│   ├── Auth/                          # IAuthService, IAccessTokenIssuer, IPasswordHashingService, IAccessCodeGenerator
│   ├── Business/                      # IBusinessService, BusinessMapper
│   └── Clients/                       # IClientAccountService, IClientSessionValidator
├── Infrastructure/
│   ├── Persistence/                   # AppDbContext, configurações EF, TimestampInterceptor, repositórios, UnitOfWork
│   │   └── Migrations/
│   └── RagApi/                        # IRagApiClient, RagApiClient, RagApiResponseTranslator, handlers
├── Security/                          # ClientRouteMatchesTokenRequirement(+Handler), JwtBearerEventsSetup
├── Observability/                     # CorrelationIdMiddleware, ICorrelationIdAccessor, LogMessages
├── ErrorHandling/                     # DomainExceptionHandler, ApiEnvelopeProblemDetailsWriter
└── Configuration/                     # DatabaseOptions, JwtOptions, RagApiOptions, EnvironmentSecrets, extensões Add*

tests/
├── RagAuth.UnitTests/                 # espelha src/: Services, Infrastructure/RagApi, Security, ErrorHandling, Contracts/Validation
└── RagAuth.IntegrationTests/
    ├── Infrastructure/                # RagAuthApiFactory (WebApplicationFactory + Testcontainers), FakeRagApiHandler, LogCaptureProvider
    ├── AuthEndpointsTests.cs
    ├── BusinessEndpointsTests.cs
    ├── ClientEndpointsTests.cs
    └── CrossCutting/                  # envelope, correlation-id, auditoria de logs (SC-006), unicidade em massa (SC-008)
```

**Structure Decision**: projeto único de web-service (`src/RagAuth.Api`) com camadas separadas por
pasta, mais dois projetos de teste. Separar camadas em projetos distintos não traz ganho para
6 rotas; a regra de camadas da Constituição I é garantida pelas dependências entre namespaces e pela
revisão (endpoints só referenciam `Services` e `Contracts`). As rotas com `{clientId}` não usam a
restrição `:int`, para que valores não numéricos caiam na policy (403) e não no roteamento (404).

### Configuração

| Arquivo (camelCase) | Chaves |
|---|---|
| `database` | `host`, `port`, `name`, `username` |
| `jwt` | `issuer`, `audience`, `expirationMinutes` (60) |
| `ragApi` | `baseUrl`, `businessPath` (`/business`), `timeoutSeconds` (10) |
| `Logging` | nível mínimo `Information`, console com scopes |

| Variável de ambiente | Uso |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | vazio (local), `uat`, `prod` |
| `DATABASE_PASSWORD` | senha do PostgreSQL |
| `JWT_SIGNING_KEY` | chave HS256 (≥ 32 bytes) |
| `RAG_API_KEY` | valor de `x-api-key` |

Ausência de qualquer item obrigatório impede a inicialização com log explicativo (R12).

### Pipeline de requisição

`CorrelationIdMiddleware` → `UseExceptionHandler` (→ `DomainExceptionHandler`) → `UseStatusCodePages`
→ `UseAuthentication` → `UseAuthorization` → validação automática → endpoint → serviço →
(`IUnitOfWork` transação) → `IRagApiClient` → commit/rollback.

## Complexity Tracking

Nenhuma violação da constituição a justificar.
