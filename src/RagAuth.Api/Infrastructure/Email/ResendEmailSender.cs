using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using Resend;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Único ponto que conhece o SDK do Resend (research R7 da feature 002).</summary>
public sealed class ResendEmailSender(IResend resend, IOptions<EmailOptions> emailOptions) : IEmailSender
{
    public async Task SendAsync(ComposedEmail email, CancellationToken cancellationToken)
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
            throw new EmailDeliveryException(response.Exception);
        }
    }
}

public sealed class EmailDeliveryException(Exception? innerException)
    : Exception("O provedor de e-mail recusou o envio do e-mail.", innerException);
