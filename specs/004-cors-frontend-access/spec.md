# Feature Specification: Liberação de CORS para o front-end

**Feature Branch**: `feat/cors_permition`

**Created**: 2026-11-22

**Status**: Draft

**Input**: User description: "liberar cors para endereço: http://localhost:4200/  metodos: GET, POST, DEL e PUT. Qualquer header"

**Relação com features anteriores**: afeta todas as rotas já expostas pelas features
`001-rag-auth-gateway`, `002-email-confirmation` e `003-password-recovery`. Nenhuma regra de
negócio dessas features é alterada; muda apenas quais origens de navegador podem consumi-las.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Front-end local consome a API pelo navegador (Priority: P1)

O desenvolvedor do front-end executa a aplicação web localmente em `http://localhost:4200` e,
pelo navegador, chama as rotas da API (cadastro, login, confirmação de e-mail, recuperação de
senha etc.). Hoje o navegador bloqueia essas chamadas por virem de outra origem; após a feature,
as chamadas são aceitas e o front-end recebe as respostas normalmente.

**Why this priority**: sem isso o front-end não consegue integrar com a API pelo navegador; é o
único objetivo da feature.

**Independent Test**: a partir de uma página servida em `http://localhost:4200`, chamar qualquer
rota da API com GET, POST, PUT e DELETE e verificar que o navegador entrega a resposta à página
sem erro de política de origem.

**Acceptance Scenarios**:

1. **Given** uma página servida em `http://localhost:4200`, **When** ela envia uma requisição
   GET, POST, PUT ou DELETE para a API, **Then** o navegador permite que a página leia a resposta.
2. **Given** uma página servida em `http://localhost:4200`, **When** o navegador faz a
   verificação prévia (preflight) antes de uma requisição com cabeçalhos personalizados (ex.:
   `Authorization`, `Content-Type: application/json`, qualquer outro), **Then** a API autoriza a
   origem, o método e os cabeçalhos solicitados.
3. **Given** uma requisição autorizada pela política de origem, **When** a rota exige
   autenticação e o token é inválido ou ausente, **Then** a API continua respondendo com o mesmo
   erro de autenticação de antes (a liberação de origem não dispensa autenticação).

---

### User Story 2 - Origens não autorizadas continuam bloqueadas (Priority: P2)

Uma página servida em qualquer outro endereço (outra porta, outro host ou outro protocolo) tenta
chamar a API pelo navegador e continua sem autorização de origem.

**Why this priority**: garante que a liberação é restrita ao endereço pedido e não abre a API
para qualquer site.

**Independent Test**: enviar requisições com origens diferentes de `http://localhost:4200` e
verificar que a API não as autoriza.

**Acceptance Scenarios**:

1. **Given** uma página servida em `http://localhost:3000` (ou `https://localhost:4200`, ou
   `http://127.0.0.1:4200`), **When** ela chama a API, **Then** a resposta não autoriza essa
   origem e o navegador bloqueia a leitura.
2. **Given** a origem `http://localhost:4200`, **When** o preflight solicita um método fora da
   lista permitida (ex.: PATCH), **Then** a API não autoriza esse método.

---

### Edge Cases

- Origem informada com barra final (`http://localhost:4200/`): navegadores enviam a origem sem
  barra; a origem permitida é tratada como `http://localhost:4200`.
- Requisições sem cabeçalho de origem (ex.: chamadas servidor-a-servidor, ferramentas de linha de
  comando, testes) continuam funcionando exatamente como antes.
- Respostas de erro (400, 401, 404, 500) também devem carregar a autorização de origem, para que o
  front-end consiga ler a mensagem de erro em vez de receber um bloqueio genérico do navegador.
- Rotas inexistentes respondem normalmente (404) sem quebrar a verificação prévia.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST autorizar requisições de navegador originadas de
  `http://localhost:4200`.
- **FR-002**: O sistema MUST autorizar, para essa origem, os métodos GET, POST, PUT e DELETE.
- **FR-003**: O sistema MUST autorizar, para essa origem, qualquer cabeçalho de requisição.
- **FR-004**: O sistema MUST responder às verificações prévias (preflight) da origem autorizada
  com sucesso, sem exigir autenticação.
- **FR-005**: O sistema MUST NOT autorizar origens diferentes da(s) configurada(s).
- **FR-006**: O sistema MUST NOT autorizar métodos fora da lista do FR-002 (ex.: PATCH).
- **FR-007**: A lista de origens autorizadas MUST ser definida por configuração, de modo que
  outras origens (ex.: o endereço do front-end em produção) possam ser adicionadas sem alteração
  de código.
- **FR-008**: A autorização de origem MUST valer para todas as rotas da API, inclusive nas
  respostas de erro.
- **FR-009**: A liberação de origem MUST NOT alterar regras de autenticação, autorização ou
  validação já existentes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das rotas existentes podem ser chamadas com sucesso a partir de uma página em
  `http://localhost:4200`, nos quatro métodos permitidos, sem erro de política de origem no
  navegador.
- **SC-002**: 100% das tentativas a partir de origens não configuradas são recusadas pela política
  de origem.
- **SC-003**: Adicionar uma nova origem autorizada exige apenas mudança de configuração, sem
  nova versão do código.
- **SC-004**: Nenhum teste existente de autenticação, validação ou regra de negócio passa a falhar
  após a mudança.

## Assumptions

- "DEL" na descrição significa o método HTTP DELETE.
- A origem pedida, `http://localhost:4200/`, é a do front-end em desenvolvimento local; o valor
  padrão da configuração é `http://localhost:4200` e outros ambientes informam suas próprias
  origens.
- O front-end envia o token de acesso em cabeçalho (`Authorization`), não em cookies; portanto
  não é necessário permitir envio de credenciais (cookies) entre origens.
- Não é necessário expor cabeçalhos de resposta personalizados ao front-end além dos padrão.
- Não é necessário definir tempo de cache do preflight; o comportamento padrão é suficiente.
