# Contrato: Recuperação de senha (`PasswordRecoveryEndpoints.cs`)

As três rotas são abertas: aceitam requisições sem token (FR-020). Toda resposta usa o envelope
`ApiResponse` (`src/RagAuth.Api/Contracts/ApiResponse.cs`), o mesmo das features 001 e 002. Os
erros vêm como `ProblemDetails` dentro do campo `error`, com títulos em português. As decisões que
justificam o contrato estão em [research.md](../research.md), seções R4 a R6.

Em todas as rotas, a barra no fim do endereço é opcional.

O segmento `pass-recuvery` está escrito como veio na entrada de planejamento. A grafia precisa ser
confirmada (research R1).

---

## POST /api/v1/client/new-pass-recuvery-request

Pede a recuperação de senha para um e-mail.

**Corpo da requisição** (`PasswordRecoveryRequest`)

```json
{ "email": "ana@exemplo.com" }
```

| Campo | Regra |
|---|---|
| `email` | Obrigatório, com formato válido e no máximo 254 caracteres. É normalizado (`Trim` e minúsculas) antes da busca. |

**202 Accepted.** A resposta é sempre esta, seja a conta ativa, pendente, removida ou inexistente,
e também quando o limite de pedidos foi atingido (FR-003):

```json
{
  "statusCode": 202,
  "success": true,
  "message": "Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes.",
  "data": null,
  "error": null
}
```

| Erro | Quando acontece |
|---|---|
| 400 | `email` ausente, mal formado ou longo demais, ou corpo ilegível. Volta como `ValidationProblem`, com o campo e a mensagem. |

**Efeito**, que só ocorre para conta ativa com e-mail confirmado e dentro dos limites de 5 minutos e
de 10 pedidos em 24 horas:

1. Revoga o link ativo do cliente.
2. Emite um link novo, válido por 24 horas.
3. Põe na fila o e-mail `PasswordRecovery`
   ([password-recovery-emails-outbound.md](./password-recovery-emails-outbound.md)).

---

## GET /api/v1/client/{selector}/pass-recuvery/{validator}

Verifica se o link é válido, **sem consumi-lo** (FR-012). A página do front-end chama esta rota
antes de mostrar o formulário de nova senha.

| Parâmetro | Regra |
|---|---|
| `selector` | Selector: 22 caracteres Base64Url |
| `validator` | Validator: 43 caracteres Base64Url |

Valores fora desse formato recebem a mesma resposta 400 de link inválido, sem detalhe de campo. O
formato é regra de domínio do link, e não validação de entrada (research R5).

**200 OK**

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Link válido. Informe a nova senha.",
  "data": { "status": "valid" },
  "error": null
}
```

A resposta nunca traz nome, e-mail nem identificador do cliente. Nada no link nem na conta é
alterado.

| Erro | Quando acontece | `message` |
|---|---|---|
| 410 | O par selector/validator está correto, o link não foi usado nem revogado, mas o prazo venceu | `O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.` |
| 400 | Selector inexistente, validator errado, link já usado ou revogado, conta removida ou não confirmada, ou formato inválido | `Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.` |

---

## PUT /api/v1/client/{selector}/pass-recuvery/{validator}

Redefine a senha. Os parâmetros de rota seguem as mesmas regras do GET.

**Corpo da requisição** (`ResetPasswordRequest`)

```json
{
  "newPassword": "NovaSenha123",
  "newPasswordConfirmation": "NovaSenha123"
}
```

| Campo | Regra | Mensagem |
|---|---|---|
| `newPassword` | Obrigatório | `Informe a nova senha.` |
| `newPassword` | De 8 a 128 caracteres, com ao menos uma letra e um número (as mesmas regras do cadastro) | `A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número.` / `A senha deve ter no máximo 128 caracteres.` |
| `newPasswordConfirmation` | Obrigatório | `Informe a confirmação da nova senha.` |
| `newPasswordConfirmation` | Igual a `newPassword` | `A confirmação da senha deve ser igual à nova senha.` |

A validação do corpo acontece antes de qualquer verificação do link. Por isso, um erro de validação
**não consome** o link (FR-014).

**200 OK.** A resposta não traz token nem dados da conta (FR-013):

```json
{
  "statusCode": 200,
  "success": true,
  "message": "Senha alterada com sucesso. Você já pode fazer login com a nova senha.",
  "data": null,
  "error": null
}
```

| Erro | Quando acontece | `message` |
|---|---|---|
| 400 com `errors` | O corpo é inválido (tabela acima) | Mensagens por campo |
| 410 | O link expirou | `O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.` |
| 400 sem `errors` | O link é inválido (mesmos casos do GET), inclusive um segundo envio com o mesmo link | `Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.` |

**Efeito** (tudo numa só transação, com a linha do cliente bloqueada):

1. Grava o novo `passwordHash`.
2. Preenche `passwordChangedAt` com a data da troca.
3. Preenche `usedAt` do link.

Depois do commit, o e-mail `PasswordChanged` entra na fila. Os tokens emitidos antes da troca
continuam válidos até expirarem (FR-019).

**Login depois da troca**: `POST /api/v1/auth/login` aceita a nova senha. A senha antiga recebe o
401 genérico de sempre (feature 001), sem nenhuma mudança na rota de login.

---

## Rotas abertas depois desta feature

Esta lista altera o FR-019 da feature 002.

| Método | Rota |
|---|---|
| POST | `/api/v1/auth/register` |
| POST | `/api/v1/auth/login` |
| GET | `/api/v1/client/{clientIdentificationHash}/confirmation-register/{keyHash}` |
| POST | `/api/v1/client/new-confirmation-register` |
| POST | `/api/v1/client/new-pass-recuvery-request` |
| GET | `/api/v1/client/{selector}/pass-recuvery/{validator}` |
| PUT | `/api/v1/client/{selector}/pass-recuvery/{validator}` |
