using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.EmailConfirmation;
using RagAuth.Api.Endpoints;
using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.UnitTests.Endpoints;

public sealed class EmailConfirmationEndpointHandlersTests
{
    private readonly Mock<IEmailConfirmationService> _emailConfirmationService = new();

    [Theory]
    [InlineData(EmailConfirmationOutcome.Confirmed, "confirmed", EmailConfirmationEndpoints.ConfirmedMessage)]
    [InlineData(EmailConfirmationOutcome.AlreadyConfirmed, "alreadyConfirmed", EmailConfirmationEndpoints.AlreadyConfirmedMessage)]
    public async Task ConfirmReturnsOkEnvelopeWithStatus(EmailConfirmationOutcome outcome, string expectedStatus, string expectedMessage)
    {
        _emailConfirmationService.Setup(service => service.ConfirmAsync("selector", "validator", CancellationToken.None)).ReturnsAsync(outcome);

        var result = await EmailConfirmationEndpoints.ConfirmEmailAsync("selector", "validator", _emailConfirmationService.Object, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<EmailConfirmationResultResponse>>>(result);
        Assert.Equal(StatusCodes.Status200OK, jsonResult.StatusCode);
        Assert.Equal(expectedMessage, jsonResult.Value!.Message);
        Assert.Equal(new EmailConfirmationResultResponse(expectedStatus), jsonResult.Value.Data);
    }

    [Fact]
    public async Task NewLinkAlwaysReturnsAcceptedEnvelopeWithoutData()
    {
        var request = new NewConfirmationLinkRequest { Email = "ana@exemplo.com" };

        var result = await EmailConfirmationEndpoints.RequestNewConfirmationLinkAsync(request, _emailConfirmationService.Object, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiResponse<object?>>>(result);
        Assert.Equal(StatusCodes.Status202Accepted, jsonResult.StatusCode);
        Assert.Equal(EmailConfirmationEndpoints.NewLinkAcceptedMessage, jsonResult.Value!.Message);
        Assert.Null(jsonResult.Value.Data);
        _emailConfirmationService.Verify(service => service.RequestNewLinkAsync("ana@exemplo.com", CancellationToken.None), Times.Once());
    }
}
