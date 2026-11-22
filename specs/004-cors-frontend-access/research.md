# Research: Liberação de CORS para o front-end

Nenhum item ficou como NEEDS CLARIFICATION no Technical Context. As decisões abaixo registram as
escolhas de design.

## R1. Mecanismo de CORS

- **Decision**: usar o CORS nativo do ASP.NET Core (`AddCors` + `UseCors`).
- **Rationale**: já faz parte do shared framework `Microsoft.AspNetCore.App`; trata preflight,
  normalização de origem e cabeçalhos `Access-Control-*` conforme a especificação Fetch. Atende à
  restrição da constituição de não adicionar dependências.
- **Alternatives considered**: middleware próprio escrevendo cabeçalhos (reimplementa regras de
  preflight e é propenso a erro); proxy reverso fazendo CORS (move a regra para fora da aplicação e
  não é testável com xUnit).

## R2. Escopo da política

- **Decision**: política nomeada `FrontendPolicy` aplicada globalmente com
  `app.UseCors(FrontendPolicyName)`.
- **Rationale**: FR-008 exige a liberação em todas as rotas; aplicar globalmente evita esquecer
  rotas futuras e cobre respostas de rotas inexistentes (404).
- **Alternatives considered**: `RequireCors` por grupo de endpoints (exige lembrar de cada novo
  grupo); política padrão (`AddDefaultPolicy`) — funciona, mas o nome explícito deixa claro nos
  testes e no pipeline qual política está ativa.

## R3. Posição no pipeline e respostas de erro

- **Decision**: `UseCors` logo após `CorrelationIdMiddleware`, antes de `UseExceptionHandler`,
  `UseStatusCodePages` e `UseAuthentication`.
- **Rationale**: o preflight precisa ser respondido sem autenticação (FR-004). Para requisições
  reais, o `CorsMiddleware` aplica os cabeçalhos por meio de `Response.OnStarting`; o
  `ExceptionHandlerMiddleware` limpa cabeçalhos e corpo (`Response.Clear`), mas não remove callbacks
  de `OnStarting`, então os cabeçalhos CORS chegam também no ProblemDetails de erro (FR-008).
- **Alternatives considered**: após `UseAuthentication` (preflight receberia 401 nas rotas
  protegidas); após `UseExceptionHandler` (funciona, mas deixa a correção dependente de detalhes do
  handler). Comportamento confirmado por testes de integração (401 sem token e exceção de domínio).

## R4. Normalização da origem configurada

- **Decision**: remover a barra final de cada origem configurada (`TrimEnd('/')`) antes de
  registrá-la na política.
- **Rationale**: o pedido usa `http://localhost:4200/`, mas o navegador envia `Origin` sem barra;
  `WithOrigins` só normaliza esquema/host para minúsculas, não a barra final.
- **Alternatives considered**: exigir a origem sem barra via validação (mais rígido e fácil de
  errar na configuração).

## R5. Métodos, cabeçalhos e credenciais

- **Decision**: `WithMethods("GET", "POST", "PUT", "DELETE")`, `AllowAnyHeader()`, sem
  `AllowCredentials()`, sem `SetPreflightMaxAge` e sem `WithExposedHeaders`.
- **Rationale**: reflete o pedido ("DEL" = DELETE, qualquer header). O token vai em
  `Authorization`, que é um cabeçalho comum de requisição e não depende de credenciais de CORS.
  Valores padrão de cache do preflight são suficientes.
- **Alternatives considered**: `AllowAnyMethod` (liberaria PATCH e outros, violando FR-006);
  `AllowCredentials` (desnecessário sem cookies e aumenta a superfície de ataque).

## R6. Configuração por ambiente

- **Decision**: seção `cors` com `allowedOrigins` (lista), validada com DataAnnotations e
  `ValidateOnStart`, mensagens em português. `appsettings.json` traz `http://localhost:4200`;
  `appsettings.dev.json` traz placeholder `SUBSTITUIR-...` como as demais URLs de UAT.
- **Rationale**: FR-007 (mudar origens sem alterar código) e mesmo padrão de `RagApiOptions` e
  `EmailOptions`. A aplicação não sobe com configuração vazia ou origem que não seja URL absoluta,
  evitando uma API silenciosamente inacessível pelo front-end.
- **Alternatives considered**: lista fixa no código (viola FR-007); variável de ambiente única
  separada por vírgula (foge do padrão de configuração do projeto).

### Confirmação na implementação (T012)

Os testes de integração de `tests/RagAuth.IntegrationTests/CorsTests.cs` confirmaram que, com
`UseCors` antes de `UseExceptionHandler`, o cabeçalho `Access-Control-Allow-Origin` chega nas
respostas 400 (validação), 401 (sem token e credenciais inválidas), 404 e 405. O preflight de rotas
protegidas responde 204 sem token. Observação: rota inexistente **sem token** responde 401 (política
de autorização da API), não 404 — comportamento pré-existente, mantido.
