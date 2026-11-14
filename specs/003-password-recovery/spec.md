# Feature Specification: Recuperação de senha

**Feature Branch**: `003-password-recovery`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "Recuperação de senha. O usuário que esqueceu ou perdeu a senha solicita a recuperação informando o e-mail cadastrado; o sistema envia ao e-mail um link para redefinir a senha, único por usuário e válido por 24 horas. Na tela do link o usuário informa e confirma a nova senha; a senha é atualizada e ele volta a acessar a aplicação normalmente. Expirado o prazo, ele deve solicitar um novo link." (ver `.mine/password recovery/specify.md`)

**Relação com features anteriores**: estende as features `001-rag-auth-gateway` (cadastro, login,
regras de senha, rotas públicas) e `002-email-confirmation` (envio de e-mail em segundo plano,
links opacos de uso único, página do front-end com endereço configurável, limites de reenvio).
Remove a premissa dessas specs de que "recuperação de senha está fora do escopo" e altera a lista
de rotas públicas (FR-019 da feature 002).

## Clarifications

### Session 2026-11-08

- Q: Os tokens de acesso emitidos antes da redefinição de senha devem deixar de valer? → A: Não;
  continuam válidos até expirarem naturalmente.
- Q: Uma conta pendente de confirmação de e-mail pode pedir recuperação de senha? → A: Não; recebe
  a resposta genérica e nenhum e-mail é enviado. Precisa confirmar o e-mail antes.
- Q: Após redefinir a senha, a API devolve token (login automático) ou o cliente faz login? → A:
  Sem login automático; a API devolve apenas mensagem de sucesso e o cliente faz login com a nova
  senha.
- Q: A API deve ter uma rota que só verifica se o link de recuperação é válido, antes de o
  front-end exibir o formulário? → A: Sim; rota pública que verifica sem consumir o link e
  responde válido, expirado ou inválido.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Solicitar a recuperação de senha (Priority: P1)

Um cliente que esqueceu ou perdeu a senha informa o e-mail cadastrado e pede a recuperação. Ele
recebe nesse e-mail uma mensagem com um link para redefinir a senha, válido por 24 horas. A
resposta ao pedido é sempre a mesma, exista ou não uma conta com aquele e-mail.

**Why this priority**: É a porta de entrada da recuperação; sem o link nenhuma redefinição é
possível.

**Independent Test**: Pedir a recuperação para um e-mail cadastrado e capturar o e-mail enviado,
verificando o link e o prazo; pedir para um e-mail inexistente e verificar que a resposta é
idêntica e que nenhum e-mail é enviado.

**Acceptance Scenarios**:

1. **Given** uma conta ativa, **When** o cliente solicita a recuperação informando o e-mail
   cadastrado, **Then** recebe uma resposta genérica de sucesso em português e um e-mail de
   recuperação é enviado a esse endereço.
2. **Given** um pedido de recuperação atendido, **When** o e-mail chega, **Then** ele contém um
   link exclusivo daquele cliente, informa que o link vale por 24 horas, orienta a pedir um novo
   link caso expire e avisa que, se o cliente não fez o pedido, basta ignorar a mensagem (a senha
   atual continua valendo).
3. **Given** um e-mail não cadastrado ou de conta removida, **When** alguém solicita a
   recuperação, **Then** recebe exatamente a mesma resposta do cenário 1 e nenhum e-mail é
   enviado.
4. **Given** um e-mail em formato inválido, **When** alguém solicita a recuperação, **Then**
   recebe HTTP 400 com o detalhe do campo.
5. **Given** dois clientes que pedem a recuperação, **When** cada um recebe seu e-mail, **Then**
   os links são diferentes, e o link de um nunca altera a senha do outro.

---

### User Story 2 - Redefinir a senha pelo link (Priority: P1)

O cliente clica no link dentro do prazo, é levado à tela de redefinição, informa a nova senha e a
confirmação. A senha é trocada, o link deixa de funcionar e o cliente passa a fazer login com a
nova senha; a senha antiga deixa de ser aceita.

**Why this priority**: É o objetivo da feature: devolver ao cliente o acesso à conta.

**Independent Test**: Pedir a recuperação, abrir o link, enviar nova senha e confirmação válidas,
verificar que o login com a senha nova é aceito, que o login com a senha antiga é recusado e que o
mesmo link não pode ser usado de novo.

**Acceptance Scenarios**:

1. **Given** um link de recuperação válido e dentro do prazo, **When** o cliente informa nova
   senha e confirmação iguais e que atendem às regras de senha, **Then** a senha é atualizada, o
   link é consumido e o cliente recebe mensagem em português informando que a senha foi alterada
   e que já pode fazer login.
2. **Given** uma senha redefinida, **When** o cliente faz login com a nova senha, **Then** o login
   é aceito exatamente como na feature 001; **When** tenta com a senha antiga, **Then** recebe a
   resposta 401 genérica.
