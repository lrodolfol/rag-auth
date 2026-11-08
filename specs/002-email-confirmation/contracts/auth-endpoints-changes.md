# Contrato: Alterações em `AuthEndpoints.cs`

Base: [../../001-rag-auth-gateway/contracts/auth-endpoints.md](../../001-rag-auth-gateway/contracts/auth-endpoints.md).

## POST /api/v1/auth/register — sem mudança de contrato

Request, resposta 201 e erros (400, 409) idênticos (FR-001). Efeitos novos, invisíveis na resposta:
- conta criada com `emailConfirmed = false`;
- link de confirmação (origem `registration`) gravado na mesma transação;
- e-mail de confirmação enfileirado após o commit (não atrasa a resposta, FR-003).

409 continua valendo para e-mail de conta pendente, com link vencido ou não.

## POST /api/v1/auth/login — novo erro 403

Ordem de verificação: credenciais (401 genérico, inalterado) → confirmação de e-mail (403) → fluxo
da feature 001.

| Erro | Quando | `message` |
|---|---|---|
| 401 | e-mail inexistente ou senha incorreta (também para conta pendente) | `E-mail ou senha inválidos.` |
| **403** | credenciais corretas, e-mail não confirmado | `O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.` |

```json
{
  "statusCode": 403,
  "success": false,
  "message": "O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.",
  "data": null,
  "error": {
    "title": "Acesso negado",
    "status": 403,
    "detail": "O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.",
    "correlationId": "..."
  }
}
```

Nenhum token é emitido no 403.
