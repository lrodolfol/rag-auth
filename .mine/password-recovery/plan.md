# Planejamento recuperação de senha

## Decisões de arquitetura

### Contratos de API
Mantenha o padrão de resposta da API consistente usando o `src\RagAuth.Api\Contracts\ApiResponse.cs`


Endereço para o e-mail para recuperação de senha:
`https://ragauth.com.br/{client-identification-hash}/pass-recuvery/{key-hash}`

Rota de API para solicitação de nova senha:
POST /api/v1/client/new-pass-recuvery-request/ - rota aberta, com body JSON

Rota de API para alterar senha (depois que o cliente clicar no link de recuperação):
PUT /api/v1/client/{client-identification-hash}/pass-recuvery/{key-hash}/ - rota aberta


### Body HTML para envio ao email
Crie um novo arquivo para o HTML
```html

```

### Tratamento de erros
- Evite lançar exceções não tratadas, utilize `try/catch` para capturar exceções e retornar mensagens de erro amigáveis para o usuário.

### Monitoria e observabildiade
- Monitoramento de logs com biblioteca padrão .NET `Microsoft.Extensions.Logging` enviando logs o console com nível de log `Information`, `Warning` e `Error`.

## Plataforma Transactional Email
- Continue usando o Resend: https://resend.com/ para enviar o e-mail
