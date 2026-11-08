using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Infrastructure.Email;

public interface IConfirmationEmailComposer
{
    ConfirmationEmail Compose(ConfirmationEmailMessage message);
}
