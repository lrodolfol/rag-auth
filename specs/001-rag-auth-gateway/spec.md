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
- Q: Quais informações o cliente informa ao cadastrar o negócio? → A: Nome, descrição, e-mail,
  endereço (linha única com rua, número, bairro e cidade), telefone e detalhes (texto livre).
- Q: O cliente acessa o negócio como "meu negócio" ou pelo identificador? → A: Apenas como "meu
  negócio": o cliente é identificado pelo token, sem identificador do negócio na requisição.
- Q: A API deve limitar tentativas de login seguidas com senha errada? → A: Não nesta versão.
- Q: O código de acesso é lido/digitado por pessoas ou só circula entre sistemas? → A: Só entre
  sistemas: identificador opaco com tamanho fixo de 12 caracteres aleatórios, sem preocupação
  com leitura humana.
- Q: Ao remover o negócio, os dados são apagados de vez ou mantidos como removidos? → A: Remoção
  definitiva nesta API e na API de recursos RAG; o cliente pode cadastrar um novo negócio em
  seguida.

### Session 2026-10-25 (revisão pós-plano)

- Q: O cliente pode remover a própria conta? → A: Sim. A remoção da conta passa a fazer parte do
  escopo: o negócio do cliente (se existir) é removido também na API de recursos RAG e a conta é
  marcada como removida; tokens dela deixam de valer, o e-mail fica livre para novo cadastro e o
  código de acesso nunca é reutilizado.
- Q: Existe uma operação para consultar o negócio? → A: Não. O login já devolve os dados do
  negócio do cliente (quando existir), a partir dos dados guardados nesta API.
- Q: Como o cliente é indicado nas operações autenticadas? → A: Pelo identificador numérico do
  cliente nesta API, devolvido no cadastro e no login. Se ele não corresponder ao cliente do token,
  a operação é negada (HTTP 403).

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
   cliente, o identificador do cliente e o código de acesso são retornados, e a resposta não
   contém a senha.
2. **Given** um e-mail já cadastrado, **When** alguém tenta cadastrar outra conta com o mesmo
   e-mail, **Then** o cadastro é recusado com mensagem de conflito em português.
3. **Given** dados de cadastro inválidos (e-mail mal formado, senha curta, nome vazio), **When** o
   cliente envia o cadastro, **Then** recebe HTTP 400 com o campo e a mensagem de cada erro.
4. **Given** uma conta existente, **When** o cliente faz login com credenciais corretas, **Then**
   recebe um token JWT de acesso com a data de expiração, que carrega seu código de acesso, além
   do seu identificador de cliente.
5. **Given** uma conta existente, **When** o cliente faz login com senha incorreta ou e-mail
   inexistente, **Then** recebe HTTP 401 com mensagem genérica que não revela qual dado está errado.
6. **Given** um cliente que possui negócio cadastrado, **When** ele faz login com sucesso, **Then**
   a resposta inclui os dados do seu negócio, lidos desta API (mesmo com a API de recursos RAG
   indisponível); se ele não tiver negócio, a resposta indica que não há negócio.

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
4. **Given** um token válido de um cliente, **When** ele acessa as operações de negócio informando
   o próprio identificador de cliente, **Then** só o negócio desse cliente é afetado, pois a
   requisição não aceita identificador de negócio.
5. **Given** um token válido, **When** a requisição informa o identificador de outro cliente (em
   operação de negócio ou de remoção de conta), **Then** a resposta é HTTP 403 Forbidden e nada é
   alterado nem encaminhado à API de recursos RAG.

---

### User Story 3 - Gerenciamento do negócio do cliente (Priority: P2)

Depois de ter sua conta, o cliente autenticado cadastra, altera e remove o seu negócio, e o vê na
resposta do login; cada cliente tem no máximo um negócio. "Negócio" é qualquer atividade do cliente (serviço, empresa, loja, mão de obra etc.) e é
um cadastro à parte da conta, sem categoria ou tipo. A API valida a requisição, garante que o
cliente é o dono do negócio, guarda os dados do negócio e
encaminha as alterações para a API de recursos RAG, devolvendo ao cliente o resultado interpretado.

**Why this priority**: É o valor de negócio entregue ao cliente, mas depende das histórias 1 e 2.

**Independent Test**: Com um cliente autenticado e a API de recursos RAG disponível (ou simulada),
executar o ciclo completo criar → fazer login (negócio na resposta) → alterar → remover do
negócio, verificando
que cada criação, alteração e remoção foi encaminhada com o token do cliente e a chave de API, e
que os dados guardados nesta API refletem o resultado.

**Acceptance Scenarios**:

1. **Given** um cliente autenticado, **When** ele cadastra um negócio com nome, descrição, e-mail,
   endereço, telefone e detalhes válidos, **Then** a operação é encaminhada à API de recursos RAG com o token JWT do cliente e o
   cabeçalho `x-api-key`, o negócio é guardado associado a ele como dono e o negócio criado é
   retornado.
