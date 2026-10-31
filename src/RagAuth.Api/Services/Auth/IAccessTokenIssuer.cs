using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.Auth;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Client client);
}

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);
