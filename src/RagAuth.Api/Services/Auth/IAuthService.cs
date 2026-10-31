using RagAuth.Api.Contracts.Auth;

namespace RagAuth.Api.Services.Auth;

public interface IAuthService
{
    Task<RegisteredClientResponse> RegisterAsync(RegisterClientRequest request, CancellationToken cancellationToken = default);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
