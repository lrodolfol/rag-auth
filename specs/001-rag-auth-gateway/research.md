# Research: API de Autenticação e Controle de Acesso (Gateway RAG)

**Feature**: `001-rag-auth-gateway` | **Data**: 2026-10-25 | **Plano**: [plan.md](./plan.md)

Este documento resolve as incógnitas do Technical Context e os conflitos entre a entrada de
planejamento (`.mine/plan.md`), a especificação (`spec.md`) e a constituição. Cada item segue o
formato **Decisão / Justificativa / Alternativas consideradas**.

---

## R1. Envelope de resposta × `ProblemDetails` (Constituição VII)

**Conflito**: `.mine/plan.md` exige o envelope `{ statusCode, success, message, error }` em todos os
endpoints; a constituição exige que erros de validação usem os mecanismos nativos
`ProblemDetails` / `ValidationProblem`. Pela governança, quem cede é o plano.

**Decisão**: o envelope é mantido como formato de transporte, e o campo `error` carrega **sempre** um
objeto `ProblemDetails` (ou `HttpValidationProblemDetails`, com `errors` por campo) produzido pelo
pipeline nativo do ASP.NET Core. Todos os erros — validação automática, `401`/`403` da
autenticação, exceções de domínio e falhas da API RAG — passam por `IProblemDetailsService`; um
`IProblemDetailsWriter` customizado (`ApiEnvelopeProblemDetailsWriter`) apenas embrulha o
`ProblemDetails` no envelope. Foi acrescentado o campo `data` para respostas de sucesso (o envelope
original não tinha onde devolver token, código de acesso ou negócio). Em sucesso, `error` é `null`;
em erro, `data` é `null`.

**Justificativa**: o mecanismo de geração de erros é 100% nativo (`ProblemDetails`,
`ValidationProblem`, `IProblemDetailsService`, `IExceptionHandler`); só a serialização final muda.
Um único ponto de escrita garante que nenhum erro escape do envelope.

**Alternativas consideradas**:
- Envelope só para sucesso e `application/problem+json` puro para erro — conforme à constituição,
  mas contraria o requisito "padrão de response para todos os endpoints".
- Envelope com `error` em formato próprio (`{ field, message }`) — viola a Constituição VII.

> ⚠️ Ponto para confirmação do dono do produto: o acréscimo do campo `data` e o `error` no
> formato `ProblemDetails`. Ver [contracts/api-envelope.md](./contracts/api-envelope.md).

## R2. Validação de entrada

**Decisão**: validação nativa de Minimal APIs do .NET 10 (`builder.Services.AddValidation()`,
gerador de código sobre atributos `System.ComponentModel.DataAnnotations`) com mensagens em
português definidas em `ErrorMessage`. Regras sem atributo pronto ganham atributos próprios
(`AlphanumericPasswordAttribute` — letra + número; `BrazilianPhoneAttribute` — 10/11 dígitos após
remover máscara). A validação automática já devolve `ValidationProblem` via
`IProblemDetailsService`, caindo no envelope (R1). JSON malformado, corpo ausente ou tipo errado
geram `BadHttpRequestException` (400), também convertida em `ProblemDetails` pelo
`IExceptionHandler`.

**Justificativa**: zero dependências novas; resposta nativa `ValidationProblem` exigida pela
constituição; validação acontece antes do endpoint, logo antes de qualquer chamada à API RAG.

**Alternativas consideradas**: FluentValidation (dependência nova sem ganho que justifique);
validação manual no serviço (espalha regra de formato pela camada de negócio).

**Verificação na implementação**: confirmar em teste de integração que o filtro de validação do
.NET 10 usa o `IProblemDetailsService` registrado. Plano B, se não usar: `ValidationEndpointFilter<T>`
próprio chamando `Validator.TryValidateObject` e `IProblemDetailsService` (também nativo).

## R3. Hash de senha

**Decisão**: `PasswordHasher<Client>` de `Microsoft.Extensions.Identity.Core` (incluído no shared
framework `Microsoft.AspNetCore.App`; PBKDF2-HMAC-SHA512, 100.000 iterações, salt de 128 bits),
encapsulado em `IPasswordHashingService`.

**Justificativa**: nativo, sem pacote novo, atende FR-005 (hash irreversível com salt) e o custo
(~50–100 ms) cabe no SC-005 (< 500 ms).

**Alternativas consideradas**: BCrypt.Net / Argon2 (dependências novas sem necessidade);
`Rfc2898DeriveBytes.Pbkdf2` manual (reimplementar formato e versionamento já prontos).

## R4. Emissão e validação de JWT

