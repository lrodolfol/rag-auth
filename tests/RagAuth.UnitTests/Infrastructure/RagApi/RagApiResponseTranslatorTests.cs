using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.RagApi;

public sealed class RagApiResponseTranslatorTests
{
    private readonly Mock<ILogger<RagApiResponseTranslator>> _logger = LoggerMocks.Create<RagApiResponseTranslator>();

    [Theory]
    [InlineData(400, "O serviço de recursos recusou os dados enviados.")]
    [InlineData(404, "O negócio não foi encontrado no serviço de recursos.")]
    [InlineData(409, "O serviço de recursos indicou conflito com os dados enviados.")]
    [InlineData(422, "O serviço de recursos recusou a operação.")]
    public void ClientErrorsKeepStatusWithPortugueseMessage(int ragStatusCode, string expectedMessage)
    {
        var exception = Translate(ragStatusCode);

        var rejectedException = Assert.IsType<RagApiRejectedException>(exception);
        Assert.Equal(ragStatusCode, rejectedException.StatusCode);
        Assert.Equal(expectedMessage, rejectedException.Message);
        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public void RejectedApiKeyBecomesBadGatewayWithErrorLog(int ragStatusCode)
    {
        var exception = Translate(ragStatusCode);

        Assert.IsType<RagApiIntegrationException>(exception);
        Assert.Equal(502, exception.StatusCode);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(302)]
    public void ServerErrorsAndUnexpectedStatusBecomeBadGateway(int ragStatusCode)
    {
        var exception = Translate(ragStatusCode);

        Assert.IsType<RagApiIntegrationException>(exception);
        Assert.Equal("Falha na comunicação com o serviço de recursos. Tente novamente mais tarde.", exception.Message);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    private DomainException Translate(int ragStatusCode)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)ragStatusCode);
        return new RagApiResponseTranslator(_logger.Object).Translate(RagApiOperations.CreateBusiness, response);
    }
}
