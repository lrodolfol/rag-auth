# Envio de confirmação para email cadastrado

## Descrição
Quando novo usuário realizar o cadastro na aplicação pelo endpoint `/api/v1/auth/register`, a aplicação deve cadastrar o usuário normalmente como é feito hoje.
Após esse cadastro realizado, a aplicação de enviar um email de confirmação para o endereço de email informado no cadastro.
O email deve conter um link de confirmação que, quando clicado, valida o endereço de email do usuário e ativa sua conta.

O link enviado no email de confirmação **deve ser único para cada usuário** e ter um tempo de expiração definido de 24 horas.
Caso o usuário não clique no link dentro do prazo, ele deverá solicitar um novo link de confirmação.

## Comportamentos esperados
- Após o cadastro do cliente, o sitema envia um email de confirmação para o endereço de email informado no cadastro.
- O email de confirmação deve conter um link único para cada usuário
- O link de confirmação deve expirar em 24 horas.
- O link de confirmação deve ser uma URL com hash único que identifica o usuário, hash de chave de acesso e a ação de confirmação.
- Caso o usuário não clique no link dentro do prazo, ele deverá solicitar um novo link de confirmação.
- Caso o usuário clique no link de confirmação dentro do prazo, a conta do usuário deve ser ativada e ele poderá acessar a aplicação normalmente.
- Caso o usuário faça login antes de confirmar o email, a aplicação deve retornar uma mensagem informando que o email ainda não foi confirmado e que ele precisa confirmar o email para acessar a aplicação.
