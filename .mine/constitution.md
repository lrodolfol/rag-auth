# Projeto de API com .NET 10 com CSharp e Minimal API

Principios para fixar e usar em todo projeto:
- Endpoint nunca acessa banco de dados diretamente, sempre usar um serviço para isso.
- Mantenha o código segregado, cada classe deve ter uma única responsabilidade.
- Regras de negócio não deve permanecer dentro de endpoint nem handler HTTP.
- Nomes de classe, métodos e variáveis devem ser auto explicativos, não economize palavras.
- Mensagens de erro devem ser escritas em português.
- Métodos devem ter no máximo 20 linhas de código, caso contrário, deve ser quebrado em métodos menores.
- Prefira usar DTOs para entrada e saída de dados, não use entidades diretamente.
- Trabalhe com injeção de dependência, evite usar `new` para criar instâncias de classes.
- Trabalhe com interfaces e classes abstratas para facilitar a testabilidade e a manutenção do código.
- Toda regra de negócio deve ser testada com testes unitários com xunit.
- 90% do código deve ser coberto por testes unitários com xunit.
- Todo endpoint deve ter um teste de integração.
- Erro de validação deve retornar 400 com detalhes do erro.
- **NENHUMA NOVA DEPENDENCIA DEVE SER ADICIONADA SEM /JUSTIFICATIVA.**

Se o plano contradisser algum princípio, quem muda é o plano.