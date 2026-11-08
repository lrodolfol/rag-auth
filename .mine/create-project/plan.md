# Planejamento API .NET 10 com CSharp e minimal APIs

## Stacks
- .NET 10 com CSharp e com minimal APIs
- Um arquivo de endpoint por recurso
- Testes com xUnit e Moq
- Utilização de Entity Framework Core
- Persistência em banco de dados relacional (PostgreSQL)

## Decisões de arquitetura

### Contratos de API
Padrão de response para todos os endpoints:
```json
{
  "statusCode": 200,	
  "success": true,
  "message": "string",
  "error": {}
}
```

#### Contratos para autenticação e controle de acesso
POST /api/v1/auth/register - rota aberta
body
```json
{
  "name": "string",
  "email": "string",
  "password": "string"
}
```

POST /api/v1/auth/login - rota aberta
body
```json
{
  "email": "string",
  "password": "string"
}
```

#### Contratos para recurso de Negócio
POST /api/v1/{clientId}/business - rota autenticada
headers
```json
{
  "Authorization": "Bearer <token>"
}
```
body
```json
{
  "name": "string",
  "description": "string",
  "email": "string",
  "telephone": "string",
  "address": "string",
  "details": "string"
}
```

PUT /api/v1/{clientId}/business - rota autenticada
headers
```json
{
  "Authorization": "Bearer <token>"
}
```
body
```json
{
  "name": "string",
  "description": "string",
  "email": "string",
  "telephone": "string",
  "address": "string",
  "details": "string"
}
```

DELETE /api/v1/{clientId}/business - rota autenticada
headers
```json
{
  "Authorization": "Bearer <token>"
}
```

#### Contratos para recurso de Cliente
DELETE /api/v1/{clientId} - rota autenticada
headers
```json
{
  "Authorization": "Bearer <token>"
}
```

### Contratos com a API RAG
- Ao receber POST /api/v1/{clientId}/business, encaminhar:
headers
```json
{
  "x-api-key": "<api-key>"
}
```
body
```json
{
  "user_code": "string",
  "request": {
	"name": "string",
	"description": "string",
	"email": "string",
	"telephone": "string",
	"address": "string",
	"details": "string"
  }
}
```

- Ao receber PUT /api/v1/{clientId}/business, encaminhar:
headers
```json
{
  "x-api-key": "<api-key>"
}
```
body
```json
{
  "user_code": "string",
  "request": {
	"name": "string",
	"description": "string",
	"email": "string",
	"telephone": "string",
	"address": "string",
	"details": "string"
  }
}
```

- Ao receber DELETE /api/v1/{clientId}/business, encaminhar:
headers
```json
{
  "x-api-key": "<api-key>"
}
```
body
```json
{
  "user_code": "string"
}
```

## Ambientação
- A aplicação deve ser capaz de rodar em ambiente local, com banco de dados local, e em ambiente de produção, com banco de dados remoto. Para isso, utilizar variáveis de ambiente para configuração do banco de dados e demais parâmetros.
- Manter aquivo de configuração `appsettings.json` para configuração local, `appsettings.uat.json` para homologação e `appsettings.prod.json` para configuração de produção.
- Por padrão, a aplicação deve rodar em ambiente local, com banco de dados local, e utilizar o arquivo `appsettings.json` para configuração. Para rodar em ambiente de homologação ou produção, utilizar a variável de ambiente `ASPNETCORE_ENVIRONMENT` para definir o ambiente e carregar o arquivo de configuração correspondente.
- Manter endereços(url) de banco de dados, API RAG e demais serviços nos arquivos de configuração, e não no código fonte.
- **Manter senhas, tokens e chaves de API em variáveis de ambiente, e não no código fonte ou arquivos de configuração.**

### Padrão de nomenclatura e nomes de configurações
- Variáveis de ambiente: `NOME_DA_VARIAVEL`
- Arquivos de configuração: `nomeDaVariavel`

### Tratamento de erros
- Evite lançar exceções não tratadas, utilize `try/catch` para capturar exceções e retornar mensagens de erro amigáveis para o usuário.
- Crie exceções customizadas para erros específicos, como `BusinessNotFoundException`, `ClientNotFoundException`, `InvalidCredentialsException`, etc.
- **Não chamar API RAG em caso de qualquer falha.**

### Monitoria e observabildiade
- Monitoramento de logs com biblioteca padrão .NET `Microsoft.Extensions.Logging` enviando logs o console com nível de log `Information`, `Warning` e `Error`.
- Mantenha um correlation-id em cada requisição, gerado no início do pipeline de requisição e propagado para todos os logs e chamadas de API até o final da requisição.

## Decisões de banco de dados
- Criação de banco de dados com 'code-first' e migrations
- Relacionamento entre cliente e negócio (1:1)
- Campo ID das tabelas como `int`, chave primária e auto-incrementável
- Tabelas do banco de dados com colunas de `createdAt` e `updatedAt`
