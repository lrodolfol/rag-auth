# Quickstart: validação ponta a ponta

**Feature**: `001-rag-auth-gateway` | **Plano**: [plan.md](./plan.md)

Guia para provar que a feature funciona. Contratos em [contracts/](./contracts/), modelo em
[data-model.md](./data-model.md).

## Pré-requisitos

- .NET SDK 10.0.x (`dotnet --list-sdks`)
- Docker (PostgreSQL local e Testcontainers nos testes de integração)
- Ferramenta EF (local, versão fixada em `.config/dotnet-tools.json`): `dotnet tool restore`

## 1. Banco local

```bash
docker run -d --name rag-auth-db -p 5432:5432 \
  -e POSTGRES_DB=rag_auth -e POSTGRES_USER=rag_auth -e POSTGRES_PASSWORD=dev_password \
  postgres:17
```

`appsettings.json` já aponta para `localhost:5432`, banco `rag_auth`, usuário `rag_auth`.

## 2. Segredos (variáveis de ambiente)

```bash
export DATABASE_PASSWORD=dev_password
export JWT_SIGNING_KEY="uma-chave-local-com-pelo-menos-32-bytes!!"
export RAG_API_KEY=chave-local-da-api-rag
```

PowerShell: `$env:DATABASE_PASSWORD = "dev_password"` (idem para as demais).

**Verificação**: sem `RAG_API_KEY`, `dotnet run` deve encerrar com log explicando a variável
ausente (edge case da spec).

## 3. Migrations e execução

```bash
dotnet ef database update --project src/RagAuth.Api
dotnet run --project src/RagAuth.Api
```

Sem `ASPNETCORE_ENVIRONMENT`, só `appsettings.json` é carregado (ambiente local). Para UAT/produção:
`ASPNETCORE_ENVIRONMENT=uat` ou `ASPNETCORE_ENVIRONMENT=prod`.

Para o fluxo de negócio sem a API RAG real, aponte `ragApi:baseUrl` para um stub local que responda
2xx (os testes de integração usam um handler fake e não precisam disso).

## 4. Cenários manuais

Use `BASE=http://localhost:5000` (porta conforme `launchSettings.json`).

| # | Ação | Resultado esperado |
|---|---|---|
| 1 | `POST $BASE/api/v1/auth/register` com nome, e-mail, senha válidos | 201; `data.clientId` numérico e `data.accessCode` com 12 caracteres; sem senha na resposta; cabeçalho `X-Correlation-Id` |
| 2 | Repetir o cadastro com o e-mail em maiúsculas | 409 |
| 3 | Cadastro com senha `abc` | 400 com `error.errors.password` |
| 4 | `POST $BASE/api/v1/auth/login` com senha errada | 401 "E-mail ou senha inválidos." |
| 5 | Login correto, antes de criar negócio | 200; `data.accessToken`, `data.expiresAt`, `data.clientId`, `data.business = null` |
| 6 | `PUT $BASE/api/v1/{clientId}/business` sem token | 401 |
| 7 | Mesmo PUT com token válido, antes de criar | 404; RAG não chamada |
| 8 | PUT com token válido e `{clientId}` de outro cliente (ou `abc`) | 403 |
| 9 | `POST .../business` com corpo válido (RAG stub 2xx) | 201; RAG recebeu `x-api-key`, `Authorization` e `user_code` |
| 10 | Segundo `POST .../business` | 409; RAG não chamada |
| 11 | `PUT .../business` com RAG stub respondendo 500 | 502; novo login mostra os dados antigos |
| 12 | Parar o stub e fazer login | 200 com `data.business` (dados locais) |
| 13 | Com o stub parado, `DELETE .../business` | 503; novo login ainda mostra o negócio |
| 14 | `DELETE $BASE/api/v1/{clientId}` com stub 2xx | 200; token antigo e login passam a dar 401 |
| 15 | Novo cadastro com o mesmo e-mail | 201 com `clientId` e `accessCode` diferentes dos anteriores |

Em todos os erros, conferir o envelope (`success: false`, `error` como `ProblemDetails`) e que o log
de console mostra o mesmo `CorrelationId` em todas as linhas da requisição, sem senha, token ou
chave.

## 5. Testes automatizados

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Esperado: todos os testes passam; cobertura de linhas ≥ 90% (gate da constituição); cada endpoint
com ao menos um teste de integração; teste de 10.000 cadastros sem colisão de código (SC-008); teste
de auditoria de logs sem dados sensíveis (SC-006).