3. **Given** um link válido, **When** a nova senha e a confirmação são diferentes, **Then** recebe
   HTTP 400 com o detalhe do campo, a senha não muda e o link continua válido.
4. **Given** um link válido, **When** a nova senha não atende às regras de senha da feature 001,
   **Then** recebe HTTP 400 com o detalhe do campo, a senha não muda e o link continua válido.
5. **Given** um link já usado para redefinir a senha, **When** alguém tenta usá-lo de novo,
   **Then** é recusado como link inválido e a senha não muda.
6. **Given** uma senha redefinida, **When** a troca é concluída, **Then** o cliente recebe um
   e-mail em português avisando que a senha foi alterada e orientando o que fazer caso não tenha
   sido ele.
7. **Given** um cliente com sessões abertas (tokens emitidos antes da troca), **When** a senha é
   redefinida, **Then** esses tokens continuam válidos até expirarem naturalmente, sem alteração
   na validação de token da feature 001.
8. **Given** um link de recuperação, **When** a tela de redefinição pede à API apenas a
   verificação do link, **Then** recebe se ele é válido, expirado ou inválido, com mensagem em
   português, sem consumi-lo e sem dados da conta; um link válido continua utilizável depois.

---

### User Story 3 - Link expirado e novo pedido (Priority: P2)

Um cliente que não usou o link dentro de 24 horas tenta usá-lo e é informado de que o link
expirou e de que deve pedir um novo. Ao pedir de novo, recebe um novo link válido por 24 horas e
os links anteriores deixam de funcionar.

**Why this priority**: Garante que o prazo seja respeitado sem deixar o cliente sem saída.

**Independent Test**: Pedir a recuperação, fazer o link expirar, verificar a recusa com mensagem
de expirado, pedir novamente e verificar que o link antigo é recusado e o novo funciona.

**Acceptance Scenarios**:

1. **Given** um link expirado, **When** o cliente tenta redefinir a senha com ele, **Then** a
   senha não muda e ele recebe mensagem em português informando que o link expirou e que deve
   solicitar uma nova recuperação.
2. **Given** um link ainda válido, **When** o cliente faz um novo pedido de recuperação e ele é
   atendido, **Then** um novo link válido por 24 horas é enviado e todos os links de recuperação
   anteriores daquela conta são invalidados.
3. **Given** um link emitido há menos de 5 minutos para a mesma conta, **When** o cliente pede
   outro, **Then** recebe a mesma resposta genérica e nenhum novo e-mail é enviado.
4. **Given** uma conta que já recebeu 10 links de recuperação nas últimas 24 horas, **When** o
   cliente pede mais um, **Then** recebe a mesma resposta genérica e nenhum novo e-mail é enviado
   até que a janela de 24 horas libere espaço.

---

### Edge Cases

- **Conta pendente de confirmação de e-mail (feature 002)** pedindo recuperação: recebe a mesma
  resposta genérica e nenhum e-mail é enviado. O cliente deve primeiro confirmar o e-mail (ou
  solicitar novo link de confirmação) e só então pedir a recuperação.
- **Link de recuperação de conta que voltou a ficar pendente**: não ocorre nesta versão, pois uma
  conta ativa nunca volta a ficar pendente.
- **Link adulterado, incompleto ou inexistente**: recusado com mensagem genérica de link inválido,
  sem indicar se o cliente existe.
- **Link invalidado por um pedido mais recente**: recusado como link inválido, orientando a usar o
  e-mail mais recente.
- **Conta removida depois da emissão do link**: o link é recusado como inválido; a conta não é
  reativada.
- **Link de confirmação de e-mail usado na redefinição (ou o contrário)**: recusado como link
  inválido; cada link só serve à sua própria ação.
- **Nova senha igual à atual**: aceita; não há verificação de histórico de senhas nesta versão.
- **Pedido de recuperação não feito pelo dono do e-mail**: nada muda na conta; a senha atual
  continua valendo e o link apenas expira se não for usado.
- **Falha no envio do e-mail de recuperação**: a resposta ao pedido continua a genérica de
  sucesso; o sistema tenta até 3 vezes em ~5 minutos e, se todas falharem, registra em log e o
  cliente pode fazer novo pedido.
- **Reinício da aplicação com envio pendente**: tentativas ainda não feitas podem se perder; o
  cliente faz novo pedido.
- **Link usado exatamente no limite de 24 horas**: vale enquanto o instante do envio da nova senha
  for anterior ou igual ao instante de expiração; após isso é recusado como expirado.
- **Dois envios simultâneos com o mesmo link**: no máximo um redefine a senha; o outro é recusado
  como link inválido.
- **Pedido de recuperação vindo de cliente já autenticado**: tratado da mesma forma; não exige nem
  usa token.

