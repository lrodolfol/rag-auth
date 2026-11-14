using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.PasswordRecovery;
using RagAuth.Api.Endpoints;
using RagAuth.Api.Services.PasswordRecovery;

namespace RagAuth.UnitTests.Endpoints;

public sealed class PasswordRecoveryEndpointHandlersTests
{
    private readonly Mock<IPasswordRecoveryService> _passwordRecoveryService = new();

    [Fact]
    public async Task RequestAlwaysReturnsAcceptedEnvelopeWithoutData()
    {
        var request = new PasswordRecoveryRequest { Email = "ana@exemplo.com" };

        var result = await PasswordRecoveryEndpoints.RequestPasswordRecoveryAsync(request, _passwordRecoveryService.Object, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<object?>>>(result);
        Assert.Equal(StatusCodes.Status202Accepted, jsonResult.StatusCode);
        Assert.Equal(PasswordRecoveryEndpoints.RecoveryRequestAcceptedMessage, jsonResult.Value!.Message);
        Assert.Null(jsonResult.Value.Data);
        _passwordRecoveryService.Verify(service => service.RequestRecoveryAsync("ana@exemplo.com", CancellationToken.None), Times.Once());
    }

    [Fact]
    public async Task VerifyReturnsOkWithValidStatus()
    {
        var result = await PasswordRecoveryEndpoints.VerifyPasswordRecoveryLinkAsync("selector", "validator", _passwordRecoveryService.Object, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<PasswordRecoveryLinkStatusResponse>>>(result);
        Assert.Equal(StatusCodes.Status200OK, jsonResult.StatusCode);
        Assert.Equal(PasswordRecoveryEndpoints.ValidLinkMessage, jsonResult.Value!.Message);
        Assert.Equal(new PasswordRecoveryLinkStatusResponse("valid"), jsonResult.Value.Data);
        _passwordRecoveryService.Verify(service => service.VerifyLinkAsync("selector", "validator", CancellationToken.None), Times.Once());
    }

    [Fact]
    public async Task ResetReturnsOkWithoutData()
    {
        var request = new ResetPasswordRequest { NewPassword = "NovaSenha123", NewPasswordConfirmation = "NovaSenha123" };

        var result = await PasswordRecoveryEndpoints.ResetPasswordAsync("selector", "validator", request, _passwordRecoveryService.Object, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<object?>>>(result);
        Assert.Equal(StatusCodes.Status200OK, jsonResult.StatusCode);
        Assert.Equal(PasswordRecoveryEndpoints.PasswordResetMessage, jsonResult.Value!.Message);
        Assert.Null(jsonResult.Value.Data);
        _passwordRecoveryService.Verify(
            service => service.ResetPasswordAsync("selector", "validator", "NovaSenha123", CancellationToken.None), Times.Once());
    }
}
