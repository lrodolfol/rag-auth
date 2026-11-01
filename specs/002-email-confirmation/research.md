# Research: Confirmação de e-mail no cadastro

**Feature**: `002-email-confirmation` | **Data**: 2026-10-31 | **Plano**: [plan.md](./plan.md)

Resolve as incógnitas do Technical Context e os conflitos entre a entrada de planejamento
(`.mine/email-confirmation/plan.md`), a especificação (`spec.md`) e a constituição. Formato:
**Decisão / Justificativa / Alternativas consideradas**.

---

## R1. Formato do link e dos dois valores (`client-identification-hash` / `key-hash`)

**Entrada**: `.mine/email-confirmation/plan.md` define o link
`https://ragauth.com.br/{client-identification-hash}/confirmation-register/{key-hash}` e pede um
"hash que identifica o usuário, hash de chave de acesso e a ação de confirmação". A spec exige valor
opaco, impossível de adivinhar, sem expor e-mail, `id` ou código de acesso (FR-004, FR-005).

**Decisão**: padrão *selector/validator* (token dividido), um par novo por link:

| Segmento | Conteúdo | Guardado no banco como |
|---|---|---|
| `{client-identification-hash}` (selector) | 16 bytes aleatórios (`RandomNumberGenerator`), Base64Url → 22 caracteres | texto puro, índice único — localiza o link e, por ele, o cliente |
| `{key-hash}` (validator) | 32 bytes aleatórios, Base64Url → 43 caracteres | só o hash `SHA-256(validator + ":" + accessCode + ":" + "email-confirmation")`, em hexa (64) |

A verificação recalcula o hash com o código de acesso do cliente dono do link e a ação, e compara em
tempo constante (`CryptographicOperations.FixedTimeEquals`). Assim o link fica **vinculado ao
cliente, ao código de acesso e à ação** (FR-004), e um vazamento do banco não permite montar links
válidos (o validator nunca é persistido).

**Justificativa**: o selector permite busca por índice sem comparar hash de todas as linhas; o
validator com 256 bits é inadivinhável; nada no link é derivável dos dados do cliente. Tudo nativo
(`System.Security.Cryptography`), sem segredo novo de configuração.

**Alternativas consideradas**:
- `client-identification-hash` fixo por cliente (HMAC do código de acesso) — exigiria uma chave
  secreta nova e deixaria o mesmo identificador em todos os links do cliente; descartado.
- Token único (um só valor) — funciona, mas não segue o formato de dois segmentos pedido.
- JWT assinado no link — não é revogável sem tabela (FR-007, FR-013) e fica longo; descartado.

## R2. Rota de confirmação: GET chamado pelo front-end

**Entrada**: `GET /api/v1/client/{client-identification-hash}/confirmation-register/{key-hash}/`,
rota aberta. Spec (Clarificação 1, FR-007a): o link abre o front-end, que chama a API.