**Decisão**: `Microsoft.AspNetCore.Authentication.JwtBearer` para validação e
`JsonWebTokenHandler` (transitivo dele) para emissão. Algoritmo HS256 com chave em
`JWT_SIGNING_KEY` (mínimo 32 bytes). Claims: `sub` = código de acesso (FR-027), `client_id` = `id`
numérico do cliente (FR-011/FR-028), `jti`, `iat`, `exp`, `iss`, `aud`. Expiração padrão 60 min
(`jwt:expirationMinutes`). Em `JwtBearerEvents.OnTokenValidated`, o
`IClientSessionValidator` confirma que a conta do `client_id` existe, está ativa e tem o mesmo
código de acesso do `sub`; caso contrário
falha → 401 (FR-010, edge case de conta removida). `OnChallenge`/`OnForbidden` escrevem via
`IProblemDetailsService` para manter o envelope.

**Justificativa**: pacote oficial da Microsoft; validar JWT à mão (assinatura, `iss`, `aud`, `exp`,
skew) é arriscado. HS256 é suficiente enquanto a API RAG confiar na chave `x-api-key` e no
`user_code`.

**Alternativas consideradas**: RS256/ES256 (API RAG validaria só com chave pública — recomendado
se a API RAG passar a validar o token; registrado como evolução); emissão manual com
`HMACSHA256` (sem ganho).

## R5. Código de acesso (FR-025, FR-026, SC-008)

**Decisão**: `RandomNumberGenerator.GetString("A–Z a–z 0–9", 12)` (CSPRNG nativo, .NET 8+), em
`IAccessCodeGenerator`. Unicidade garantida por índice único na coluna `accessCode`; em violação
desse índice o cadastro gera novo código e tenta de novo (até 5 tentativas). Contas removidas são
**soft delete** (R9), então o código nunca volta a ficar livre.

**Justificativa**: 62¹² ≈ 3,2×10²¹ combinações — colisão praticamente impossível, mas o índice
torna a garantia determinística.

**Alternativas consideradas**: GUID truncado (viés e menos entropia por caractere); checar
existência antes de inserir (condição de corrida).

## R6. Persistência — EF Core + PostgreSQL

**Decisão**: EF Core 10 com `Npgsql.EntityFrameworkCore.PostgreSQL`, code-first com migrations.
IDs `int` identity. Colunas `createdAt`/`updatedAt` preenchidas por `SaveChangesInterceptor` usando
`TimeProvider` (testável). Nomes de colunas em camelCase (padrão de nomenclatura de
`.mine/plan.md`) aplicados por convenção no `OnModelCreating`, sem pacote extra. E-mail
normalizado (trim + minúsculas) antes de persistir; índice único parcial
`WHERE "deletedAt" IS NULL`. Repositórios (`IClientRepository`, `IBusinessRepository`) e
`IUnitOfWork` (com transação) isolam o `DbContext` dos serviços, permitindo testes unitários com
Moq.

**Justificativa**: exigências explícitas de `.mine/plan.md`; repositórios atendem a Constituição I e
V e viabilizam 90% de cobertura unitária.

**Alternativas consideradas**: `citext` para e-mail (extensão de banco a mais para algo resolvido
com normalização); `EFCore.NamingConventions` (dependência só para nomes de coluna).

## R7. Consistência com a API RAG (FR-022, "não chamar a API RAG em caso de falha")

**Decisão**: padrão "transação local envolvendo a chamada externa":
1. Validar entrada, posse e pré-condições (existência, 409 de negócio duplicado) **antes** de
   qualquer chamada externa.
2. Abrir transação, aplicar a mudança e `SaveChanges` (o índice único de `clientId` em `businesses`
   barra criação concorrente aqui → 409, sem chamar a RAG).
3. Chamar a API RAG.
4. Sucesso → `Commit`; falha → `Rollback` e erro traduzido (R8).
5. Se o `Commit` falhar depois de a RAG confirmar (raro), registrar log `Error` com correlation-id
   e tentar compensação na RAG (DELETE para criação; para alteração/remoção apenas o log, pois não
   há estado anterior na RAG para restaurar).

**Justificativa**: nenhuma alteração local sobrevive a uma falha da RAG e nenhum caso de falha
local conhecido chega a chamar a RAG. A transação fica aberta no máximo pelo timeout da RAG (10 s),
aceitável para o volume previsto.

**Alternativas consideradas**: chamar a RAG antes de gravar (falha local após sucesso remoto
deixaria a RAG com dado órfão sem nenhuma barreira); outbox com reprocessamento (complexidade
desproporcional para v1).

