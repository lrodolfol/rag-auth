# Feature Specification: API de Autenticação e Controle de Acesso (Gateway RAG)

**Feature Branch**: `001-rag-auth-gateway`

**Created**: 2026-10-25

**Status**: Draft

**Input**: User description: "API de autenticação e controle de acesso para o sistema de gestão de recursos (RAG). Responsável por autenticar usuários, gerenciar tokens de acesso e controlar o acesso a recursos do sistema, servindo como camada de segurança para se comunicar com a API de recursos RAG. O cliente poderá se cadastrar, fazer login, cadastrar e gerenciar (CRUD) serviços ou empresas dele." (ver `.mine/specify.md`)

## Clarifications

### Session 2026-10-25

- Q: Esta API guarda os dados completos de empresa/serviço ou só o vínculo de propriedade? → A:
  Guarda os dados completos, em cópia equivalente à da API de recursos RAG.
- Q: Empresa e serviço são recursos independentes ou relacionados? → A: Existe um único recurso,
  **Negócio**, cadastrado à parte pelo cliente já cadastrado. "Negócio" abrange serviço, empresa,
  loja, mão de obra ou qualquer outra atividade; não há categoria nem tipo.
- Q: Um cliente pode ter mais de um negócio? → A: Não; cada cliente tem no máximo um negócio.
- Q: Como o código de acesso é usado nas requisições? → A: Vai apenas dentro do token JWT; o
  cliente não o envia separadamente.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cadastro e login do cliente (Priority: P1)

Um cliente novo cria sua conta informando nome, e-mail e senha. Em seguida faz login com e-mail e
senha e recebe um token de acesso que comprova sua identidade nas requisições seguintes.

**Why this priority**: Sem conta e sem token nenhuma outra funcionalidade é acessível; é a base de
toda a camada de segurança.

**Independent Test**: Cadastrar um cliente, fazer login e verificar que um token de acesso válido
é emitido; tentar login com senha errada e verificar a rejeição.

**Acceptance Scenarios**:

1. **Given** um e-mail ainda não cadastrado, **When** o cliente envia nome, e-mail e senha válidos,
   **Then** a conta é criada e persistida, um código de acesso único e exclusivo é gerado para o
   cliente e retornado, e a resposta não contém a senha.
2. **Given** um e-mail já cadastrado, **When** alguém tenta cadastrar outra conta com o mesmo
   e-mail, **Then** o cadastro é recusado com mensagem de conflito em português.
3. **Given** dados de cadastro inválidos (e-mail mal formado, senha curta, nome vazio), **When** o
   cliente envia o cadastro, **Then** recebe HTTP 400 com o campo e a mensagem de cada erro.
4. **Given** uma conta existente, **When** o cliente faz login com credenciais corretas, **Then**
   recebe um token JWT de acesso com a data de expiração, que carrega seu código de acesso.
5. **Given** uma conta existente, **When** o cliente faz login com senha incorreta ou e-mail
   inexistente, **Then** recebe HTTP 401 com mensagem genérica que não revela qual dado está errado.

---

### User Story 2 - Proteção de rotas por token (Priority: P1)

Toda requisição a recursos protegidos precisa apresentar um token válido. Requisições sem token,
com token inválido/adulterado ou expirado são rejeitadas antes de qualquer processamento.

**Why this priority**: É o propósito central da API como camada de segurança; sem isso a API de
recursos RAG ficaria exposta.

**Independent Test**: Chamar uma rota protegida sem token, com token adulterado, com token expirado
e com token válido, verificando respectivamente 401, 401, 401 e acesso permitido.

**Acceptance Scenarios**:

1. **Given** uma rota protegida, **When** a requisição chega sem token, **Then** a resposta é HTTP
   401 Unauthorized.
2. **Given** uma rota protegida, **When** a requisição chega com token de assinatura inválida ou
   malformado, **Then** a resposta é HTTP 401 Unauthorized.
3. **Given** um token expirado, **When** o cliente o usa em uma rota protegida, **Then** a resposta
   é HTTP 401 Unauthorized.
4. **Given** um token válido de um cliente, **When** ele tenta acessar recurso que pertence a
   outro cliente, **Then** a resposta é HTTP 403 Forbidden.

---

### User Story 3 - Gerenciamento do negócio do cliente (Priority: P2)

