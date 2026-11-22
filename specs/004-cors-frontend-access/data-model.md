# Data Model: Liberação de CORS para o front-end

Esta feature não cria nem altera entidades persistidas e não exige migração de banco.

O único modelo novo é o objeto de configuração lido na inicialização.

## FrontendCorsOptions (configuração)

Seção: `cors`

| Campo | Chave de configuração | Tipo | Regras |
|-------|-----------------------|------|--------|
| `AllowedOrigins` | `cors:allowedOrigins` | lista de texto | Obrigatória; ao menos 1 item; cada item é URL absoluta `http`/`https`. A barra final é removida ao montar a política. |

**Mensagens de validação** (falha em `ValidateOnStart`, aplicação não sobe):

- `A configuração 'cors:allowedOrigins' deve conter ao menos uma origem.`
- `A configuração 'cors:allowedOrigins' deve conter apenas URLs absolutas.`

**Valores por ambiente**

| Arquivo | Valor |
|---------|-------|
| `appsettings.json` | `["http://localhost:4200"]` |
| `appsettings.dev.json` | `["https://SUBSTITUIR-front-uat.ragauth.com.br"]` (placeholder) |

## Política derivada: FrontendPolicy

Montada a partir de `FrontendCorsOptions` (não é persistida):

| Aspecto | Valor |
|---------|-------|
| Origens | `AllowedOrigins` normalizadas |
| Métodos | GET, POST, PUT, DELETE |
| Cabeçalhos de requisição | qualquer um |
| Credenciais | não permitidas |
| Cabeçalhos expostos | padrão |
| Cache do preflight | padrão |
