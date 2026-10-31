using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services.Business;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.RagApi;

public sealed class RagApiClientFailureTests
{
    private static readonly NormalizedBusinessData BusinessData = new("Padaria", "Pães", null, null, null, null);

    private readonly Mock<ILogger<RagApiClient>> _logger = LoggerMocks.Create<RagApiClient>();

    [Fact]
    public async Task ConnectionFailureBecomesUnavailableException()
    {
        var client = CreateClient(StubHttpMessageHandler.Throwing(new HttpRequestException("Conexão recusada.")));

        await Assert.ThrowsAsync<RagApiUnavailableException>(() => client.CreateBusinessAsync("Ab3dEf6hIj9k", BusinessData));

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task TimeoutBecomesTimeoutException()
    {
        var timeout = new TaskCanceledException("Tempo esgotado.", new TimeoutException());
        var client = CreateClient(StubHttpMessageHandler.Throwing(timeout));

        await Assert.ThrowsAsync<RagApiTimeoutException>(() => client.UpdateBusinessAsync("Ab3dEf6hIj9k", BusinessData));

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task CancellationByCallerIsNotTreatedAsTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = CreateClient(StubHttpMessageHandler.Throwing(new TaskCanceledException("Cancelado.")));

        var exception = await Record.ExceptionAsync(() => client.DeleteBusinessAsync("Ab3dEf6hIj9k", cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    private RagApiClient CreateClient(StubHttpMessageHandler handler) =>
        RagApiClientFactory.Create(handler, Mock.Of<IRagApiResponseTranslator>(), _logger.Object);
}
