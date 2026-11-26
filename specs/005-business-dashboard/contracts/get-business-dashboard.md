# Contrato: GET /api/v1/{clientId}/business

Consulta só de leitura dos dados do painel do cliente logado.

## Requisição

| Item | Valor |
|------|-------|
| Método | `GET` |
| Rota | `/api/v1/{clientId}/business` |
| `clientId` | inteiro; MUST ser igual ao claim `client_id` do token |
| Cabeçalho | `Authorization: Bearer <accessToken>` |
| Corpo | nenhum |
| CORS | mesma política global `FrontendPolicy` (feature 004) |

## Respostas

Todas usam o envelope `ApiResponse` (`statusCode`, `success`, `message`, `data`, `error`).
Nas falhas, `error` é o `ProblemDetails` padrão da API (com `correlationId`).

### 200 — cliente com negócio

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Negócio encontrado com sucesso.",
  "data": {
    "client": {
      "id": 42,
      "name": "Maria Silva",
      "email": "maria@exemplo.com.br",
      "emailConfirmed": true,
      "createdAt": "2026-11-01T14:00:00+00:00",
      "updatedAt": "2026-11-01T14:05:00+00:00"
    },
    "business": {
      "name": "Padaria Pão Quente",
      "description": "Padaria artesanal com pães de fermentação natural.",
      "email": "contato@paoquente.com.br",
      "telephone": "(11) 98765-4321",
      "address": "Rua das Flores, 123 - Centro, São Paulo - SP",
      "details": "Aberto de seg. a sáb., 6h às 20h.",
      "createdAt": "2026-11-01T14:32:10+00:00",
      "updatedAt": "2026-11-20T09:15:44+00:00"
    }
  },
  "error": null
}
```

Campos opcionais do negócio sem valor vêm como `null`.

### 200 — cliente sem negócio

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Nenhum negócio cadastrado. Cadastre o seu negócio para vê-lo no painel.",
  "data": {
    "client": { "id": 42, "name": "Maria Silva", "email": "maria@exemplo.com.br", "emailConfirmed": true, "createdAt": "...", "updatedAt": "..." },
    "business": null
  },
  "error": null
}
```

### 401 — sem token, token inválido/expirado, conta removida ou e-mail não confirmado

```json
{
  "statusCode": 401,
  "success": false,
  "message": "Token ausente, inválido ou expirado.",
  "data": null,
  "error": { "title": "Não autenticado", "status": 401, "detail": "Token ausente, inválido ou expirado.", "instance": "/api/v1/42/business", "correlationId": "..." }
}
```

### 403 — `clientId` da rota diferente do token (ou não numérico)

Mesmo comportamento das demais rotas do grupo protegido pela política `ClientRouteOwner`.

### 500 — erro inesperado

`message` = "Ocorreu um erro inesperado. Tente novamente mais tarde." (`DomainExceptionHandler`).

## Garantias

- Nunca devolve `passwordHash`, `accessCode`, `deletedAt` nem links de confirmação/recuperação.
- Não chama a API de recursos RAG; funciona com ela fora do ar.
- Não altera dados; chamadas repetidas devolvem o mesmo resultado enquanto nada mudar.
