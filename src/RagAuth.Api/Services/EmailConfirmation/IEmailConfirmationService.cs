namespace RagAuth.Api.Services.EmailConfirmation;

public interface IEmailConfirmationService
{
    Task<EmailConfirmationOutcome> ConfirmAsync(string selector, string validator, CancellationToken cancellationToken = default);

    /// <summary>Emite novo link quando cabível; nunca revela, por exceção ou retorno, a situação da conta.</summary>
    Task RequestNewLinkAsync(string email, CancellationToken cancellationToken = default);
}
