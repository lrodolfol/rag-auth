using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RagAuth.IntegrationTests.Infrastructure;

public static class TestTokens
{
    public static string Create(
        TestClientAccount account,
        DateTime? expiresAt = null,
        string issuer = RagAuthApiFactory.JwtIssuer,
        string audience = RagAuthApiFactory.JwtAudience,
        string signingKey = RagAuthApiFactory.JwtSigningKey)
    {
        var expiration = expiresAt ?? DateTime.UtcNow.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiration.AddHours(-1),
            NotBefore = expiration.AddHours(-1),
            Expires = expiration,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = account.AccessCode,
                ["client_id"] = account.ClientId,
                ["jti"] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static string TamperSignature(string token)
    {
        var lastCharacter = token[^1] == 'A' ? 'B' : 'A';
        return string.Concat(token.AsSpan(0, token.Length - 1), lastCharacter.ToString());
    }
}