2. **Given** um cliente autenticado que já possui um negócio, **When** ele tenta cadastrar outro,
   **Then** recebe HTTP 409 com mensagem em português e nada é encaminhado à API de recursos RAG.
3. **Given** um cliente autenticado que possui um negócio, **When** ele altera ou remove o "meu
   negócio", **Then** a operação é concluída sobre o negócio dele e o resultado é retornado; o
   próximo login reflete a alteração ou a ausência do negócio.
4. **Given** dados inválidos na criação ou alteração, **When** o cliente envia a requisição,
   **Then** recebe HTTP 400 com os detalhes de validação e nada é encaminhado à API de recursos RAG.
5. **Given** um cliente autenticado que ainda não cadastrou negócio (ou já o removeu), **When** ele
   tenta alterar ou remover o "meu negócio", **Then** recebe HTTP 404 com mensagem em português
   informando que não há negócio cadastrado.

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

### User Story 5 - Remoção da própria conta (Priority: P3)

O cliente autenticado remove a própria conta. Se ele tiver negócio, o negócio é removido também na
API de recursos RAG; depois a conta é marcada como removida e deixa de poder ser usada.

**Why this priority**: Dá ao cliente controle sobre seus dados, mas não bloqueia o fluxo principal.

**Independent Test**: Com um cliente com negócio e outro sem, remover cada conta e verificar a
remoção na API de recursos RAG (só no primeiro caso), a rejeição do token antigo, o login recusado
e a possibilidade de novo cadastro com o mesmo e-mail, recebendo código de acesso diferente.

**Acceptance Scenarios**:

1. **Given** um cliente autenticado com negócio, **When** ele remove a própria conta, **Then** a
   remoção do negócio é encaminhada à API de recursos RAG e, após a confirmação, o negócio é apagado
   e a conta é marcada como removida.
2. **Given** um cliente autenticado sem negócio, **When** ele remove a própria conta, **Then** a
   conta é marcada como removida sem nenhuma chamada à API de recursos RAG.
3. **Given** que a API de recursos RAG falha ao remover o negócio, **When** o cliente remove a
   própria conta, **Then** recebe o erro correspondente (User Story 4) e a conta e o negócio
   permanecem como estavam.
4. **Given** uma conta removida, **When** alguém usa um token dela ou tenta fazer login com ela,
   **Then** recebe HTTP 401.
5. **Given** uma conta removida, **When** alguém cadastra uma nova conta com o mesmo e-mail,
   **Then** o cadastro é aceito com novo identificador e novo código de acesso, nunca o da conta
   removida.

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
- API de recursos RAG indisponível: o login continua devolvendo o negócio a partir dos dados
  guardados nesta API; criações, alterações e remoções (inclusive a remoção de conta com negócio)
  retornam 503.
- Identificador de cliente na requisição que não é numérico ou não existe: tratado como
  divergente do cliente do token (403), sem revelar se o identificador existe.
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
- **FR-011**: As operações autenticadas MUST receber o identificador numérico do cliente (o mesmo
  devolvido no cadastro e no login). O sistema MUST responder HTTP 403, sem alterar dados nem
  chamar a API de recursos RAG, quando esse identificador não corresponder ao cliente do token
  válido (inclusive identificador inexistente).
- **FR-012**: Cadastro e login MUST ser as únicas rotas acessíveis sem token.

**Gerenciamento do negócio**

- **FR-013**: Clientes autenticados MUST poder criar, alterar e remover o seu negócio. As
  operações MUST atuar sobre o "meu negócio" do cliente do token; as requisições MUST NOT receber
  identificador de negócio. Sem negócio cadastrado, alteração e remoção MUST retornar HTTP 404.
  Não há operação própria de consulta do negócio.
- **FR-014**: O sistema MUST persistir os dados completos de cada negócio (nome, descrição, e-mail,
  endereço, telefone, detalhes, dono e datas), mantendo uma cópia equivalente à registrada na API
  de recursos RAG. A resposta de login bem-sucedido MUST incluir os dados do negócio do cliente,
  lidos desta API, ou indicar que ele não possui negócio.
- **FR-015**: Negócio MUST ser um cadastro separado da conta do cliente, criado somente por um
  cliente já cadastrado e autenticado, e pertencer a um único cliente. Negócio é um recurso único
  que abrange serviço, empresa, loja, mão de obra ou qualquer outra atividade, sem categoria ou
  tipo.
- **FR-016**: Cada cliente MUST ter no máximo um negócio. A tentativa de cadastrar um segundo
  negócio MUST retornar HTTP 409, sem encaminhamento à API de recursos RAG. A remoção do negócio
  MUST ser definitiva: após a confirmação da API de recursos RAG, os dados do negócio MUST ser
  apagados desta API, sem cópia marcada como removida e sem possibilidade de recuperação. Após
  remover o seu negócio, o cliente MUST poder cadastrar um novo imediatamente.
- **FR-016a**: Os dados do negócio MUST seguir estas regras: nome obrigatório (até 150
  caracteres); descrição obrigatória (até 1.000 caracteres); e-mail opcional, em formato válido
  quando informado; endereço opcional, em uma única linha de texto livre com rua, número, bairro e
  cidade (até 300 caracteres); telefone opcional, apenas dígitos com DDD (10 ou 11 dígitos, aceitando
  máscara na entrada); detalhes opcional, texto livre informado pelo cliente (até 2.000 caracteres).
