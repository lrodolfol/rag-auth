---

description: "Task list for feature 005-business-dashboard"
---

# Tasks: Dados do painel (dashboard) do negócio

**Input**: Design documents from `/specs/005-business-dashboard/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/get-business-dashboard.md, quickstart.md

**Tests**: INCLUÍDOS — exigidos pela constituição (princípio VI) e listados no plan.md (unitários do
serviço, mapper e handler; integração do endpoint; cobertura ≥ 90%).

**Organization**: tarefas agrupadas por user story. US1 e US3 são P1; US2 é P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: US1, US2, US3 (ver spec.md)

## Path Conventions

Projeto único: `src/RagAuth.Api/`, `tests/RagAuth.UnitTests/`, `tests/RagAuth.IntegrationTests/`.
Nenhum pacote novo, nenhuma migração (data-model.md).

---

## Phase 1: Setup

**Purpose**: nada a inicializar — solução, pacotes, banco e grupo de rotas já existem.

- [X] T001 Confirmar que a solução compila e os testes atuais passam antes de mudar código: `dotnet build RagAuth.slnx` e `dotnet test RagAuth.slnx` na raiz do repositório

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: contratos de saída, consulta ao banco e serviço do painel, usados por todas as stories.

**⚠️ CRITICAL**: nenhuma story começa antes desta fase terminar.

- [X] T002 [P] Criar o record `ClientSummaryResponse(int Id, string Name, string Email, bool EmailConfirmed, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)` em `src/RagAuth.Api/Contracts/Dashboard/ClientSummaryResponse.cs` (namespace `RagAuth.Api.Contracts.Dashboard`). NÃO incluir `AccessCode`, `PasswordHash`, `DeletedAt`, `EmailConfirmedAt`, `PasswordChangedAt` nem links (FR-007)
- [X] T003 [P] Criar o record `BusinessDashboardResponse(ClientSummaryResponse Client, BusinessResponse? Business)` em `src/RagAuth.Api/Contracts/Dashboard/BusinessDashboardResponse.cs`, reutilizando `RagAuth.Api.Contracts.Business.BusinessResponse`; `Business` é `null` quando o cliente não tem negócio (FR-006)
- [X] T004 [P] Adicionar ao `IClientRepository` o método `Task<Client?> FindConfirmedWithBusinessByIdAsync(int clientId, CancellationToken cancellationToken = default);` com `<summary>` em português ("Cliente ativo com e-mail confirmado, já com o negócio carregado (LEFT JOIN), sem rastreamento.") em `src/RagAuth.Api/Infrastructure/Persistence/Repositories/IClientRepository.cs`
- [X] T005 Implementar `FindConfirmedWithBusinessByIdAsync` em `src/RagAuth.Api/Infrastructure/Persistence/Repositories/EntityFrameworkClientRepository.cs` como `databaseContext.Clients.AsNoTracking().Include(client => client.Business).FirstOrDefaultAsync(client => client.Id == clientId && client.EmailConfirmed, cancellationToken)`; manter o filtro global `DeletedAt == null` (research R4) (depende de T004)
- [X] T006 [P] Criar o record interno `BusinessDashboardResult(string Message, BusinessDashboardResponse Dashboard)` em `src/RagAuth.Api/Services/Dashboard/BusinessDashboardResult.cs` (namespace `RagAuth.Api.Services.Dashboard`) (depende de T003)
- [X] T007 [P] Criar a classe estática `BusinessDashboardMapper` em `src/RagAuth.Api/Services/Dashboard/BusinessDashboardMapper.cs` com `ToClientSummary(Client client)` → `ClientSummaryResponse` e `ToResponse(Client client)` → `BusinessDashboardResponse`, onde `Business = client.Business is null ? null : BusinessMapper.ToResponse(client.Business)` (reusar `RagAuth.Api.Services.Business.BusinessMapper`) (depende de T002, T003)
- [X] T008 [P] Criar a interface `IBusinessDashboardService` com `Task<BusinessDashboardResult> GetDashboardAsync(int clientId, CancellationToken cancellationToken = default);` em `src/RagAuth.Api/Services/Dashboard/IBusinessDashboardService.cs` (depende de T006)
- [X] T009 Implementar `BusinessDashboardService(IClientRepository clientRepository) : IBusinessDashboardService` em `src/RagAuth.Api/Services/Dashboard/BusinessDashboardService.cs`: chama `FindConfirmedWithBusinessByIdAsync`; se `null` lança `ClientNotFoundException` (→ 401, research R5); mensagem `"Negócio encontrado com sucesso."` quando há negócio, senão `"Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel."` (textos exatos do data-model.md, como constantes privadas); devolve `new BusinessDashboardResult(message, BusinessDashboardMapper.ToResponse(client))`. Não depende da API RAG (FR-008); métodos ≤ 20 linhas (depende de T005, T007, T008)
- [X] T010 Registrar `services.AddScoped<IBusinessDashboardService, BusinessDashboardService>();` em `src/RagAuth.Api/Services/BusinessServiceExtensions.cs` (adicionar `using RagAuth.Api.Services.Dashboard;`) (depende de T009)
- [X] T011 Adicionar o handler `internal static async Task<IResult> GetDashboardAsync(int clientId, IBusinessDashboardService dashboardService, CancellationToken cancellationToken)` e o mapeamento `businessGroup.MapGet(string.Empty, GetDashboardAsync);` no grupo existente (herda a política `ClientRouteOwner`) em `src/RagAuth.Api/Endpoints/BusinessEndpoints.cs`; o handler chama o serviço e devolve `TypedResults.Json(ApiResponse.Succeeded(StatusCodes.Status200OK, result.Message, result.Dashboard), statusCode: StatusCodes.Status200OK)` (depende de T010)

**Checkpoint**: `GET /api/v1/{clientId}/business` responde; as stories abaixo validam cada comportamento.

---

## Phase 3: User Story 1 - Ver o painel do meu negócio (Priority: P1) 🎯 MVP

**Goal**: cliente logado com negócio recebe 200 com o resumo da conta e todos os dados do negócio.

**Independent Test**: cadastrar cliente, confirmar e-mail, login, cadastrar negócio, `GET` com o
token → `data.client` e `data.business` idênticos ao cadastrado.

### Tests for User Story 1

- [X] T012 [P] [US1] Criar `tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardMapperTests.cs`: (a) cliente com negócio completo mapeia todos os campos de `ClientSummaryResponse` e `BusinessResponse`; (b) negócio sem `Email`, `Telephone`, `Address`, `Details` mapeia esses campos como `null` sem erro (US1 cenário 3)
- [X] T013 [P] [US1] Criar `tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardServiceTests.cs` com `Mock<IClientRepository>` (padrão de `BusinessServiceTests.cs`): cliente com negócio → mensagem `"Negócio encontrado com sucesso."` e `Dashboard.Business` preenchido; repositório devolve `null` → lança `ClientNotFoundException`
- [X] T014 [P] [US1] Adicionar a `tests/RagAuth.UnitTests/Endpoints/EndpointHandlersTests.cs` um teste de `BusinessEndpoints.GetDashboardAsync` com `Mock<IBusinessDashboardService>`: devolve 200 com envelope `success = true`, `message` e `data` vindos do `BusinessDashboardResult`
- [X] T015 [US1] Criar `tests/RagAuth.IntegrationTests/BusinessDashboardEndpointsTests.cs` (usar `RagAuthApiFactory` e os helpers de `BusinessEndpointsTests.cs`) com: (a) cliente com negócio → 200, `message = "Negócio encontrado com sucesso."`, `data.client` (id, name, email, emailConfirmed, createdAt, updatedAt) e `data.business` iguais ao cadastrado; (b) após `PUT`, novo `GET` traz os dados e o `updatedAt` novos (US1 cenário 2); (c) negócio só com campos obrigatórios → opcionais `null` (US1 cenário 3); (d) o JSON de `data.client` NÃO contém `passwordHash`, `accessCode`, `deletedAt` (FR-007); (e) dois `GET` seguidos devolvem o mesmo corpo `data` (FR-009); (f) nenhuma chamada à API RAG é feita no `GET` e com a RAG falhando o `GET` continua 200 (FR-008, SC-005)

### Implementation for User Story 1

- [X] T016 [US1] Rodar `dotnet test --filter "FullyQualifiedName~BusinessDashboard"` e `dotnet test tests/RagAuth.UnitTests --filter "FullyQualifiedName~EndpointHandlers"`; corrigir em `src/RagAuth.Api/Services/Dashboard/` ou `src/RagAuth.Api/Endpoints/BusinessEndpoints.cs` qualquer falha dos testes T012–T015

**Checkpoint**: US1 funcional e testada de forma independente.

---

## Phase 4: User Story 3 - Proteção dos dados do painel (Priority: P1)

**Goal**: só o próprio cliente autenticado vê o painel; 401 sem token válido, 403 para outro cliente.

**Independent Test**: `GET` sem token, com token inválido/expirado e com token de outro cliente → todos recusados, `data = null`.

### Tests for User Story 3

- [X] T017 [P] [US3] Adicionar a `tests/RagAuth.IntegrationTests/BusinessDashboardEndpointsTests.cs`: (a) sem token e com token inválido/expirado → 401, `data = null`, `message = "Token ausente, inválido ou expirado."`; (b) token do cliente A com `clientId` do cliente B → 403, `data = null`, nenhum dado de B no corpo; (c) conta removida após emitir o token → 401; (d) `clientId` não numérico na rota → recusado sem dados (mesmo status das outras rotas do grupo)
- [X] T018 [P] [US3] Adicionar a `tests/RagAuth.IntegrationTests/CorsTests.cs` um caso de `GET /api/v1/{clientId}/business` (e preflight `OPTIONS`) com `Origin` liberada (ex.: `http://localhost:4200`) recebendo `Access-Control-Allow-Origin` (FR-011)

