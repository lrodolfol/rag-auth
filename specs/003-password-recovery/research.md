# Research: Recuperação de senha

**Feature**: `003-password-recovery` | **Data**: 2026-11-08 | **Plano**: [plan.md](./plan.md)

Este documento resolve as incógnitas do Technical Context e os conflitos entre a entrada de
planejamento (`.mine/password recovery/plan.md`), a especificação (`spec.md`), a constituição e o
código já entregue pela feature 002. Cada item segue o formato **Decisão / Justificativa /
Alternativas consideradas**.

---

## R1. Formato do link de recuperação

**Entrada**: `https://ragauth.com.br/{client-identification-hash}/pass-recuvery/{key-hash}`.

**Decisão**: o link usa o mesmo padrão *selector/validator* da feature 002 (research R1 daquela
feature), com um par novo para cada link:

| Segmento | Conteúdo | Como é guardado no banco |
|---|---|---|
| `{client-identification-hash}` (selector) | 16 bytes aleatórios em Base64Url (22 caracteres) | texto puro, com índice único |
| `{key-hash}` (validator) | 32 bytes aleatórios em Base64Url (43 caracteres) | apenas `SHA-256(validator:accessCode:password-recovery)`, em hexadecimal (64 caracteres) |

Como a ação (`password-recovery`) entra no hash, um par selector/validator de confirmação de e-mail
nunca é aceito como link de recuperação, nem o contrário (FR-007).

O segmento `pass-recuvery` é mantido **exatamente como veio na entrada**, porque o front-end usa o
mesmo caminho. ⚠️ A grafia parece ter erro de digitação ("recuvery" no lugar de "recovery"). O
dono do produto precisa confirmar antes da implementação. O segmento fica numa constante única
(`PasswordRecoveryLinkIssuer.RecoveryPathSegment`) e no modelo das rotas. Corrigir depois exige
mudar só esses dois pontos.

O endereço base vem da nova configuração `email:passwordRecoveryPageBaseUrl`, com padrão
`https://ragauth.com.br`. A spec pede que o endereço da tela de redefinição seja distinto e
configurável.

**Justificativa**: o código já existe e foi testado (`RandomConfirmationLinkGenerator`). O
validator tem 256 bits. Nada no link pode ser derivado dos dados do cliente (FR-008).

**Alternativas consideradas**:
- Reaproveitar `confirmationPageBaseUrl`. Isso acopla duas páginas diferentes do front-end, então
  foi descartado.
- Usar um token único em um só segmento. Contraria o formato de dois segmentos pedido na entrada.

## R2. Gerador de link compartilhado entre as duas ações

**Problema**: `RandomConfirmationLinkGenerator.ComputeValidatorHash` hoje fixa a ação
`email-confirmation`.

**Decisão**: o gerador passa a ser genérico. As mudanças são:
- `IConfirmationLinkGenerator` passa a se chamar `ISecureLinkGenerator`.
- `RandomConfirmationLinkGenerator` passa a se chamar `RandomSecureLinkGenerator` e vai para
  `Services/SecureLinks/`.
- `ConfirmationLinkFormat` passa a se chamar `SecureLinkFormat`.
- `ComputeValidatorHash` e `IsValidatorMatch` passam a receber a ação (`purpose`).

Os chamadores da feature 002 passam `EmailConfirmationLink.EmailConfirmationPurpose`. Os hashes
já gravados continuam válidos, porque a entrada do hash não muda para essa ação.

**Justificativa**: evita duplicar a parte criptográfica (Constituição II). O nome genérico descreve
o novo papel (Constituição III).

**Alternativas consideradas**:
- Criar um segundo gerador quase idêntico. Duplica a parte de segurança mais sensível, então foi
  descartado.
- Manter o nome antigo e só acrescentar o parâmetro. O nome passaria a mentir sobre o que a classe
  faz.

## R3. Armazenamento: tabela própria `password_recovery_links`

**Decisão**: criar a entidade nova `PasswordRecoveryLink` e a tabela `password_recovery_links`,
com as mesmas colunas de `email_confirmation_links` exceto `origin`. Os índices são três:
- selector único;
- "no máximo um link ativo por cliente", único e parcial
  (`"usedAt" IS NULL AND "revokedAt" IS NULL`);
- `(clientId, issuedAt)`, usado nos limites de emissão.

A tabela tem filtro global para conta removida.

**Justificativa**:
- O índice parcial "um ativo por cliente" da tabela de confirmação não considera a ação. Juntar as
  duas ações na mesma tabela exigiria recriar esse índice, mexer em todas as consultas da feature
  002 e renomear a entidade, com alto risco de regressão.
- Uma tabela por ação mantém as invariantes (FR-002, FR-013) simples e as consultas independentes.
- A spec já trata o link de recuperação como "mesmo modelo, distinguido pela ação".

