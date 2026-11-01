# Contrato de saída: e-mail de confirmação (Resend)

Decisões em [research.md](../research.md) R6–R8.

## Abstração interna

```csharp
public interface IConfirmationEmailSender
{
    Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken);
}
// ConfirmationEmail(string RecipientEmail, string Subject, string HtmlBody)
```

Implementação de produção: `ResendConfirmationEmailSender` (usa `IResend` injetado). Falha = qualquer
exceção ou resposta de erro do SDK; o dispatcher decide a nova tentativa.

## Mensagem enviada

| Campo | Valor |
|---|---|
| From | `email:fromAddress` |
| To | e-mail do cliente (normalizado) |
| Subject | `Confirme seu cadastro no {email:appName}` |
| HtmlBody | template `ConfirmationEmail.html` (de `.mine/email-confirmation/plan.md`) |

Placeholders do template:

| Placeholder | Valor |
|---|---|
| `{{CLIENT_NAME}}` | nome do cliente, **codificado em HTML** |
| `{{CONFIRMATION_URL}}` | `{email:confirmationPageBaseUrl}/{selector}/confirmation-register/{validator}` |
| `{{APP_NAME}}` | `email:appName` |
| `{{YEAR}}` | ano corrente (`TimeProvider`) |

Ajustes ao HTML original: rodapé `© {{YEAR}} {{APP_NAME}}` e parágrafo
"Se o link expirar, solicite um novo link de confirmação na página de login." (FR-002).

## Tentativas

| Tentativa | Quando |
|---|---|
| 1 | imediata, ao sair da fila |
| 2 | `retryDelaysSeconds[0]` (30 s) após a falha |
| 3 | `retryDelaysSeconds[1]` (120 s) após a falha |

Esgotadas → log 1502 (Error). Segredo `RESEND_API_KEY` nunca aparece em logs.
