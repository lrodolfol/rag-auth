using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Email;

namespace RagAuth.Api.Services.PasswordRecovery;

/// <summary>
/// Link recém-emitido. A <see cref="Message"/> leva o validator dentro da URL só em memória:
/// nunca é persistida nem registrada em log.
/// </summary>
public sealed record IssuedPasswordRecoveryLink(PasswordRecoveryLink Entity, OutgoingEmailMessage Message);