Depois de ter sua conta, o cliente autenticado cadastra, consulta, altera e remove o seu negócio;
cada cliente tem no máximo um negócio. "Negócio" é qualquer atividade do cliente (serviço, empresa, loja, mão de obra etc.) e é
um cadastro à parte da conta, sem categoria ou tipo. A API valida a requisição, garante que o
cliente é o dono do negócio, guarda os dados do negócio e
encaminha as alterações para a API de recursos RAG, devolvendo ao cliente o resultado interpretado.

**Why this priority**: É o valor de negócio entregue ao cliente, mas depende das histórias 1 e 2.

**Independent Test**: Com um cliente autenticado e a API de recursos RAG disponível (ou simulada),
executar o ciclo completo criar → consultar → alterar → remover do negócio, verificando
que cada criação, alteração e remoção foi encaminhada com o token do cliente e a chave de API, e
que os dados guardados nesta API refletem o resultado.

**Acceptance Scenarios**:

1. **Given** um cliente autenticado, **When** ele cadastra um negócio com nome e descrição
   válidos, **Then** a operação é encaminhada à API de recursos RAG com o token JWT do cliente e o
   cabeçalho `x-api-key`, o negócio é guardado associado a ele como dono e o negócio criado é
   retornado.
2. **Given** um cliente autenticado que já possui um negócio, **When** ele tenta cadastrar outro,
   **Then** recebe HTTP 409 com mensagem em português e nada é encaminhado à API de recursos RAG.
3. **Given** um cliente autenticado dono de um negócio, **When** ele consulta, altera ou remove
   esse negócio, **Then** a operação é concluída e o resultado é retornado.
4. **Given** um cliente autenticado, **When** ele tenta consultar, alterar ou remover recurso de
   outro cliente, **Then** recebe HTTP 403 e nada é encaminhado à API de recursos RAG.
5. **Given** dados inválidos na criação ou alteração, **When** o cliente envia a requisição,
   **Then** recebe HTTP 400 com os detalhes de validação e nada é encaminhado à API de recursos RAG.
6. **Given** um identificador de recurso inexistente, **When** o cliente tenta consultá-lo,
   alterá-lo ou removê-lo, **Then** recebe HTTP 404 com mensagem em português.

---

### User Story 4 - Tratamento de falhas da API de recursos RAG (Priority: P3)

Quando a API de recursos RAG responde com erro, demora demais ou está indisponível, o cliente
recebe uma resposta clara e consistente em português, sem detalhes internos.

**Why this priority**: Melhora a robustez e a experiência, mas o fluxo feliz já entrega valor.

**Independent Test**: Simular a API de recursos RAG respondendo com erro de validação, erro
interno, demora excessiva e conexão recusada; verificar a resposta devolvida ao cliente em cada
caso.

**Acceptance Scenarios**:

1. **Given** que a API de recursos RAG rejeita a requisição com erro do cliente (ex.: 400, 404,
   409), **When** o cliente faz a operação, **Then** recebe o mesmo status com mensagem em
   português.
2. **Given** que a API de recursos RAG responde com erro interno, **When** o cliente faz a
   operação, **Then** recebe HTTP 502 com mensagem genérica, sem detalhes internos.
3. **Given** que a API de recursos RAG está inacessível ou excede o tempo limite, **When** o
   cliente faz a operação, **Then** recebe HTTP 503 (indisponível) ou 504 (tempo esgotado) com
   mensagem em português.
4. **Given** que a API de recursos RAG rejeita a chave `x-api-key`, **When** o cliente faz a
   operação, **Then** recebe HTTP 502 (falha de integração, não culpa do cliente) e o incidente é
   registrado em log.

---

### Edge Cases

- Token emitido para um cliente cuja conta foi removida: deve ser tratado como inválido (401).
- Token válido porém com cabeçalho `Authorization` em formato errado (sem prefixo `Bearer`): 401.
- E-mail cadastrado com variação de maiúsculas/minúsculas (`Ana@x.com` vs `ana@x.com`): tratado
  como o mesmo e-mail.
- Corpo da requisição ausente, JSON malformado ou campos com tipo errado: 400 com detalhes.
- Chave `x-api-key` ausente na configuração do ambiente: a API não deve iniciar e deve registrar o
  motivo em log.
- Falha na API de recursos RAG durante criação, alteração ou remoção: os dados guardados nesta API
  não podem ficar diferentes dos da API RAG (o negócio não aparece se não existir na API RAG, e
  uma alteração ou remoção recusada não é aplicada localmente).
