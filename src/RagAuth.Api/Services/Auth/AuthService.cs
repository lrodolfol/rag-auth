using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Contracts.Auth;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Infrastructure.Persistence.Repositories;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Services.Auth;

public sealed class AuthService(
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService,
    IAccessCodeGenerator accessCodeGenerator,
    IAccessTokenIssuer accessTokenIssuer,
    IBusinessService businessService,
    ILogger<AuthService> logger) : IAuthService
{
    public const int MaximumAccessCodeAttempts = 5;
    public const string BearerTokenType = "Bearer";

    public async Task<RegisteredClientResponse> RegisterAsync(RegisterClientRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        await EnsureEmailIsAvailableAsync(normalizedEmail, cancellationToken);
        var client = CreateClient(request, normalizedEmail);
        clientRepository.Add(client);
        await SaveWithUniqueAccessCodeAsync(client, cancellationToken);
        LogMessages.ClientRegistered(logger, client.Id);
        return new RegisteredClientResponse(client.Id, client.AccessCode, client.Name, client.Email, client.CreatedAt);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var client = await FindClientWithValidCredentialsAsync(request, cancellationToken);
        var accessToken = accessTokenIssuer.Issue(client);
        var business = await businessService.FindOwnedBusinessAsync(client.Id, cancellationToken);
        return new LoginResponse(accessToken.Token, BearerTokenType, accessToken.ExpiresAt, client.Id, client.AccessCode, business);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task EnsureEmailIsAvailableAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (await clientRepository.FindActiveByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new EmailAlreadyRegisteredException();
        }
    }

    private Client CreateClient(RegisterClientRequest request, string normalizedEmail)
    {
        var client = new Client
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            AccessCode = accessCodeGenerator.Generate(),
        };
        client.PasswordHash = passwordHashingService.HashPassword(client, request.Password);
        return client;
    }

    private async Task SaveWithUniqueAccessCodeAsync(Client client, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAccessCodeAttempts; attempt++)
        {
            if (await TrySaveNewClientAsync(cancellationToken))
            {
                return;
            }

            client.AccessCode = accessCodeGenerator.Generate();
        }

        throw new AccessCodeGenerationException();
    }

    private async Task<bool> TrySaveNewClientAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ActiveClientEmail))
        {
            throw new EmailAlreadyRegisteredException();
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ClientAccessCode))
        {
            return false;
        }
    }

    private async Task<Client> FindClientWithValidCredentialsAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var client = await clientRepository.FindActiveByEmailAsync(normalizedEmail, cancellationToken);
        if (client is not null && passwordHashingService.VerifyPassword(client, request.Password))
        {
            return client;
        }

        LogMessages.LoginFailed(logger, SensitiveDataMasker.MaskEmail(normalizedEmail));
        throw new InvalidCredentialsException();
    }
}
