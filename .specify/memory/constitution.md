<!--
Sync Impact Report
- Version change: (template sem preencher) → 1.0.0
- Modified principles: n/a (primeira ratificação)
- Added principles:
  - I. Arquitetura em Camadas
  - II. Responsabilidade Única e Métodos Curtos
  - III. Nomenclatura Autoexplicativa
  - IV. Contratos via DTO
  - V. Injeção de Dependência e Abstrações
  - VI. Testes (NÃO NEGOCIÁVEL)
  - VII. Erros e Validação
- Added sections: Restrições Tecnológicas; Fluxo de Desenvolvimento e Quality Gates; Governance
- Removed sections: nenhuma
- Follow-up TODOs: nenhum
-->

# rag-auth Constitution

## Core Principles

### I. Arquitetura em Camadas

- Endpoints MUST NOT acessar o banco de dados diretamente; todo acesso a dados MUST passar por um
  serviço.
- Regras de negócio MUST NOT residir em endpoints nem em handlers HTTP; endpoints apenas recebem a
  requisição, delegam ao serviço e traduzem o resultado em resposta HTTP.

**Justificativa**: isolar transporte, negócio e persistência permite testar regras sem HTTP nem
banco e trocar qualquer camada sem efeito cascata.

### II. Responsabilidade Única e Métodos Curtos

- Cada classe MUST ter uma única responsabilidade.
- Métodos MUST ter no máximo 20 linhas de código; acima disso MUST ser quebrados em métodos
  menores com nomes que descrevam cada etapa.

**Justificativa**: classes focadas e métodos curtos são mais fáceis de ler, revisar e testar.

### III. Nomenclatura Autoexplicativa

- Nomes de classes, métodos e variáveis MUST ser autoexplicativos e descrever a intenção.
- Abreviações e nomes genéricos (ex.: `x`, `tmp`, `data`, `mgr`) MUST NOT ser usados; não se
  economiza palavras em nomes.

**Justificativa**: o código é lido muito mais vezes do que escrito; nomes claros dispensam
comentários explicativos.

### IV. Contratos via DTO

- Entrada e saída de dados da API MUST usar DTOs.
- Entidades de domínio ou de persistência MUST NOT ser expostas diretamente em requisições ou
  respostas.

**Justificativa**: DTOs desacoplam o contrato público do modelo interno, evitando vazamento de
dados e quebras de contrato acidentais.

### V. Injeção de Dependência e Abstrações

- Serviços, repositórios e demais dependências MUST ser obtidos via injeção de dependência;
  `new` MUST NOT ser usado para instanciá-los. DTOs, entidades e value objects são exceção.
- Dependências entre componentes MUST ser expressas por interfaces ou classes abstratas.

**Justificativa**: abstrações injetadas permitem substituir implementações por dublês em testes e
reduzem o acoplamento.

### VI. Testes (NÃO NEGOCIÁVEL)

- Toda regra de negócio MUST ter testes unitários com xUnit.
- A cobertura de linhas por testes unitários com xUnit MUST ser de no mínimo 90%.
- Todo endpoint MUST ter ao menos um teste de integração.

**Justificativa**: testes são a garantia de que as regras de negócio e os contratos HTTP continuam
corretos a cada mudança.

### VII. Erros e Validação

- Mensagens de erro retornadas ou registradas MUST ser escritas em português.
- Erros de validação MUST retornar HTTP 400 com os detalhes de cada erro (campo e mensagem),
  usando os mecanismos nativos `ProblemDetails` / `ValidationProblem`.

**Justificativa**: respostas de erro consistentes e no idioma dos usuários facilitam o consumo da
API e o diagnóstico de problemas.

## Restrições Tecnológicas

- Plataforma: .NET 10, linguagem C#, estilo Minimal API.
- Testes: xUnit para testes unitários e de integração.
- **NENHUMA NOVA DEPENDÊNCIA DEVE SER ADICIONADA SEM JUSTIFICATIVA (MUST NOT).** Toda nova dependência
  (pacote NuGet, serviço externo ou ferramenta) MUST ter, registrados no plano da feature, o
  motivo da adoção e a alternativa nativa do .NET avaliada e descartada.

## Fluxo de Desenvolvimento e Quality Gates

- O Constitution Check de cada plano MUST verificar todos os princípios desta constituição antes
  da fase de design e novamente após ela.
- Um PR MUST NOT ser aprovado a menos que:
  - todos os testes passam;
  - a cobertura de linhas é de no mínimo 90%;
  - cada endpoint novo ou alterado possui teste de integração;
  - cada dependência nova possui justificativa registrada;
  - nenhum método excede 20 linhas e nenhum endpoint acessa dados ou contém regra de negócio.

## Governance

- Esta constituição prevalece sobre qualquer outra prática, guia ou preferência do projeto.
- **Se um plano contradisser algum princípio, quem muda é o plano**, nunca o princípio. Exceções
  só são possíveis por emenda formal desta constituição.
- Emendas MUST ser feitas via `/speckit-constitution`, com o Sync Impact Report revisado e a
  versão incrementada segundo SemVer:
  - MAJOR: remoção ou redefinição incompatível de princípios ou regras de governança;
  - MINOR: novo princípio ou seção, ou expansão material de orientação existente;
  - PATCH: esclarecimentos, ajustes de redação e correções sem efeito semântico.
- A conformidade MUST ser revisada em cada plano (Constitution Check) e em cada revisão de PR.

**Version**: 1.0.0 | **Ratified**: 2026-10-24 | **Last Amended**: 2026-10-24