- API de recursos RAG indisponível: consultas continuam funcionando com os dados
  guardados nesta API; criações, alterações e remoções retornam 503.
- Resposta da API de recursos RAG em formato inesperado: 502 com mensagem genérica.
- Geração de código de acesso que coincida com o de outro cliente: o sistema deve gerar outro
  código, nunca atribuindo o mesmo código a dois clientes.
- Dois cadastros simultâneos com o mesmo e-mail: apenas um é aceito; o outro recebe 409.

## Requirements *(mandatory)*

### Functional Requirements

**Cadastro e autenticação**

- **FR-001**: O sistema MUST permitir que um cliente se cadastre informando nome, e-mail e senha.
- **FR-002**: O sistema MUST persistir os dados de cadastro do cliente em armazenamento durável.
- **FR-003**: O sistema MUST garantir unicidade de e-mail entre clientes, sem diferenciar
  maiúsculas de minúsculas, recusando duplicatas com HTTP 409.
- **FR-004**: O sistema MUST exigir senha com no mínimo 8 caracteres, contendo ao menos uma letra e
  um número.
- **FR-005**: O sistema MUST armazenar senhas apenas em forma irreversível (hash com salt) e nunca
  retorná-las em respostas ou logs.
- **FR-006**: O sistema MUST permitir login por e-mail e senha e, em caso de sucesso, emitir um
  token JWT de acesso assinado contendo a identificação do cliente e a data de expiração.
- **FR-007**: O sistema MUST recusar login com credenciais inválidas com HTTP 401 e mensagem
  genérica que não indique se o e-mail existe.
- **FR-008**: Tokens de acesso MUST expirar após um tempo configurável (padrão: 60 minutos).

**Validação de token e autorização**

- **FR-009**: O sistema MUST validar o token JWT (assinatura, emissor, público e expiração) em toda
  requisição a rotas protegidas.
- **FR-010**: O sistema MUST responder HTTP 401 a requisições sem token, com token inválido,
  malformado, expirado ou pertencente a conta inexistente.
- **FR-011**: O sistema MUST responder HTTP 403 quando o token for válido, mas o cliente não for o
  dono do recurso solicitado.
- **FR-012**: Cadastro e login MUST ser as únicas rotas acessíveis sem token.

**Gerenciamento do negócio**

- **FR-013**: Clientes autenticados MUST poder criar, consultar, alterar e remover o seu negócio.
- **FR-014**: O sistema MUST persistir os dados completos de cada negócio (nome, descrição, dono e
  datas), mantendo uma cópia equivalente à registrada na API de recursos RAG. Consultas MUST ser
  atendidas a partir dos dados persistidos nesta API.
- **FR-015**: Negócio MUST ser um cadastro separado da conta do cliente, criado somente por um
  cliente já cadastrado e autenticado, e pertencer a um único cliente. Negócio é um recurso único
  que abrange serviço, empresa, loja, mão de obra ou qualquer outra atividade, sem categoria ou
  tipo.
- **FR-016**: Cada cliente MUST ter no máximo um negócio. A tentativa de cadastrar um segundo
  negócio MUST retornar HTTP 409, sem encaminhamento à API de recursos RAG. Após remover o seu
  negócio, o cliente MUST poder cadastrar um novo.
- **FR-017**: O sistema MUST validar os dados de entrada antes de encaminhar qualquer operação;
  dados inválidos MUST retornar HTTP 400 com campo e mensagem de cada erro, sem encaminhamento.

**Integração com a API de recursos RAG**

- **FR-018**: Criação, alteração e remoção de negócios MUST ser encaminhadas à API de
  recursos RAG, incluindo o token JWT do cliente no cabeçalho `Authorization` e o cabeçalho
  `x-api-key` com valor lido de variável de ambiente.
- **FR-019**: O endereço da API de recursos RAG e a chave `x-api-key` MUST ser configuráveis por
  ambiente e nunca expostos ao cliente nem registrados em logs.
- **FR-020**: O sistema MUST interpretar a resposta da API de recursos RAG e devolver ao cliente o
  resultado no contrato desta API: sucesso com o recurso; erros do cliente (4xx) com o mesmo status
  e mensagem em português; erros internos ou resposta inesperada com 502; indisponibilidade com
  503; tempo esgotado com 504.
- **FR-021**: O sistema MUST aplicar tempo limite configurável às chamadas à API de recursos RAG
  (padrão: 10 segundos).
