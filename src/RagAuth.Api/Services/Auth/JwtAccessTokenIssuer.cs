using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Security;

namespace RagAuth.Api.Services.Auth;

public sealed class JwtAccessTokenIssuer(
    JsonWebTokenHandler tokenHandler,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(Client client)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt.AddMinutes(jwtOptions.Value.ExpirationMinutes);
        var token = tokenHandler.CreateToken(CreateTokenDescriptor(client, issuedAt, expiresAt));
        return new IssuedAccessToken(token, expiresAt);
    }

    private SecurityTokenDescriptor CreateTokenDescriptor(Client client, DateTimeOffset issuedAt, DateTimeOffset expiresAt) =>
        new()
        {
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = client.AccessCode,
                [ClientClaimTypes.ClientId] = client.Id,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = CreateSigningCredentials(),
        };

    private SigningCredentials CreateSigningCredentials()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey));
        return new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    }
}
