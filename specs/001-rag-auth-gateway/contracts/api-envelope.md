# Contrato: Envelope de resposta

Todas as respostas (sucesso e erro) usam `Content-Type: application/json` e o envelope abaixo.
Decisão e conciliação com a Constituição VII em [research.md](../research.md) R1.

```json
{
  "statusCode": 200,
  "success": true,
  "message": "string",
  "data": {},
  "error": null
}
```

| Campo | Tipo | Regra |
|---|---|---|
| `statusCode` | int | igual ao status HTTP |
| `success` | bool | `true` para 2xx |
| `message` | string | mensagem curta em português |
| `data` | object \| null | payload em sucesso; `null` em erro |
| `error` | `ProblemDetails` \| null | `null` em sucesso; `ProblemDetails` nativo em erro |

## Erro genérico

```json
{
  "statusCode": 404,
  "success": false,
  "message": "Não há negócio cadastrado para este cliente.",
  "data": null,
  "error": {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    "title": "Recurso não encontrado",
    "status": 404,
    "detail": "Não há negócio cadastrado para este cliente.",
    "instance": "/api/v1/42/business",
    "correlationId": "0b6f9a1e-3c1d-4b9e-9d1a-6f0c2a7e5d11"
  }
}
```

## Erro de validação (400)

```json
{
  "statusCode": 400,
  "success": false,
  "message": "Um ou mais campos são inválidos.",
  "data": null,
  "error": {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Um ou mais campos são inválidos.",
    "status": 400,
    "errors": {
      "email": ["Informe um e-mail válido."],
      "password": ["A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número."]
    },
    "correlationId": "0b6f9a1e-3c1d-4b9e-9d1a-6f0c2a7e5d11"
  }
}
```

## Cabeçalhos comuns

| Cabeçalho | Direção | Regra |
|---|---|---|
| `Authorization: Bearer <token>` | requisição | obrigatório em toda rota exceto `/api/v1/auth/*` |
| `X-Correlation-Id` | resposta | GUID gerado no início do pipeline |

## Status comuns a rotas autenticadas

| Status | Quando |
|---|---|
| 401 | sem token, `Authorization` sem `Bearer`, token malformado/adulterado/expirado, conta removida |
| 403 | `{clientId}` da rota diferente da claim `client_id` do token (inclusive não numérico ou inexistente) |
| 500 | erro inesperado (mensagem genérica, detalhes só no log) |
