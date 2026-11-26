# Feature Specification: Dados do painel (dashboard) do negócio

**Feature Branch**: `005-business-dashboard`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "Endpoint para dashboard. Após o usuário logar no sistema, ele será redirecionado para o dashboard, onde poderá visualizar informações sobre o negócio cadastrado. O usuário logado terá acesso a um dashboard com informações relevantes sobre o negócio. As informações exibidas são enviadas por esse backend para o frontend. Tarefa simples sem complexidade e engenharia excessiva." (ver `.mine/dashboard-resource/specify.md`)

**Relação com features anteriores**: usa o cadastro, o login, o token de acesso e o recurso
**Negócio** da feature `001-rag-auth-gateway`, o estado de confirmação de e-mail da feature
`002-email-confirmation` e a liberação de acesso pelo navegador da feature
`004-cors-frontend-access`. Hoje o login já devolve os dados do negócio uma única vez. Esta feature
cria uma consulta própria, que o front-end pode repetir sempre que abrir ou recarregar o painel sem
precisar fazer login de novo.

## Clarifications

### Session 2026-11-25

- Q: O painel deve mostrar só os dados que esta API já guarda (resumo da conta + cadastro do
  negócio), ou também métricas/estatísticas do negócio? → A: Só os dados já guardados (resumo da
  conta e cadastro do negócio). Métricas ficam para uma feature futura.
- Q: Cliente sem negócio deve receber 404 (rascunho em `.mine/dashboard-resource/plan.md`) ou
  sucesso indicando a falta do negócio? → A: Sucesso (200) com o resumo da conta e negócio vazio,
  como em FR-006.
- Q: O erro deve usar `{ code, message }` (rascunho) ou o `ProblemDetails` atual da API? → A:
  Manter o `ProblemDetails` atual dentro do envelope, como nas outras operações (FR-010).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver o painel do meu negócio (Priority: P1)

Um cliente logado que já cadastrou o seu negócio abre o painel. O front-end pede os dados do painel
e recebe o resumo da conta (nome, e-mail, se o e-mail está confirmado e desde quando é cliente) e
os dados do negócio guardados nesta API: nome, descrição, e-mail, telefone, endereço, detalhes,
data de cadastro e data da última atualização.

**Why this priority**: É o objetivo da feature. Sem esses dados o painel fica vazio depois do
login.

**Independent Test**: Cadastrar um cliente, confirmar o e-mail, fazer login, cadastrar o negócio,
pedir os dados do painel com o token e conferir que a resposta traz exatamente os dados do cliente
e do negócio cadastrados.

**Acceptance Scenarios**:

1. **Given** um cliente logado com negócio cadastrado, **When** o front-end pede os dados do
   painel informando o identificador do próprio cliente, **Then** recebe uma resposta de sucesso
   com o resumo da conta e todos os dados do negócio.
2. **Given** um cliente que acabou de atualizar o negócio, **When** o front-end pede os dados do
   painel de novo, **Then** a resposta já mostra os dados atualizados e a nova data de
   atualização.
3. **Given** um negócio cadastrado sem os campos opcionais (e-mail, telefone, endereço ou
   detalhes), **When** o front-end pede os dados do painel, **Then** esses campos vêm vazios, sem
   erro.

---

### User Story 2 - Painel de quem ainda não cadastrou o negócio (Priority: P2)

Um cliente logado que ainda não cadastrou o negócio abre o painel. Ele recebe o resumo da conta e
a indicação clara de que ainda não há negócio, para que o front-end o convide a cadastrar um.

**Why this priority**: Todo cliente novo passa por esse estado logo depois do primeiro login. O
painel não pode quebrar nesse caso.

**Independent Test**: Fazer login com um cliente sem negócio, pedir os dados do painel e conferir
que a resposta é de sucesso, traz o resumo da conta e indica que não há negócio cadastrado.

**Acceptance Scenarios**:

1. **Given** um cliente logado sem negócio, **When** o front-end pede os dados do painel, **Then**
   recebe uma resposta de sucesso com o resumo da conta, a indicação de que não há negócio
   cadastrado e uma mensagem em português orientando a cadastrar o negócio.
2. **Given** um cliente que removeu o negócio, **When** o front-end pede os dados do painel,
   **Then** recebe a mesma resposta do cenário 1.

---

### User Story 3 - Proteção dos dados do painel (Priority: P1)

Só o próprio cliente, autenticado, vê os dados do seu painel. Ninguém vê o painel de outro cliente.

**Why this priority**: O painel expõe dados de contato do cliente e do negócio. Vazar esses dados
é falha de segurança.

**Independent Test**: Pedir os dados do painel sem token, com token inválido ou expirado e com o
token de um cliente informando o identificador de outro, e conferir que todos os pedidos são
recusados.

**Acceptance Scenarios**:

1. **Given** um pedido sem token ou com token inválido, expirado ou de conta removida, **When** o
   front-end pede os dados do painel, **Then** recebe HTTP 401 e nenhum dado.
