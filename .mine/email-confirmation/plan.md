# Planejamento email de confirmação de cadastro

## Decisões de arquitetura

### Contratos de API
Mantenha o padrão de resposta da API consistente usando o `src\RagAuth.Api\Contracts\ApiResponse.cs`

Endereço de envio de e-mail de confirmação de cadastro:
`https://ragauth.com.br/{client-identification-hash}/confirmation-register/{key-hash}`

Rota de API para confirmação de cadastro:
GET /api/v1/client/{client-identification-hash}/confirmation-register/{key-hash}/ - rota aberta

Rota de API caso o cliente solicite reenvio do e-mail de confirmação:
POST /api/v1/client/new-confirmation-register/ - rota aberta, com body JSON contendo o e-mail do cliente.

### Body HTML para envio ao email
```html

<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Confirme seu e-mail</title>
</head>
<body style="margin: 0; padding: 0; background-color: #f4f4f4; font-family: Arial, sans-serif;">

    <table width="100%" cellpadding="0" cellspacing="0" style="padding: 30px 10px;">
        <tr>
            <td align="center">
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="max-width: 500px; background-color: #ffffff; border-radius: 8px; padding: 35px;">

                    <tr>
                        <td align="center">
                            <h2 style="color: #222222; margin-top: 0;">
                                Confirme seu e-mail!
                            </h2>
                        </td>
                    </tr>

                    <tr>
                        <td style="color: #555555; font-size: 15px; line-height: 1.6;">
                            <p>Olá, <strong>{{CLIENT_NAME}}</strong>!</p>

                            <p>
                                Obrigado por se cadastrar em nossa plataforma.
                                Para concluir seu cadastro, precisamos confirmar
                                seu endereço de e-mail.
                            </p>

                            <p>
                                Clique no botão abaixo para confirmar seu e-mail:
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td align="center" style="padding: 20px 0;">
                            <a href="{{CONFIRMATION_URL}}"
                               style="background-color: #2563eb;
                                      color: #ffffff;
                                      text-decoration: none;
                                      padding: 14px 28px;
                                      border-radius: 6px;
                                      font-size: 15px;
                                      font-weight: bold;
                                      display: inline-block;">
                                Confirmar meu e-mail
                            </a>
                        </td>
                    </tr>

                    <tr>
                        <td style="color: #777777; font-size: 13px; line-height: 1.6;">
                            <p>
                                Este link é válido por <strong>24 horas</strong>.
                            </p>

                            <p>
                                Caso o botão não funcione, copie e cole o
                                endereço abaixo no navegador:
                            </p>

                            <p style="word-break: break-all;">
                                <a href="{{CONFIRMATION_URL}}"
                                   style="color: #2563eb;">
                                    {{CONFIRMATION_URL}}
                                </a>
                            </p>

                            <p>
                                Se você não realizou esse cadastro, ignore
                                este e-mail.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style="border-top: 1px solid #eeeeee;
                                   padding-top: 20px;
                                   text-align: center;
                                   color: #999999;
                                   font-size: 12px;">
                            © 2026 Sua Empresa. Todos os direitos reservados.
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>

</body>
</html>
```

### Tratamento de erros
- Evite lançar exceções não tratadas, utilize `try/catch` para capturar exceções e retornar mensagens de erro amigáveis para o usuário.
- Crie exceções customizadas para erros específicos, como `BusinessNotFoundException`, `ClientNotFoundException`, `InvalidCredentialsException`, etc.

### Monitoria e observabildiade
- Monitoramento de logs com biblioteca padrão .NET `Microsoft.Extensions.Logging` enviando logs o console com nível de log `Information`, `Warning` e `Error`.

## Decisões de banco de dados
- Atualizar tabela de clientes com coluna `emailConfirmed` do tipo `bool` para indicar se o e-mail foi confirmado.

## Plataforma Transactional Email
- Resend: https://resend.com/
- Código de exemplo (não é código real) Mantenha a estrutura segregada e limpa e com injeções de dependências:
```CSharp
    using Resend;

    IResend resend = ResendClient.Create("{{RESEND_API_KEY}}");

    var resp = await resend.EmailSendAsync( new EmailMessage()
    {
        From = emailFromConfiguration,
        To = clientEmail,
        Subject = "Confirme seu cadastro no {appName}",
        HtmlBody = {bodyHtml},
    } );
```
API-KEY para conectar no resend deve ser armazenada em variáveis de ambiente e não no código-fonte com o nome: `RESEND_API_KEY`
