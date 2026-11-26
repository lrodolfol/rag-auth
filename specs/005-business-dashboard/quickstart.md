# Quickstart: validar o painel do negócio

**Feature**: `005-business-dashboard`

## Pré-requisitos

- .NET 10 SDK e PostgreSQL configurado como nas features anteriores (ver `README.md`).
- Nenhuma migração nova.

## Testes automatizados

```powershell
dotnet test RagAuth.slnx
dotnet test tests/RagAuth.IntegrationTests --filter "FullyQualifiedName~BusinessDashboard"
dotnet test tests/RagAuth.UnitTests --filter "FullyQualifiedName~BusinessDashboard" --collect:"XPlat Code Coverage"
```

Esperado: todos passam; cobertura de linhas ≥ 90%.

## Validação manual

1. Suba a API: `dotnet run --project src/RagAuth.Api`.
2. Cadastre um cliente, confirme o e-mail e faça login; guarde `accessToken` e `clientId`.
3. **Sem negócio (US2)**: `GET /api/v1/{clientId}/business` com `Authorization: Bearer <token>`
   → 200, `data.business = null`, mensagem orientando o cadastro.
4. Cadastre o negócio (`POST /api/v1/{clientId}/business`).
5. **Com negócio (US1)**: repita o `GET` → 200, `data.client` e `data.business` iguais ao
   cadastrado.
6. Atualize o negócio (`PUT`) e repita o `GET` → dados e `updatedAt` novos.
7. Remova o negócio (`DELETE`) e repita o `GET` → mesmo resultado do passo 3.
8. **Proteção (US3)**:
   - sem token ou token inválido → 401, `data = null`;
   - token de outro cliente com o `clientId` deste → 403, `data = null`.
9. **Independência da API RAG**: com a API RAG parada, o `GET` continua respondendo 200.
10. **CORS**: o `GET` feito pelo navegador a partir de `http://localhost:4200` é aceito.

Formato completo das respostas: [contracts/get-business-dashboard.md](./contracts/get-business-dashboard.md).
Campos e consulta: [data-model.md](./data-model.md).
