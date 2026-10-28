using System.Security.Claims;
using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Services.Clients;

namespace RagAuth.UnitTests.Services.Clients;

public sealed class ClientSessionValidatorTests
{
    private readonly Mock<IClientRepository> _clientRepository = new();

    public ClientSessionValidatorTests()
    {
        _clientRepository.Setup(repository => repository.FindActiveByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client { Id = 42, AccessCode = "Ab3dEf6hIj9k" });
    }

    [Fact]
    public async Task SessionIsActiveWhenAccountExistsAndAccessCodeMatches()
    {
        Assert.True(await CreateValidator().IsSessionActiveAsync(CreatePrincipal("42", "Ab3dEf6hIj9k")));
    }

    [Theory]
    [InlineData("7", "Ab3dEf6hIj9k")]
    [InlineData("42", "OutroCodigo1")]
    [InlineData("abc", "Ab3dEf6hIj9k")]
    [InlineData(null, "Ab3dEf6hIj9k")]
    public async Task SessionIsInactiveWhenAccountIsMissingRemovedOrClaimsDiffer(string? clientId, string accessCode)
    {
        Assert.False(await CreateValidator().IsSessionActiveAsync(CreatePrincipal(clientId, accessCode)));
    }

    private ClientSessionValidator CreateValidator() => new(_clientRepository.Object);

    private static ClaimsPrincipal CreatePrincipal(string? clientId, string accessCode)
    {
        var claims = new List<Claim> { new("sub", accessCode) };
        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