### Implementation for User Story 3

- [X] T019 [US3] Rodar os testes T017–T018; se algum falhar, ajustar apenas o mapeamento em `src/RagAuth.Api/Endpoints/BusinessEndpoints.cs` (a rota deve estar no `businessGroup` com `RequireAuthorization(AuthorizationPolicies.ClientRouteOwner)`), sem código novo de segurança (research R1)

**Checkpoint**: US1 e US3 funcionam e estão protegidas.

---

## Phase 5: User Story 2 - Painel de quem ainda não cadastrou o negócio (Priority: P2)

**Goal**: cliente sem negócio recebe 200, resumo da conta, `business = null` e mensagem orientando o cadastro.

**Independent Test**: login com cliente sem negócio → `GET` → 200, `data.business = null`, mensagem de orientação.

### Tests for User Story 2

- [X] T020 [P] [US2] Adicionar a `tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardServiceTests.cs` o caso cliente sem negócio → mensagem `"Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel."`, `Dashboard.Business == null` e `Dashboard.Client` preenchido
- [X] T021 [P] [US2] Adicionar a `tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardMapperTests.cs` o caso `client.Business == null` → `BusinessDashboardResponse.Business == null`
- [X] T022 [US2] Adicionar a `tests/RagAuth.IntegrationTests/BusinessDashboardEndpointsTests.cs`: (a) cliente recém-logado sem negócio → 200, `success = true`, `data.client` preenchido, `data.business = null`, mensagem de orientação exata (US2 cenário 1); (b) cliente que cadastrou e depois removeu o negócio (`DELETE`) → mesma resposta (US2 cenário 2)

