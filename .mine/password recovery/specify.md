# Recuperação de senha

## Descrição
Deve ser possivel que o usuario recupere sua senha caso ele tenha esquecido ou perdido a mesma.
Para isso, o sistema deve enviar um email para o endereço de email informado no cadastro do usuário com um link para redefinir a senha.
Nessa tela, o usuário deve informar uma nova senha e confirmar a mesma.

## Comportamentos esperados
- Cliente solicita a recuperação de senha informando o endereço de email cadastrado.
- O sistema envia um email para o endereço de email informado no cadastro do usuário com um link para redefinir a senha.
- O link enviado no email de recuperação de senha **deve ser único para cada usuário** e ter um tempo de expiração definido de 24 horas.
- Caso o usuário não clique no link dentro do prazo, ele deverá solicitar um novo link de recuperação de senha.
- Caso o usuário clique no link de recuperação de senha dentro do prazo e informe uma nova senha, a senha do usuário deve ser atualizada e ele poderá acessar a aplicação normalmente.
