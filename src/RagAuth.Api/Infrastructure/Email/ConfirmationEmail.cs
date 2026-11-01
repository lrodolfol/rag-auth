namespace RagAuth.Api.Infrastructure.Email;

/// <summary>E-mail pronto para envio. O corpo contém o link: nunca é registrado em log.</summary>
public sealed record ConfirmationEmail(string RecipientEmail, string Subject, string HtmlBody);
