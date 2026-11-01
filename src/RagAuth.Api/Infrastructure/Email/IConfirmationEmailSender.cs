namespace RagAuth.Api.Infrastructure.Email;

public interface IConfirmationEmailSender
{
    /// <summary>Envia o e-mail; qualquer exceção indica falha da tentativa.</summary>
    Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken);
}
