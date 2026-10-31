# Contrato: Negócio (`BusinessEndpoints.cs`)

Todas as rotas exigem `Authorization: Bearer <token>`. `{clientId}` é o `id` numérico do cliente
(devolvido no cadastro e no login) e precisa ser igual à claim `client_id` do token, senão **403**
(research R10). Não há identificador de negócio na rota: a operação é sempre sobre "meu negócio"
(FR-013). Não há rota de consulta: o negócio vem na resposta do login
([auth-endpoints.md](./auth-endpoints.md)). Status comuns (401/403/500) em
[api-envelope.md](./api-envelope.md).

## Corpo de criação/alteração (`BusinessRequest`)

```json
{
  "name": "Padaria da Ana",
  "description": "Pães artesanais e cafés.",
  "email": "contato@padariadaana.com",
  "telephone": "(11) 98765-4321",
  "address": "Rua das Flores, 123, Centro, São Paulo",
  "details": "Aberto de segunda a sábado, das 6h às 20h."
}
```

| Campo | Regra (FR-016a) |
|---|---|
| `name` | obrigatório, até 150 |
| `description` | obrigatório, até 1.000 |
| `email` | opcional; formato válido se informado |
| `telephone` | opcional; 10 ou 11 dígitos com DDD após remover máscara |
| `address` | opcional; até 300; sem quebra de linha |
| `details` | opcional; até 2.000 |

## Resposta de negócio (`BusinessResponse`)

```json
{
  "name": "Padaria da Ana",
  "description": "Pães artesanais e cafés.",
  "email": "contato@padariadaana.com",
  "telephone": "11987654321",
  "address": "Rua das Flores, 123, Centro, São Paulo",
  "details": "Aberto de segunda a sábado, das 6h às 20h.",
  "createdAt": "2026-10-25T13:05:00Z",
  "updatedAt": "2026-10-25T13:05:00Z"
}
```

## POST /api/v1/{clientId}/business

Ordem: validar → checar se já existe (409) → gravar em transação → RAG POST → commit.

| Status | Quando | Chama RAG? |
|---|---|---|
| 201 | negócio criado; `data` = `BusinessResponse` | sim |
| 400 | validação | não |
| 409 | cliente já possui negócio | não |
| 4xx / 502 / 503 / 504 | falha da RAG ([rag-api-outbound.md](./rag-api-outbound.md)); nada é gravado | sim |

## PUT /api/v1/{clientId}/business

Substituição completa (mesmo corpo do POST). Ordem: validar → buscar (404) → alterar em transação →
RAG PUT → commit.

| Status | Quando | Chama RAG? |
|---|---|---|
| 200 | `data` = `BusinessResponse` atualizado | sim |
| 400 | validação | não |
| 404 | sem negócio cadastrado | não |
| 4xx / 502 / 503 / 504 | falha da RAG; dados anteriores mantidos | sim |

## DELETE /api/v1/{clientId}/business

Ordem: buscar (404) → apagar em transação → RAG DELETE → commit. Remoção definitiva (FR-016).

| Status | Quando | Chama RAG? |
|---|---|---|
| 200 | `data` = `null`, `message` = "Negócio removido com sucesso." | sim |
| 404 | sem negócio cadastrado | não |
| 4xx / 502 / 503 / 504 | falha da RAG; negócio mantido | sim |