## R8. Cliente HTTP da API RAG e tradução de falhas (FR-018–FR-021)

**Decisão**: `IHttpClientFactory` com cliente tipado `RagApiClient : IRagApiClient`,
`BaseAddress` = `ragApi:baseUrl`, `Timeout` = `ragApi:timeoutSeconds` (padrão 10). Três
`DelegatingHandler`s nativos: `ApiKeyHeaderHandler` (`x-api-key` de `RAG_API_KEY`),
`ClientTokenForwardingHandler` (repassa o `Authorization: Bearer` da requisição original, conforme
FR-018) e `CorrelationIdHeaderHandler` (`X-Correlation-Id`). Corpo `{ user_code, request }` em
snake_case conforme `.mine/plan.md`. Tradução, em `try/catch` dentro do `RagApiClient`:

| Situação | Exceção de domínio | Resposta ao cliente |
|---|---|---|
| 2xx | — | sucesso |
| 401/403 da RAG (chave rejeitada) | `RagApiIntegrationException` + log `Error` | 502 |
| Outro 4xx | `RagApiRejectedException(status)` | mesmo status, mensagem em PT |
| 5xx | `RagApiIntegrationException` | 502 |
| `HttpRequestException` (conexão) | `RagApiUnavailableException` | 503 |
| `TaskCanceledException` por timeout | `RagApiTimeoutException` | 504 |

Sem retentativas automáticas (POST não é idempotente).

**Justificativa**: tudo nativo; handlers mantêm o `RagApiClient` focado (Constituição II).

**Alternativas consideradas**: `Microsoft.Extensions.Http.Resilience` (retries/circuit breaker não
pedidos e perigosos para POST); Refit (dependência sem ganho para três chamadas).

**Premissas (a confirmar com o time da API RAG)**: as três operações usam o mesmo caminho
configurável `ragApi:businessPath` (padrão `/business`) com POST/PUT/DELETE; DELETE aceita corpo;
o corpo de resposta 2xx não é necessário (o dado devolvido ao cliente é o persistido localmente).

## R9. Remoção de conta — `DELETE /api/v1/{clientId}` (FR-029, FR-030)

**Decisão**: se houver negócio, segue o fluxo de remoção de negócio (R7, com chamada RAG DELETE);
depois a conta recebe `deletedAt` (soft delete), tudo na mesma transação. Tokens da conta passam a
dar 401 (R4). O e-mail fica livre para novo cadastro (índice parcial); o código de acesso continua
reservado (índice único total) e o `id` identity nunca é reaproveitado pelo PostgreSQL.

**Justificativa**: soft delete é a forma mais simples de cumprir "identificador e código de acesso
da conta removida MUST NOT ser atribuídos a outra conta".

**Alternativas consideradas**: hard delete + tabela de códigos aposentados (duas tabelas para o
mesmo fim); hard delete puro (viola FR-025). Anonimização de nome/e-mail para LGPD fica como
evolução.

## R10. `{clientId}` na rota (FR-011, FR-028)

**Decisão**: `{clientId}` é o `id` (int) da tabela `clients`, devolvido no cadastro e no login. Uma
policy de autorização nativa (`ClientRouteMatchesTokenRequirement` + handler) compara o valor bruto
da rota com a claim `client_id` do token; divergência → **403**. A rota **não** usa a restrição
`{clientId:int}`: com ela, um valor não numérico daria 404 de roteamento, e a spec exige 403 para
identificador não numérico ou inexistente. Como a policy só aprova valores iguais à claim, o
endpoint recebe sempre um inteiro válido. O negócio continua sendo "meu negócio" — sem
identificador de negócio na rota (FR-013).

**Justificativa**: o `id` sequencial fica exposto, mas a policy impede IDOR; o 403 não revela se o
`id` existe.

**Alternativas consideradas**: código de acesso na rota (descartado por decisão do usuário).

## R11. Consulta do negócio pelo login (FR-014)

**Decisão**: não há rota de consulta. `POST /api/v1/auth/login` devolve, além do token, o
`BusinessResponse` do cliente (ou `null`), lido só do banco local — funciona com a RAG fora do ar.
O `IAuthService` delega a leitura ao `IBusinessService` (sem acessar repositório de negócio
diretamente), mantendo responsabilidade única.

## R12. Configuração por ambiente e segredos

**Decisão**:
- `appsettings.json` (local, padrão), `appsettings.uat.json`, `appsettings.prod.json`, carregados pelo
  mecanismo nativo `appsettings.{ASPNETCORE_ENVIRONMENT}.json` (valores `uat` e `prod`, exatamente
  em minúsculas por causa do sistema de arquivos case-sensitive no Linux). Sem a variável, só o
  arquivo base vale → ambiente local.
