# Feature Specification: Confirmação de e-mail no cadastro

**Feature Branch**: `002-email-confirmation`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "Envio de confirmação para e-mail cadastrado. Após o cadastro em `/api/v1/auth/register`, a aplicação envia um e-mail com link único por usuário, válido por 24 horas, que confirma o e-mail e ativa a conta. Expirado o prazo, o usuário solicita um novo link. Login antes da confirmação deve informar que o e-mail ainda não foi confirmado." (ver `.mine/email-confirmation/specify.md`)

**Relação com features anteriores**: estende a feature `001-rag-auth-gateway`. Substitui a premissa
daquela spec de que "não há verificação de e-mail nesta versão" e altera o FR-012 (rotas públicas).

## Clarifications

### Session 2026-10-31

- Q: Quando o cliente clica no link do e-mail, o que deve abrir? → A: Uma página do front-end
  (endereço configurável), que envia o valor do link à API para confirmar e mostra o resultado
  ao cliente.
- Q: Se uma conta continua pendente porque o link expirou e ninguém confirmou, outra pessoa pode
  se cadastrar com o mesmo e-mail? → A: Não. O e-mail de conta pendente sempre bloqueia novo
  cadastro (HTTP 409), mesmo com o link expirado; a única saída é solicitar novo link.
- Q: O que acontece com as contas que já existem no banco antes desta feature entrar no ar? →
  A: São marcadas como confirmadas na migração e continuam fazendo login normalmente, sem
  receber e-mail.
- Q: Se o envio do e-mail de confirmação falhar logo após o cadastro, o sistema deve tentar
  enviar de novo sozinho? → A: Sim, até 3 tentativas em segundo plano com intervalo crescente,
  todas concluídas em até ~5 minutos; esgotadas, a falha é registrada em log.
- Q: Qual limite deve ter o pedido de novo link de confirmação, que qualquer pessoa pode fazer sem
  login informando um e-mail? → A: Intervalo mínimo de 5 minutos entre emissões por conta e no
  máximo 10 novos links por conta a cada 24 horas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receber e usar o link de confirmação (Priority: P1)

Um cliente novo se cadastra informando nome, e-mail e senha. O cadastro é concluído como hoje e,
em seguida, o cliente recebe no e-mail informado uma mensagem com um link de confirmação. Ao
clicar no link dentro de 24 horas, o e-mail é confirmado, a conta é ativada e o cliente passa a
conseguir fazer login normalmente.

**Why this priority**: É o núcleo da feature: comprova que o cliente é dono do e-mail informado
e é a única forma de uma conta nova se tornar utilizável.

**Independent Test**: Cadastrar um cliente, capturar o e-mail enviado, abrir o link recebido e
verificar que a conta passa a ativa e que o login é aceito.

**Acceptance Scenarios**:

1. **Given** um e-mail ainda não cadastrado, **When** o cliente envia nome, e-mail e senha
   válidos, **Then** a conta é criada como hoje (mesma resposta de sucesso, com identificador e
   código de acesso), fica com situação "pendente de confirmação" e um e-mail de confirmação é
   enviado ao endereço informado.
2. **Given** um cadastro recém-concluído, **When** o e-mail de confirmação chega, **Then** ele
   contém um link exclusivo daquele cliente, informa que o link vale por 24 horas e explica como
   pedir um novo link caso expire.
3. **Given** um link de confirmação válido e dentro do prazo, **When** o cliente clica nele e a
   página do front-end envia o valor do link à API, **Then** o e-mail é marcado como confirmado,
   a conta passa a "ativa" e a API devolve mensagem em português informando que a conta foi
   ativada e que já pode fazer login.
4. **Given** uma conta confirmada, **When** o cliente faz login com credenciais corretas,
   **Then** o login é aceito exatamente como na feature 001 (token, identificador e negócio).
5. **Given** dois clientes cadastrados, **When** cada um recebe seu e-mail, **Then** os links são
   diferentes, e o link de um nunca confirma a conta do outro.

---

### User Story 2 - Login bloqueado antes da confirmação (Priority: P1)

Um cliente que se cadastrou mas ainda não confirmou o e-mail tenta fazer login. A aplicação não
emite token e informa que o e-mail ainda não foi confirmado e que é preciso confirmá-lo para
acessar a aplicação.