- **FR-022**: Os dados persistidos nesta API MUST ser alterados somente após a API de recursos RAG
  confirmar a operação: se a API RAG falhar em uma criação, o negócio MUST NOT ser guardado; se
  falhar em uma alteração ou remoção, os dados anteriores MUST ser mantidos.

**Erros e observabilidade**

- **FR-023**: Todas as mensagens de erro retornadas ou registradas MUST estar em português e seguir
  um formato padronizado de detalhes de problema.
- **FR-024**: O sistema MUST registrar em log tentativas de login malsucedidas, respostas 401/403 e
  falhas de integração com a API de recursos RAG, sem dados sensíveis (senhas, tokens, chave de API).

**Código de acesso do cliente**

- **FR-025**: O sistema MUST gerar automaticamente, no cadastro, um código de acesso único e
  exclusivo para cada cliente, persistido junto à conta; dois clientes MUST NOT compartilhar o
  mesmo código, e o código MUST NOT ser reutilizado mesmo após remoção da conta.
- **FR-026**: O código de acesso MUST ser imutável, não adivinhável a partir de outros dados do
  cliente (e-mail, nome, sequência) e retornado ao cliente no cadastro e no login.
- **FR-027**: O código de acesso MUST ser incluído no token JWT emitido no login e é a forma de
  identificar o cliente nas requisições seguintes; o cliente MUST NOT precisar enviá-lo separadamente.
  A API de recursos RAG identifica o cliente pelo código de acesso contido no token encaminhado.

### Key Entities *(include if feature involves data)*

- **Cliente**: pessoa que usa o sistema. Atributos: identificador, código de acesso (único,
  exclusivo e imutável), nome, e-mail (único), senha em hash, data de criação. Possui zero ou um
  negócio.
- **Negócio**: atividade cadastrada pelo cliente (serviço, empresa, loja, mão de obra etc.), em
  cadastro separado da conta e sem categoria. Atributos mínimos: identificador, nome, descrição,
  dono (Cliente), datas de criação/atualização. Pertence a um único cliente.
- **Token de acesso**: credencial temporária emitida no login; contém o código de acesso do
  cliente e tem expiração. Não é persistido.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um cliente novo consegue se cadastrar e fazer login em menos de 1 minuto.
- **SC-002**: 100% das requisições a rotas protegidas sem token válido são rejeitadas com 401, e
  100% das tentativas de acessar recurso de outro cliente são rejeitadas com 403 (verificado por
  testes automatizados em todas as rotas protegidas).
- **SC-003**: 100% das requisições com dados inválidos retornam 400 com o detalhe de cada campo
  inválido, sem encaminhamento à API de recursos RAG.
- **SC-004**: 100% das chamadas encaminhadas à API de recursos RAG levam o token do cliente e a
  chave de API.
- **SC-005**: 95% das operações de cadastro, login e validação de token concluem em menos de 500 ms,
  sem contar o tempo de resposta da API de recursos RAG.
- **SC-006**: Nenhuma senha, token ou chave de API aparece em respostas de erro ou logs (verificado
  por auditoria de logs em testes).
- **SC-007**: Quando a API de recursos RAG falha, 100% das respostas ao cliente têm status
  adequado e mensagem em português, sem detalhes internos.
- **SC-008**: 100% dos clientes cadastrados possuem código de acesso distinto (verificado por teste
  com cadastro em massa de ao menos 10.000 clientes sem nenhuma colisão).

## Assumptions

- Login é feito por e-mail e senha; login social/SSO está fora do escopo desta versão.
- Apenas um papel existe (cliente); não há perfil administrador nesta versão. A permissão (403) é
  baseada exclusivamente em propriedade do recurso.
- Não há refresh token, logout com revogação, recuperação de senha nem verificação de e-mail nesta
  versão; o cliente faz novo login quando o token expira.
- Limitação de tentativas de login (rate limiting / bloqueio) está fora do escopo desta versão.
- A API de recursos RAG aceita o token JWT emitido por esta API e a chave `x-api-key`, e expõe
  operações de criação, alteração e remoção de negócios, identificando o dono pelo código de
  acesso contido no token.
- Divergências entre os dados desta API e os da API de recursos RAG causadas por alterações feitas
  diretamente na API RAG estão fora do escopo; esta API é o único ponto de entrada para alterações.
- Alteração e remoção da conta do próprio cliente estão fora do escopo desta versão.
- A API deve seguir a constituição do projeto (mensagens em português, erros padronizados, DTOs).
