# Planejamento endpoint para dashboard


## Decisões de negócio
- O endpoint será protegido por autenticação, garantindo que apenas usuários logados possam acessá-lo.
- O usuário poderá visualizar somente informações relacionadas ao seu próprio negócio, garantindo a privacidade e segurança dos dados.
- O campo `client_id` que esta dentro do JWT será utilizado para identificar o cliente logado e filtrar os dados do negócio correspondente.


## Decisões de arquitetura

### Endpoint para consulta:
GET
/api/v1/{client-id}/business

response 200:
```json
200 ok
 {
   "statusCode": 200,
   "success": true,
   "message": "Negócio encontrado com sucesso.",
   "data": {
     "name": "Padaria Pão Quente",
     "description": "Padaria artesanal com pães de fermentação natural.",
     "email": "contato@paoquente.com.br",
     "telephone": "(11) 98765-4321",
     "address": "Rua das Flores, 123 - Centro, São Paulo - SP",
     "details": "Aberto de seg. a sáb., 6h às 20h.",
     "createdAt": "2026-11-01T14:32:10+00:00",
     "updatedAt": "2026-11-20T09:15:44+00:00"
   },
   "error": null
 }
```

response 404:
```json
 {
   "statusCode": 404,
   "success": false,
   "message": "Negócio não encontrado.",
   "data": null,
   "error": {
     "code": "BUSINESS_NOT_FOUND",
     "message": "O negócio solicitado não foi encontrado."
   }
 }
```

response 500x:
```json
 {
   "statusCode": 500,
   "success": false,
   "message": "Erro interno do servidor.",
   "data": null,
   "error": {
     "code": "INTERNAL_SERVER_ERROR",
     "message": "Ocorreu um erro inesperado ao processar a solicitação."
   }
 }
```

### Consulta banco de dados
- Deve consultar as tabelas `clients` com left join com a tabela `business` para obter as informações do negócio associado ao usuário logado.
- Dados que devem ser retornados da tabela `business`: (name, description, email, telephone, address, details, createdAt, updatedAt).
- Dados que devem ser retornados da tabela `clients`: (id, name, email, createdAt, updatedAt).
- Somente dados de clientes com 'emailConfirmed' igual a true devem ser retornados, garantindo que apenas usuários que confirmaram o e-mail possam acessar o dashboard.