**Alternativas consideradas**:
- Tabela única `client_action_links` com a coluna `purpose`. É mais genérica, mas exige migrar
  dados, refatorar a feature 002 e reescrever índices. Foi descartada por custo e risco.
- Reaproveitar `email_confirmation_links` sem mudar o índice. Um link de recuperação revogaria o
  de confirmação e vice-versa, o que é incorreto.

## R4. Rotas

**Entrada**: `POST /api/v1/client/new-pass-recuvery-request/` e
`PUT /api/v1/client/{client-identification-hash}/pass-recuvery/{key-hash}/`, ambas abertas. A
clarificação 4 da spec acrescenta a verificação do link.

**Decisão**:

| Rota | Uso | Resposta de sucesso |
|---|---|---|
| `POST /api/v1/client/new-pass-recuvery-request` | Pede a recuperação. Corpo `{ "email" }` | sempre 202 genérico |
| `GET /api/v1/client/{selector}/pass-recuvery/{validator}` | Verifica o link **sem consumir** (FR-012) | 200 `{ "status": "valid" }` |
| `PUT /api/v1/client/{selector}/pass-recuvery/{validator}` | Redefine a senha. Corpo `{ "newPassword", "newPasswordConfirmation" }` | 200 sem token (FR-013) |

As três rotas são anônimas e aceitam barra final, como as da feature 002. As duas rotas que levam
o link no caminho recebem `SensitiveRouteValuesMetadata`, para que o `instance` dos erros não
exponha o selector e o validator (FR-021, SC-007).

