using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.Auth;
using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Endpoints;
using RagAuth.Api.Services.Auth;
using RagAuth.Api.Services.Business;
using RagAuth.Api.Services.Clients;

namespace RagAuth.UnitTests.Endpoints;

public sealed class EndpointHandlersTests
{
    private static readonly BusinessResponse Business =
        new("Padaria", "Pães", null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IBusinessService> _businessService = new();
    private readonly Mock<IClientAccountService> _clientAccountService = new();
    private readonly BusinessRequest _businessRequest = new() { Name = "Padaria", Description = "Pães" };

    [Fact]
    public async Task RegisterReturnsCreatedEnvelope()
    {
        var registeredClient = new RegisteredClientResponse(42, "Ab3dEf6hIj9k", "Ana", "ana@exemplo.com", DateTimeOffset.UtcNow);
        var request = new RegisterClientRequest();
        _authService.Setup(service => service.RegisterAsync(request, CancellationToken.None)).ReturnsAsync(registeredClient);

        var result = await AuthEndpoints.RegisterAsync(request, _authService.Object, CancellationToken.None);

        AssertEnvelope(result, StatusCodes.Status201Created, "Cadastro realizado com sucesso.", registeredClient);
    }

    [Fact]
    public async Task LoginReturnsOkEnvelope()
    {
        var login = new LoginResponse("token", "Bearer", DateTimeOffset.UtcNow, 42, "Ab3dEf6hIj9k", null);
        var request = new LoginRequest();
        _authService.Setup(service => service.LoginAsync(request, CancellationToken.None)).ReturnsAsync(login);

        var result = await AuthEndpoints.LoginAsync(request, _authService.Object, CancellationToken.None);

        AssertEnvelope(result, StatusCodes.Status200OK, "Login realizado com sucesso.", login);
    }

    [Fact]
    public async Task CreateBusinessReturnsCreatedEnvelope()
    {
        _businessService.Setup(service => service.CreateOwnedBusinessAsync(42, _businessRequest, CancellationToken.None)).ReturnsAsync(Business);

        var result = await BusinessEndpoints.CreateAsync(42, _businessRequest, _businessService.Object, CancellationToken.None);

        AssertEnvelope(result, StatusCodes.Status201Created, "Negócio cadastrado com sucesso.", Business);
    }

    [Fact]
    public async Task UpdateBusinessReturnsOkEnvelope()
    {
        _businessService.Setup(service => service.UpdateOwnedBusinessAsync(42, _businessRequest, CancellationToken.None)).ReturnsAsync(Business);

        var result = await BusinessEndpoints.UpdateAsync(42, _businessRequest, _businessService.Object, CancellationToken.None);

        AssertEnvelope(result, StatusCodes.Status200OK, "Negócio atualizado com sucesso.", Business);
    }

    [Fact]
    public async Task DeleteBusinessReturnsOkEnvelopeWithoutData()
    {
        var result = await BusinessEndpoints.DeleteAsync(42, _businessService.Object, CancellationToken.None);

        AssertEnvelope<object?>(result, StatusCodes.Status200OK, "Negócio removido com sucesso.", null);
        _businessService.Verify(service => service.DeleteOwnedBusinessAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAccountReturnsOkEnvelopeWithoutData()
    {
        var result = await ClientEndpoints.DeleteAccountAsync(42, _clientAccountService.Object, CancellationToken.None);

        AssertEnvelope<object?>(result, StatusCodes.Status200OK, "Conta removida com sucesso.", null);
        _clientAccountService.Verify(service => service.DeleteAccountAsync(42, CancellationToken.None));
    }

    private static void AssertEnvelope<TData>(IResult result, int expectedStatus, string expectedMessage, TData expectedData)
    {
        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<TData>>>(result);
        Assert.Equal(expectedStatus, jsonResult.StatusCode);
        Assert.Equal(new ApiResponse<TData>(expectedStatus, true, expectedMessage, expectedData, null), jsonResult.Value);
    }
}
