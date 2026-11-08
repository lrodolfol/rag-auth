using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.EmailConfirmation;

/// <summary>
/// Link recém-emitido. O <see cref="Validator"/> só vive em memória: nunca é persistido nem registrado em log.
/// </summary>
public sealed record IssuedConfirmationLink(EmailConfirmationLink Entity, string Validator, ConfirmationEmailMessage Message);