**Why this priority**: Sem esse bloqueio a confirmação não tem efeito prático; é o que garante
que só contas com e-mail comprovado acessam o sistema.

**Independent Test**: Cadastrar um cliente sem abrir o link, tentar login com as credenciais
corretas e verificar a recusa com a mensagem de e-mail não confirmado; tentar com senha errada e
verificar que a resposta continua sendo a genérica de credenciais inválidas.

**Acceptance Scenarios**:

1. **Given** uma conta pendente de confirmação, **When** o cliente faz login com e-mail e senha
   corretos, **Then** recebe HTTP 403 com mensagem em português informando que o e-mail ainda não
   foi confirmado e que ele precisa confirmá-lo (ou solicitar novo link) para acessar, e nenhum
   token é emitido.
2. **Given** uma conta pendente de confirmação, **When** alguém tenta login com senha incorreta,
   **Then** recebe a mesma resposta HTTP 401 genérica da feature 001, sem revelar que a conta
   existe nem que está pendente.
3. **Given** uma conta que acabou de ser confirmada, **When** o cliente tenta login novamente,
   **Then** o login é aceito.

---

### User Story 3 - Solicitar novo link de confirmação (Priority: P2)

Um cliente que não confirmou o e-mail a tempo (link expirado) ou que não recebeu/perdeu o e-mail
solicita um novo link informando o seu e-mail. Um novo e-mail é enviado com um novo link válido
por 24 horas, e os links anteriores deixam de funcionar.

**Why this priority**: Sem ele, um cliente que perde o prazo fica com a conta bloqueada para
sempre, já que o e-mail não pode ser cadastrado novamente.

**Independent Test**: Cadastrar um cliente, fazer o link expirar, solicitar novo link, verificar
que o link antigo é recusado e que o novo ativa a conta.

**Acceptance Scenarios**:

1. **Given** uma conta pendente de confirmação, **When** o cliente solicita novo link informando
   seu e-mail, **Then** um novo e-mail de confirmação é enviado com um link novo, válido por 24
   horas a partir da solicitação, e todos os links anteriores daquela conta são invalidados.
2. **Given** um link expirado, **When** o cliente clica nele, **Then** a conta continua pendente
   e ele recebe mensagem em português informando que o link expirou e como solicitar um novo.
3. **Given** um e-mail não cadastrado, de conta removida ou de conta já confirmada, **When**
   alguém solicita novo link, **Then** recebe a mesma resposta genérica de sucesso dada a uma
   conta pendente, sem revelar a situação do e-mail, e nenhum e-mail de confirmação é enviado.
4. **Given** um link emitido há menos de 5 minutos para a mesma conta, **When** o cliente pede
   outro, **Then** recebe a mesma resposta genérica e nenhum novo e-mail é enviado.
5. **Given** uma conta que já recebeu 10 novos links nas últimas 24 horas, **When** o cliente pede
   mais um, **Then** recebe a mesma resposta genérica e nenhum novo e-mail é enviado até que a
   janela de 24 horas libere espaço.

---

### Edge Cases

- **Link já usado**: clicar novamente num link que já confirmou a conta retorna mensagem
  informando que o e-mail já está confirmado, sem erro e sem alterar nada.
- **Link anterior a uma nova solicitação**: link invalidado por um pedido de novo link é recusado
  com mensagem de link inválido, orientando a usar o e-mail mais recente.
- **Link adulterado, incompleto ou inexistente**: recusado com mensagem genérica de link inválido,
  sem indicar se o cliente existe.
- **Link de conta removida**: recusado como link inválido; a conta removida não é reativada.
- **Falha no envio do e-mail no cadastro**: o cadastro continua concluído com sucesso (conta
  pendente); o sistema tenta até 3 vezes em ~5 minutos e, se todas falharem, registra em log e o
  cliente pode solicitar novo link.
- **Reinício da aplicação com envio pendente**: tentativas ainda não feitas podem se perder; o
  cliente recorre ao pedido de novo link.
- **Novo cadastro com e-mail de conta pendente**: recusado com HTTP 409 como hoje, esteja o link
  dentro do prazo ou expirado; a conta pendente nunca é substituída. O cliente deve solicitar
  novo link em vez de se cadastrar de novo.
- **Contas existentes antes desta feature**: são consideradas confirmadas e continuam fazendo
  login normalmente, sem receber e-mail.
