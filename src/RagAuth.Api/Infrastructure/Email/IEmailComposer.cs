namespace RagAuth.Api.Infrastructure.Email;

public interface IEmailComposer
{
    ComposedEmail Compose(OutgoingEmailMessage message);
}