## Requirements *(mandatory)*

### Functional Requirements

**Pedido de recuperação**

- **FR-001**: O sistema MUST permitir solicitar a recuperação de senha informando apenas o e-mail,
  sem token. E-mail em formato inválido MUST retornar HTTP 400 com o detalhe do campo.
- **FR-002**: Para conta ativa, o sistema MUST emitir um link de recuperação válido por 24 horas,
  enviá-lo ao e-mail cadastrado e invalidar todos os links de recuperação anteriores daquela
  conta.
- **FR-003**: A resposta ao pedido MUST ser idêntica (mesmo status e mensagem) para e-mail de conta
  ativa, pendente, removida ou inexistente, e também quando o pedido é barrado pelos limites do
  FR-005; só a conta ativa recebe e-mail. Conta pendente de confirmação de e-mail MUST NOT
  receber link de recuperação.
- **FR-004**: O e-mail de recuperação MUST estar em português e conter o link, o prazo de validade
  (24 horas), a orientação para pedir um novo link caso expire e o aviso de que, se o cliente não
  fez o pedido, pode ignorar a mensagem e a senha atual continua valendo.
- **FR-005**: Por conta, o sistema MUST emitir no máximo um link de recuperação a cada 5 minutos e
  no máximo 10 links de recuperação nas últimas 24 horas (janela móvel). Pedidos acima desses
  limites MUST receber a resposta genérica do FR-003, sem emitir link nem enviar e-mail, e MUST
  ser registrados em log. Esses limites são independentes dos limites de reenvio de confirmação
  de e-mail da feature 002.
- **FR-006**: O envio do e-mail MUST ocorrer em segundo plano e MUST NOT atrasar a resposta ao
  pedido. Em caso de falha, o sistema MUST fazer até 3 tentativas no total, com intervalo
  crescente, concluídas em até ~5 minutos; esgotadas, a falha MUST ser registrada em log. Garantir
  envios pendentes após reinício da aplicação está fora do escopo.

**Link de recuperação**

- **FR-007**: Cada link de recuperação MUST ser único: identifica um único cliente, está vinculado
  à ação "redefinição de senha" e MUST NOT servir para outra ação (inclusive confirmação de
  e-mail) nem para outro cliente.
- **FR-008**: O link MUST conter apenas um valor opaco e impossível de adivinhar ou derivar; MUST
  NOT expor em texto claro o e-mail, o identificador numérico nem o código de acesso do cliente.
- **FR-009**: O link MUST expirar 24 horas após a sua emissão.
- **FR-010**: O link MUST ser de uso único: depois de redefinir a senha, MUST ser recusado como
  inválido em qualquer nova tentativa.
- **FR-011**: O link MUST apontar para uma página do front-end (tela de redefinição), cujo
  endereço base é configurável; a página envia à API o valor do link, a nova senha e a
  confirmação. A API MUST NOT alterar nada apenas pelo acesso ao endereço do link.
- **FR-012**: O sistema MUST permitir que a página do front-end verifique, antes de exibir o
  formulário, se um valor de link é válido, expirado ou inválido, sem consumi-lo, devolvendo
  mensagem em português para cada caso. A verificação MUST NOT alterar o link nem a conta e MUST
  NOT devolver dados da conta (nome, e-mail, identificador); os casos de link inválido seguem a
  mesma resposta genérica do FR-016. O resultado "válido" vem no corpo da resposta; "expirado" e
  "inválido" são informados pelo código HTTP (410 e 400), com as mensagens do FR-015 e do FR-016.

**Redefinição**

- **FR-013**: Ao receber um valor de link válido e dentro do prazo, com nova senha e confirmação
  iguais e que atendem às regras de senha da feature 001 (FR-004 daquela spec), o sistema MUST
  substituir a senha da conta, consumir o link, invalidar os demais links de recuperação da conta
  e devolver resultado de sucesso com mensagem em português. A resposta MUST NOT conter token de
  acesso nem dados da conta; o cliente MUST fazer login com a nova senha para obter um token.
- **FR-014**: Nova senha e confirmação diferentes, ou nova senha fora das regras, MUST retornar
  HTTP 400 com o detalhe de cada campo; nesse caso a senha MUST NOT mudar e o link MUST continuar
  válido.
- **FR-015**: Valor de link expirado MUST ser recusado sem alterar a senha, com mensagem em
  português informando que o link expirou e que é preciso solicitar uma nova recuperação.
- **FR-016**: Valor de link inválido (adulterado, inexistente, já usado, invalidado por pedido mais
  recente, de outra ação ou de conta removida) MUST ser recusado com mensagem genérica em
  português, sem revelar a existência ou a situação da conta.
- **FR-017**: Após a redefinição, o login com a nova senha MUST ser aceito como na feature 001 e o
  login com a senha antiga MUST receber a resposta 401 genérica.
