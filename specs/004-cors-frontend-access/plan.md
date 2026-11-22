# Implementation Plan: Liberação de CORS para o front-end

**Branch**: `feat/cors_permition` | **Date**: 2026-11-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-cors-frontend-access/spec.md`

## Summary

Permitir que o front-end servido em `http://localhost:4200` consuma a API pelo navegador
(GET, POST, PUT, DELETE, qualquer cabeçalho), mantendo as demais origens e métodos bloqueados e sem
alterar autenticação/validação. Abordagem: CORS nativo do ASP.NET Core com uma política nomeada
(`FrontendPolicy`) aplicada globalmente no pipeline, alimentada por uma seção de configuração
`cors:allowedOrigins` validada na inicialização. Detalhes das decisões em [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (Minimal API)

**Primary Dependencies**: ASP.NET Core CORS (`Microsoft.AspNetCore.Cors`, parte do shared framework
`Microsoft.AspNetCore.App`) — nenhum pacote NuGet novo

**Storage**: N/A (nenhum dado persistido; sem migração)

**Testing**: xUnit — unitários em `tests/RagAuth.UnitTests`, integração em
`tests/RagAuth.IntegrationTests` com `RagAuthApiFactory`

**Target Platform**: Servidor Linux/Windows (Kestrel)

**Project Type**: web-service (API de autenticação)

**Performance Goals**: sem impacto mensurável; preflight respondido pelo middleware sem tocar em
banco ou autenticação

**Constraints**: origens somente via configuração (FR-007); cabeçalhos CORS também em respostas de
erro (FR-008); sem envio de credenciais/cookies entre origens

**Scale/Scope**: 1 política, 1 seção de configuração, todas as rotas existentes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Avaliação pré-design | Pós-design |
|-----------|----------------------|------------|
| I. Arquitetura em Camadas | Mudança apenas no pipeline HTTP e na configuração; nenhum endpoint ganha regra de negócio ou acesso a dados | ✔ |
| II. Responsabilidade Única / métodos ≤ 20 linhas | `FrontendCorsOptions` (configuração) e `CorsServiceExtensions` (registro da política) com responsabilidades separadas; métodos curtos | ✔ |
| III. Nomenclatura | `FrontendCorsOptions`, `AllowedOrigins`, `FrontendPolicyName`, `AddFrontendCors` | ✔ |
| IV. Contratos via DTO | N/A — nenhum contrato de dados novo | ✔ |
| V. DI e abstrações | Política construída a partir de `IOptions<FrontendCorsOptions>` injetado; nada instanciado com `new` além do objeto de options | ✔ |
| VI. Testes | Unitários para validação de options e política resolvida via `ICorsPolicyProvider`; integração para preflight, origens bloqueadas e respostas de erro; cobertura ≥ 90% mantida | ✔ |
| VII. Erros e validação | Mensagens de validação da configuração em português, falha na inicialização (`ValidateOnStart`) | ✔ |
| Restrições tecnológicas | Nenhuma dependência nova (CORS faz parte do framework) | ✔ |

Nenhuma violação → seção Complexity Tracking não se aplica.

## Project Structure

### Documentation (this feature)

```text
specs/004-cors-frontend-access/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── cors-policy.md
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

```text
src/RagAuth.Api/
├── Configuration/
│   ├── FrontendCorsOptions.cs           # NOVO — seção "cors", AllowedOrigins + validação
│   ├── OptionsServiceExtensions.cs      # ALTERADO — registra FrontendCorsOptions (ValidateOnStart)
│   └── ApplicationServiceExtensions.cs  # ALTERADO — AddFrontendCors() + UseCors(FrontendPolicy)
├── Security/
│   └── CorsServiceExtensions.cs         # NOVO — AddFrontendCors(), política FrontendPolicy
├── appsettings.json                     # ALTERADO — cors:allowedOrigins = ["http://localhost:4200"]
└── appsettings.dev.json                 # ALTERADO — placeholder da origem do front-end UAT

tests/RagAuth.UnitTests/Configuration/
├── FrontendCorsOptionsTests.cs          # NOVO — regras de validação
└── FrontendCorsPolicyTests.cs           # NOVO — política resolvida (origens normalizadas, métodos, headers, sem credenciais)

tests/RagAuth.IntegrationTests/
├── CorsTests.cs                         # NOVO — preflight, origens/métodos bloqueados, erros com cabeçalho
└── StartupConfigurationTests.cs         # ALTERADO — não sobe com cors:allowedOrigins vazio/inválido
```

**Structure Decision**: projeto único existente (`src/RagAuth.Api` + dois projetos de teste).
A política fica em `Security/` junto da autenticação/autorização; as options ficam em
`Configuration/`, seguindo `RagApiOptions`/`EmailOptions`.

### Pipeline resultante (`MapApplication`)

```text
CorrelationIdMiddleware
UseCors(FrontendPolicy)     ← novo: antes do exception handler e da autenticação
UseExceptionHandler
UseStatusCodePages
UseAuthentication
UseAuthorization
Map...Endpoints
```

- Preflight (`OPTIONS` + `Access-Control-Request-Method`) é respondido pelo middleware de CORS
  antes da autenticação (FR-004).
- Em requisições reais o middleware registra os cabeçalhos via `Response.OnStarting`; eles são
  aplicados mesmo quando o exception handler limpa a resposta e escreve o ProblemDetails (FR-008).
  Coberto por teste de integração com exceção de domínio e com 401 da autenticação.

## Complexity Tracking

Não se aplica — nenhuma violação da constituição.
