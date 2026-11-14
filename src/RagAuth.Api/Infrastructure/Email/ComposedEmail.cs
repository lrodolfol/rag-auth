namespace RagAuth.Api.Infrastructure.Email;

/// <summary>E-mail pronto para envio. O corpo pode conter um link secreto: nunca é registrado em log.</summary>
public sealed record ComposedEmail(string RecipientEmail, string Subject, string HtmlBody);