- Chaves de configuração em camelCase (`database:host`, `ragApi:baseUrl`, `jwt:issuer`...).
- Segredos só em variáveis de ambiente em `NOME_DA_VARIAVEL`: `DATABASE_PASSWORD`,
  `JWT_SIGNING_KEY`, `RAG_API_KEY`. Como esses nomes não seguem o mapeamento `__` do .NET, são lidos
  explicitamente e aplicados com `PostConfigure` nas classes de options. A connection string é
  montada com `NpgsqlConnectionStringBuilder` (host/porta/banco/usuário do arquivo + senha da
  variável).
- `ValidateDataAnnotations().ValidateOnStart()` em todas as options: chave ausente → a aplicação
  não sobe e registra o motivo (edge case da spec). Inclusive localmente os segredos vêm só de
  variáveis de ambiente (sem `dotnet user-secrets`), conforme `.mine/plan.md`.

## R13. Logs e correlation-id

**Decisão**: `Microsoft.Extensions.Logging` com provedor de console (`AddSimpleConsole`,
`IncludeScopes = true`), nível mínimo `Information`. `CorrelationIdMiddleware` é o primeiro do
pipeline: gera um GUID por requisição, guarda em `ICorrelationIdAccessor`, abre
`logger.BeginScope({ CorrelationId })`, devolve no cabeçalho de resposta `X-Correlation-Id` e é
propagado à RAG pelo `CorrelationIdHeaderHandler`. Logs exigidos por FR-024 (login malsucedido,
401/403, falhas RAG) usam `LoggerMessage` source-generated e nunca recebem senha, token ou chave.

**Alternativas consideradas**: Serilog (dependência nova; o console nativo atende); usar
`Activity.TraceId` (não pedido; pode ser adotado com OpenTelemetry depois).

## R14. Tratamento de erros

**Decisão**: exceções de domínio customizadas (`ClientNotFoundException`,
`BusinessNotFoundException`, `BusinessAlreadyExistsException`, `EmailAlreadyRegisteredException`,
`InvalidCredentialsException`, `RagApi*Exception`), todas derivadas de `DomainException` com
status HTTP e mensagem em português. Um único `DomainExceptionHandler : IExceptionHandler` as
converte em `ProblemDetails`; exceções inesperadas viram 500 genérico com log `Error`. O
`try/catch` explícito fica nos pontos de fronteira (RAG, `DbUpdateException` de índice único), que
traduzem exceções técnicas em exceções de domínio.

## R15. Testes

**Decisão**:
- **Unitários** (`RagAuth.UnitTests`): xUnit + Moq sobre serviços, geradores, tradutor de respostas
  RAG, handlers e validações. Cobertura com `coverlet.collector` (≥ 90% de linhas).
- **Integração** (`RagAuth.IntegrationTests`): `WebApplicationFactory<Program>`
  (`Microsoft.AspNetCore.Mvc.Testing`) + PostgreSQL real em contêiner
  (`Testcontainers.PostgreSql`). A API RAG é simulada por um `HttpMessageHandler` fake injetado no
  cliente tipado (nativo, sem WireMock), que registra as requisições para os asserts de SC-004.

**Justificativas das dependências de teste** (Restrições Tecnológicas):

| Pacote | Motivo | Alternativa nativa descartada |
|---|---|---|
| Moq | Exigido em `.mine/plan.md`; reduz dublês escritos à mão | Fakes manuais — viáveis, mas o usuário escolheu Moq |
| coverlet.collector | Medir o gate de 90% | Nenhuma ferramenta de cobertura no SDK sem pacote (`Microsoft.Testing.Extensions.CodeCoverage` também é pacote; coverlet já vem no template xUnit) |
| Microsoft.AspNetCore.Mvc.Testing | Host em memória para testes de integração de cada endpoint | Subir a API num processo e chamar via HTTP — lento e frágil |
| Testcontainers.PostgreSql | Índice parcial, transações e violação de unicidade só se comportam fielmente em PostgreSQL | Provider InMemory/SQLite do EF — não reproduzem índice parcial nem erros `23505` |

## R16. Escala e desempenho

**Decisão**: alvo inicial de até ~10.000 clientes (SC-008) e baixo volume concorrente; SC-005
(p95 < 500 ms sem a RAG) é atendido com uma consulta indexada por requisição autenticada
(`OnTokenValidated`) e hash PBKDF2 só em cadastro/login. Nenhum cache necessário em v1.