- **FR-018**: Após a redefinição, o sistema MUST enviar ao e-mail da conta um aviso em português
  de que a senha foi alterada, com orientação para o caso de o cliente não reconhecer a troca. O
  aviso MUST NOT conter a senha nem link de redefinição.
- **FR-019**: Tokens de acesso emitidos antes da redefinição MUST continuar válidos até expirarem
  naturalmente; a redefinição MUST NOT alterar a validação de token da feature 001.

**Rotas e registros**

- **FR-020**: As rotas acessíveis sem token MUST passar a ser: cadastro, login, confirmação de
  e-mail, solicitação de novo link de confirmação, solicitação de recuperação de senha,
  verificação do link de recuperação e redefinição de senha (altera o FR-019 da feature 002).
- **FR-021**: O sistema MUST registrar em log os pedidos de recuperação (atendidos ou barrados por
  limite), o envio dos e-mails (sucesso ou falha), as redefinições realizadas e as tentativas com
  link expirado ou inválido, sem registrar o valor do link, senhas nem dados sensíveis.

### Key Entities *(include if feature involves data)*

- **Cliente** (existente, alterado): a senha passa a poder ser substituída por redefinição; ganha
  a data da última alteração de senha.
- **Link de recuperação de senha**: autorização de uso único para redefinir a senha de um
  cliente. Atributos: cliente a que pertence, ação (redefinição de senha), valor opaco usado no
  link (guardado de forma que não permita reconstruir o link), data de emissão, data de
  expiração, data de uso (vazia se não usado) e indicação de invalidação. Um cliente pode ter
  vários ao longo do tempo, mas no máximo um válido por vez. Usa o mesmo formato do link de
  confirmação de e-mail da feature 002 (selector/validator), mas fica guardado à parte e é
  distinguido pela ação.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 95% dos pedidos atendidos, o e-mail de recuperação é entregue ao serviço de envio
  em menos de 1 minuto após o pedido.
- **SC-002**: Um cliente que recebeu o e-mail consegue redefinir a senha e fazer login com ela em
  menos de 3 minutos.
- **SC-003**: A resposta ao pedido de recuperação é indistinguível (status, mensagem e tempo de
  resposta dentro da mesma faixa) entre e-mails ativos, pendentes, removidos e inexistentes em
  100% dos testes.
- **SC-004**: 100% dos links expirados, já usados, invalidados, adulterados ou de outra ação são
  recusados sem alterar a senha (verificado por testes automatizados para cada caso).
- **SC-005**: 100% dos links emitidos são distintos (verificado por teste com emissão de ao menos
  10.000 links sem nenhuma colisão).
- **SC-006**: Após a redefinição, 100% das tentativas de login com a senha antiga são recusadas.
- **SC-007**: Nenhum valor de link de recuperação nem senha aparece em logs ou respostas de erro
  (verificado por auditoria de logs em testes).
- **SC-008**: Pedidos de suporte para recuperar acesso à conta caem a zero nos casos em que o
  cliente ainda tem acesso ao e-mail cadastrado.

## Assumptions

- O serviço de envio de e-mail e o mecanismo de envio em segundo plano com novas tentativas da
  feature 002 são reutilizados; nenhuma dependência nova é esperada para esta feature.
- A tela de redefinição é uma página do front-end. O endereço base é configurado separadamente do
  da confirmação de e-mail, mas pode ter o mesmo valor, porque o caminho do link distingue as
  páginas. Construí-la é responsabilidade do front-end e está fora do escopo desta
  API; aqui o escopo é gerar o link e expor as rotas que a página chama.
- O prazo de 24 horas é fixo nesta versão, sem configuração, e coincide com o texto do e-mail.
- "Link único para cada usuário" significa que cada link pertence a um único cliente, é distinto
  de todos os outros e só pode ser usado uma vez.
- As regras de senha são as mesmas do cadastro (feature 001); não há verificação de histórico nem
  proibição de repetir a senha atual.
- Um pedido de recuperação não altera a conta: a senha atual continua valendo até a redefinição
  ser concluída.
- Os limites por conta (5 minutos e 10 por 24 horas) protegem contra envio abusivo de e-mails;
  limitação por origem (IP) ou global continua fora do escopo, como nas features 001 e 002.
- Revogação de sessões (tokens) após a troca de senha está fora do escopo; tokens antigos expiram
  naturalmente.
- Troca de senha por cliente autenticado (informando a senha atual) está fora do escopo; esta
  feature cobre apenas a recuperação de quem não sabe a senha.
- Alteração de e-mail da conta continua fora do escopo; o link é sempre enviado ao e-mail
  cadastrado.
- A API deve seguir a constituição do projeto (mensagens em português, erros padronizados, DTOs).
