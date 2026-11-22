# Quickstart: validar a liberação de CORS

## Pré-requisitos

- .NET 10 SDK, PostgreSQL e variáveis de ambiente já usadas pela API (`DATABASE_PASSWORD`,
  `JWT_SIGNING_KEY`, `RAG_API_KEY`, `RESEND_API_KEY`).
- `cors:allowedOrigins` contendo `http://localhost:4200` (padrão de `appsettings.json`).

## 1. Testes automatizados

```bash
dotnet test RagAuth.slnx --settings coverlet.runsettings
```

Esperado: todos os testes passam (incluindo `CorsTests`, `FrontendCorsOptionsTests`,
`FrontendCorsPolicyTests` e os casos novos de `StartupConfigurationTests`); cobertura de linhas ≥ 90%.

## 2. Validação manual com a API rodando

```bash
dotnet run --project src/RagAuth.Api
```

Substitua `<porta>` pela porta exibida no console.

**Origem autorizada (contrato C1)** — esperado `204` com `Access-Control-Allow-Origin: http://localhost:4200`:

```bash
curl -i -X OPTIONS http://localhost:<porta>/api/v1/auth/login \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: content-type,authorization"
```

**Origem não autorizada (C3)** — esperado: sem `Access-Control-Allow-Origin`:

```bash
curl -i -X OPTIONS http://localhost:<porta>/api/v1/auth/login \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: POST"
```

**Método não permitido (C2)** — repetir o primeiro comando com
`Access-Control-Request-Method: PATCH`; esperado: PATCH não autorizado.

**Erro com cabeçalho (C4)** — login com credenciais inválidas a partir da origem autorizada;
esperado: status de erro de sempre **e** `Access-Control-Allow-Origin: http://localhost:4200`:

```bash
curl -i -X POST http://localhost:<porta>/api/v1/auth/login \
  -H "Origin: http://localhost:4200" -H "Content-Type: application/json" \
  -d '{"email":"naoexiste@exemplo.com","password":"Senha123"}'
```

## 3. Validação pelo front-end

Com o front-end rodando em `http://localhost:4200`, executar cadastro, login e uma chamada a rota
protegida; o console do navegador não deve exibir erros de CORS.
