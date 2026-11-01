# Contrato: Confirmação de e-mail (`EmailConfirmationEndpoints.cs`)

Rotas abertas (FR-019). Envelope `ApiResponse` igual à feature 001
([../../001-rag-auth-gateway/contracts/api-envelope.md](../../001-rag-auth-gateway/contracts/api-envelope.md)).
Decisões em [research.md](../research.md) R2–R5.

## GET /api/v1/client/{clientIdentificationHash}/confirmation-register/{keyHash}

Chamada pela página do front-end aberta pelo link do e-mail.

| Parâmetro | Regra |
|---|---|
| `clientIdentificationHash` | selector, 22 caracteres Base64Url |
| `keyHash` | validator, 43 caracteres Base64Url |

Valores fora do formato → **400 inválido** (mesma resposta de link inexistente, sem detalhe de campo,
para não diferenciar casos). O formato é regra de domínio do link, não validação de entrada
(research R3; Constitution Check do plano). Barra final opcional.

**200 OK** — confirmado agora

```json
{
  "statusCode": 200,
  "success": true,
  "message": "E-mail confirmado com sucesso. Sua conta está ativa e você já pode fazer login.",
  "data": { "status": "confirmed" },
  "error": null
}
```

**200 OK** — já confirmado (link já usado, ou conta confirmada com link antigo)

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Este e-mail já foi confirmado. Você já pode fazer login.",
  "data": { "status": "alreadyConfirmed" },
  "error": null
}
```

| Erro | Quando | `message` |
|---|---|---|
| 410 | link correto, conta pendente, prazo vencido | `O link de confirmação expirou. Solicite um novo link de confirmação.` |
| 400 | selector inexistente, validator errado, link revogado, conta removida, formato inválido | `Link de confirmação inválido. Use o link do e-mail de confirmação mais recente.` |

Erros saem como `ProblemDetails` em `error` (títulos em português).

## POST /api/v1/client/new-confirmation-register

**Request** (`NewConfirmationLinkRequest`)

```json
{ "email": "ana@exemplo.com" }
```

| Campo | Regra |
|---|---|
| `email` | obrigatório, formato válido, até 254; comparado sem diferenciar maiúsculas |

**202 Accepted** — sempre, para pendente, confirmado, removido, inexistente ou limite atingido

```json
{
  "statusCode": 202,
  "success": true,
  "message": "Se o e-mail estiver cadastrado e aguardando confirmação, enviaremos um novo link em instantes.",
  "data": null,
  "error": null
}
```

| Erro | Quando |
|---|---|
| 400 | `email` ausente, mal formado ou longo demais; corpo inválido (`ValidationProblem` com o campo) |

Efeito (só conta pendente e dentro dos limites): revoga o link ativo, emite novo (origem `new-link-request`,
24 h) e enfileira o e-mail.