2. **Given** um cliente logado, **When** pede os dados do painel informando o identificador de
   outro cliente, **Then** recebe HTTP 403 e nenhum dado do outro cliente.

---

### Edge Cases

- O identificador do cliente na rota não é numérico: o pedido é recusado sem expor dados, como já
  acontece nas outras operações autenticadas.
- A conta foi removida depois da emissão do token: o pedido é recusado com HTTP 401, como nas
  outras operações autenticadas.
- O negócio é alterado ou removido entre duas consultas: cada consulta devolve o estado atual
  guardado nesta API.
- A API de recursos RAG está fora do ar: o painel continua funcionando, porque usa só os dados
  guardados nesta API.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST oferecer uma operação autenticada de consulta, só de leitura, que
  devolve os dados do painel do cliente.
- **FR-002**: O cliente MUST ser indicado na operação pelo seu identificador numérico nesta API,
  como nas outras operações autenticadas. Se ele não corresponder ao cliente do token, a operação
  MUST ser negada com HTTP 403.
- **FR-003**: Pedidos sem token válido MUST ser recusados com HTTP 401, sem devolver dados.
- **FR-004**: A resposta MUST trazer o resumo da conta: identificador, nome, e-mail, indicação de
  e-mail confirmado e data de cadastro do cliente.
- **FR-005**: Quando o cliente tem negócio cadastrado, a resposta MUST trazer os dados do negócio:
  nome, descrição, e-mail, telefone, endereço, detalhes, data de cadastro e data da última
  atualização.
- **FR-006**: Quando o cliente não tem negócio cadastrado, a resposta MUST ser de sucesso, indicar
  de forma explícita que não há negócio e trazer uma mensagem em português orientando o cadastro.
- **FR-007**: A resposta MUST NOT trazer dados internos ou sensíveis da conta, como senha, código de
  acesso, links de confirmação ou de recuperação.
- **FR-008**: Os dados MUST vir apenas do que está guardado nesta API. A consulta MUST NOT depender
  da API de recursos RAG.
- **FR-009**: A consulta MUST NOT alterar nenhum dado. Pedidos repetidos devolvem o mesmo resultado
  enquanto os dados não mudarem.
- **FR-010**: A resposta MUST seguir o mesmo formato de envelope (status, mensagem em português e
  dados) das outras respostas da API.
- **FR-011**: A operação MUST estar acessível pelo front-end no navegador, nas mesmas condições de
  origem liberadas pela feature `004-cors-frontend-access`.
- **FR-012**: A resposta MUST NOT trazer métricas nem estatísticas de uso (visitas, buscas,
  interações ou valores calculados). Esta feature MUST NOT criar coleta ou armazenamento de dados
  novos.

### Key Entities

- **Painel do cliente**: visão só de leitura montada a cada consulta. Junta o resumo da conta e,
  se existir, o negócio do cliente. Não é guardada.
- **Resumo da conta**: identificador, nome, e-mail, situação de confirmação do e-mail e data de
  cadastro do cliente (entidade Cliente existente).
- **Negócio**: entidade existente, no máximo um por cliente, com nome, descrição, e-mail,
  telefone, endereço, detalhes e datas de cadastro e de atualização.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos clientes logados com negócio cadastrado veem no painel dados idênticos aos
  que cadastraram ou atualizaram por último.
- **SC-002**: 100% dos clientes logados sem negócio recebem uma resposta de sucesso que indica a
  falta do negócio. Nenhum recebe erro.
- **SC-003**: 0 casos em que um cliente vê dados do painel de outro cliente ou em que um pedido sem
  autenticação recebe dados.
- **SC-004**: O painel carrega os dados em até 1 segundo em 95% das consultas, em condições
  normais de uso.
- **SC-005**: O painel continua respondendo mesmo quando a API de recursos RAG está indisponível.

## Assumptions

- "Informações relevantes sobre o negócio" são os dados que esta API já guarda: o resumo da conta
  e os dados cadastrais do negócio (confirmado na sessão de clarificação de 2026-11-25). Métricas
  e estatísticas (visitas, buscas, interações) ficam fora do escopo, porque esta API não as coleta
  hoje. Elas entram em uma feature futura, se forem necessárias.
- O redirecionamento para o painel depois do login é responsabilidade do front-end. Esta feature
  cobre só a entrega dos dados pelo backend.
- O login continua devolvendo os dados do negócio como hoje. A nova consulta é adicional e não
  muda o login.
- Um cliente com e-mail ainda não confirmado não consegue fazer login (feature 002), então não
  chega ao painel. Mesmo assim, o resumo da conta informa a situação de confirmação do e-mail.
- O painel é só de leitura. A edição do negócio continua nas operações existentes de cadastro,
  atualização e remoção do negócio.
- A operação segue o mesmo padrão de rota por identificador do cliente e a mesma regra de dono da
  rota usadas nas operações de negócio.
- Não é preciso cache nem paginação: o volume de dados por cliente é pequeno (uma conta e no
  máximo um negócio).
