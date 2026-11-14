# Contrato de saída: e-mails de recuperação de senha (Resend)

Este contrato descreve como a API monta e envia os e-mails de recuperação de senha e o aviso de
senha alterada. As decisões por trás dele estão em [research.md](../research.md), seções R11, R12
e R15.

## Abstração interna, generalizada a partir da feature 002

```csharp
public enum EmailKind { EmailConfirmation, PasswordRecovery, PasswordChanged }

// Mensagem da fila. ActionUrl leva o validator: nunca é gravado em banco nem em log.
public sealed record OutgoingEmailMessage(
    EmailKind Kind, int ClientId, string RecipientEmail, string ClientName,
    string? ActionUrl, DateTimeOffset? OccurredAt = null, int AttemptNumber = 1);

public interface IEmailQueue    { bool TryEnqueue(OutgoingEmailMessage message); IAsyncEnumerable<OutgoingEmailMessage> ReadAllAsync(CancellationToken ct); }
public interface IEmailComposer { ComposedEmail Compose(OutgoingEmailMessage message); }
public interface IEmailSender   { Task SendAsync(ComposedEmail email, CancellationToken ct); }
// ComposedEmail(string RecipientEmail, string Subject, string HtmlBody)
```

O envio em produção é feito por `ResendEmailSender`, o único lugar do código que conhece o SDK do
Resend. A `RESEND_API_KEY` e o remetente `email:fromAddress` são os mesmos da feature 002.

## E-mail `PasswordRecovery`

| Campo | Valor |
|---|---|
| To | e-mail do cliente (já normalizado) |
| Subject | `Redefina sua senha no {email:appName}` |
| HtmlBody | conteúdo de `Templates/PasswordRecoveryEmail.html`, um recurso embutido novo |

O modelo precisa conter, para atender o FR-004:
- a saudação com o nome do cliente;
- um botão e o link em texto para `{{ACTION_URL}}`;
- o texto "Este link é válido por **24 horas**.";
- o texto "Se o link expirar, solicite uma nova recuperação de senha na página de login.";
- o texto "Se você não pediu a redefinição de senha, ignore este e-mail: sua senha atual continua
  valendo.";
- o rodapé `© {{YEAR}} {{APP_NAME}}`.

O link segue o formato abaixo. O segmento `pass-recuvery` é o da entrada de planejamento e ainda
precisa ter a grafia confirmada.

```text
{{ACTION_URL}} = {email:passwordRecoveryPageBaseUrl}/{selector}/pass-recuvery/{validator}
```

## E-mail `PasswordChanged`

| Campo | Valor |
|---|---|
| To | e-mail do cliente |
| Subject | `Sua senha foi alterada no {email:appName}` |
| HtmlBody | conteúdo de `Templates/PasswordChangedEmail.html`, um recurso embutido novo |

O modelo precisa conter, para atender o FR-018:
- a saudação;
- o aviso "A senha da sua conta foi alterada em {{CHANGED_AT}}.";
- o texto "Se não foi você, solicite imediatamente a recuperação de senha e entre em contato com o
  suporte.";
- o rodapé.

Esse e-mail **não** leva link de redefinição nem senha, e `ActionUrl` vai nulo. `{{CHANGED_AT}}`
recebe `OccurredAt` convertido para o fuso `America/Sao_Paulo`, no formato
`dd/MM/yyyy 'às' HH:mm`.

## Placeholders

Todo valor é codificado em HTML (`HtmlEncoder`) antes de entrar no modelo.

| Placeholder | Usado em | Valor |
|---|---|---|
| `{{CLIENT_NAME}}` | todos os modelos | nome do cliente |
| `{{ACTION_URL}}` | confirmação, recuperação | URL de ação. No modelo de confirmação, substitui o antigo `{{CONFIRMATION_URL}}` |
| `{{APP_NAME}}` | todos os modelos | `email:appName` |
| `{{YEAR}}` | todos os modelos | ano atual, obtido de `TimeProvider` |
| `{{CHANGED_AT}}` | senha alterada | data e hora da troca |

## Tentativas de envio

As regras são as mesmas da feature 002 para os três tipos de e-mail. A primeira tentativa é
imediata. As seguintes esperam o intervalo definido em `email:retryDelaysSeconds`, com no máximo 3
tentativas no total. Quando as tentativas se esgotam, o sistema grava o log 1502 (Error) indicando
o `EmailKind`.

O corpo do e-mail, a URL e a `RESEND_API_KEY` nunca aparecem em logs.
