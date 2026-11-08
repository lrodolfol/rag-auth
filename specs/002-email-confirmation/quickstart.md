# Quickstart: validação ponta a ponta

**Feature**: `002-email-confirmation` | **Plano**: [plan.md](./plan.md)

Guia para provar que a feature funciona. Contratos em [contracts/](./contracts/), modelo em
[data-model.md](./data-model.md). Pré-requisitos e banco local iguais aos da
[feature 001](../001-rag-auth-gateway/quickstart.md) (passos 1–3).

## 1. Segredos e configuração novos

```bash
export RESEND_API_KEY=re_xxxxxxxx          # chave real do Resend (ou de teste)
```

PowerShell: `$env:RESEND_API_KEY = "re_xxxxxxxx"`.

Em `appsettings.json`, seção `email`: `fromAddress` (domínio verificado no Resend),
`confirmationPageBaseUrl`, `appName`. Os demais valores têm padrão ([data-model.md](./data-model.md)).

**Verificação**: sem `RESEND_API_KEY` ou sem `email:fromAddress`, `dotnet run` encerra com log
explicando o item ausente.

## 2. Migration

```bash
dotnet ef database update --project src/RagAuth.Api
```

**Verificação (FR-020)**: contas criadas antes da migration ficam com `"emailConfirmed" = true` e
continuam fazendo login (cenário 9).

## 3. Cenários manuais

`BASE=http://localhost:5000`. O link chega no e-mail como
`{confirmationPageBaseUrl}/{selector}/confirmation-register/{validator}`; para chamar a API, use
`GET $BASE/api/v1/client/{selector}/confirmation-register/{validator}`.

| # | Ação | Resultado esperado |
|---|---|---|
| 1 | `POST /api/v1/auth/register` com dados válidos | 201 igual à feature 001; e-mail recebido em < 1 min com link, prazo de 24 h e orientação de novo link |
| 2 | Login com a senha correta antes de confirmar | 403 com a mensagem de e-mail não confirmado; sem `accessToken` |
| 3 | Login com senha errada na conta pendente | 401 "E-mail ou senha inválidos." |
| 4 | `GET` de confirmação com o link recebido | 200 `data.status = confirmed` |
| 5 | Repetir o `GET` | 200 `data.status = alreadyConfirmed` |
| 6 | Login correto | 200 como na feature 001 |
| 7 | `GET` com o validator alterado em 1 caractere | 400 link inválido |
| 8 | Novo cadastro com o e-mail da conta do cenário 1 | 409 |
| 9 | Login com conta criada antes da migration | 200 |

### Novo link e limites

| # | Ação | Resultado esperado |
|---|---|---|
| 10 | Cadastrar outra conta; logo em seguida `POST /api/v1/client/new-confirmation-register` | 202 genérico; **nenhum** e-mail novo (intervalo de 5 min desde o cadastro) |
| 11 | Repetir após 5 min | 202; novo e-mail; o link anterior passa a dar 400 |
| 12 | Pedir novo link para e-mail inexistente e para a conta já confirmada | 202 com mensagem idêntica; nenhum e-mail |
| 13 | `POST` com `email` mal formado | 400 com `error.errors.email` |
| 14 | Expiração: em banco local, `UPDATE email_confirmation_links SET "expiresAt" = now() - interval '1 minute'` no link ativo e chamar o `GET` | 410 link expirado; conta continua pendente |

### Falha de envio

| # | Ação | Resultado esperado |
|---|---|---|
| 15 | Rodar com `RESEND_API_KEY` inválida e cadastrar | 201 rápido; logs 1501 (Warning) por tentativa e 1502 (Error) após a 3ª; conta pendente |

## 4. Testes automatizados

```bash
dotnet test --settings coverlet.runsettings
```

Esperado: todos verdes, cobertura de linhas ≥ 90%. Cobrem em especial: expiração no limite exato
(`AdjustableTimeProvider`), unicidade de 10.000 links (SC-005), respostas indistinguíveis do novo link
(SC-007), ausência de selector/validator/URL nos logs (SC-008), 3 tentativas de envio com atrasos
zerados, e as duas rotas novas listadas como abertas.
