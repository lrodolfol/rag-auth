using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Services.Auth;

public interface IPasswordHashingService
{
    string HashPassword(Client client, string password);

    bool VerifyPassword(Client client, string providedPassword);
}