- **FR-017**: O sistema MUST validar os dados de entrada antes de encaminhar qualquer operação;
  dados inválidos MUST retornar HTTP 400 com campo e mensagem de cada erro, sem encaminhamento.

**Integração com a API de recursos RAG**

- **FR-018**: Criação, alteração e remoção de negócios (inclusive a remoção decorrente da remoção
  de conta, FR-029) MUST ser encaminhadas à API de recursos RAG, incluindo o token JWT do cliente no cabeçalho `Authorization` e o cabeçalho
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
- **FR-026**: O código de acesso MUST ser um identificador opaco com exatamente 12 caracteres
  alfanuméricos (letras maiúsculas, minúsculas e dígitos) gerados aleatoriamente, imutável, não
  adivinhável a partir de outros dados do cliente (e-mail, nome, sequência) e retornado ao cliente
  no cadastro e no login. O código circula apenas entre sistemas e não é feito para ser lido ou
  digitado por pessoas.
- **FR-027**: O código de acesso MUST ser incluído no token JWT emitido no login e é a forma de
  identificar o cliente nas requisições seguintes; o cliente MUST NOT precisar enviá-lo separadamente.
  A API de recursos RAG identifica o cliente pelo código de acesso contido no token encaminhado.
- **FR-028**: O cadastro e o login MUST retornar o identificador numérico do cliente nesta API,
  usado para indicar o cliente nas operações autenticadas (FR-011).

**Remoção da conta**

- **FR-029**: O cliente autenticado MUST poder remover a própria conta. Se houver negócio, a
  remoção dele MUST ser encaminhada à API de recursos RAG e, só após a confirmação, o negócio MUST
  ser apagado e a conta marcada como removida; se a API RAG falhar, conta e negócio MUST
  permanecer inalterados (FR-022). Sem negócio, a conta é marcada como removida sem chamar a API
  RAG.
- **FR-030**: Após a remoção, a conta MUST NOT permitir login nem aceitar tokens já emitidos (401);
  o e-mail MUST ficar disponível para novo cadastro, e o identificador e o código de acesso da
  conta removida MUST NOT ser atribuídos a outra conta.

### Key Entities *(include if feature involves data)*

- **Cliente**: pessoa que usa o sistema. Atributos: identificador numérico (devolvido ao cliente e
  usado nas operações autenticadas), código de acesso (opaco, 12 caracteres alfanuméricos
  aleatórios, único, exclusivo e imutável), nome, e-mail (único entre contas ativas), senha em
  hash, datas de criação/atualização e situação (ativa ou removida). Possui zero ou um negócio.
- **Negócio**: atividade cadastrada pelo cliente (serviço, empresa, loja, mão de obra etc.), em
  cadastro separado da conta e sem categoria. Atributos: identificador, nome, descrição, e-mail,
  endereço (linha única), telefone, detalhes, dono (Cliente), datas de criação/atualização. Pertence a um único cliente.
- **Token de acesso**: credencial temporária emitida no login; contém o código de acesso do
  cliente e tem expiração. Não é persistido.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um cliente novo consegue se cadastrar e fazer login em menos de 1 minuto.
- **SC-002**: 100% das requisições a rotas protegidas sem token válido são rejeitadas com 401, e
  em 100% dos testes com dois clientes cada um só consegue alterar ou remover o próprio negócio e
  a própria conta, recebendo 403 ao informar o identificador do outro (verificado por testes
  automatizados em todas as rotas protegidas).
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
- Apenas um papel existe (cliente); não há perfil administrador nesta versão. A permissão é
  baseada exclusivamente em propriedade: cada cliente só opera o próprio negócio.
- Não há refresh token, logout com revogação, recuperação de senha nem verificação de e-mail nesta
  versão; o cliente faz novo login quando o token expira.
- Limitação de tentativas de login (rate limiting / bloqueio) está fora do escopo desta versão, por
  decisão explícita; as falhas de login continuam registradas em log (FR-024), o que permite
  identificar abusos.
- A API de recursos RAG aceita o token JWT emitido por esta API e a chave `x-api-key`, e expõe
  operações de criação, alteração e remoção de negócios, identificando o dono pelo código de
  acesso contido no token.
- Divergências entre os dados desta API e os da API de recursos RAG causadas por alterações feitas
  diretamente na API RAG estão fora do escopo; esta API é o único ponto de entrada para alterações.
- Alteração dos dados da conta (nome, e-mail, senha) está fora do escopo desta versão; a remoção
  da própria conta está no escopo (FR-029, FR-030).
- A conta removida é mantida apenas como registro inativo, para garantir que identificador e
  código de acesso não sejam reaproveitados; anonimização de dados pessoais da conta removida fica
  para uma versão futura.
- A API deve seguir a constituição do projeto (mensagens em português, erros padronizados, DTOs).
