using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Observability;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.RagApi;

public sealed class RagApiHeaderHandlersTests
{
    [Fact]
    public async Task ApiKeyHandlerAddsConfiguredKey()
    {
        var handler = new ApiKeyHeaderHandler(Options.Create(new RagApiOptions { ApiKey = "chave-secreta" }));

        var request = await SendThroughAsync(handler);

        Assert.Equal("chave-secreta", request.Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task TokenForwardingHandlerCopiesAuthorizationOfCurrentRequest()
    {
        var tokenAccessor = new Mock<ICurrentRequestTokenAccessor>();
        tokenAccessor.Setup(accessor => accessor.AuthorizationHeader).Returns("Bearer token-do-cliente");

        var request = await SendThroughAsync(new ClientTokenForwardingHandler(tokenAccessor.Object));

        Assert.Equal("Bearer token-do-cliente", request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task TokenForwardingHandlerSkipsMissingAuthorization()
    {
        var request = await SendThroughAsync(new ClientTokenForwardingHandler(Mock.Of<ICurrentRequestTokenAccessor>()));

        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task CorrelationIdHandlerAddsCurrentCorrelationId()
    {
        var correlationIdAccessor = new Mock<ICorrelationIdAccessor>();
        correlationIdAccessor.Setup(accessor => accessor.CorrelationId).Returns("id-da-requisicao");

        var request = await SendThroughAsync(new CorrelationIdHeaderHandler(correlationIdAccessor.Object));

        Assert.Equal("id-da-requisicao", request.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public void RequestTokenAccessorReadsAuthorizationHeaderOfCurrentRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer abc";
        var accessor = new HttpContextRequestTokenAccessor(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal("Bearer abc", accessor.AuthorizationHeader);
        Assert.Null(new HttpContextRequestTokenAccessor(Mock.Of<IHttpContextAccessor>()).AuthorizationHeader);
    }

    private static async Task<HttpRequestMessage> SendThroughAsync(DelegatingHandler handler)
    {
        var innerHandler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        handler.InnerHandler = innerHandler;
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "http://rag-api.teste/business"), CancellationToken.None);

        return innerHandler.LastRequest!;
    }
}
