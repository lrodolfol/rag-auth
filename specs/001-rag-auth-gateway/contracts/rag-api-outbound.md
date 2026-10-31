# Contrato: Chamadas à API de recursos RAG (saída)

Implementado por `RagApiClient : IRagApiClient` (research R8). URL base e caminho vêm de
configuração; a chave vem de `RAG_API_KEY`. Só é chamada depois de toda validação local passar.

## Cabeçalhos (todas as chamadas)

| Cabeçalho | Valor | Origem |
|---|---|---|
| `x-api-key` | chave da API RAG | variável de ambiente `RAG_API_KEY` (`.mine/plan.md`) |
| `Authorization` | `Bearer <token do cliente>` | repassado da requisição original (FR-018) |
| `X-Correlation-Id` | correlation-id da requisição | `CorrelationIdMiddleware` |
| `Content-Type` | `application/json` | — |

## Operações

| Gatilho | Método e caminho | Corpo |
|---|---|---|
| `POST /api/v1/{clientId}/business` | `POST {ragApi:baseUrl}{ragApi:businessPath}` | `{ "user_code", "request" }` |
| `PUT /api/v1/{clientId}/business` | `PUT {ragApi:baseUrl}{ragApi:businessPath}` | `{ "user_code", "request" }` |
| `DELETE /api/v1/{clientId}/business` e `DELETE /api/v1/{clientId}` (com negócio) | `DELETE {ragApi:baseUrl}{ragApi:businessPath}` | `{ "user_code" }` |

`user_code` = código de acesso do cliente. `request` = dados já validados e normalizados (telefone
só com dígitos, opcionais vazios como `null`):

```json
{
  "user_code": "Ab3dEf6hIj9k",
  "request": {
    "name": "Padaria da Ana",
    "description": "Pães artesanais e cafés.",
    "email": "contato@padariadaana.com",
    "telephone": "11987654321",
    "address": "Rua das Flores, 123, Centro, São Paulo",
    "details": "Aberto de segunda a sábado, das 6h às 20h."
  }
}
```

## Tradução das respostas (FR-020, FR-021)

Timeout: `ragApi:timeoutSeconds` (padrão 10). Sem retentativas.

| Resposta da RAG | Status ao cliente | Mensagem (PT) | Log |
|---|---|---|---|
| 2xx | sucesso da operação | — | `Information` |
| 400 | 400 | "O serviço de recursos recusou os dados enviados." | `Warning` |
| 401 / 403 | 502 | "Falha na comunicação com o serviço de recursos. Tente novamente mais tarde." | `Error` (chave rejeitada) |
| 404 | 404 | "O negócio não foi encontrado no serviço de recursos." | `Warning` |
| 409 | 409 | "O serviço de recursos indicou conflito com os dados enviados." | `Warning` |
| outro 4xx | mesmo status | "O serviço de recursos recusou a operação." | `Warning` |
| 5xx | 502 | mensagem genérica de falha de comunicação | `Error` |
| outro status (1xx, 3xx) | 502 | mensagem genérica de falha de comunicação | `Error` |
| conexão recusada / DNS | 503 | "O serviço de recursos está indisponível no momento." | `Error` |
| timeout | 504 | "O serviço de recursos demorou demais para responder." | `Error` |

Logs nunca incluem `x-api-key`, o token nem o corpo da resposta da RAG.

## Premissas a confirmar com o time da API RAG

1. As três operações compartilham o mesmo caminho (`/business` por padrão, configurável).
2. `DELETE` aceita corpo JSON.
3. O corpo de respostas 2xx não é usado; a resposta ao cliente vem dos dados locais.
4. Com `user_code` no corpo, o `Authorization` repassado é informativo; se a RAG passar a validar o
   JWT, avaliar migração para RS256 (research R4).