**Decisão**: manter o `GET` pedido. O link do e-mail aponta para o front-end
(`email:confirmationPageBaseUrl`), nunca para a API, o que atende FR-007a ("a API não confirma
apenas pelo acesso ao endereço do link"). A operação é idempotente do ponto de vista do cliente
(repetir um link já usado devolve "já confirmado", FR-011).

**Justificativa**: segue a decisão do dono do produto; a idempotência reduz o risco usual de GET com
efeito colateral.

**Risco anotado**: scanners de e-mail que executam JavaScript da página do front-end podem disparar a
confirmação. Mitigação fica com o front-end (chamar a API só após ação do usuário ou ao carregar,
conforme o produto decidir). Não bloqueia esta API.

**Alternativas consideradas**: `POST` com corpo — mais "correto" em HTTP, mas contraria a entrada.

## R3. Status HTTP da confirmação

**Decisão**:

| Situação (só avaliada se o validator confere) | Status | Mensagem |
|---|---|---|
| Link válido, dentro do prazo | 200 | `E-mail confirmado com sucesso. Sua conta está ativa e você já pode fazer login.` |
| Link já usado / conta já confirmada | 200 | `Este e-mail já foi confirmado. Você já pode fazer login.` |
| Link expirado (conta ainda pendente) | 410 | `O link de confirmação expirou. Solicite um novo link de confirmação.` |
| Selector inexistente, validator errado, link revogado, conta removida, valores mal formados | 400 | `Link de confirmação inválido. Use o link do e-mail de confirmação mais recente.` |

"Expirado" e "já usado" só são revelados a quem tem o par selector+validator correto, então não
vazam a existência da conta (FR-010).

**Formato dos segmentos × Constituição VII**: o tamanho e o alfabeto Base64Url de
`clientIdentificationHash` (22) e `keyHash` (43) são **regra de domínio do link**, verificada pelo
`EmailConfirmationService`, e não validação de entrada da requisição. Um valor mal formado é
indistinguível de um link inexistente e gera `InvalidConfirmationLinkException` (400, `ProblemDetails`
com a mensagem genérica, sem `errors` por campo). Assim a Constituição VII continua atendida: não há
erro de *validação de entrada* nessa rota; a validação de entrada (com campo e mensagem via
`ValidationProblem`) se aplica ao corpo de `POST /api/v1/client/new-confirmation-register`.

**Alternativas**: 404 para inválido — confunde com rota inexistente; 422 — não usado no projeto.

## R4. Rota de novo link e resposta genérica

**Decisão**: `POST /api/v1/client/new-confirmation-register` (aberta), corpo
`{ "email": "..." }` validado pela validação nativa (`[Required]`, `[EmailAddress]`,
`[MaxLength(254)]` → 400 com detalhe do campo). A resposta é **sempre** `202 Accepted` com
`Se o e-mail estiver cadastrado e aguardando confirmação, enviaremos um novo link em instantes.`
(FR-014), inclusive quando os limites são atingidos (FR-015).

**Justificativa**: 202 descreve bem "pedido aceito, envio em segundo plano"; a mensagem única não
distingue pendente / confirmado / removido / inexistente.

**Risco residual aceito**: diferença de tempo de resposta (só a conta pendente grava no banco). O
envio é em segundo plano, então a diferença é pequena; mitigação adicional fora do escopo.

## R5. Limites de novo link (5 min / 10 por 24 h) e concorrência

**Decisão**: antes de emitir, o serviço consulta os links da conta:
- se existe link emitido (de qualquer origem) há menos de 5 min → não emite;
- se existem ≥ 10 links de origem `new-link-request` emitidos nas últimas 24 h (janela móvel) → não emite.

Valores em configuração (`email:newLinkIntervalMinutes` = 5, `email:maximumNewLinksPerDay` = 10).
Concorrência: índice único parcial "no máximo um link ativo (não usado e não revogado) por cliente".
A emissão revoga o link ativo e insere o novo na mesma transação; dois pedidos simultâneos geram
violação de unicidade no segundo, que é tratado como "limite atingido" (resposta genérica, log).

**Alternativas**: `Microsoft.AspNetCore.RateLimiting` — limita por partição de requisição em memória,
não por conta com janela de 24 h persistida, e seria perdido no reinício; descartado.

## R6. Envio em segundo plano com 3 tentativas

**Decisão**: fila em memória com `System.Threading.Channels` (`IConfirmationEmailQueue`) consumida
por um `BackgroundService` (`ConfirmationEmailDispatcher`). Cada mensagem tenta até 3 vezes:
imediata, depois de 30 s, depois de 2 min (≈ 2,5 min no total, dentro dos ~5 min da Clarificação 4).
A espera usa `TimeProvider` (testável) e não bloqueia as demais mensagens: a nova tentativa é
reagendada e reenfileirada. Esgotadas as tentativas → log de erro (FR-003, FR-021). Toda exceção do
envio é capturada no dispatcher; nenhuma derruba o host.

O cadastro e o novo link só fazem `TryWrite` na fila depois do `SaveChanges` (FR-003: não atrasa nem
desfaz). A mensagem leva o validator em memória — ele nunca é gravado.

**Justificativa**: nativo do .NET, atende "não atrasar o cadastro" e as tentativas. A perda de
mensagens no reinício foi aceita na spec.

**Alternativas**: `Microsoft.Extensions.Http.Resilience` (Polly) — dependência nova só para três
tentativas fixas; tabela *outbox* durável — explicitamente fora do escopo (Clarificação 4, opção C
não escolhida); `Task.Run` solto — sem controle de ciclo de vida nem de falhas.

## R7. Provedor de e-mail: Resend

**Entrada**: Resend, com chave em `RESEND_API_KEY` e estrutura segregada com DI.

**Decisão**: pacote NuGet `Resend` (SDK oficial), registrado por DI conforme a documentação do SDK:
`AddHttpClient<ResendClient>()` + `Configure<ResendClientOptions>(ApiToken ← RESEND_API_KEY)` +
`AddTransient<IResend, ResendClient>()`. O código de exemplo `ResendClient.Create(...)` **não** é
usado (viola a Constituição V). O SDK fica isolado em `Infrastructure/Email/ResendConfirmationEmailSender`,
que implementa `IConfirmationEmailSender`; serviços não conhecem o Resend.

**Justificativa da dependência nova** (Restrições Tecnológicas): provedor escolhido pelo dono do
produto; o SDK oficial traz modelos tipados e tratamento de erro do provedor, mantidos pelo
fornecedor, e usa `IHttpClientFactory`.

**Alternativas nativas descartadas**:
- `HttpClient` direto na API REST do Resend — viável, mas reimplementa contrato, serialização e
  erros que o SDK oficial já mantém; o ganho de evitar a dependência não compensa.
- `System.Net.Mail.SmtpClient` (SMTP do Resend) — a própria Microsoft não recomenda o `SmtpClient`
  para novos desenvolvimentos e ele não oferece async real nem retorno estruturado de erro.

A versão do pacote é fixada no `.csproj` na implementação (última estável).

## R8. Template HTML do e-mail

**Decisão**: o HTML de `.mine/email-confirmation/plan.md` vira recurso embutido
(`Infrastructure/Email/Templates/ConfirmationEmail.html`). `ConfirmationEmailComposer` substitui
`{{CLIENT_NAME}}` (com `HtmlEncoder.Default`, pois o nome é entrada do usuário), `{{CONFIRMATION_URL}}`
e `{{APP_NAME}}`. Assunto: `Confirme seu cadastro no {appName}`.

**Ajuste proposto ao template**: o rodapé `© 2026 Sua Empresa` passa a `© {{YEAR}} {{APP_NAME}}`, e
um parágrafo com a orientação de pedir novo link quando expirar é acrescentado (exigido por FR-002 e
ausente do HTML original).

## R9. Modelo de dados: `emailConfirmed`

**Entrada**: coluna `emailConfirmed` (`bool`) em `clients`. Spec: registrar a data da confirmação.

**Decisão**: duas colunas em `clients` — `emailConfirmed` (`boolean`, não nulo, padrão `false`) e
`emailConfirmedAt` (`timestamptz`, nulo). A situação "pendente de confirmação" é derivada:
`deletedAt IS NULL AND emailConfirmed = false`. Links ficam na nova tabela
`email_confirmation_links` ([data-model.md](./data-model.md)).

**Migração (FR-020)**: a migration adiciona as colunas e executa
`UPDATE clients SET "emailConfirmed" = true, "emailConfirmedAt" = "createdAt" WHERE "deletedAt" IS NULL`.
Nenhum e-mail é disparado.

**Alternativa**: enum de situação substituindo `deletedAt` — refatoração ampla da feature 001, sem
ganho; descartada.

## R10. Login de conta pendente

**Decisão**: `AuthService.LoginAsync` valida credenciais primeiro (mensagem 401 genérica intacta,
FR-017) e só então verifica `EmailConfirmed`; se falso, lança `EmailNotConfirmedException`
(`DomainException`, 403, título `Acesso negado`, mensagem
`O e-mail ainda não foi confirmado. Confirme seu e-mail pelo link enviado ou solicite um novo link para acessar a aplicação.`)
e registra log (FR-021). Nenhum token é emitido.

## R11. Tratamento de erros (entrada: try/catch + exceções customizadas)

**Decisão**: manter o padrão existente — exceções customizadas derivadas de `DomainException`
traduzidas pelo `DomainExceptionHandler` em `ProblemDetails` no envelope `ApiResponse`. Novas:
`EmailNotConfirmedException` (403), `InvalidConfirmationLinkException` (400),
`ExpiredConfirmationLinkException` (410). `try/catch` explícito apenas onde o erro precisa virar
outro fluxo: violação de unicidade na emissão de link (R5) e o dispatcher de e-mail (R6), que nunca
propaga exceções.

## R12. Observabilidade

**Decisão**: novos eventos em `LogMessages` (`Microsoft.Extensions.Logging`, console), faixa 1500:

| EventId | Nível | Evento |
|---|---|---|
| 1500 | Information | E-mail de confirmação enviado (cliente, tentativa) |
| 1501 | Warning | Falha numa tentativa de envio (cliente, tentativa) |
| 1502 | Error | Envio esgotou as tentativas (cliente) |
| 1503 | Information | E-mail confirmado (cliente) |
| 1504 | Warning | Link de confirmação expirado usado (cliente) |
| 1505 | Warning | Link de confirmação inválido usado (sem dados do link) |
| 1506 | Warning | Login recusado por e-mail não confirmado (cliente) |
| 1507 | Information | Novo link emitido (cliente) |
| 1508 | Warning | Pedido de novo link recusado por limite (cliente) |
| 1509 | Error | Fila de e-mail cheia / mensagem descartada (cliente) |

Nunca se registra selector, validator, URL do link, corpo do e-mail nem a `RESEND_API_KEY`
(FR-021, SC-008). E-mails aparecem só mascarados (`SensitiveDataMasker`).

## R13. Impacto nos testes existentes

- `TestClientAccounts.RegisterAndLoginAsync` (integração) cadastra e faz login: passará a confirmar
  a conta antes do login, usando o link capturado pelo `FakeConfirmationEmailSender`.
- Testes de rotas abertas passam a incluir as duas rotas novas (FR-019).
- `RagAuthApiFactory` substitui `IConfirmationEmailSender` por um fake e `TimeProvider` por um
  `AdjustableTimeProvider` (subclasse manual de `TimeProvider` nos projetos de teste, que permite
  avançar o relógio) para testar expiração e limites. **Sem dependência nova**: o pacote
  `Microsoft.Extensions.TimeProvider.Testing` foi avaliado e descartado porque a subclasse manual
  de poucas linhas basta.
- Os atrasos entre tentativas (R6) ficam em configuração (`email:retryDelaysSeconds`), zerados nos
  testes de integração.
