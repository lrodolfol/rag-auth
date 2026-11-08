using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Infrastructure.Email;

public sealed class HtmlConfirmationEmailComposer(IOptions<EmailOptions> emailOptions, TimeProvider timeProvider) : IConfirmationEmailComposer
{
    public const string TemplateResourceName = "RagAuth.Api.Infrastructure.Email.Templates.ConfirmationEmail.html";
    public const string SubjectTemplate = "Confirme seu cadastro no {0}";

    private static readonly Lazy<string> Template = new(LoadTemplate);

    public ConfirmationEmail Compose(ConfirmationEmailMessage message)
    {
        var appName = emailOptions.Value.AppName;
        var htmlBody = Template.Value
            .Replace("{{CLIENT_NAME}}", HtmlEncoder.Default.Encode(message.ClientName), StringComparison.Ordinal)
            .Replace("{{CONFIRMATION_URL}}", HtmlEncoder.Default.Encode(message.ConfirmationUrl), StringComparison.Ordinal)
            .Replace("{{APP_NAME}}", HtmlEncoder.Default.Encode(appName), StringComparison.Ordinal)
            .Replace("{{YEAR}}", timeProvider.GetUtcNow().Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var subject = string.Format(CultureInfo.InvariantCulture, SubjectTemplate, appName);
        return new ConfirmationEmail(message.RecipientEmail, subject, htmlBody);
    }

    private static string LoadTemplate()
    {
        using var stream = typeof(HtmlConfirmationEmailComposer).Assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException("O modelo do e-mail de confirmação não foi encontrado nos recursos da aplicação.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
