# Quickstart: validação ponta a ponta

**Feature**: `003-password-recovery` | **Plano**: [plan.md](./plan.md)

Este guia mostra como provar que a recuperação de senha funciona de ponta a ponta. Os contratos
estão em [contracts/](./contracts/) e o modelo de dados em [data-model.md](./data-model.md).

Os pré-requisitos, o banco local e a `RESEND_API_KEY` são os mesmos das features
[001](../001-rag-auth-gateway/quickstart.md) e [002](../002-email-confirmation/quickstart.md).

## 1. Configuração nova

Na seção `email` do `appsettings.json`, defina `passwordRecoveryPageBaseUrl`. As outras chaves
novas já têm valor padrão ([data-model.md](./data-model.md)).

**Como verificar**: com `email:passwordRecoveryPageBaseUrl` vazio ou com um valor que não seja URL,
o `dotnet run` deve encerrar e registrar no log qual item está faltando ou inválido.

## 2. Migration

```bash
dotnet ef database update --project src/RagAuth.Api
```

**Como verificar**: a tabela `password_recovery_links` deve existir com os 3 índices listados no
modelo de dados, e `clients` deve ter a coluna `passwordChangedAt` nula em todas as linhas.

## 3. Cenários manuais

Considere `BASE=http://localhost:5000`. O link chega no e-mail neste formato:

```text
{passwordRecoveryPageBaseUrl}/{selector}/pass-recuvery/{validator}
```

Para testar direto na API, use o mesmo `{selector}/pass-recuvery/{validator}` depois de
`$BASE/api/v1/client/`.

Pré-condição: uma conta **confirmada** `ana@exemplo.com` com a senha `SenhaAntiga1` (conforme a
feature 002), e uma conta **pendente** `bia@exemplo.com`.

| # | Ação | Resultado esperado |
|---|---|---|
| 1 | `POST /api/v1/client/new-pass-recuvery-request` `{ "email": "ana@exemplo.com" }` | 202 genérico. O e-mail de recuperação chega em menos de 1 min, com o link, o prazo de 24 h e o aviso "ignore se não pediu". |
| 2 | Repetir o passo 1 com `naoexiste@exemplo.com` e depois com `bia@exemplo.com` | Resposta 202 **idêntica** à do passo 1, sem nenhum e-mail enviado. O log mostra o evento 1602. |
| 3 | `POST ...` `{ "email": "invalido" }` | 400 com o campo `email` em `error.errors`. |
| 4 | `GET .../{selector}/pass-recuvery/{validator}` com o link do passo 1 | 200 com `{ "status": "valid" }`. Chamar de novo continua dando 200, porque a verificação não consome o link. |
| 5 | `PUT` com o mesmo link e `{ "newPassword": "NovaSenha1", "newPasswordConfirmation": "Outra1234" }` | 400 com o campo `newPasswordConfirmation`. O GET do passo 4 ainda responde 200. |
| 6 | `PUT` com `newPassword: "curta"` | 400 com o campo `newPassword`. O link continua válido. |
| 7 | `PUT` com `{ "newPassword": "NovaSenha1", "newPasswordConfirmation": "NovaSenha1" }` | 200 com a mensagem de sucesso, sem token. O e-mail "Sua senha foi alterada" chega sem nenhum link. |
| 8 | `POST /api/v1/auth/login` com `NovaSenha1` e depois com `SenhaAntiga1` | O primeiro dá 200 com token. O segundo dá 401 genérico. |
| 9 | Repetir o `PUT` do passo 7 com o mesmo link | 400 de link inválido. A senha não muda. |
| 10 | Usar um token obtido **antes** do passo 7 numa rota protegida | O acesso continua permitido até o token expirar (FR-019). |
| 11 | Pedir a recuperação 2 vezes seguidas com menos de 5 min entre elas | As duas respostas são 202, mas só o primeiro pedido envia e-mail. O log mostra o evento 1601. |
| 12 | Pedir de novo depois de 5 min e usar o link **anterior** no GET | O link anterior dá 400 inválido. O link novo dá 200. |
| 13 | Trocar o último caractere do validator no GET ou no PUT | 400 inválido, sem detalhe de campo. |
| 14 | Usar no GET de recuperação o selector e o validator de um link de **confirmação de e-mail** | 400 inválido. |

Os cenários de expiração (o link vale até exatamente 24 h e passa a dar 410 depois disso), de PUTs
simultâneos e de conta removida dependem de controlar o relógio e a concorrência. Por isso são
cobertos pelos testes de integração, que usam o `AdjustableTimeProvider`.

## 4. Testes automatizados

```bash
dotnet test --settings coverlet.runsettings
```

**O que esperar**:
- todos os testes passam;
- a cobertura de linhas fica em pelo menos 90%;
- cada uma das 3 rotas novas tem testes em `PasswordRecoveryEndpointsTests`;
- `SensitiveDataAuditTests` confirma que nenhum selector, validator ou senha aparece nos logs ou
  nos erros (SC-007);
- `PasswordRecoveryLinkUniquenessTests` emite 10.000 links sem nenhuma colisão (SC-005).

## 5. Logs esperados no console

Os eventos 1600, 1601, 1602, 1603, 1604 e 1605 vêm desta feature ([research.md](./research.md),
R14). O envio de e-mail continua gerando os eventos 1500 a 1502, agora com o tipo do e-mail
(`PasswordRecovery` ou `PasswordChanged`).

Em nenhuma linha de log deve aparecer URL de link, validator ou senha.