- **Link aberto exatamente no limite de 24 horas**: o link vale enquanto o instante da abertura
  for anterior ou igual ao instante de expiração; após isso é recusado como expirado.
- **Conta pendente tentando rotas protegidas**: impossível, pois nenhum token é emitido para ela.

## Requirements *(mandatory)*

### Functional Requirements

**Cadastro e envio**

- **FR-001**: O cadastro MUST continuar funcionando como na feature 001 (mesmas validações, mesma
  resposta de sucesso e de erro), com a diferença de que a conta nova MUST ser criada com
  situação "pendente de confirmação".
- **FR-002**: Após o cadastro ser concluído, o sistema MUST enviar um e-mail de confirmação ao
  endereço informado, contendo o link de confirmação, o prazo de validade (24 horas) e a
  orientação de como pedir um novo link. O texto do e-mail MUST estar em português.
- **FR-003**: O envio do e-mail MUST ocorrer em segundo plano e MUST NOT atrasar nem desfazer o
  cadastro. Em caso de falha, o sistema MUST fazer até 3 tentativas no total, com intervalo
  crescente entre elas, concluídas em até ~5 minutos após o cadastro. Esgotadas as tentativas, a
  falha MUST ser registrada em log e a conta MUST permanecer criada e pendente (o cliente pode
  solicitar novo link). O mesmo vale para o envio de novo link (FR-013). Garantir os envios
  pendentes após reinício da aplicação está fora do escopo.

**Link de confirmação**

- **FR-004**: Cada link de confirmação MUST ser único: identifica um único cliente, está vinculado
  ao seu código de acesso e à ação "confirmação de e-mail", e não pode ser usado para nenhuma
  outra ação nem para outro cliente.
- **FR-005**: O link MUST conter apenas um valor opaco e impossível de adivinhar ou derivar; MUST
  NOT expor em texto claro o e-mail, o identificador numérico nem o código de acesso do cliente.
- **FR-006**: O link MUST expirar 24 horas após a sua emissão.
- **FR-007**: Um link MUST ser de uso único: depois de confirmar a conta, não produz novo efeito.
- **FR-007a**: O link MUST apontar para uma página do front-end, cujo endereço base é
  configurável; a página envia o valor do link à rota de confirmação desta API, que devolve o
  resultado (sucesso, expirado, inválido ou já confirmado) com mensagem em português para o
  front-end exibir. A API MUST NOT confirmar a conta apenas pelo acesso ao endereço do link.
- **FR-008**: Ao receber um valor de link válido e dentro do prazo, o sistema MUST marcar o e-mail
  como confirmado, registrar a data da confirmação, mudar a situação da conta para "ativa" e
  devolver resultado de sucesso com mensagem em português.
- **FR-009**: Ao receber um valor de link expirado, o sistema MUST manter a conta pendente e informar, em
  português, que o link expirou e como solicitar um novo.
- **FR-010**: Ao receber um valor de link inválido (adulterado, inexistente, invalidado por novo pedido ou de
  conta removida), o sistema MUST recusar com mensagem genérica em português, sem revelar a
  existência ou a situação da conta.
- **FR-011**: Ao receber um valor de link de conta já confirmada, o sistema MUST informar que o e-mail já
  está confirmado, sem alterar dados.

**Novo link**

- **FR-012**: O sistema MUST permitir solicitar novo link de confirmação informando apenas o
  e-mail, sem token. E-mail em formato inválido MUST retornar HTTP 400 com o detalhe do campo.
- **FR-013**: Para conta pendente, o sistema MUST emitir novo link válido por 24 horas, enviá-lo
  por e-mail e invalidar todos os links anteriores daquela conta.
- **FR-014**: A resposta à solicitação de novo link MUST ser idêntica (mesmo status e mensagem)
  para e-mail pendente, já confirmado, removido ou inexistente; só a conta pendente recebe e-mail.
- **FR-015**: Por conta, o sistema MUST emitir no máximo um novo link a cada 5 minutos (contados
  da emissão anterior, inclusive a do cadastro) e no máximo 10 novos links nas últimas 24 horas
  (janela móvel; o link do cadastro não entra nessa contagem). Pedidos acima desses limites MUST
  receber a mesma resposta genérica (FR-014), sem emitir link nem enviar e-mail, e MUST ser
  registrados em log.

