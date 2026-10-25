# API de autenticação e controle de acesso para o sistema de gestão de recursos (RAG). 

## Descrição
A API é responsável por autenticar usuários, gerenciar tokens de acesso e controlar o acesso a recursos do sistema.
Ela servirá como uma camada de segurança para se comunicar com outra API de recursos RAG.

O cliente poderá se cadastrar, fazer login, cadastrar e gerenciar serviços ou empresas dele como CRUD.

## Comportamentos esperados
- Cliente pode se cadastrar e fazer login. Os dados de cadastro devem ser persistidos em um banco de dados.
- O login deverá ser realizado com email e senha, retornando um token JWT válido para autenticação e autorização.
- Cada cliente terá um código de acesso ÚNICO e EXCLUSIVO, que será usado para identificar o cliente em requisições subsequentes.
- Cliente pode cadastrar serviço ou empresa dele, podendo fazer CRUD desses recursos. Esses dados também devem ser persistidos em um banco de dados.
- Requisições recebidas sem token JWT válido ou com token expirado devem retornar HTTP 401 Unauthorized.
- Requisições recebidas com token JWT válido, mas sem permissão para acessar o recurso solicitado, devem retornar HTTP 403 Forbidden.
- Requisições recebidas com dados inválidos devem retornar HTTP 400 Bad Request, incluindo detalhes do erro de validação.
- A API deve gerar tokens JWT para autenticação e autorização de usuários.
- A API deve validar tokens JWT em cada requisição para garantir que o usuário está autenticado e autorizado a acessar os recursos.
- Ao receber requisições sobre gerenciamento de serviços ou empresas, a API deve encaminhar a requisição para a API de recursos RAG, incluindo o token JWT no cabeçalho da requisição e uma chave 'x-api-key' que vira de uma variável de ambiente.
- A API deve interpretar a resposta da API de recursos RAG e retornar o resultado para o cliente, incluindo mensagens de erro apropriadas em caso de falha.
