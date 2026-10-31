using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.ErrorHandling;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.ErrorHandling;

public sealed class DomainExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsService = new();
    private readonly Mock<ILogger<DomainExceptionHandler>> _logger = LoggerMocks.Create<DomainExceptionHandler>();
    private ProblemDetailsContext? _writtenContext;

    public DomainExceptionHandlerTests()
    {
        _problemDetailsService
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => _writtenContext = context)
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task MapsDomainExceptionToItsStatusTitleAndMessage()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await CreateHandler().TryHandleAsync(httpContext, new TestDomainException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(409, httpContext.Response.StatusCode);
        Assert.Equal(409, _writtenContext!.ProblemDetails.Status);
        Assert.Equal("Conflito de teste", _writtenContext.ProblemDetails.Title);
        Assert.Equal("Mensagem de teste.", _writtenContext.ProblemDetails.Detail);
        Assert.IsType<TestDomainException>(_writtenContext.Exception);
    }

    [Fact]
    public async Task MapsBadHttpRequestExceptionToBadRequest()
    {
        var httpContext = new DefaultHttpContext();

        await CreateHandler().TryHandleAsync(httpContext, new BadHttpRequestException("json inválido"), CancellationToken.None);

        Assert.Equal(400, httpContext.Response.StatusCode);
        Assert.Equal(DomainExceptionHandler.InvalidRequestBodyMessage, _writtenContext!.ProblemDetails.Detail);
        Assert.Equal("Um ou mais campos são inválidos.", _writtenContext.ProblemDetails.Title);
    }

    [Fact]
    public async Task MapsUnexpectedExceptionToGenericInternalErrorAndLogsIt()
    {
        var httpContext = new DefaultHttpContext();

        await CreateHandler().TryHandleAsync(httpContext, new InvalidOperationException("detalhe interno"), CancellationToken.None);

        Assert.Equal(500, httpContext.Response.StatusCode);
        Assert.Equal(DomainExceptionHandler.UnexpectedErrorMessage, _writtenContext!.ProblemDetails.Detail);
        Assert.DoesNotContain("detalhe interno", _writtenContext.ProblemDetails.Detail);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    private DomainExceptionHandler CreateHandler() => new(_problemDetailsService.Object, _logger.Object);

    private sealed class TestDomainException() : DomainException(409, "Conflito de teste", "Mensagem de teste.");
}
