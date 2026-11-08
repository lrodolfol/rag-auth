Sim, sugira as edições concretas para os 5 primeiros (C1, F1, U1, I1, I2).

- C1. Siga com a recomendação. Pode ser intencional: não diferenciar link mal formado de link inexistente. Nesse caso, registrar no Constitution Check do plan.md (e em
research R3) que o formato dos segmentos é regra de domínio do link (InvalidConfirmationLinkException), não validação de entrada. Sem esse registro, o
gate fica violado.
- F1. Siga com a recomendação. Passar a criação de IConfirmationEmailSender.cs e ConfirmationEmail.cs para a Fase 2. A T035 fica só com o ResendConfirmationEmailSender.
- U1. Siga com a recomendação. Issue deve devolver um IssuedConfirmationLink { Entity, Validator, Message }. RehashForAccessCode(IssuedConfirmationLink, string
newAccessCode) recalcula ValidatorHash e a URL não muda. Detalhar isso em T017 e T039.
