# rag-auth

API de autenticação e controle de acesso que fica na frente da API de recursos RAG. Ela cadastra e
autentica clientes (e-mail e senha → JWT com o código de acesso de 12 caracteres), protege as rotas
por token e gerencia o único negócio de cada cliente, repassando criação, alteração e remoção à API
RAG com `x-api-key`, o token do cliente e `user_code`. A gravação local só é confirmada depois do 2xx
da API RAG.

Especificação, plano e contratos: [`specs/001-rag-auth-gateway/`](specs/001-rag-auth-gateway/).
Validação ponta a ponta: [`quickstart.md`](specs/001-rag-auth-gateway/quickstart.md).

## Rotas

| Método | Rota | Token |
|---|---|---|
| `POST` | `/api/v1/auth/register` | não |
| `POST` | `/api/v1/auth/login` (devolve também o negócio) | não |
| `POST` / `PUT` / `DELETE` | `/api/v1/{clientId}/business` | sim, `{clientId}` = claim `client_id` |
| `DELETE` | `/api/v1/{clientId}` (remove a própria conta) | sim, `{clientId}` = claim `client_id` |

Toda resposta usa o envelope `{ statusCode, success, message, data, error }`, com `error` no formato
`ProblemDetails`.

## Pré-requisitos

- .NET SDK 10.0.x
- Docker (PostgreSQL local e Testcontainers nos testes de integração)
- Ferramentas locais: `dotnet tool restore` (instala o `dotnet-ef` fixado em `.config/dotnet-tools.json`)

## Configuração

As chaves não secretas ficam em `src/RagAuth.Api/appsettings.json` (local), `appsettings.uat.json` e
`appsettings.prod.json`. Os segredos vêm **só** de variáveis de ambiente:

| Variável | Uso |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | vazio (local), `uat` ou `prod` |
| `DATABASE_PASSWORD` | senha do PostgreSQL |
| `JWT_SIGNING_KEY` | chave HS256, no mínimo 32 caracteres |
| `RAG_API_KEY` | valor enviado em `x-api-key` à API RAG |

Se faltar algum segredo, a aplicação não inicia e a mensagem cita a variável ausente.

> No Windows, nomes de variáveis de ambiente não diferenciam maiúsculas: uma variável já existente
> como `database_password` (de outro projeto) sobrescreve `DATABASE_PASSWORD`. Remova-a da sessão
> antes de rodar a API.

## Executar localmente

```bash
docker run -d --name rag-auth-db -p 5432:5432 \
  -e POSTGRES_DB=rag_auth -e POSTGRES_USER=rag_auth -e POSTGRES_PASSWORD=dev_password postgres:17

export DATABASE_PASSWORD=dev_password
export JWT_SIGNING_KEY="uma-chave-local-com-pelo-menos-32-bytes!!"
export RAG_API_KEY=chave-local-da-api-rag

dotnet tool restore
dotnet ef database update --project src/RagAuth.Api
dotnet run --project src/RagAuth.Api
```

No PowerShell, defina as variáveis com `$env:DATABASE_PASSWORD = "dev_password"` (idem para as demais).
O `dotnet ef` também precisa das três variáveis, porque monta a aplicação para ler a configuração.

Nova migration: `dotnet ef migrations add <Nome> --project src/RagAuth.Api --output-dir Infrastructure/Persistence/Migrations`.

## Testes

```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

Os testes de integração sobem um PostgreSQL 17 via Testcontainers (o Docker precisa estar rodando) e
simulam a API RAG com um `HttpMessageHandler` fake. A cobertura de linhas dos testes unitários deve
ser de no mínimo 90% (Constituição VI); o relatório Cobertura fica em `TestResults/`.
