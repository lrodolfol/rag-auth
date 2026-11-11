using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;

namespace RagAuth.Api.Services.EmailConfirmation;

/// <summary>
/// Link recém-emitido. O <see cref="Validator"/> só vive em memória: nunca é persistido nem registrado em log.
/// </summary>
public sealed record IssuedConfirmationLink(EmailConfirmationLink Entity, string Validator, OutgoingEmailMessage Message);
