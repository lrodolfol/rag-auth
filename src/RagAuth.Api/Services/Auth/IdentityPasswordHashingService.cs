using Microsoft.AspNetCore.Identity;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.Auth;

public sealed class IdentityPasswordHashingService(IPasswordHasher<Client> passwordHasher) : IPasswordHashingService
{
    public string HashPassword(Client client, string password) => passwordHasher.HashPassword(client, password);

    public bool VerifyPassword(Client client, string providedPassword) =>
        passwordHasher.VerifyHashedPassword(client, client.PasswordHash, providedPassword) != PasswordVerificationResult.Failed;
}