**Justificativa**: o `GET` no mesmo caminho do `PUT` é o par natural em REST ("consultar o
recurso" e "alterar o recurso") e não tem efeito colateral.

**Alternativas consideradas**:
- `GET .../pass-recuvery/{validator}/status`. Acrescenta um segmento sem necessidade.
- `POST` de verificação. Seria uma consulta disfarçada de comando.

## R5. Status HTTP e mensagens

| Situação | Rotas | Status | Mensagem |
|---|---|---|---|
| Pedido aceito, em **todos** os casos (ativa, pendente, removida, inexistente, limite) | POST | 202 | `Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes.` |
| E-mail em formato inválido | POST | 400 com o campo | `Informe um e-mail válido.` |
| Link válido | GET | 200 | `Link válido. Informe a nova senha.` |
| Senha redefinida | PUT | 200 | `Senha alterada com sucesso. Você já pode fazer login com a nova senha.` |
| Senha fora das regras, confirmação diferente ou campo ausente | PUT | 400 com o campo | mensagens de validação (R6) |
| Link expirado | GET, PUT | 410 | `O link de redefinição de senha expirou. Solicite uma nova recuperação de senha.` |
| Link inválido: inexistente, validator errado, já usado, revogado, conta removida ou não confirmada, formato errado | GET, PUT | 400 sem campo | `Link de redefinição de senha inválido. Use o link do e-mail de recuperação mais recente.` |

"Expirado" só é revelado a quem tem o par correto, então não indica se a conta existe (FR-016).
"Já usado" vira "inválido", e não "já concluído" como na confirmação de e-mail, porque informar
que a senha já foi trocada não ajuda o cliente e revelaria um estado da conta.

Formato dos segmentos e Constituição VII: o tamanho e o alfabeto do selector e do validator são
regra de domínio do link, e não validação de entrada. É a mesma decisão da feature 002 (research
R3 daquela feature). A validação de entrada, com campo e mensagem via `ValidationProblem`, vale para
os corpos do POST e do PUT.

## R6. Validação da nova senha e da confirmação

**Decisão**: o DTO `ResetPasswordRequest` usa só atributos nativos e os atributos que já existem no
projeto:

| Campo | Regras |
|---|---|
| `newPassword` | `[Required]`, `[MinLength(8)]`, `[MaxLength(128)]`, `[AlphanumericPassword]`, as mesmas do cadastro (feature 001, FR-004) |
| `newPasswordConfirmation` | `[Required]`, `[Compare(nameof(NewPassword))]` com a mensagem `A confirmação da senha deve ser igual à nova senha.` |

A validação nativa roda antes do serviço. Por isso, um erro de validação nunca toca o link, que
continua válido (FR-014).

**Alternativa considerada**: comparar as duas senhas dentro do serviço. Isso misturaria validação
de entrada com regra de negócio e não produziria o detalhe por campo.

## R7. Quem é elegível a receber o link

**Decisão**: novo método `IClientRepository.FindConfirmedByEmailAsync(normalizedEmail)`, que
encontra contas não removidas com `emailConfirmed = true`. O filtro de conta removida já é global.
Conta pendente, removida ou inexistente devolve `null`, e o serviço responde com o 202 genérico
sem enviar e-mail (clarificação 2, FR-003).

Na redefinição e na verificação, o serviço também confere que a conta continua confirmada. Hoje é
impossível uma conta confirmada voltar a ficar pendente, mas a checagem protege contra mudanças
futuras.

## R8. Limites de emissão (5 min e 10 links em 24 h)

**Decisão**: criar a classe `PasswordRecoveryLinkIssuePolicy`, com a mesma lógica de
`ConfirmationLinkIssuePolicy`, mas lendo da nova tabela e de configurações próprias (FR-005:
limites independentes):
- `email:passwordRecoveryIntervalMinutes`, padrão 5;
- `email:maximumPasswordRecoveryLinksPerDay`, padrão 10.

Todos os links de recuperação contam para o limite diário, porque não existe link "de cadastro"
nessa ação. A concorrência é tratada como na feature 002:
1. Abre transação e bloqueia a linha do cliente (`LockForUpdateAsync`).
2. Avalia a política.
3. Revoga o link ativo.
4. Insere o novo link.
5. Faz commit. Se ainda assim houver violação do índice "um ativo por cliente", ela é tratada como
   limite atingido.

**Alternativa considerada**: uma política genérica parametrizada por repositório. A abstração
extra não compensa para duas classes de cerca de 30 linhas, e pode ser feita depois se surgir uma
terceira ação.

## R9. Redefinição: atomicidade e concorrência

**Decisão**: `PasswordRecoveryService.ResetPasswordAsync` segue estes passos:
1. Localiza o link pelo selector, já com o cliente.
2. Compara o hash em tempo constante.
3. Classifica o link: usado ou revogado é inválido, prazo vencido é expirado.
4. Abre transação e executa `LockForUpdateAsync(clientId)`.
5. **Relê** o link e confirma que `usedAt` continua nulo. Isso impede que dois PUT simultâneos
   troquem a senha duas vezes.
6. Grava `PasswordHash` (com `IPasswordHashingService.HashPassword`), `passwordChangedAt` e
   `usedAt` no link.
7. Faz commit.
8. Põe na fila o aviso de "senha alterada".

O link usado deixa de ser ativo, então não existe outro link ativo para revogar. A regra
"invalidar os demais links" (FR-013) já é garantida pelo índice "um ativo por cliente".

O prazo é verificado com o relógio do instante do PUT, inclusive no limite de 24 h: o link vale se
`agora <= expiresAt`.

## R10. Tokens existentes após a troca

**Decisão**: nenhuma mudança em `ClientSessionValidator` nem na emissão de JWT (clarificação 1,
FR-019). O código de acesso não muda na redefinição, então os tokens continuam válidos até
expirarem.

## R11. Fila e envio de e-mails: generalizar a infraestrutura da feature 002

**Problema**: a fila, o compositor, o remetente e o dispatcher atuais são tipados para o e-mail de
confirmação (`ConfirmationEmailMessage` e `IConfirmationEmailComposer`).

**Decisão**: generalizar sem mudar o comportamento da feature 002:

| Atual (feature 002) | Novo |
|---|---|
| `ConfirmationEmailMessage` | `OutgoingEmailMessage(EmailKind Kind, int ClientId, string RecipientEmail, string ClientName, string? ActionUrl, DateTimeOffset? OccurredAt = null, int AttemptNumber = 1)` |
| — | `enum EmailKind { EmailConfirmation, PasswordRecovery, PasswordChanged }` |
| `IConfirmationEmailQueue` / `ChannelConfirmationEmailQueue` | `IEmailQueue` / `ChannelEmailQueue` |
| `IConfirmationEmailComposer` / `HtmlConfirmationEmailComposer` | `IEmailComposer` / `HtmlEmailComposer`, que escolhe o modelo e o assunto pelo `EmailKind` a partir de um catálogo `EmailTemplateCatalog` |
| `ConfirmationEmail` | `ComposedEmail` |
| `IConfirmationEmailSender` / `ResendConfirmationEmailSender` | `IEmailSender` / `ResendEmailSender` |
| `ConfirmationEmailDispatcher` | `EmailDispatcher`, com a mesma lógica de 3 tentativas e logs com o tipo do e-mail |

Os eventos de log 1500–1502 e 1509 passam a ter a forma "e-mail de {EmailKind}". Os `EventId`
continuam os mesmos.

**Justificativa**: o mesmo mecanismo de fila e de tentativas (FR-006) serve para os três e-mails,
o Resend continua isolado num único remetente e não entra nenhuma dependência nova. Criar uma
segunda fila e um segundo dispatcher duplicaria código e dobraria o número de consumidores.

**Alternativas consideradas**:
- Montar o HTML antes de pôr na fila. Isso tira do dispatcher a responsabilidade de compor, mas
  guarda o HTML inteiro na memória da fila e mistura a camada de serviço com templates.
- Criar uma fila separada para a recuperação. Gera duplicação, então foi descartada.

## R12. Templates HTML

**Entrada**: "Crie um novo arquivo para o HTML". O bloco veio vazio.

**Decisão**: dois recursos embutidos novos, com o mesmo estilo visual de `ConfirmationEmail.html`:
- `Templates/PasswordRecoveryEmail.html`
  - Assunto: `Redefina sua senha no {appName}`.
  - Conteúdo: saudação com `{{CLIENT_NAME}}`, botão e link em texto com `{{ACTION_URL}}`, validade
    de **24 horas**, orientação de pedir nova recuperação se o link expirar, e o aviso "Se você não
    pediu a redefinição, ignore este e-mail; sua senha atual continua valendo." (FR-004).
- `Templates/PasswordChangedEmail.html`
  - Assunto: `Sua senha foi alterada no {appName}`.
  - Conteúdo: aviso de troca com a data em horário de Brasília e a orientação "Se não foi você,
    solicite imediatamente a recuperação de senha e entre em contato com o suporte." O aviso **não**
    tem link de redefinição nem a senha (FR-018).

Os placeholders `{{CLIENT_NAME}}`, `{{ACTION_URL}}`, `{{APP_NAME}}`, `{{YEAR}}` e `{{CHANGED_AT}}`
passam por `HtmlEncoder`. O template de confirmação troca `{{CONFIRMATION_URL}}` por
`{{ACTION_URL}}`, para que todos os modelos usem o mesmo placeholder.

⚠️ Os textos precisam ser validados pelo dono do produto, como foi feito na feature 002.

## R13. Tratamento de erros

**Entrada**: "evite exceções não tratadas, use try/catch e devolva mensagens amigáveis".

**Decisão**: manter o padrão do projeto, explicado a seguir.
- Exceções de domínio novas, derivadas de `DomainException`:
  - `InvalidPasswordRecoveryLinkException` (400);
  - `ExpiredPasswordRecoveryLinkException` (410).
- Essas exceções são traduzidas pelo `DomainExceptionHandler` global em `ProblemDetails` dentro do
  `ApiResponse`. Por isso nenhuma exceção chega ao cliente sem tratamento.
- Exceções inesperadas continuam no handler genérico (500 com mensagem em português).
- `try/catch` explícito só onde o erro muda o fluxo:
  - violação de unicidade na emissão (R8);
  - o dispatcher de e-mail (R11), que nunca propaga exceções.

**Justificativa**: espalhar `try/catch` nos endpoints violaria a Constituição I e II e duplicaria o
handler global, que já cumpre o objetivo da entrada.

## R14. Observabilidade

**Decisão**: eventos novos em `LogMessages`, na faixa 1600, usando
`Microsoft.Extensions.Logging` com saída no console:

| EventId | Nível | Evento |
|---|---|---|
| 1600 | Information | Link de recuperação emitido (cliente) |
| 1601 | Warning | Pedido de recuperação recusado por limite (cliente) |
| 1602 | Information | Pedido de recuperação para e-mail não elegível (sem dados do e-mail) |
| 1603 | Information | Senha redefinida (cliente) |
| 1604 | Warning | Link de recuperação expirado usado (cliente) |
| 1605 | Warning | Link de recuperação inválido usado (sem dados do link) |

O envio, as falhas, o esgotamento das tentativas e a fila cheia reaproveitam os eventos 1500–1502
e 1509, agora com o `EmailKind`.

Nunca são registrados selector, validator, URL, corpo do e-mail nem senhas (FR-021, SC-007). E-mails
só aparecem mascarados pelo `SensitiveDataMasker`.

## R15. Provedor de e-mail

**Decisão**: o Resend continua como provedor (entrada). Usa o mesmo SDK oficial já referenciado e a
mesma `RESEND_API_KEY`. **Nenhuma dependência nova.**

## R16. Impacto nos testes existentes

- Os fakes `FakeConfirmationEmailSender` (testes de integração) e os testes de dispatcher,
  compositor e remetente passam a usar os tipos generalizados de R11. O fake ganha um filtro por
  `EmailKind` e um extrator de caminho genérico (`ExtractActionPath`).
- `ConfirmationLinkUniquenessTests` e os testes do gerador passam a usar `ISecureLinkGenerator`
  com a ação.
- `PersistenceSchemaTests` passa a verificar os índices novos.
- `SensitiveDataAuditTests` passa a cobrir as rotas e os e-mails de recuperação (SC-007).
- Não entra nenhuma dependência nova nos projetos de teste.
