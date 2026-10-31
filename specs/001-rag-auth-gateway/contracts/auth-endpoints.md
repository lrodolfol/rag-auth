# Contrato: Autenticação (`AuthEndpoints.cs`)

Rotas abertas (FR-012). Envelope em [api-envelope.md](./api-envelope.md).

## POST /api/v1/auth/register

**Request** (`RegisterClientRequest`)

```json
{ "name": "Ana Souza", "email": "Ana@Exemplo.com", "password": "senhaSegura1" }
```

| Campo | Regra |
|---|---|
| `name` | obrigatório, até 150 |
| `email` | obrigatório, formato válido, até 254; comparado sem diferenciar maiúsculas |
| `password` | obrigatório, 8–128, ao menos uma letra e um número |

**201 Created** (`RegisteredClientResponse`)

```json
{
  "statusCode": 201,
  "success": true,
  "message": "Cadastro realizado com sucesso.",
  "data": {
    "clientId": 42,
    "accessCode": "Ab3dEf6hIj9k",
    "name": "Ana Souza",
    "email": "ana@exemplo.com",
    "createdAt": "2026-10-25T13:00:00Z"
  },
  "error": null
}
```

A senha nunca aparece na resposta.

| Erro | Quando |
|---|---|
| 400 | campos inválidos, JSON malformado, corpo ausente |
| 409 | e-mail já cadastrado em conta ativa (inclusive em cadastro simultâneo) |

## POST /api/v1/auth/login

**Request** (`LoginRequest`)

```json
{ "email": "ana@exemplo.com", "password": "senhaSegura1" }
```

**200 OK** (`LoginResponse`)

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Login realizado com sucesso.",
  "data": {
    "accessToken": "<jwt>",
    "tokenType": "Bearer",
    "expiresAt": "2026-10-25T14:00:00Z",
    "clientId": 42,
    "accessCode": "Ab3dEf6hIj9k",
    "business": {
      "name": "Padaria da Ana",
      "description": "Pães artesanais e cafés.",
      "email": "contato@padariadaana.com",
      "telephone": "11987654321",
      "address": "Rua das Flores, 123, Centro, São Paulo",
      "details": "Aberto de segunda a sábado, das 6h às 20h.",
      "createdAt": "2026-10-25T13:05:00Z",
      "updatedAt": "2026-10-25T13:05:00Z"
    }
  },
  "error": null
}
```

`business` é o `BusinessResponse` ([business-endpoints.md](./business-endpoints.md)) lido dos dados
locais, ou `null` se o cliente não tiver negócio (FR-014). Não depende da API RAG. Não há rota de
consulta do negócio: para ver dados atualizados, o cliente usa a resposta do POST/PUT ou faz novo
login.

| Erro | Quando |
|---|---|
| 400 | e-mail ou senha ausentes / malformados |
| 401 | e-mail inexistente, conta removida ou senha incorreta — sempre "E-mail ou senha inválidos." (FR-007), registrado em log sem a senha (FR-024) |
