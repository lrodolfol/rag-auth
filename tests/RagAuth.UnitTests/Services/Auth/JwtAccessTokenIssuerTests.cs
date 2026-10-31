using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Auth;

namespace RagAuth.UnitTests.Services.Auth;

public sealed class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 25, 13, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "rag-auth",
        Audience = "rag-api",
        ExpirationMinutes = 60,
        SigningKey = "chave-de-assinatura-dos-testes-com-mais-de-32-caracteres",
    };

    [Fact]
    public void IssuesHs256TokenWithClientClaimsAndConfiguredExpiration()
    {
        var client = new Client { Id = 42, AccessCode = "Ab3dEf6hIj9k" };

        var issuedToken = CreateIssuer().Issue(client);

        var token = new JsonWebToken(issuedToken.Token);
        Assert.Equal("HS256", token.Alg);
        Assert.Equal("Ab3dEf6hIj9k", token.Subject);
        Assert.Equal("42", token.GetClaim("client_id").Value);
        Assert.False(string.IsNullOrWhiteSpace(token.Id));
        Assert.Equal("rag-auth", token.Issuer);
        Assert.Equal("rag-api", Assert.Single(token.Audiences));
        Assert.Equal(CurrentTime.UtcDateTime, token.IssuedAt);
        Assert.Equal(CurrentTime.AddMinutes(60).UtcDateTime, token.ValidTo);
        Assert.Equal(CurrentTime.AddMinutes(60), issuedToken.ExpiresAt);
    }

    [Fact]
    public void EachTokenHasUniqueIdentifier()
    {
        var issuer = CreateIssuer();
        var client = new Client { Id = 1, AccessCode = "Ab3dEf6hIj9k" };

        var firstToken = new JsonWebToken(issuer.Issue(client).Token);
        var secondToken = new JsonWebToken(issuer.Issue(client).Token);

        Assert.NotEqual(firstToken.Id, secondToken.Id);
    }

    private JwtAccessTokenIssuer CreateIssuer()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(provider => provider.GetUtcNow()).Returns(CurrentTime);
        return new JwtAccessTokenIssuer(new JsonWebTokenHandler(), Options.Create(_jwtOptions), timeProvider.Object);
    }
}
