using Microsoft.AspNetCore.Identity;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Auth;

namespace RagAuth.UnitTests.Services.Auth;

public sealed class IdentityPasswordHashingServiceTests
{
    private const string Password = "senhaSegura1";
    private readonly IdentityPasswordHashingService _hashingService = new(new PasswordHasher<Client>());

    [Fact]
    public void HashIsDifferentFromPassword()
    {
        Assert.NotEqual(Password, _hashingService.HashPassword(new Client(), Password));
    }

    [Fact]
    public void SamePasswordGeneratesDifferentHashesBecauseOfSalt()
    {
        var firstHash = _hashingService.HashPassword(new Client(), Password);
        var secondHash = _hashingService.HashPassword(new Client(), Password);

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void VerificationAcceptsCorrectPasswordAndRejectsWrongOne()
    {
        var client = new Client();
        client.PasswordHash = _hashingService.HashPassword(client, Password);

        Assert.True(_hashingService.VerifyPassword(client, Password));
        Assert.False(_hashingService.VerifyPassword(client, "senhaErrada2"));
    }
}
