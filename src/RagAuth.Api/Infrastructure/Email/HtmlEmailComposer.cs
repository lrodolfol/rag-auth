using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;

namespace RagAuth.Api.Infrastructure.Email;

/// <summary>Escolhe o modelo pelo <see cref="EmailKind"/> e preenche os placeholders, sempre codificados em HTML.</summary>
public sealed class HtmlEmailComposer(IOptions<EmailOptions> emailOptions, TimeProvider timeProvider) : IEmailComposer
{
    public const string BrasiliaTimeZoneId = "America/Sao_Paulo";
    public const string ChangedAtFormat = "dd/MM/yyyy 'às' HH:mm";

    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Lazy<TimeZoneInfo> BrasiliaTimeZone = new(() => ResolveBrasiliaTimeZone(BrasiliaTimeZoneId));

    public ComposedEmail Compose(OutgoingEmailMessage message)
    {
        var appName = emailOptions.Value.AppName;
        var template = EmailTemplateCatalog.For(message.Kind);
        var htmlBody = FillPlaceholders(EmailTemplateCatalog.LoadBody(message.Kind), message, appName);
        var subject = string.Format(CultureInfo.InvariantCulture, template.SubjectTemplate, appName);
        return new ComposedEmail(message.RecipientEmail, subject, htmlBody);
    }

    internal static string FormatChangedAt(DateTimeOffset? occurredAt, TimeZoneInfo timeZone) =>
        occurredAt is null
            ? string.Empty
            : TimeZoneInfo.ConvertTime(occurredAt.Value, timeZone).ToString(ChangedAtFormat, BrazilianCulture);

    /// <summary>Usa o fuso do sistema; sem tzdata (contêiner mínimo), cai para um fuso fixo de UTC-3.</summary>
    internal static TimeZoneInfo ResolveBrasiliaTimeZone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.CreateCustomTimeZone(timeZoneId, TimeSpan.FromHours(-3), "Horário de Brasília", "Horário de Brasília");

    private string FillPlaceholders(string templateBody, OutgoingEmailMessage message, string appName) =>
        templateBody
            .Replace("{{CLIENT_NAME}}", Encode(message.ClientName), StringComparison.Ordinal)
            .Replace("{{ACTION_URL}}", Encode(message.ActionUrl ?? string.Empty), StringComparison.Ordinal)
            .Replace("{{APP_NAME}}", Encode(appName), StringComparison.Ordinal)
            .Replace("{{YEAR}}", timeProvider.GetUtcNow().Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{CHANGED_AT}}", Encode(FormatChangedAt(message.OccurredAt, BrasiliaTimeZone.Value)), StringComparison.Ordinal);

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
