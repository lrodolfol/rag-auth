using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using Resend;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Único ponto que conhece o SDK do Resend (research R7).</summary>
public sealed class ResendConfirmationEmailSender(IResend resend, IOptions<EmailOptions> emailOptions) : IConfirmationEmailSender
{
    public async Task SendAsync(ConfirmationEmail email, CancellationToken cancellationToken)
    {
        var message = new EmailMessage
        {
            From = emailOptions.Value.FromAddress,
            To = email.RecipientEmail,
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
        };
        var response = await resend.EmailSendAsync(message, cancellationToken);
        if (!response.Success)
        {
            throw new ConfirmationEmailDeliveryException(response.Exception);
        }
    }
}

public sealed class ConfirmationEmailDeliveryException(Exception? innerException)
    : Exception("O provedor de e-mail recusou o envio do e-mail de confirmação.", innerException);