**Login**

- **FR-016**: O login de conta pendente com credenciais corretas MUST ser recusado com HTTP 403 e
  mensagem em português informando que o e-mail ainda não foi confirmado e que é necessário
  confirmá-lo para acessar; nenhum token MUST ser emitido.
- **FR-017**: A verificação de confirmação MUST ocorrer somente após a validação das credenciais;
  credenciais inválidas MUST continuar recebendo a resposta 401 genérica da feature 001.
- **FR-018**: O login de conta ativa MUST seguir sem alteração o comportamento da feature 001.

**Rotas e registros**

- **FR-019**: As rotas acessíveis sem token MUST passar a ser: cadastro, login, confirmação de
  e-mail e solicitação de novo link (altera o FR-012 da feature 001).
- **FR-020**: Contas ativas existentes antes desta feature MUST ser marcadas como confirmadas na
  migração de dados e continuar fazendo login sem alteração; nenhum e-mail de confirmação MUST ser
  enviado a elas. Contas removidas permanecem removidas.
- **FR-021**: O sistema MUST registrar em log o envio de e-mail de confirmação (sucesso ou falha),
  confirmações realizadas, tentativas com link expirado ou inválido e logins recusados por e-mail
  não confirmado, sem registrar o valor do link nem dados sensíveis.

### Key Entities *(include if feature involves data)*

- **Cliente** (existente, alterado): ganha a situação "pendente de confirmação" além de "ativa" e
  "removida", e a data de confirmação do e-mail (vazia enquanto pendente).
- **Link de confirmação de e-mail**: autorização de uso único para confirmar o e-mail de um
  cliente. Atributos: cliente a que pertence, ação (confirmação de e-mail), valor opaco usado no
  link (guardado de forma que não permita reconstruir o link), data de emissão, data de
  expiração, data de uso (vazia se não usado) e indicação de invalidação. Um cliente pode ter
  vários ao longo do tempo, mas no máximo um válido por vez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 95% dos cadastros, o e-mail de confirmação é entregue ao serviço de envio em
  menos de 1 minuto após a conclusão do cadastro.
- **SC-002**: O tempo de resposta do cadastro continua dentro da meta da feature 001 (95% abaixo de
  500 ms), independentemente do envio do e-mail.
- **SC-003**: 100% das tentativas de login de contas pendentes com credenciais corretas são
  recusadas com a mensagem de e-mail não confirmado, sem emissão de token.
- **SC-004**: 100% dos links expirados, já invalidados ou adulterados são recusados (verificado
  por testes automatizados para cada caso).
- **SC-005**: 100% dos links emitidos são distintos (verificado por teste com emissão de ao menos
  10.000 links sem nenhuma colisão).
- **SC-006**: Um cliente que recebeu o e-mail consegue confirmar a conta e fazer o primeiro login
  em menos de 2 minutos.
- **SC-007**: A resposta à solicitação de novo link é indistinguível entre e-mails pendentes,
  confirmados, removidos e inexistentes em 100% dos testes.
- **SC-008**: Nenhum valor de link de confirmação aparece em logs ou respostas de erro (verificado
  por auditoria de logs em testes).

## Assumptions

- Existe um serviço de envio de e-mail disponível e configurável (remetente, credenciais); a
  escolha do serviço e a justificativa de qualquer dependência nova ficam para o plano, conforme
  a constituição.
- O link aponta para uma página do front-end (endereço base configurável). A construção dessa
  página é responsabilidade do front-end e está fora do escopo desta API; aqui o escopo é gerar o
  link com esse endereço e expor a rota de confirmação que a página chama.
- O prazo de 24 horas é fixo nesta versão, sem configuração, e coincide com o texto do e-mail.
- Os limites de novo link (5 minutos de intervalo e 10 por 24 horas, por conta) protegem contra
  envio abusivo de e-mails a um mesmo endereço; limitação por origem (IP) ou global continua fora
  do escopo, como na feature 001.
- Contas pendentes que nunca forem confirmadas permanecem pendentes; limpeza automática dessas
  contas está fora do escopo desta versão.
- Alteração de e-mail da conta continua fora do escopo; portanto não há reconfirmação por troca
  de e-mail.
- Recuperação de senha continua fora do escopo.
- A API deve seguir a constituição do projeto (mensagens em português, erros padronizados, DTOs).
