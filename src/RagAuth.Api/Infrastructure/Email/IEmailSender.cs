namespace RagAuth.Api.Infrastructure.Email;

public interface IEmailSender
{
    /// <summary>Envia o e-mail; qualquer exceção indica falha da tentativa.</summary>
    Task SendAsync(ComposedEmail email, CancellationToken cancellationToken);
}
