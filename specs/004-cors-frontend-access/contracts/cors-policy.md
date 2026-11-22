# Contrato HTTP: política de CORS (`FrontendPolicy`)

Vale para **todas** as rotas da API. Origem autorizada por padrão: `http://localhost:4200`
(configurável em `cors:allowedOrigins`, ver [data-model.md](../data-model.md)).

## C1. Preflight de origem autorizada

```http
OPTIONS /api/v1/auth/login
Origin: http://localhost:4200
Access-Control-Request-Method: POST
Access-Control-Request-Headers: content-type, authorization, x-qualquer-coisa
```

Resposta: `204 No Content`, sem exigir token, com:

- `Access-Control-Allow-Origin: http://localhost:4200`
- `Access-Control-Allow-Methods`: o método solicitado (um entre GET, POST, PUT, DELETE)
- `Access-Control-Allow-Headers`: os cabeçalhos solicitados
- **sem** `Access-Control-Allow-Credentials`

Vale igualmente para rotas protegidas (ex.: `/api/v1/business/...`) e para métodos GET, PUT e DELETE.

## C2. Preflight com método não permitido

`Access-Control-Request-Method: PATCH` (ou outro fora da lista) a partir da origem autorizada →
resposta sem `Access-Control-Allow-Methods` contendo o método; o navegador bloqueia a requisição.

## C3. Origem não autorizada

Qualquer `Origin` fora da configuração — ex.: `http://localhost:3000`, `https://localhost:4200`,
`http://127.0.0.1:4200` — em preflight ou requisição real → resposta **sem**
`Access-Control-Allow-Origin`. O status e o corpo da requisição real não mudam; apenas o navegador
deixa de entregá-los à página.

## C4. Requisição real de origem autorizada

Qualquer requisição GET/POST/PUT/DELETE com `Origin: http://localhost:4200` recebe
`Access-Control-Allow-Origin: http://localhost:4200` **inclusive em respostas de erro**:

| Situação | Status | Corpo | ACAO presente |
|----------|--------|-------|---------------|
| Sucesso | 2xx | inalterado | sim |
| Validação | 400 | ValidationProblem inalterado | sim |
| Sem token / token inválido | 401 | inalterado | sim |
| Exceção de domínio (ex.: credenciais inválidas) | 4xx | ProblemDetails inalterado | sim |
| Rota inexistente | 404 | inalterado | sim |

A liberação de origem não dispensa autenticação nem validação (FR-009).

## C5. Requisição sem `Origin`

Chamadas servidor-a-servidor, `curl` sem `Origin` e testes: comportamento idêntico ao anterior,
sem cabeçalhos `Access-Control-*`.
