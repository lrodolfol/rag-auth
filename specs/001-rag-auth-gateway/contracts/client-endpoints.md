# Contrato: Cliente (`ClientEndpoints.cs`)

User Story 5, FR-029, FR-030.

## DELETE /api/v1/{clientId}

`Authorization: Bearer <token>`; `{clientId}` = `id` numérico do cliente = claim `client_id` do
token, senão 403.

Ordem: se houver negócio → apagar negócio em transação → RAG DELETE; marcar conta com `deletedAt`;
commit. Sem negócio, não chama a RAG.

| Status | Quando | Chama RAG? |
|---|---|---|
| 200 | `data` = `null`, `message` = "Conta removida com sucesso." | só se houver negócio |
| 401 | token inválido ou conta já removida | não |
| 403 | `{clientId}` ≠ `client_id` do token | não |
| 4xx / 502 / 503 / 504 | falha da RAG ao remover o negócio; conta e negócio mantidos | sim |

Após o sucesso: tokens da conta → 401; login → 401; o e-mail pode ser usado em novo cadastro, que
recebe `clientId` e código de acesso novos.
