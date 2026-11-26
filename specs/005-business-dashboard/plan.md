# Implementation Plan: Dados do painel (dashboard) do negócio

**Branch**: `feat/dashboard_resource` (feature `005-business-dashboard`) | **Date**: 2026-11-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-business-dashboard/spec.md` + decisões do autor
em `.mine/dashboard-resource/plan.md`

## Summary

Expor uma consulta autenticada e só de leitura, `GET /api/v1/{clientId}/business`, que devolve o
resumo da conta do cliente logado e, se existir, o seu negócio. A consulta faz `clients LEFT JOIN
businesses` em uma única ida ao banco (EF Core `Include` + `AsNoTracking`), só para clientes
ativos com e-mail confirmado. Sem negócio → 200 com `business: null` e mensagem orientando o
cadastro. Erros seguem o envelope `ApiResponse` + `ProblemDetails` atual. Segurança (401/403) e
CORS vêm do grupo de rotas e da política global já existentes. Detalhes em
[research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (Minimal API)

**Primary Dependencies**: ASP.NET Core, EF Core + Npgsql, JWT Bearer — todos já presentes; nenhum
pacote novo

**Storage**: PostgreSQL, tabelas existentes `clients` e `businesses` (somente leitura; sem migração)

**Testing**: xUnit — unitários em `tests/RagAuth.UnitTests`, integração em
`tests/RagAuth.IntegrationTests` com `RagAuthApiFactory`

**Target Platform**: Servidor Linux/Windows (Kestrel)

**Project Type**: web-service (API de autenticação e cadastro de negócio)

**Performance Goals**: p95 ≤ 1 s (SC-004); uma consulta indexada por PK

**Constraints**: não depender da API RAG (FR-008); não expor dados sensíveis (FR-007); sem
métricas nem coleta nova (FR-012)

**Scale/Scope**: 1 endpoint, 1 serviço, 1 método de repositório, 3 DTOs/records novos

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Avaliação pré-design | Pós-design |
|-----------|----------------------|------------|
| I. Arquitetura em Camadas | Endpoint só delega a `IBusinessDashboardService` e monta o envelope; acesso a dados via `IClientRepository`; a escolha da mensagem fica no serviço | ✔ |
| II. Responsabilidade Única / métodos ≤ 20 linhas | Serviço de leitura separado de `BusinessService`; mapper estático separado; todos os métodos com poucas linhas | ✔ |
| III. Nomenclatura | `BusinessDashboardService`, `FindConfirmedWithBusinessByIdAsync`, `ClientSummaryResponse`, `BusinessDashboardResult` | ✔ |
| IV. Contratos via DTO | Saída por `BusinessDashboardResponse`, `ClientSummaryResponse` e o `BusinessResponse` existente; entidades nunca expostas | ✔ |
| V. DI e abstrações | `IBusinessDashboardService` registrado em `BusinessServiceExtensions`; depende de `IClientRepository` injetado | ✔ |
| VI. Testes | Unitários do serviço (com/sem negócio, cliente ausente), mapper e handler; integração do endpoint (200 com/sem negócio, atualização, remoção, 401, 403, API RAG fora, campos sensíveis ausentes, CORS); cobertura ≥ 90% | ✔ |
| VII. Erros e Validação | Mensagens em português; erros pelo `ProblemDetails` existente; sem entrada validável além do `clientId` (já tratado pela política) | ✔ |
| Restrições tecnológicas | Nenhuma dependência nova | ✔ |

Nenhuma violação → seção Complexity Tracking não se aplica.

## Project Structure

### Documentation (this feature)

```text
specs/005-business-dashboard/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── get-business-dashboard.md
├── checklists/
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

```text
src/RagAuth.Api/
├── Contracts/Dashboard/
│   ├── BusinessDashboardResponse.cs        # NOVO — { Client, Business? }
│   └── ClientSummaryResponse.cs            # NOVO — id, name, email, emailConfirmed, createdAt, updatedAt
├── Endpoints/
│   └── BusinessEndpoints.cs                # ALTERADO — MapGet(string.Empty, GetDashboardAsync) no grupo existente
├── Infrastructure/Persistence/Repositories/
│   ├── IClientRepository.cs                # ALTERADO — FindConfirmedWithBusinessByIdAsync
│   └── EntityFrameworkClientRepository.cs  # ALTERADO — AsNoTracking + Include(Business) + EmailConfirmed
├── Services/
│   ├── BusinessServiceExtensions.cs        # ALTERADO — registra IBusinessDashboardService
│   └── Dashboard/
│       ├── IBusinessDashboardService.cs    # NOVO
│       ├── BusinessDashboardService.cs     # NOVO — busca, lança ClientNotFoundException, escolhe mensagem
│       ├── BusinessDashboardMapper.cs      # NOVO — entidade → DTOs (reusa BusinessMapper)
│       └── BusinessDashboardResult.cs      # NOVO — (Message, Dashboard)

tests/RagAuth.UnitTests/
├── Services/Dashboard/BusinessDashboardServiceTests.cs   # NOVO
├── Services/Dashboard/BusinessDashboardMapperTests.cs    # NOVO
└── Endpoints/EndpointHandlersTests.cs                    # ALTERADO — caso do GetDashboardAsync

tests/RagAuth.IntegrationTests/
└── BusinessDashboardEndpointsTests.cs                    # NOVO
```

**Structure Decision**: projeto único existente (`src/RagAuth.Api` + dois projetos de teste). A
rota entra no grupo `/api/v1/{clientId}/business` de `BusinessEndpoints`, herdando a política
`ClientRouteOwner`; a lógica fica num serviço próprio em `Services/Dashboard`.

## Complexity Tracking

Não se aplica — nenhuma violação da constituição.