### Implementation for User Story 2

- [X] T023 [US2] Rodar os testes T020–T022; corrigir em `src/RagAuth.Api/Services/Dashboard/BusinessDashboardService.cs` ou `BusinessDashboardMapper.cs` qualquer falha (o `Include` deve gerar LEFT JOIN e manter o cliente quando não há negócio)

**Checkpoint**: todas as stories funcionam de forma independente.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T024 Rodar `dotnet test RagAuth.slnx` completo e confirmar que nenhum teste das features anteriores (login, negócio, CORS, rotas protegidas) quebrou
- [X] T025 Rodar `dotnet test tests/RagAuth.UnitTests --filter "FullyQualifiedName~BusinessDashboard" --collect:"XPlat Code Coverage"` e confirmar cobertura de linhas ≥ 90% em `src/RagAuth.Api/Services/Dashboard/`; adicionar testes se faltar
- [X] T026 [P] Documentar `GET /api/v1/{clientId}/business` (respostas 200 com/sem negócio, 401, 403) na seção de endpoints do `README.md`, se o README listar os endpoints
- [X] T027 Revisar que todos os métodos novos têm ≤ 20 linhas e nomes conforme a constituição (princípios II e III) em `src/RagAuth.Api/Services/Dashboard/` e `src/RagAuth.Api/Endpoints/BusinessEndpoints.cs`
- [ ] T028 Executar a validação manual de `specs/005-business-dashboard/quickstart.md` (passos 1–10)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: depende do Setup — BLOQUEIA todas as stories
- **US1, US3, US2 (Phases 3–5)**: dependem só da Phase 2; podem rodar em paralelo entre si
- **Polish (Phase 6)**: depende das stories concluídas

### User Story Dependencies

- **US1 (P1)**: só Phase 2
- **US3 (P1)**: só Phase 2 (a segurança vem do grupo de rotas já existente)
- **US2 (P2)**: só Phase 2; T020/T021 editam os mesmos arquivos de teste que T013/T012, e T022 o mesmo de T015/T017 — se feitas em paralelo, coordenar edições nesses arquivos

### Within Phase 2

T004 → T005; T002, T003 → T007; T003 → T006 → T008; T005 + T007 + T008 → T009 → T010 → T011

### Parallel Opportunities

- T002, T003, T004 em paralelo; depois T006 e T007 em paralelo
- Em US1: T012, T013, T014 em paralelo (arquivos diferentes)
- Em US3: T017 e T018 em paralelo
- Em US2: T020 e T021 em paralelo

---

## Parallel Example: User Story 1

```text
Task: "T012 BusinessDashboardMapperTests em tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardMapperTests.cs"
Task: "T013 BusinessDashboardServiceTests em tests/RagAuth.UnitTests/Services/Dashboard/BusinessDashboardServiceTests.cs"
Task: "T014 teste do GetDashboardAsync em tests/RagAuth.UnitTests/Endpoints/EndpointHandlersTests.cs"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 + Phase 2 (endpoint pronto)
2. Phase 3 (US1) → validar com e sem token válido
3. Phase 4 (US3) → garantir 401/403 antes de liberar ao front-end (US3 também é P1)

### Incremental Delivery

1. Foundation → endpoint responde
2. US1 → painel com negócio (MVP)
3. US3 → proteção comprovada por testes
4. US2 → estado "sem negócio" coberto
5. Polish → suíte completa, cobertura, quickstart

---

## Notes

- Nenhuma migração, nenhum pacote novo, nenhuma chamada à API RAG
- Mensagens em português, textos exatos do data-model.md
- Commit após cada fase ou grupo lógico
